$ErrorActionPreference = 'Stop'
function Get-MeetLinkDirectory {
    return Join-Path $env:LOCALAPPDATA 'MeetLinkOutlookAddin\OfflinePreview'
}
function Assert-OutlookClosed {
    if (Get-Process -Name OUTLOOK -ErrorAction SilentlyContinue) {
        throw 'Bitte Outlook vollständig schließen und den Installer anschließend erneut starten.'
    }
}
function Get-VstoInstaller {
    $platform = $null
    foreach ($key in @('HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration', 'HKLM:\SOFTWARE\Microsoft\Office\16.0\Outlook', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Office\16.0\Outlook')) {
        $properties = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        if ($properties.Platform) { $platform = $properties.Platform; break }
        if ($properties.Bitness) { $platform = $properties.Bitness; break }
    }
    $x64 = Join-Path ${env:ProgramFiles} 'Common Files\microsoft shared\VSTO\10.0\VSTOInstaller.exe'
    $x86 = if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'Common Files\microsoft shared\VSTO\10.0\VSTOInstaller.exe' } else { $x64 }
    $candidates = if ($platform -eq 'x86') { @($x86, $x64) } else { @($x64, $x86) }
    foreach ($path in $candidates) { if (Test-Path -LiteralPath $path) { return $path } }
    throw 'VSTO Runtime fehlt. Bitte Microsoft Visual Studio 2010 Tools for Office Runtime installieren: https://www.microsoft.com/download/details.aspx?id=48217 . Anschließend Install.cmd erneut starten.'
}
function Assert-Framework {
    $framework = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
    if (!$framework -or $framework.Release -lt 528040) {
        throw '.NET Framework 4.8 fehlt. Bitte über https://dotnet.microsoft.com/download/dotnet-framework/net48 installieren und danach Install.cmd erneut starten.'
    }
}
function Invoke-VstoInstaller([string]$Tool, [string]$Operation, [string]$Manifest) {
    $process = Start-Process -FilePath $Tool -ArgumentList "$Operation `"$Manifest`"" -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "VSTOInstaller meldet Fehler $($process.ExitCode). Installation/Vertrauen und Outlook Classic prüfen." }
}
function Assert-LocalManifest([string]$Manifest) {
    $root = [IO.Path]::GetFullPath((Get-MeetLinkDirectory)).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($Manifest)
    if (!$full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($full) -ne 'JitsiAdmin.OutlookPlugin.vsto') {
        throw 'Installationsnachweis enthält einen unerwarteten Manifestpfad.'
    }
}
