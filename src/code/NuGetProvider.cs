// Copyright (c) Thomas Nieto - All Rights Reserved
// You may use, distribute and modify this code under the
// terms of the MIT license.

using System.Management.Automation;
using System.Text;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;

namespace AnyPackage.Provider.NuGet;

[PackageProvider("NuGet", FileExtensions = [".nupkg", ".nuspec"])]
public class NuGetProvider : PackageProvider, IFindPackage, IGetPackage, IInstallPackage, ISavePackage, IGetSource, ISetSource
{
    protected override bool IsSource(string source)
    {
        return GetEnabledSources().ToArray().Length > 0;
    }

    protected override object? GetDynamicParameters(string commandName)
    {
        return commandName switch
        {
            "Install-Package" => new InstallPackageDynamicParameters(),
            "Save-Package" => new InstallPackageDynamicParameters(),
            "Register-PackageSource" => new SetSourceDynamicParameters(),
            "Set-PackageSource" => new SetSourceDynamicParameters(),
            _ => null,
        };
    }

    public void FindPackage(PackageRequest request)
    {
        if (request.ParameterSetName == "Name")
        {
            foreach (var package in FindPackageByName(request))
            {
                request.WritePackage(package);
            }
        }
        else if (Path.GetExtension(request.Path) == ".nupkg")
        {
            var package = FindPackageByNuPkg(request.Path);
            request.WritePackage(package);
        }
        else if (Path.GetExtension(request.Path) == ".nuspec")
        {
            var package = FindPackageByNuSpec(request.Path);
            request.WritePackage(package);
        }
    }

    public void GetPackage(PackageRequest request)
    {
        var settings = Settings.LoadDefaultSettings(root: null);
        var packagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);

        if (!Directory.Exists(packagesPath))
        {
            return;
        }

        string name;
        if (WildcardPattern.ContainsWildcardCharacters(request.Name))
        {
            name = "*";
        }
        else
        {
            name = request.Name.ToLower();
        }

