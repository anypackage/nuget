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

### Get installed packages

Gets installed packages from the NuGet global packages directory.

```powershell
Get-Package
```

### Install packages

Installs packages to the NuGet global packages directory.

```powershell
Install-Package -Name System.Management.Automation
```

### Install packages with specified framework

```powershell
Install-Package -Name Avalonia -Provider NuGet -Framework netstandard2.0
```

### Install packages with dependency behavior

```powershell
Install-Package -Name Avalonia -Provider NuGet -DependencyBehavior Highest
```

### Save packages

Saves packages to a directory.

```powershell
Save-Package -Name System.Management.Automation -Path C:\Temp
```

### Get registered package sources

```powershell
Get-PackageSource
```

### Register package source

```powershell

$params = @{
    Name            = 'nuget.org'
    Location        = 'https://api.nuget.org/v3/index.json'
    Provider        = 'NuGet'
    ProtocolVersion = 3
}

Register-PackageSource @params
```

### Set package source

```powershell
Set-PackageSource -Name nuget.org -Location url
```

### Unregister package source

```powershell
Unregister-PackageSource -Name nuget.org
```
