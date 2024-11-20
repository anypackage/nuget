##Requires -Modules AnyPackage.NuGet

Describe Get-PackageProvider {
    Context 'without parameters' {
        It 'should return package sources' {
            Get-PackageSource |
            Should -Not -BeNullOrEmpty
        }
    }

    Context 'with Name parameter' {
        It 'should return nuget.org' {
            Get-PackageSource -Name nuget.org |
            Select-Object -ExpandProperty Name |
            Should -Be nuget.org
        }

        It 'wildcard should return nuget.org' {
            Get-PackageSource -Name nuget* |
            Select-Object -ExpandProperty Name |
            Should -Be nuget.org
        }
    }
}
