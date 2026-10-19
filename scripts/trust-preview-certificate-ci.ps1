[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CertificatePath,
    [Parameter(Mandatory=$true)][string]$ExpectedThumbprint
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Maschinenvertrauen ist ausschließlich für den kurzlebigen GitHub-hosted CI-Runner vorgesehen.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Der GitHub-Runner benötigt Administratorrechte für den CI-Zertifikatsspeicher.'
}
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
if ($certificate.Thumbprint -ne $ExpectedThumbprint) { throw 'Unerwartetes CI-Zertifikat.' }
# CurrentUser\Root may trigger a native Security Warning dialog that -NonInteractive
# cannot answer. Use LocalMachine on the disposable elevated runner instead.
$utility = Join-Path $env:SystemRoot 'System32\certutil.exe'
foreach ($store in @('Root', 'TrustedPublisher')) {
    Write-Host "Importiere öffentliches Testzertifikat in LocalMachine\$store ..."
    $arguments = "-f -addstore $store `"$CertificatePath`""
    $process = Start-Process -FilePath $utility -ArgumentList $arguments -NoNewWindow -PassThru
    try {
        if (!$process.WaitForExit(30000)) {
            try { $process.Kill() } catch { }
            throw "CI-Zertifikatsimport nach 30 Sekunden abgebrochen: $store"
        }
        if ($process.ExitCode -ne 0) { throw "certutil-Import fehlgeschlagen: $store (Exit $($process.ExitCode))" }
    } finally { $process.Dispose() }
    if (!(Test-Path "Cert:\LocalMachine\$store\$ExpectedThumbprint")) { throw "Import nicht bestätigt: $store" }
    Write-Host "CI-Vertrauen bestätigt: LocalMachine\$store"
}
