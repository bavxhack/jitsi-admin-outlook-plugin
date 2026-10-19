# Offline-Installationspaket und Windows-Pipeline

Aktueller Umfang: Outlook-Classic-VSTO-Add-in mit **Kalendereintrag erstellen**, vollständig ohne API. Ein neuer ungespeicherter nativer Termin wird geöffnet; der Nutzer speichert selbst. Keine Konferenz und kein Mock-Link. Core/API bleiben für später im Repository und werden nicht als Hostabhängigkeit ausgeliefert.

## Pipeline ausführen und Artefakt beziehen

Workflow: `.github/workflows/windows-installer.yml`, Name **Windows installer (offline preview)**. Start durch Push, Pull Request oder „Run workflow“ auf GitHub. Die Änderungen müssen dafür zunächst im Repository vorhanden sein.

Der Windows-2022-Job installiert SDK 8 und nötigenfalls die Visual-Studio-2022-Office-Workload. Anschließend erstellt er ein nicht exportierbares temporäres Code-Signing-Zertifikat, vertraut dessen öffentlichem Teil ausschließlich im kurzlebigen CI-Runner, führt Core-Tests und VSTO-Build aus und publiziert mit Signierung. Der private Schlüssel wird am Ende entfernt; er landet nie im Artefakt. Es werden keine Token oder Signierungs-Secrets benötigt.

`scripts/package-offline.ps1` stellt den Paketordner zusammen. Ein etwaiger `deploymentProvider` mit festem Publish-/Updatepfad wird entfernt und das Deployment-Manifest danach **erneut signiert**. Sämtliche Manifeste werden anschließend mit Windows-SDK `mage.exe -Verify` geprüft. Ohne gültige Manifestprüfung oder bei fehlgeschlagenem Test/Build gibt es kein Installationsartefakt.

Nach einem erfolgreichen Lauf unter „Artifacts“ **Meet-Link-Outlook-Offline-<Laufnummer>** herunterladen. GitHub erzeugt daraus ein ZIP. Zusätzlich gibt es **Core-test-results-<Laufnummer>** mit TRX-Berichten, auch bei später fehlgeschlagenem Host-Build, soweit die Berichte existieren. Aufbewahrung des Installationspakets: 30 Tage.

## Paketinhalt

- `Install.cmd` / `Install.ps1`: nutzerbezogene Installation, Laufzeitprüfung und Zertifikatsbestätigung.
- `Uninstall.cmd` / `Uninstall.ps1`: Deinstallation und Entfernen des selbst hinzugefügten Testvertrauens.
- `Installer.Common.ps1`: gemeinsame Laufzeit-/Pfadprüfung.
- `Application/JitsiAdmin.OutlookPlugin.vsto`: signiertes Deployment-Manifest.
- `Application/Application Files/...`: versioniertes Application-Manifest, Host-DLL und VSTO-Abhängigkeiten.
- `MeetLink-Test.cer`: ausschließlich öffentliches Testzertifikat.
- `package.json`, `SHA256SUMS.txt`, `LIESMICH.txt`: Version, Fingerprint, Prüfsummen und Installationshinweise.

Keine PFX-, P12- oder SNK-Datei, kein privater Schlüssel, kein Token und keine notwendige API-Konfiguration im Paket. Der aktuelle Host kompiliert weder `OutlookTarget.cs` noch `UiDispatcher.cs` aus der späteren Konferenzintegration und referenziert keine Core-/Newtonsoft-/Word-Assembly. Die übrigen VSTO-/Office-Referenzen bleiben echte Windows-Hostreferenzen.

## Installation ohne Visual Studio

1. Voraussetzungen: Outlook **Classic**, .NET Framework 4.8, aktuelle Visual Studio 2010 Tools for Office Runtime. Die Runtime muss zur Office-Umgebung passen. Neues Outlook wird nicht unterstützt.
2. Gesamtes ZIP in einen normalen Ordner entpacken; Outlook vollständig schließen.
3. `Install.cmd` starten. Falls eine Runtime fehlt, nennt der Starter den offiziellen Download; nach deren Installation erneut starten. Der Starter lädt keine unbekannten Installationsprogramme herunter.
4. Testzertifikat/Fingerprint gegen das erwartete eigene Pipeline-Paket prüfen und Vertrauen mit `JA` bestätigen. Es werden ausschließlich `CurrentUser\Root` und `CurrentUser\TrustedPublisher` verwendet. Ohne Bestätigung werden keine Zertifikate importiert.
5. Der Starter kopiert das Paket nach `%LOCALAPPDATA%\MeetLinkOutlookAddin\OfflinePreview\<Version-Fingerprint>`, entsperrt dort die bewusst installierten Dateien und ruft den VSTOInstaller auf. Der standardmäßige ClickOnce-Mechanismus registriert das Add-in; keine manuell erfundene COM-Registrierung.
6. Outlook starten und unter „COM-Add-ins“ bzw. im Reiter „Meet Link“ prüfen. Kalenderbutton testen und einen Termin nativ speichern.

