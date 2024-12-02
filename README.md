# AnyPackage.NuGet

[![gallery-image]][gallery-site]
[![build-image]][build-site]
[![cf-image]][cf-site]

[gallery-image]: https://img.shields.io/powershellgallery/dt/AnyPackage.NuGet
[build-image]: https://img.shields.io/github/actions/workflow/status/anypackage/nuget/ci.yml
[cf-image]: https://img.shields.io/codefactor/grade/github/anypackage/nuget
[gallery-site]: https://www.powershellgallery.com/packages/AnyPackage.NuGet
[build-site]: https://github.com/anypackage/nuget/actions/workflows/ci.yml
[cf-site]: https://www.codefactor.io/repository/github/anypackage/nuget

NuGet provider for AnyPackage.

## Install AnyPackage.NuGet

```powershell
Install-PSResource AnyPackage.NuGet
```

## Import AnyPackage.NuGet

```powershell
Import-Module AnyPackage.NuGet
```

## Sample usages

### Find available packages

```powershell
Find-Package -Name System.Management.Automation
```

### Install packages

Installs packages to the NuGet global packages directory.

```powershell
Install-Package -Name System.Management.Automation
```

### Save packages

Saves packages to a directory.

```powershell
Save-Package -Name System.Management.Automation -Path C:\Temp
```
