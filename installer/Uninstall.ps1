[CmdletBinding()]
param()
. "$PSScriptRoot\Installer.Common.ps1"
Assert-OutlookClosed
$base = Get-MeetLinkDirectory
$receiptPath = Join-Path $base 'installation.json'
if (!(Test-Path -LiteralPath $receiptPath)) { throw 'Kein Installationsnachweis vorhanden. Gegebenenfalls über Windows Programme und Features deinstallieren.' }
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
Assert-LocalManifest $receipt.manifest
$tool = Get-VstoInstaller
Invoke-VstoInstaller $tool '/Uninstall' $receipt.manifest
# Remove only certificates that this installer itself added, never pre-existing trust.
foreach ($owned in @($receipt.ownedCertificates)) {
    if ($owned.thumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw 'Ungültiger Zertifikatsnachweis.' }
    foreach ($store in @($owned.stores)) {
        if ($store -notin @('Root', 'TrustedPublisher')) { throw 'Unerwarteter Zertifikatsspeicher.' }
        $path = "Cert:\CurrentUser\$store\$($owned.thumbprint)"
        if (Test-Path $path) { Remove-Item $path -Force }
    }
}
Remove-Item -LiteralPath $base -Recurse -Force
Write-Host 'Meet Link Offline-Vorschau deinstalliert. Vorhandene Kalendertermine und Konfigurationen bleiben erhalten.'
