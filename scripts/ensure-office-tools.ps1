[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) { throw 'Visual Studio 2022 fehlt.' }
$installation = & $vswhere -latest -products * -version '[17.0,18.0)' -property installationPath
if (!$installation) { throw 'Visual Studio 2022 nicht gefunden.' }
$targets = Join-Path $installation 'MSBuild\Microsoft\VisualStudio\v17.0\OfficeTools\Microsoft.VisualStudio.Tools.Office.targets'
if (!(Test-Path $targets)) {
    $installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
    $arguments = "modify --installPath `"$installation`" --add Microsoft.VisualStudio.Workload.Office --add Microsoft.Net.Component.4.8.SDK --add Microsoft.Net.Component.4.8.TargetingPack --includeRecommended --quiet --norestart"
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) { throw "OfficeTools-Installation fehlgeschlagen: $($process.ExitCode)" }
}
if (!(Test-Path $targets)) { throw "Office-VSTO-Targets fehlen weiterhin: $targets" }
$msbuild = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.OfficeTools -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'OfficeTools/MSBuild nicht verfügbar.' }
Write-Host "OfficeTools vorhanden: $targets"
