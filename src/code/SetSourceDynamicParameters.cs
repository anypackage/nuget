// Copyright (c) Thomas Nieto - All Rights Reserved
// You may use, distribute and modify this code under the
// terms of the MIT license.

using System.Management.Automation;

namespace AnyPackage.Provider.NuGet;

public class SetSourceDynamicParameters
{
    [ValidateSet("2", "3")]
    [Parameter]
    public int ProtocolVersion { get; set; }
}
