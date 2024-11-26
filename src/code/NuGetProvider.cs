// Copyright (c) Thomas Nieto - All Rights Reserved
// You may use, distribute and modify this code under the
// terms of the MIT license.

using System.Management.Automation;
using System.Text;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace AnyPackage.Provider.NuGet;

[PackageProvider("NuGet", FileExtensions = [".nupkg", ".nuspec"])]
public class NuGetProvider : PackageProvider, IFindPackage, IGetPackage, IGetSource, ISetSource
{
    protected override bool IsSource(string source)
    {
        return GetEnabledSources().ToArray().Length > 0;
    }

    protected override object? GetDynamicParameters(string commandName)
    {
        return commandName switch
        {
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
