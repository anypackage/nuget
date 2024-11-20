@{
    RootModule = 'NuGetProvider.dll'
    ModuleVersion = '0.1.0'
    CompatiblePSEditions = @('Desktop', 'Core')
    GUID = 'df399e6f-5c6e-4f6c-8a18-b278ff74f1e7'
    Author = 'Thomas Nieto'
    Copyright = '(c) 2024 Thomas Nieto. All rights reserved.'
    Description = 'NuGet provider for AnyPackage.'
    PowerShellVersion = '5.1'
    RequiredModules = @(
        @{ ModuleName = 'AnyPackage'; ModuleVersion = '0.8.0' })
    FunctionsToExport = @()
    CmdletsToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        AnyPackage = @{
            Providers = 'NuGet'
        }
        PSData = @{
            Tags = @('AnyPackage', 'Provider', 'NuGet', 'Windows', 'MacOS', 'Linux')
            LicenseUri = 'https://github.com/anypackage/nuget/blob/main/LICENSE'
            ProjectUri = 'https://github.com/anypackage/nuget'
        }
    }
    HelpInfoURI = 'https://go.anypackage.dev/help'
}