        var packagesDirectory = new DirectoryInfo(packagesPath);
        foreach (var file in packagesDirectory.EnumerateFiles($"{name}.nuspec", SearchOption.AllDirectories))
        {
            var package = FindPackageByNuSpec(file.FullName);

            if (request.IsMatch(package.Name, package.Version!))
            {
                request.WritePackage(package);
            }
        }
    }

    public void InstallPackage(PackageRequest request)
    {
        InstallPackage(request, path: null);
    }

    public void SavePackage(PackageRequest request)
    {
        InstallPackage(request, request.Path);
    }

    public void GetSource(SourceRequest request)
    {
        foreach (var source in GetEnabledSources())
        {
            if (request.IsMatch(source.Name))
            {
                var sourceInfo = new PackageSourceInfo(source.Name, source.Source, ProviderInfo);
                request.WriteSource(sourceInfo);
            }
        }
    }

    public void RegisterSource(SourceRequest request)
    {
        var sourceProvider = GetPackageSourceProvider();
        var source = sourceProvider.GetPackageSourceByName(request.Name);
        var force = request.Force ?? false;

        if (source is not null && !force)
        {
            return;
        }

        source = new PackageSource(request.Location!, request.Name);

        var uri = new Uri(request.Location);

        if (uri.Scheme == "http" || uri.Scheme == "https")
        {
            var dynamicParameters = request.DynamicParameters as SetSourceDynamicParameters;

            if (dynamicParameters is not null && dynamicParameters.ProtocolVersion != 0)
            {
                source.ProtocolVersion = dynamicParameters.ProtocolVersion;
            }
            else
            {
                throw new InvalidOperationException("Location scheme of 'http' or 'https' requires ProtocolVersion parameter.");
            }
        }

        sourceProvider.AddPackageSource(source);
        var sourceInfo = new PackageSourceInfo(source.Name, source.Source, ProviderInfo);
        request.WriteSource(sourceInfo);
    }

    public void SetSource(SourceRequest request)
    {
        var sourceProvider = GetPackageSourceProvider();
        var source = sourceProvider.GetPackageSourceByName(request.Name);

        if (source is not null && request.Location is not null)
        {
            var uri = new Uri(request.Location);

            if (uri.Scheme == "http" || uri.Scheme == "https")
            {
                var dynamicParameters = request.DynamicParameters as SetSourceDynamicParameters;

                if (dynamicParameters is not null && dynamicParameters.ProtocolVersion != 0)
                {
                    source.ProtocolVersion = dynamicParameters.ProtocolVersion;
                }
                else
                {
                    throw new InvalidOperationException("Location scheme of 'http' or 'https' requires ProtocolVersion parameter.");
                }
            }

            source.Source = request.Location;
            sourceProvider.UpdatePackageSource(source, updateCredentials: false, updateEnabled: false);
            var sourceInfo = new PackageSourceInfo(source.Name, source.Source, ProviderInfo);
            request.WriteSource(sourceInfo);
        }
    }

    public void UnregisterSource(SourceRequest request)
    {
        var sourceProvider = GetPackageSourceProvider();
        var source = sourceProvider.GetPackageSourceByName(request.Name);

        if (source is not null)
        {
            sourceProvider.RemovePackageSource(request.Name);
            var sourceInfo = new PackageSourceInfo(source.Name, source.Source, ProviderInfo);
            request.WriteSource(sourceInfo);
        }
    }

    private IEnumerable<PackageInfo> FindPackageByName(PackageRequest request)
    {
        var sources = GetEnabledSources(request.Source);
        var query = GetQuery(request);
        var filter = new SearchFilter(request.Prerelease);

        foreach (var source in sources)
        {
            var repo = GetSourceRepository(source);
            var sourceInfo = new PackageSourceInfo(source.Name, source.Source, ProviderInfo);

            foreach (var result in GetSearchResults(query, filter, repo, request))
            {
                if (request.IsMatch(result.Identity.Id))
                {
                    IEnumerable<VersionInfo> versions;
                    if (request.Version is null)
                    {
                        versions = new VersionInfo[] { new(result.Identity.Version) };
                    }
                    else
                    {
                        versions = result.GetVersionsAsync()
                                         .ConfigureAwait(false)
                                         .GetAwaiter()
                                         .GetResult();
                    }

                    foreach (var version in versions)
                    {
                        if (request.IsMatch((PackageVersion)version.Version.ToString()))
                        {
                            yield return new PackageInfo(result.Identity.Id,
                                                         version.Version.ToString(),
                                                         sourceInfo,
                                                         result.Description,
                                                         ProviderInfo);
                        }
                    }
                }
            }
        }
    }

    private PackageInfo FindPackageByNuSpec(string path)
    {
        var reader = new NuspecReader(path);
        return GetPackageInfo(path, reader);
    }

    private PackageInfo FindPackageByNuPkg(string path)
    {
        using var archiveReader = new PackageArchiveReader(path);
        return GetPackageInfo(path, archiveReader.NuspecReader);
    }

    private void InstallPackage(PackageRequest request, string? path)
    {
        var framework = NuGetFramework.AnyFramework;
        var dependencyBehavior = DependencyBehavior.Lowest;
        if (request.DynamicParameters is InstallPackageDynamicParameters dynamicParameters)
        {
            framework = NuGetFramework.Parse(dynamicParameters.Framework);
            dependencyBehavior = dynamicParameters.DependencyBehavior;
        }

        var package = FindPackageByName(request).FirstOrDefault();

        if (package is null || package.Version is null)
        {
            return;
        }

        var version = new NuGetVersion(package.Version.ToString());
        var identity = new PackageIdentity(package.Name, version);
        var sources = GetEnabledSources(request.Source);
        var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
        using var cache = new SourceCacheContext();
        var task = GetPackageDependenciesAsync(identity, framework, cache, sources, availablePackages, request);
        task.Wait();

        var resolverContext = new PackageResolverContext(dependencyBehavior,
                                                         new[] { request.Name.ToLower() },
                                                         Enumerable.Empty<string>(),
                                                         Enumerable.Empty<PackageReference>(),
                                                         Enumerable.Empty<PackageIdentity>(),
                                                         availablePackages,
                                                         sources,
                                                         NullLogger.Instance);

        var resolver = new PackageResolver();
        var packagesToInstall = resolver.Resolve(resolverContext, CancellationToken.None)
                                        .Select(p => availablePackages.Single(x => PackageIdentityComparer.Default.Equals(x, p)));

        var downloadedPackages = DownloadPackageAsync(packagesToInstall, cache, path: path).GetAwaiter().GetResult();

        foreach (var downloadedPackage in downloadedPackages)
        {
            if (request.IsMatch(downloadedPackage.Name, downloadedPackage.Version!))
            {
                request.WritePackage(downloadedPackage);
            }
            else
            {
                request.WriteVerbose($"Installed dependency '{downloadedPackage.Name}' with version '{downloadedPackage.Version}'.");
            }
        }
    }

    private async Task<IEnumerable<PackageInfo>> DownloadPackageAsync(IEnumerable<SourcePackageDependencyInfo> packagesToInstall, SourceCacheContext cache, string? path)
    {
        var settings = Settings.LoadDefaultSettings(root: null);
        var globalPath = SettingsUtility.GetGlobalPackagesFolder(settings);
        var packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3,
                                                                    XmlDocFileSaveMode.None,
                                                                    ClientPolicyContext.GetClientPolicy(settings, NullLogger.Instance),
                                                                    NullLogger.Instance);

        // GetTempPath is not actually used just required to create PackagePathResolver
        var rootPath = path ?? Path.GetTempPath();
        var packagePathResolver = new PackagePathResolver(Path.GetFullPath(rootPath));

        var downloadedPackages = new List<PackageInfo>();
        foreach (var packageToInstall in packagesToInstall)
        {
            if (path is not null)
            {
                var installedPath = packagePathResolver.GetInstalledPath(packageToInstall);

                if (installedPath is not null)
                {
                    continue;
                }
            }
            else
            {
                var installPath = Path.Combine(globalPath, packageToInstall.Id, packageToInstall.Version.ToString());

                if (Directory.Exists(installPath))
                {
                    continue;
                }
            }

            var downloadResource = await packageToInstall.Source.GetResourceAsync<DownloadResource>();
            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(packageToInstall,
                                                                                       new PackageDownloadContext(cache),
                                                                                       SettingsUtility.GetGlobalPackagesFolder(settings),
                                                                                       NullLogger.Instance,
                                                                                       CancellationToken.None);

            if (path is not null)
            {
                await PackageExtractor.ExtractPackageAsync(downloadResult.PackageSource,
                                                       downloadResult.PackageStream,
                                                       packagePathResolver,
                                                       packageExtractionContext,
                                                       CancellationToken.None);
            }

            var sourceInfo = new PackageSourceInfo(packageToInstall.Source.PackageSource.Name, packageToInstall.Source.PackageSource.Source, ProviderInfo);
            var packageInfo = new PackageInfo(packageToInstall.Id, packageToInstall.Version.ToString(), sourceInfo, ProviderInfo);
            downloadedPackages.Add(packageInfo);
        }

        return downloadedPackages;
    }

    private static async Task GetPackageDependenciesAsync(PackageIdentity identity,
                                                          NuGetFramework framework,
                                                          SourceCacheContext cache,
                                                          IEnumerable<PackageSource> sources,
                                                          ISet<SourcePackageDependencyInfo> availablePackages,
                                                          PackageRequest request)
    {
        if (availablePackages.Contains(identity)) { return; }

        foreach (var source in sources)
        {
            var repo = GetSourceRepository(source);
            var depResource = repo.GetResource<DependencyInfoResource>();
            var depInfo = await depResource.ResolvePackage(identity,
                                                     framework,
                                                     cache,
                                                     NullLogger.Instance,
                                                     CancellationToken.None);

            if (depInfo is null) { continue; }

            availablePackages.Add(depInfo);

            foreach (var dependency in depInfo.Dependencies)
            {
                // MinVersion could be exclusive
                await GetPackageDependenciesAsync(new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion), framework, cache, sources, availablePackages, request);
            }
        }
    }

    private static PackageSourceProvider GetPackageSourceProvider()
    {
        var settings = Settings.LoadDefaultSettings(root: null);
        var sourceProvider = new PackageSourceProvider(settings);
        return sourceProvider;
    }

    private PackageInfo GetPackageInfo(string path, NuspecReader reader)
    {
        var source = new PackageSourceInfo(path, path, ProviderInfo);
        var package = new PackageInfo(reader.GetId(),
                                      reader.GetVersion().ToString(),
                                      source,
                                      reader.GetDescription(),
                                      ProviderInfo);
        return package;
    }

    private static string GetQuery(PackageRequest request)
    {
        var builder = new StringBuilder();

        if (WildcardPattern.ContainsWildcardCharacters(request.Name))
        {
            builder.Append($"id:");
        }
        else
        {
            builder.Append($"packageid:");
        }

        builder.Append(request.Name.ToLower());

        return builder.ToString();
    }

    private static SourceRepository GetSourceRepository(PackageSource source)
    {
        if (source.ProtocolVersion == 2)
        {
            return Repository.Factory.GetCoreV2(source);
        }
        else
        {
            return Repository.Factory.GetCoreV3(source);
        }
    }

    private static IEnumerable<IPackageSearchMetadata> GetSearchResults(string query, SearchFilter filter, SourceRepository repo, PackageRequest request)
    {
        var resource = repo.GetResource<PackageSearchResource>();
        var allResults = new List<IPackageSearchMetadata>();
        var skip = 0;
        var take = 1000;

        while (true)
        {
            var task = resource.SearchAsync(query, filter, skip, take, NullLogger.Instance, CancellationToken.None);
            var results = task.ConfigureAwait(false)
                              .GetAwaiter()
                              .GetResult()
                              .ToList();

            allResults.AddRange(results);

            if (results.Count < take)
            {
                break;
            }

            skip += 1000;
        }

        return allResults;
    }

    private static IEnumerable<PackageSource> GetEnabledSources(string? name = null)
    {
        var settings = Settings.LoadDefaultSettings(root: null);
        var enabledSources = SettingsUtility.GetEnabledSources(settings);

        if (!string.IsNullOrWhiteSpace(name))
        {
            enabledSources = enabledSources.Where(x => x.Name == name);
        }

        return enabledSources;
    }
}
