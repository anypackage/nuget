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
public class NuGetProvider : PackageProvider, IFindPackage, IGetPackage, IGetSource
{
    protected override bool IsSource(string source)
    {
        return GetEnabledSources().ToArray().Length > 0;
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
