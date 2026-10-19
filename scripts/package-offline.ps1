[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [string]$Version = '0.1.0.0'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$package = Join-Path $root 'artifacts\Meet-Link-Outlook-Offline'
if (Test-Path $package) { Remove-Item -Recurse -Force $package }
New-Item -ItemType Directory -Force $package | Out-Null
$publish = Join-Path $package 'Application'
& "$PSScriptRoot\publish-windows.ps1" -CertificateThumbprint $CertificateThumbprint -Version $Version -OutputDirectory $publish -Portable
# A portable preview must never point to a hosted runner's C:\ path.
$deployment = Get-Item (Join-Path $publish 'JitsiAdmin.OutlookPlugin.vsto')
[xml]$xml = Get-Content -LiteralPath $deployment.FullName -Raw
$providers = @($xml.SelectNodes("//*[local-name()='deploymentProvider']"))
if ($providers.Count -gt 0) {
    foreach ($node in $providers) { $null = $node.ParentNode.RemoveChild($node) }
    $xml.Save($deployment.FullName)
}
$tools = "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows"
$mage = Get-ChildItem -Path $tools -Filter mage.exe -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (!$mage) { throw 'mage.exe aus dem Windows/.NET Framework SDK fehlt.' }
if ($providers.Count -gt 0) {
    & $mage.FullName -Sign $deployment.FullName -CertHash $CertificateThumbprint
    if ($LASTEXITCODE -ne 0) { throw 'Portables Deployment-Manifest konnte nicht erneut signiert werden.' }
}
$manifests = @(Get-ChildItem -Path $publish -Filter '*.manifest' -Recurse)
if ($manifests.Count -eq 0) { throw 'Application-Manifest fehlt.' }
foreach ($manifest in (@($deployment) + $manifests)) {
    [xml]$document = Get-Content -LiteralPath $manifest.FullName -Raw
    foreach ($attribute in $document.SelectNodes('//@codebase')) {
        $absolute = $null
        if ([Uri]::TryCreate($attribute.Value, [UriKind]::Absolute, [ref]$absolute)) {
            throw "Nicht portabler absoluter codebase-Pfad im Manifest: $($manifest.Name)"
        }
    }
    & $mage.FullName -Verify $manifest.FullName
    if ($LASTEXITCODE -ne 0) { throw "Manifestprüfung fehlgeschlagen: $($manifest.Name)" }
}
[xml]$verified = Get-Content -LiteralPath $deployment.FullName -Raw
if ($verified.SelectNodes("//*[local-name()='deploymentProvider']").Count -ne 0) { throw 'Deployment enthält weiterhin einen festen Updatepfad.' }
foreach ($name in @('Install.cmd', 'Install.ps1', 'Installer.Common.ps1', 'Uninstall.cmd', 'Uninstall.ps1', 'LIESMICH.txt')) {
    Copy-Item -LiteralPath (Join-Path $root "installer\$name") -Destination $package
}
$cert = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint"
if (!$cert.HasPrivateKey) { throw 'Signierungszertifikat ohne privaten Schlüssel.' }
# Export only the PUBLIC certificate. No PFX or private key belongs in the artifact.
Export-Certificate -Cert $cert -FilePath (Join-Path $package 'MeetLink-Test.cer') | Out-Null
@{ version = $Version; certificateThumbprint = $cert.Thumbprint; preview = $true } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $package 'package.json')
Get-ChildItem $package -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    $hash = Get-FileHash $_.FullName -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash, $_.FullName.Substring($package.Length + 1)
} | Set-Content -Encoding UTF8 (Join-Path $package 'SHA256SUMS.txt')
if (Get-ChildItem $package -Recurse -File | Where-Object { $_.Extension -in @('.pfx', '.p12', '.snk') }) { throw 'Privater Schlüssel im Paket gefunden.' }
Write-Host "Installationsartefakt: $package"
