// Copyright (c) Thomas Nieto - All Rights Reserved
// You may use, distribute and modify this code under the
// terms of the MIT license.

using System.Management.Automation;

using static AnyPackage.Provider.PackageProviderManager;

namespace AnyPackage.Provider.NuGet;

public sealed class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    private readonly Guid _id = new("ffb7190a-2041-4491-a155-56210a74cbfd");

    public void OnImport()
    {
        RegisterProvider(_id, typeof(NuGetProvider), "AnyPackage.NuGet");
    }

    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        UnregisterProvider(_id);
    }
}
