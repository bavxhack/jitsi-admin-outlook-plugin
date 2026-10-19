[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [string]$CertificateThumbprint)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot\office-toolchain.ps1"
$toolchain = Resolve-OfficeToolchain -InstallationPaths @(Get-VisualStudioInstallationPaths)
$msbuild = $toolchain.MSBuildPath
Push-Location $root
try {
    dotnet test tests/JitsiAdmin.OutlookPlugin.Core.Tests/JitsiAdmin.OutlookPlugin.Core.Tests.csproj -c $Configuration --logger 'trx;LogFileName=core.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Core-Tests fehlgeschlagen.' }
    $arguments = @('src/JitsiAdmin.OutlookPlugin/JitsiAdmin.OutlookPlugin.csproj', '/restore', '/t:Build', "/p:Configuration=$Configuration", '/p:Platform=AnyCPU', '/v:minimal')
    if ($CertificateThumbprint) { $arguments += @('/p:SignManifests=true', "/p:ManifestCertificateThumbprint=$CertificateThumbprint") }
    $arguments += "/p:VSToolsPath=$($toolchain.VSToolsPath)"
    & $msbuild @arguments
    if ($LASTEXITCODE -ne 0) { throw 'VSTO-Build fehlgeschlagen.' }
} finally { Pop-Location }
