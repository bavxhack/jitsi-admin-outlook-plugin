# Meet Link – installierbare Offline-Vorschau

Diese erste Version ist bewusst **ohne API**: Im Outlook-Hauptfenster gibt es den Reiter **Meet Link** und den Button **Kalendereintrag erstellen**. Ein Klick öffnet einen neuen nativen Outlook-Termin im Standardkalender. Betreff, Zeit und weitere Werte werden von Outlook vorgegeben; Bearbeiten und Speichern erfolgen mit den normalen Outlook-Befehlen. Markierte Termine werden nicht verändert.

Kein Server, kein Token, keine Konfigurationsdatei und kein Beispiellink erforderlich. Ziel bleibt ein echtes **VSTO-Add-in für Outlook Classic unter Windows**, kein Office.js-Add-in. Neues Outlook wird nicht unterstützt.

## Fertiges Installationspaket aus der Pipeline

Der Workflow [.github/workflows/windows-installer.yml](.github/workflows/windows-installer.yml) läuft bei Push, Pull Request und manuell über **Actions → Windows installer (offline preview) → Run workflow**.

1. Änderungen ins GitHub-Repository übernehmen/pushen und den Workflow abwarten.
2. Im erfolgreichen Lauf unter **Artifacts** das Paket **Meet-Link-Outlook-Offline-<Laufnummer>** herunterladen.
3. ZIP vollständig entpacken, Outlook schließen und **Install.cmd** doppelklicken.
4. Das beiliegende Testzertifikat prüfen und dessen Vertrauen einmal mit `JA` bestätigen. Das Paket nur aus dem erwarteten eigenen GitHub-Lauf installieren.
5. Outlook Classic starten: **Meet Link → Kalendereintrag erstellen**. Termin bearbeiten und nativ speichern.

Der Installationsrechner benötigt **keine Entwicklungswerkzeuge**. Voraussetzungen sind Outlook Classic, .NET Framework 4.8 und VSTO Runtime. Wenn eine Laufzeit fehlt, zeigt der Installationsstarter den offiziellen Download. Installation des Add-ins und Zertifikatsvertrauen erfolgen für den aktuellen Benutzer. Organisationsrichtlinien können eine Freigabe durch die IT verlangen.

**Noch kein erfolgreicher Windows-Lauf nachgewiesen:** Die Pipeline ist angelegt, konnte aus dieser Linux-Umgebung wegen der nicht erreichbaren Proxy-/GitHub-Verbindung aber nicht gestartet werden. Ein fertiges Binärartefakt liegt deshalb noch nicht vor. Der erste erfolgreiche GitHub-Lauf erstellt es; ein fehlgeschlagener Build lädt kein scheinbar fertiges Installationspaket hoch. Details: [Prüfstatus](docs/implementation-status.md).

## Was die Pipeline liefert

- Windows-2022-Runner, .NET SDK 8, Office-/VSTO-Buildwerkzeuge; fehlende OfficeTools werden installiert.
- Core-Tests vor dem Paketbau, anschließend echter .NET-Framework-4.8-VSTO-Build.
- Temporäres selbstsigniertes Code-Signing-Zertifikat ohne Repository-Secrets; gültig drei Monate. Nur das öffentliche Zertifikat wird ausgeliefert.
- Signierte und mit `mage.exe` überprüfte Deployment-/Application-Manifeste.
- Portables ZIP mit `Install.cmd`, `Uninstall.cmd`, öffentlichen Zertifikat, Prüfsummen und vollständigen ClickOnce-Dateien. Keine feste Update-URL zum CI-Runner, keine automatischen Updates.
- Separates Artefakt mit Core-Testberichten. Installationspakete bleiben 30 Tage bei GitHub verfügbar.

Für eine neuere Vorschau deren `Install.cmd` ausführen. Der Installer entfernt die bisher registrierte Vorschau vor Installation der neuen Version. Mit `Uninstall.cmd` werden Add-in und das ausschließlich durch diesen Installer hinzugefügte Zertifikatsvertrauen entfernt. Details und Grenzen: [Bereitstellung](docs/deployment.md).

## Lokal unter Windows bauen

Nur Entwickler benötigen Visual Studio 2022 mit Office-/SharePoint-Entwicklung, .NET Framework 4.8 Developer Pack und .NET SDK 8:

```powershell
.\scripts\ensure-office-tools.ps1
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=Meet Link Offline Preview' -CertStoreLocation Cert:\CurrentUser\My -Provider 'Microsoft Enhanced RSA and AES Cryptographic Provider' -KeyAlgorithm RSA -KeySpec Signature
# Testzertifikat nur auf der eigenen Build-Testmaschine vertrauen:
$public = Export-Certificate -Cert $cert -FilePath "$env:TEMP\meetlink.cer"
Import-Certificate -FilePath $public.FullName -CertStoreLocation Cert:\CurrentUser\Root
Import-Certificate -FilePath $public.FullName -CertStoreLocation Cert:\CurrentUser\TrustedPublisher
.\scripts\package-offline.ps1 -CertificateThumbprint $cert.Thumbprint
```

Ergebnis: `artifacts\Meet-Link-Outlook-Offline\`. Den gesamten Ordner weitergeben, nicht nur eine DLL oder `.vsto`. Kein privater Schlüssel wird mitkopiert. Ein Testzertifikat ist keine produktive Signierung.

## Spätere API-Stufe

Core, Beispielkonfiguration, API-Tests und bisherige Office-Integrationsquellen bleiben als Grundlage im Repository. **Der aktuelle Host bindet Core/API, Word-Einfügung und Servermenü nicht ein**; sie sind nicht Teil des Offline-Installationspakets. Auch eine vorhandene `config.json` aktiviert keine API.

[API-Vertrag](docs/api-contract.md) und [Konfiguration](docs/configuration.md) beschreiben ausschließlich die spätere Stufe und sind keine Voraussetzung für die aktuelle Installation. Die damaligen Endpunkte sind unbestätigte Beispiele. Erst später werden Serverauswahl und echte Konferenzerstellung wieder in den Host integriert.

Offline-Abnahme: [manueller Testplan](docs/manual-test-plan.md). Lokal mögliche Strukturprüfung: `python3 scripts/verify-source.py`; sie ersetzt weder Build noch Outlook-Test.
