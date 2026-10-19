# Shared discovery for all VSTO build/publish steps. No optional component-ID filter.
function Get-VisualStudioInstallationPaths {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (!(Test-Path -LiteralPath $vswhere -PathType Leaf)) { throw 'Visual Studio Installer/vswhere fehlt.' }
    $paths = @(& $vswhere -all -products '*' -version '[17.0,18.0)' -sort -property installationPath)
    if ($LASTEXITCODE -ne 0) { throw "vswhere fehlgeschlagen: $LASTEXITCODE" }
    $paths = @($paths | Where-Object { ![string]::IsNullOrWhiteSpace($_) })
    if ($paths.Count -eq 0) { throw 'Keine Visual-Studio-2022-Installation gefunden.' }
    return $paths
}
function Resolve-OfficeToolchain {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string[]]$InstallationPaths, [switch]$AllowMissing)
    $diagnostics = @()
    foreach ($installation in $InstallationPaths) {
        $vstools = Join-Path $installation 'MSBuild\Microsoft\VisualStudio\v17.0'
        $targets = Join-Path $vstools 'OfficeTools\Microsoft.VisualStudio.Tools.Office.targets'
        $msbuild = $null
        foreach ($relative in @('MSBuild\Current\Bin\MSBuild.exe', 'MSBuild\Current\Bin\amd64\MSBuild.exe')) {
            $candidate = Join-Path $installation $relative
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $msbuild = $candidate; break }
        }
        $hasTargets = Test-Path -LiteralPath $targets -PathType Leaf
        if ($msbuild -and $hasTargets) {
            return [pscustomobject]@{
                InstallationPath = $installation
                MSBuildPath = $msbuild
                VSToolsPath = $vstools
                OfficeTargetsPath = $targets
            }
        }
        $diagnostics += "$installation : MSBuild=$([bool]$msbuild), OfficeTargets=$hasTargets"
    }
    if ($AllowMissing) { return $null }
    throw ("Keine vollständige VSTO-Toolchain in derselben VS-Installation gefunden. " + ($diagnostics -join '; '))
}
