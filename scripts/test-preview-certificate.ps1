[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('meetlink-cert-test-' + [Guid]::NewGuid().ToString('N'))
$previousOutput = $env:GITHUB_OUTPUT
$previousActions = $env:GITHUB_ACTIONS
$certificateTestState = @{ CreationParameters = $null; FailExport = $false }
# Mock Windows cmdlets: these tests create neither real keys nor trust entries.
function New-SelfSignedCertificate {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param($Type, $Subject, $CertStoreLocation, $Provider, $KeyAlgorithm, $KeyLength,
        $HashAlgorithm, $KeySpec, $KeyExportPolicy, $KeyProtection, $NotAfter)
    $certificateTestState.CreationParameters = $PSBoundParameters
    return [pscustomobject]@{ Thumbprint = ('A' * 40); HasPrivateKey = $true }
}
function Export-Certificate {
    param($Cert, [string]$FilePath, [switch]$Force)
    if ($certificateTestState.FailExport) { throw 'Simulated export failure' }
    [IO.File]::WriteAllText($FilePath, 'public certificate fixture')
}
function Import-Certificate { throw 'Certificate creation must not import trust.' }
try {
    New-Item -ItemType Directory -Force $temporary | Out-Null
    $env:GITHUB_OUTPUT = Join-Path $temporary 'output.txt'
    & "$PSScriptRoot\create-preview-certificate.ps1" -OutputDirectory $temporary
    if ($certificateTestState.CreationParameters.KeyProtection -ne 'None') { throw 'Key protection UI was not disabled.' }
    if ($certificateTestState.CreationParameters.KeyExportPolicy -ne 'NonExportable') { throw 'Private key must be non-exportable.' }
    if (!$certificateTestState.CreationParameters.ContainsKey('Confirm') -or $certificateTestState.CreationParameters.Confirm) { throw 'Confirmation must explicitly be disabled.' }
    $expected = 'thumbprint=' + ('A' * 40)
    if ((Get-Content $env:GITHUB_OUTPUT -Raw).Trim() -ne $expected) { throw 'Missing cleanup identity.' }
    if (!(Test-Path (Join-Path $temporary 'meetlink.cer'))) { throw 'Public certificate export missing.' }

    # A failure after key creation must still leave the thumbprint for CI cleanup.
    Remove-Item $env:GITHUB_OUTPUT
    $certificateTestState.FailExport = $true
    $failed = $false
    try { & "$PSScriptRoot\create-preview-certificate.ps1" -OutputDirectory $temporary } catch { $failed = $true }
    if (!$failed -or (Get-Content $env:GITHUB_OUTPUT -Raw).Trim() -ne $expected) { throw 'Cleanup identity lost on export failure.' }

    $env:GITHUB_ACTIONS = 'false'
    $refused = $false
    try {
        & "$PSScriptRoot\trust-preview-certificate-ci.ps1" -CertificatePath 'not-a-certificate' -ExpectedThumbprint ('A' * 40)
    } catch {
        $refused = $_.Exception.Message -like '*GitHub-hosted*'
    }
    if (!$refused) { throw 'Machine trust must be refused outside the hosted CI runner.' }
    Write-Host 'PASS: prompt-free certificate parameters, public export, cleanup identity and hosted-runner trust guard (mocked).'
} finally {
    $env:GITHUB_OUTPUT = $previousOutput
    $env:GITHUB_ACTIONS = $previousActions
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