Nach erfolgreicher Installation kann der Downloadordner entfernt werden; die persistente Kopie und ein Installationsnachweis liegen unter LOCALAPPDATA. Der Benutzer benötigt keine .NET-SDKs, Office-Entwicklerwerkzeuge oder Buildbefehle. Für das Add-in selbst sind bei vorinstallierten Laufzeiten keine Administratorrechte vorgesehen; Unternehmensrichtlinien können Installation, PowerShell oder nicht zentral vertrauenswürdige Publisher blockieren. Der CMD-Starter verwendet ExecutionPolicy Bypass nur für seinen PowerShell-Prozess; zentral erzwungene Richtlinien werden dadurch nicht aufgehoben.

Die Preview verwendet ein pro Lauf neu erzeugtes, drei Monate gültiges Testzertifikat. Das bewusste Vertrauen in einen solchen Herausgeber ist eine Voraussetzung dieses Testpakets. Für reguläre Verteilung wird ein dauerhaftes, organisationsseitig akzeptiertes Code-Signing-Zertifikat mit geeigneter Zeitstempel-/Erneuerungsrichtlinie benötigt. Abgelaufene Vorschaupakete neu bauen. Keine pauschale Office-Sicherheitsabsenkung.

## Neuere Version und Deinstallation

Eine neue CI-Version hat eine erhöhte ClickOnce-Version `0.1.<Laufnummer>.0` und ein eigenes Testzertifikat. Outlook schließen und `Install.cmd` des neuen Pakets ausführen. Der Starter deinstalliert anhand seines Installationsnachweises die alte Registrierung, bevor er die neue installiert. Es gibt keine automatische Updateprüfung und keinen fest eingebetteten Pfad auf dem Runner. Wenn die neue Installation nach alter Deinstallation fehlschlägt, lässt sich die vorherige Version mit deren Paket erneut installieren; ein vollständiger transaktionaler Rollback wird nicht zugesichert.

Zum Entfernen Outlook schließen und `Uninstall.cmd` aus dem Paket oder aus der persistenten Installationskopie starten. Der Installer entfernt nur die von ihm selbst hinzugefügten Zertifikatseinträge; vorab bestehendes Vertrauen bleibt bestehen. Kalendertermine, vorhandene API-Konfiguration und sonstige Benutzerdaten werden nicht gelöscht. Ein manuelles Entfernen über „Programme und Features“ ersetzt nicht die gesonderte Bereinigung des Testzertifikatsvertrauens.

## Entwickler und verbleibende Abnahme

`scripts/office-toolchain.ps1` erkennt MSBuild und Office-Targets anhand der tatsächlichen Dateien derselben Visual-Studio-Installation. Ein optionaler Komponentenregistrierungsfilter ist nicht erforderlich; Build und Publish verwenden denselben expliziten VSToolsPath. `scripts/test-office-toolchain.ps1` prüft die Erkennung vor der CI-Provisionierung mit temporären Dateisystem-Fixtures.

Lokale Buildvoraussetzungen: Visual Studio 2022 mit OfficeTools, .NET Framework 4.8 Developer/Targeting Pack, SDK 8 und Windows SDK mit `mage.exe`. `scripts/ensure-office-tools.ps1` kann die Office-Workload ergänzen; das benötigt auf dem Buildrechner entsprechende Installationsrechte. Der Runner braucht Outlook nicht für die Kompilierung, kann daher aber auch dessen Ladefähigkeit nicht testen.

AnyCPU/Prefer32Bit=false ermöglicht denselben verwalteten Host für beide Outlook-Bitnessen; **32-Bit und 64-Bit müssen trotzdem getrennt real getestet werden**. Ein grüner Core-/Host-Build und verifizierte Manifeste beweisen weder erfolgreiche Outlook-Ladung noch funktionierende Office-COM-Integration. [Manuellen Testplan](manual-test-plan.md) ausführen.

Aktuell ist noch kein Windows-CI-Lauf aus dieser Umgebung ausgeführt worden. Ein herunterladbares Binary darf erst nach erfolgreichem Workflow behauptet werden; siehe [Implementierungsstatus](implementation-status.md).
