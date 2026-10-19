[CmdletBinding()]
param()
. "$PSScriptRoot\Installer.Common.ps1"
Assert-OutlookClosed
Assert-Framework
$tool = Get-VstoInstaller
$metadata = Get-Content -LiteralPath "$PSScriptRoot\package.json" -Raw | ConvertFrom-Json
if ($metadata.version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Ungültige Paketversion.' }
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $PSScriptRoot 'MeetLink-Test.cer'))
if ($certificate.Thumbprint -ne $metadata.certificateThumbprint -or $certificate.NotAfter -le (Get-Date)) { throw 'Testzertifikat stimmt nicht mit dem Paket überein oder ist abgelaufen. Frisches Pipeline-Artefakt laden.' }
$missing = @('Root', 'TrustedPublisher') | Where-Object { !(Test-Path "Cert:\CurrentUser\$_\$($certificate.Thumbprint)") }
if (@($missing).Count -gt 0) {
    Write-Host 'Dieses Paket ist eine selbstsignierte Testversion für Outlook Classic.'
    Write-Host "Zertifikat: $($certificate.Subject)"
    Write-Host "Fingerabdruck: $($certificate.Thumbprint)"
    Write-Host 'Nur fortfahren, wenn das Paket aus dem erwarteten GitHub-Actions-Lauf stammt.'
    if ((Read-Host 'Testzertifikat für diesen Windows-Benutzer vertrauen? Zum Bestätigen JA eingeben') -cne 'JA') { throw 'Installation ohne Änderung der Zertifikatsspeicher abgebrochen.' }
}
$base = Get-MeetLinkDirectory
$receiptPath = Join-Path $base 'installation.json'
$previous = if (Test-Path $receiptPath) { Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json } else { $null }
$destination = Join-Path $base ("$($metadata.version)-$($certificate.Thumbprint.Substring(0,8))")
New-Item -ItemType Directory -Force $destination | Out-Null
if ([IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') -ne [IO.Path]::GetFullPath($destination).TrimEnd('\')) {
    Get-ChildItem -LiteralPath $PSScriptRoot | Copy-Item -Destination $destination -Recurse -Force
}
Get-ChildItem -LiteralPath $destination -Recurse -File | Unblock-File
$manifest = Join-Path $destination 'Application\JitsiAdmin.OutlookPlugin.vsto'
if (!(Test-Path -LiteralPath $manifest)) { throw 'Deployment-Manifest fehlt. Bitte das komplette ZIP entpacken.' }
$added = @()
try {
    foreach ($store in $missing) {
        Import-Certificate -FilePath "$PSScriptRoot\MeetLink-Test.cer" -CertStoreLocation "Cert:\CurrentUser\$store" | Out-Null
        $added += $store
    }
    if ($previous) {
        Assert-LocalManifest $previous.manifest
        if (Test-Path -LiteralPath $previous.manifest) { Invoke-VstoInstaller $tool '/Uninstall' $previous.manifest }
    }
    Invoke-VstoInstaller $tool '/Install' $manifest
    $owned = @()
    if ($previous) { $owned += @($previous.ownedCertificates) }
    if ($added.Count -gt 0) { $owned += @{ thumbprint = $certificate.Thumbprint; stores = $added } }
    @{ manifest = $manifest; version = $metadata.version; ownedCertificates = $owned } | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $receiptPath
} catch {
    foreach ($store in $added) { Remove-Item "Cert:\CurrentUser\$store\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue }
    throw
}
Write-Host 'Meet Link ist installiert. Outlook Classic starten: Meet Link > Kalendereintrag erstellen.'
Write-Host 'Keine API, kein Token und keine Konfigurationsdatei erforderlich. Termin mit Outlook selbst speichern.'
