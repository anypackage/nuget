// Copyright (c) Thomas Nieto - All Rights Reserved
// You may use, distribute and modify this code under the
// terms of the MIT license.

using System.Management.Automation;

using NuGet.Frameworks;
using NuGet.Resolver;

namespace AnyPackage.Provider.NuGet;

public class InstallPackageDynamicParameters
{
    [Parameter]
    public string Framework { get; set; } = NuGetFramework.AnyFramework.ToString();

    [Parameter]
    public DependencyBehavior DependencyBehavior { get; set; } = DependencyBehavior.Lowest;
}
