[CmdletBinding()]
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$directory = Join-Path $env:APPDATA 'MeetLinkOutlookAddin'
$path = Join-Path $directory 'config.json'
if ((Test-Path $path) -and !$Force) { throw 'Konfiguration vorhanden. Zum bewussten Ersetzen -Force verwenden.' }
New-Item -ItemType Directory -Force $directory | Out-Null
Copy-Item (Join-Path (Split-Path $PSScriptRoot -Parent) 'config\config.mock.json') $path
Write-Host "MOCK-Konfiguration: $path. MEETLINK_OUTLOOK_CONFIG kann diesen Pfad übersteuern."
