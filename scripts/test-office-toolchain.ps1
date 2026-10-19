[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\office-toolchain.ps1"
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('meetlink-toolchain-' + [Guid]::NewGuid().ToString('N'))
function Add-TestFile([string]$Root, [string]$Relative) {
    $path = Join-Path $Root $Relative
    New-Item -ItemType Directory -Force (Split-Path $path -Parent) | Out-Null
    Set-Content -LiteralPath $path -Value 'test fixture only'
}
function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw $Message }
}
try {
    # Existing runner toolchain must work without requiring a component registration ID.
    $valid = Join-Path $temporary 'VS Enterprise'
    Add-TestFile $valid 'MSBuild\Current\Bin\MSBuild.exe'
    Add-TestFile $valid 'MSBuild\Microsoft\VisualStudio\v17.0\OfficeTools\Microsoft.VisualStudio.Tools.Office.targets'
    $result = Resolve-OfficeToolchain -InstallationPaths @($valid)
    Assert-Equal $valid $result.InstallationPath 'Vorhandene Toolchain nicht gefunden.'
    Assert-Equal (Join-Path $valid 'MSBuild\Current\Bin\MSBuild.exe') $result.MSBuildPath 'Falscher MSBuild-Pfad.'
    Assert-Equal (Join-Path $valid 'MSBuild\Microsoft\VisualStudio\v17.0') $result.VSToolsPath 'Falscher Office-Importpfad.'

    # Never mix MSBuild from one installation with Office targets from another.
    $buildOnly = Join-Path $temporary 'VS Build Only'
    $officeOnly = Join-Path $temporary 'VS Office Only'
    Add-TestFile $buildOnly 'MSBuild\Current\Bin\MSBuild.exe'
    Add-TestFile $officeOnly 'MSBuild\Microsoft\VisualStudio\v17.0\OfficeTools\Microsoft.VisualStudio.Tools.Office.targets'
    $missing = Resolve-OfficeToolchain -InstallationPaths @($buildOnly, $officeOnly) -AllowMissing
    if ($null -ne $missing) { throw 'Unvollständige Installationen wurden vermischt.' }
    $failed = $false
    try { Resolve-OfficeToolchain -InstallationPaths @($buildOnly) | Out-Null } catch { $failed = $true }
    if (!$failed) { throw 'Fehlende Office-Targets wurden nicht abgelehnt.' }
    $result = Resolve-OfficeToolchain -InstallationPaths @($buildOnly, $valid)
    Assert-Equal $valid $result.InstallationPath 'Vollständige alternative Installation nicht gewählt.'

    $amd64 = Join-Path $temporary 'VS amd64'
    Add-TestFile $amd64 'MSBuild\Current\Bin\amd64\MSBuild.exe'
    Add-TestFile $amd64 'MSBuild\Microsoft\VisualStudio\v17.0\OfficeTools\Microsoft.VisualStudio.Tools.Office.targets'
    $result = Resolve-OfficeToolchain -InstallationPaths @($amd64)
    Assert-Equal (Join-Path $amd64 'MSBuild\Current\Bin\amd64\MSBuild.exe') $result.MSBuildPath 'amd64-Fallback fehlgeschlagen.'
    Write-Host 'PASS: Office/MSBuild-Dateierkennung, Leerzeichen, getrennte Installationen, Fehlerfall und amd64-Fallback.'
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
