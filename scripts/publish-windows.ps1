[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [string]$InstallUrl,
    [switch]$Portable,
    [string]$Version = '0.1.0.0',
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\publish')
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$' -or @($Version.Split('.') | Where-Object { [long]$_ -gt 65535 }).Count -gt 0) { throw 'Version muss vier Zahlen zwischen 0 und 65535 enthalten.' }
if (!$Portable -and [string]::IsNullOrWhiteSpace($InstallUrl)) { throw 'InstallUrl angeben oder -Portable verwenden.' }
$cert = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint"
if (!$cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date)) { throw 'Gültiges Zertifikat mit privatem Schlüssel erforderlich.' }
& "$PSScriptRoot\build-windows.ps1" -Configuration Release -CertificateThumbprint $CertificateThumbprint
. "$PSScriptRoot\office-toolchain.ps1"
$toolchain = Resolve-OfficeToolchain -InstallationPaths @(Get-VisualStudioInstallationPaths)
$msbuild = $toolchain.MSBuildPath
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$publish = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\') + '\'
$project = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\JitsiAdmin.OutlookPlugin\JitsiAdmin.OutlookPlugin.csproj'
$arguments = @($project, '/restore', '/t:Publish', '/p:Configuration=Release', '/p:Platform=AnyCPU', '/p:SignManifests=true',
    "/p:ManifestCertificateThumbprint=$CertificateThumbprint", "/p:PublishUrl=$publish", "/p:ApplicationVersion=$Version", '/v:minimal')
if ($Portable) {
    # No runner-specific update URL or bootstrapper URLs in a downloadable offline preview.
    $arguments += @('/p:InstallUrl=', '/p:UpdateUrl=', '/p:UpdateEnabled=false', '/p:BootstrapperEnabled=false')
} else { $arguments += "/p:InstallUrl=$InstallUrl" }
$arguments += "/p:VSToolsPath=$($toolchain.VSToolsPath)"
& $msbuild @arguments
if ($LASTEXITCODE -ne 0) { throw 'ClickOnce-Publish fehlgeschlagen.' }
if (!(Get-ChildItem $publish -Filter '*.vsto' -Recurse)) { throw 'Deployment-Manifest fehlt.' }
if (!(Get-ChildItem $publish -Filter '*.manifest' -Recurse)) { throw 'Application-Manifest fehlt.' }
Write-Host "ClickOnce-Ausgabe: $publish"
