// Copyright (c) Thomas Nieto - All Rights Reserved
// You may use, distribute and modify this code under the
// terms of the MIT license.

using NuGet.Configuration;

namespace AnyPackage.Provider.NuGet;

[PackageProvider("NuGet")]
public class NuGetProvider : PackageProvider, IGetSource
{
    public void GetSource(SourceRequest request)
    {
        var settings = Settings.LoadDefaultSettings(root: null);
        var enabledSources = SettingsUtility.GetEnabledSources(settings);

        foreach (var source in enabledSources)
        {
            if (request.IsMatch(source.Name))
            {
                var sourceInfo = new PackageSourceInfo(source.Name, source.Source, ProviderInfo);
                request.WriteSource(sourceInfo);
            }
        }
    }
}
