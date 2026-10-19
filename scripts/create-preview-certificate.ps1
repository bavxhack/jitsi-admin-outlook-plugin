[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
Write-Host 'Erzeuge temporären RSA-Code-Signing-Schlüssel ohne Schutzdialog ...'
$parameters = @{
    Type = 'CodeSigningCert'
    Subject = 'CN=Meet Link Offline Preview'
    CertStoreLocation = 'Cert:\CurrentUser\My'
    Provider = 'Microsoft Enhanced RSA and AES Cryptographic Provider'
    KeyAlgorithm = 'RSA'
    KeyLength = 2048
    HashAlgorithm = 'SHA256'
    KeySpec = 'Signature'
    KeyExportPolicy = 'NonExportable'
    KeyProtection = 'None'
    NotAfter = (Get-Date).AddMonths(3)
    Confirm = $false
}
$certificate = New-SelfSignedCertificate @parameters
if (!$certificate.HasPrivateKey) { throw 'Das erzeugte Zertifikat besitzt keinen privaten Schlüssel.' }
Write-Host "Zertifikat erzeugt: $($certificate.Thumbprint)"
# Record immediately: even if public export fails, CI can clean up the private key.
if ($env:GITHUB_OUTPUT) {
    "thumbprint=$($certificate.Thumbprint)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}
$publicPath = Join-Path $OutputDirectory 'meetlink.cer'
Export-Certificate -Cert $certificate -FilePath $publicPath -Force | Out-Null
Write-Host "Öffentliches Zertifikat exportiert: $publicPath"
