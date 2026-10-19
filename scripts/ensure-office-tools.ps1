[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\office-toolchain.ps1"
$installations = @(Get-VisualStudioInstallationPaths)
$toolchain = Resolve-OfficeToolchain -InstallationPaths $installations -AllowMissing
if (!$toolchain) {
    $installation = $installations[0]
    $installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
    $arguments = "modify --installPath `"$installation`" --add Microsoft.VisualStudio.Workload.Office --add Microsoft.Net.Component.4.8.SDK --add Microsoft.Net.Component.4.8.TargetingPack --includeRecommended --quiet --norestart"
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) { throw "OfficeTools-Installation fehlgeschlagen: $($process.ExitCode)" }
    $toolchain = Resolve-OfficeToolchain -InstallationPaths @(Get-VisualStudioInstallationPaths)
}
Write-Host "MSBuild vorhanden: $($toolchain.MSBuildPath)"
Write-Host "OfficeTools vorhanden: $($toolchain.OfficeTargetsPath)"
