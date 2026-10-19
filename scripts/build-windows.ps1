[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [string]$CertificateThumbprint)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) { throw 'Visual Studio 2022 mit Office/SharePoint-Entwicklung installieren.' }
$msbuild = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.OfficeTools -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'MSBuild mit OfficeTools fehlt. Visual Studio Installer prüfen.' }
Push-Location $root
try {
    dotnet test tests/JitsiAdmin.OutlookPlugin.Core.Tests/JitsiAdmin.OutlookPlugin.Core.Tests.csproj -c $Configuration --logger 'trx;LogFileName=core.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Core-Tests fehlgeschlagen.' }
    $arguments = @('src/JitsiAdmin.OutlookPlugin/JitsiAdmin.OutlookPlugin.csproj', '/restore', '/t:Build', "/p:Configuration=$Configuration", '/p:Platform=AnyCPU', '/v:minimal')
    if ($CertificateThumbprint) { $arguments += @('/p:SignManifests=true', "/p:ManifestCertificateThumbprint=$CertificateThumbprint") }
    & $msbuild @arguments
    if ($LASTEXITCODE -ne 0) { throw 'VSTO-Build fehlgeschlagen.' }
} finally { Pop-Location }
