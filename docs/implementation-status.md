# CI-Korrektur: OfficeTools/MSBuild-Erkennung

Der gemeldete Windows-Lauf erreichte vorhandene Office-Targets, scheiterte aber anschließend am zusätzlichen `vswhere -requires Microsoft.VisualStudio.Component.OfficeTools`-Filter. Dieser Metadatenfilter wird nicht mehr verwendet.

`scripts/office-toolchain.ps1` ermittelt VS-2022-Installationen und prüft MSBuild.exe sowie Office-Targets im Dateisystem derselben Installation. Ensure, Build und Publish verwenden dieselbe Erkennung; der gewählte VSToolsPath wird explizit an MSBuild übergeben. Eine bereits vollständige Toolchain wird ohne unnötige Neuinstallation verwendet. Fehlende Dateien werden mit konkreten Pfaden diagnostiziert.

Prüfungen dieser Korrektur:

- Portable PowerShell 7.4.6 im Arbeitsbereich bereitgestellt; erster Start ohne Cache-Override scheiterte am schreibgeschützten Benutzerverzeichnis. Mit XDG-Cache-/Konfigurations-/Datenpfaden im Arbeitsbereich erfolgreich gestartet.
- `pwsh -NoProfile -File scripts/test-office-toolchain.ps1`: Exit 0. Vorhandene Dateien ohne Komponentenmetadaten, Pfade mit Leerzeichen, getrennte unvollständige Installationen, fehlende Targets, alternative vollständige Installation und amd64-Fallback geprüft. Diese Dateisystemtests laufen künftig auch vor OfficeTools-Provisionierung in CI.
- PowerShell-Parserprüfung aller `.ps1`-Dateien: Exit 0 unter PowerShell 7.4.6/Linux. Windows PowerShell 5.1 wird weiterhin im Workflow geprüft.
- `python3 scripts/verify-source.py`: erfolgreich. Kein Windows-/VSTO-Build durch diesen Test belegt; dazu ist der nächste CI-Lauf erforderlich.

---

# Aktueller Status: Offline-Kalenderbutton und Windows-Pipeline

Stand 19. Oktober 2026, nach der Eingrenzung durch den Nutzer: zunächst **nur installierbares Plugin ohne API**, das einen Kalendereintrag erstellt, plus Pipeline für ein direkt installierbares Artefakt.

## Aktuell implementiert

- Outlook-Explorer-Ribbon mit direktem Button **Kalendereintrag erstellen**. Öffnet einen neuen nativen Termin im Standardkalender, ohne vorhandene Elemente zu ändern. Keine Save-/Send-Aufrufe; Nutzer speichert nativ.
- Der aktive Host hat keinen API-Client und keinen Konfigurationslader. Core/Newtonsoft und die künftige Word-/Konferenzintegration sind keine Buildabhängigkeiten des Offline-Hosts. Eine vorhandene JSON-Datei aktiviert keine API.
- `.github/workflows/windows-installer.yml`: Push/PR/manueller Windows-2022-Build, OfficeTools-Provisionierung, SDK 8, Core-Tests, signierter VSTO-Build/Publish, `mage`-Prüfung und Upload eines Installationspakets plus Testberichten.
- `scripts/package-offline.ps1`: portable ClickOnce-Manifeste ohne Runner-/Updatepfad, erneute Signierung nach einer nötigen Provider-Entfernung, nur öffentliches CER, Prüfsummen, Installer/Uninstaller.
- `installer/`: Doppelklickstarter, Laufzeitprüfung, explizites Testzertifikatsvertrauen im Benutzerkontext, persistente lokale Installation, Austausch einer alten Vorschau und Deinstallation einschließlich selbst hinzugefügtem Zertifikatsvertrauen.
- README, Bereitstellung und manueller Testplan auf Offline-Vorschau umgestellt. API-Quellen/-Dokumentation bleiben als spätere Stufe erhalten, nicht als aktive Produktfunktion.

## Tatsächlich geprüft / noch blockiert

- Bestehenden Checkout/Branch `work` beibehalten, vorherige Dateien erhalten und gezielt geändert. Weiterhin kein Commit/Push aus dieser Sitzung.
- Erneute Umgebungsprüfung: Linux, weiterhin kein `dotnet` oder `pwsh`; daher weder C#-Build noch PowerShell-/Windows-Ausführung möglich.
- `curl -I --max-time 10 https://dot.net/v1/dotnet-install.sh`: Exit 7, Proxy auf Port 8080 nicht erreichbar.
- `gh auth status`: Exit 1; GitHub-Verbindung/Authentifizierung über die aktuelle Laufzeit nicht nutzbar. Keine Tokens ausgegeben, keine Zugangsdaten geändert.
- `python3 scripts/verify-source.py`: Exit 0; sechs XML-Dateien, beide JSON-Beispiele, alle Datei-/Dokumentverweise, C#-Klammerbalance, verbotene Aufrufe sowie der API-freie Offline-Host und CMD-Starter geprüft.
- Workflow mit Python/PyYAML (BaseLoader) eingelesen: YAML gültig; Push-/PR-/manueller Trigger, Windows-Runner, vorhandene Scriptverweise, PowerShell-Parserstep und Artefakt-Upload mit Fehler bei fehlenden Dateien geprüft. Exit 0.
- `bash -n scripts/test-core.sh`: Exit 0.
- `./scripts/test-core.sh`: erneut Exit 1, `dotnet` fehlt. Es wurde kein xUnit-Test ausgeführt.
- PowerShell-Scripts für Windows PowerShell 5.1 als UTF-8 mit BOM gespeichert. Deren Parser-/Laufzeitprüfung erfolgt erstmals im Windows-Workflow, nicht durch die lokalen Strukturprüfungen.

**Noch kein Binärartefakt erzeugt und kein grüner CI-Lauf nachgewiesen.** Die Pipeline-Datei muss im GitHub-Repository vorhanden sein und erfolgreich laufen, bevor ein fertiges Paket heruntergeladen werden kann. Windows-Runner-/OfficeTools-Installation, PowerShell-Syntax/-Laufzeit, tatsächliche VSTO-Kompilierung, Manifeste und Installation sind hier nicht validierbar. Der Workflow bricht bei Fehlern ab und lädt dann kein Installationspaket hoch.

Danach bleibt die echte Outlook-Abnahme des Kalenderbuttons, nativen Speicherns, Ladens, Installierens/Deinstallierens und beider Bitnessen gemäß [manual-test-plan.md](manual-test-plan.md). Temporäre dreimonatige Testsignierung ist kein Ersatz für eine produktiv vertrauenswürdige Herausgebersignatur. Fehlende .NET/VSTO-Laufzeiten auf dem Client werden nicht heimlich installiert, sondern mit offiziellen Downloadhinweisen gemeldet.

---

# Historischer Stand vor der Offline-Eingrenzung

Die folgende frühere Vollimplementierung beschreibt teilweise bewusst nicht mehr im aktuellen Host aktive API-/Word-/Ribbon-Funktionen. Sie ist kein aktueller Funktionsnachweis des Offline-Pakets.

# Implementierungs- und Prüfstatus

Stand: 19. Oktober 2026. **Implementierung erstellt; Windows-/Outlook-Freigabe steht aus.** Keine behaupteten Build-, Installations- oder xUnit-Erfolge.

## Ausgangsprüfung

Direkt im vorhandenen Checkout `/workspace/jitsi-admin-outlook-plugin` gearbeitet. Remote `https://github.com/bavxhack/jitsi-admin-outlook-plugin.git`; HEAD `d6bf9f58fdb15566e721be3775af7edf008762b5`. Branch war **`work`**, nicht `main`; unverändert beibehalten. `git status --short --branch` zeigte `## work`, keine Benutzeränderungen. Nur leere `.gitkeep` vorhanden. Keine `AGENTS.md`, keine vorhandene API-Dokumentation, keine Integration oder Builddateien. Kein neues Repository, kein Backend geändert, kein Push und keine Installation auf Benutzergeräten vorgenommen.

Linux x86_64, Kernel `6.18.55`. `command -v dotnet msbuild xbuild mono csc nuget` fand keine Werkzeuge. `/usr/share/dotnet` und `/usr/lib/mono` fehlen. Keine .NET-Framework-Referenzassemblies, Office-PIAs oder VSTO-Targets gefunden. Outlook/Word sind hier nicht verfügbar.

## Implementiert

| Anforderung | Umsetzung |
|---|---|
| Echter Windows-VSTO-Host | .NET Framework 4.8, Office-Projekttyp und VSTO-Targets, OutlookAddInBase/StartupObject, Designer/Blueprint/Ressourcen, Startup/Shutdown und Ribbon-Registrierung. |
| UI | Deutsche Texte, eigener Ribbon-Reiter/Gruppe Meet Link in Explorer, Appointment und Mail.Compose. Dynamisches XML-Menü mit sicheren Control-IDs, getrennten Anzeigenamen/IDs, Zuständen und Verwaltungsaktionen. |
| Asynchronität | HTTP über HttpClient; expliziter Control-basierter UI-Dispatcher, keine UI-Netzwerkwaits, generationsgesicherter Cache, Invalidierung auf UI-Thread, beobachtete Tasks und Shutdown-Cancellation. |
| Ziele | Auslösendes Fenster/Item vor Request erfasst, kein ActiveInspector-Retargeting. Explorer erzeugt nach Erfolg neuen nativen Standardkalendertermin. Item-GUID in UserProperties koordiniert auch ungespeicherte Elemente/mehrere Fenster. |
| Einfügung | WordEditor nur nach IsWordMail/EditorType-Prüfung, gezielte End-Range und Hyperlink, begrenzter Activate-/Timer-basierter Bereitschaftscheck (8 Sekunden). Klartextmail über Body-Anhang. Native Terminfelder bleiben unangetastet. |
| Duplikatschutz | Pending-Marker vor Remote-Seiteneffekt, Sitzungssperre und persistent speicherbare UserProperties; teilweise Einfügung/unklarer Ausgang bleibt geschützt. Keine automatischen Retries. |
| Wiederherstellung | Bei geschlossenen/nicht mehr bearbeitbaren Zielen oder Einfügungsfehler nach Erfolg: Teilnehmerlinkdialog mit manueller Kopiermöglichkeit. |
| Konfiguration | Externes JSON, Standardpfad/Override, unveränderliche Snapshotwerte, Methoden/URLs/Origin-/Bereichsprüfung; Neuladen deaktiviert bei Fehler und verwirft Cache. |
| API/Auth | Austauschbarer Beispieladapter und Environment-Bearer-Authentifizierung, ausschließlich GET/POST-Beispielvertrag. HTTPS, effektive Origin-Prüfung, Redirects/Cookies/Default-Credentials deaktiviert. |
| Mock | Zwei Server, zufällige `.invalid`-Beispiellinks, keine Credentials/Netzwerk, dieselbe Ablaufsteuerung/Outlook-Einfügung mit sichtbarem MOCK-Hinweis. |
| Betrieb | Keine Save-/Send-Aufrufe, keine Synchronisation/automatische Löschung, datensparsame rotierte lokale Ereignislogs. |
| Lieferung | Solution, Core/Host/Tests, Konfiguration, Build-/Publish-/Mock-/Prüfscripts und Installations-/API-/Abnahmedokumentation. |

## Ausgeführte Prüfungen und Ergebnisse

- `git status --short --branch`, `git remote -v`, `git log -1 --format='%H %s'`, `ls -la`, `find .. -name AGENTS.md -print`: oben dokumentierter leerer Checkout, keine Benutzeränderungen/AGENTS.
- `uname -a`, `command -v dotnet msbuild xbuild mono csc nuget`, Suche nach SDK/Compilern/VSTO-Assemblies in den üblichen Installationspfaden: Linux, keine C#-/Office-Buildwerkzeuge.
- Cloud-Umgebungsstatus und `/etc/codex/network-policy.json` gelesen: keine bereitgestellten Credentials/Identitäten; HTTP-Proxy konfiguriert, Policyzustand im Status `unknown`, kein VPN konfiguriert. Keine Tokenwerte ausgegeben.
- `curl -fsSL https://dot.net/v1/dotnet-install.sh -o /workspace/work/dotnet-install.sh`: Exit 7, Verbindung zu `proxy:8080` nicht möglich. Deshalb konnte das nachgeschaltete SDK-Installationsscript nicht starten.
- `curl -I --retry 2 --retry-delay 1 https://api.nuget.org/v3/index.json` und `curl -I https://builds.dotnet.microsoft.com`: ebenfalls Exit 7, Proxy nicht erreichbar. Kein Netzwerk-Bypass versucht.
- `./scripts/test-core.sh`: Exit 1, „Erforderlich: .NET SDK 8 (dotnet fehlt).“ **Kein Test wurde dadurch ausgeführt.**
- `python3 scripts/verify-source.py`: Exit 0. Sechs XML-Dateien, zwei JSON-Dateien, Solution-/Dateiverweise und Dokumentlinks gültig; keine verbotenen Save/Send/Wait/Result-/FinalRelease-/Platzhalteraufrufe; Klammerbalance in 12 C#-Dateien und nachgestellte Leerzeichen geprüft. Das ist ausdrücklich kein C#-Compiler oder Verhaltenstest.
- `bash -n scripts/test-core.sh`: Exit 0, Shellsyntax gültig.
- `git diff --check`: Exit 0; da die neuen Dateien untracked sind, deckt dieser Befehl diese Dateien allein nicht ab. Deren Leerzeichen-/Strukturprüfung erfolgt durch das obige Script.
- PowerShell-Scripts, Windows-MSBuild, Manifest-Signaturprüfung und Outlook-Tests nicht ausgeführt: Windows/PowerShell/Office-/VSTO-Werkzeuge fehlen. Keine generierten Deployment-Manifeste vorgetäuscht.

Die xUnit-Tests sind implementiert, aber hier nicht ausführbar. Sie prüfen Konfigurationsauflösung, Origin-/Redirectregeln, JSON-/ID-/Linkvalidierung, tatsächliche POST-Payload, HTTP-Fehler/Timeout/Verbindung, Mock ohne Credentials/Netzwerk sowie verzögerte und konkurrierende Abläufe mit abstrakten Outlook-Zielen. Letzteres beweist keine COM-, Word- oder Outlook-Funktion.

## Technische Annahmen und Grenzen

- Die tatsächliche Meet-Link-API ist unbekannt. Beispieladapter ist absichtlich isoliert. Kein Tokenrefresh/interaktiver Login, keine zugesicherte Moderatorzuordnung, keine erfundene Idempotenz.
- Eigener Meet-Link-Reiter statt Abhängigkeit von verschiedenen Outlook-Home-Tab-IDs. Inline-Mailantworten sind ausgeschlossen; Explorer-Aktion erzeugt immer einen neuen Termin.
- Editierbarkeit wird konservativ über Fensterlebenszyklus, Mail-Sendestatus, Appointment-MeetingStatus, PR_ACCESS bei gespeicherten Elementen und später Editor-Schutz geprüft. Besondere Exchange-/Freigabekonstellationen erfordern reale Abnahme.
- GUID-UserProperty funktioniert ohne EntryID. Kopierte Elemente können Metadaten erben und bleiben geschützt. Metadaten werden ohne explizites Speichern gesetzt; nach Verwerfen eines ungespeicherten Elements ist keine dauerhafte lokale Sperre garantiert, die remote Konferenz bleibt bestehen.
- Pending-Schutz bleibt vorsorglich auch bei fehlenden Credentials/abgelehnten Requests bestehen. Bewusste Freigabe erst nach Serverprüfung; siehe Konfigurationsdokumentation. Kein automatisches Auflösen unklarer Ergebnisse.
- Beim Explorer bleibt ein unklarer/teilweise fehlgeschlagener Ablauf für diese Sitzung gesperrt. Nach erfolgreicher Einfügung darf ein weiterer neuer Termin erstellt werden.
- Abbruch/Shutdown stellt keine Remote-Rücknahme dar. Beim Outlook-Shutdown kann kein Wiederherstellungsdialog zugesichert werden; serverseitiger Status ist dann manuell zu prüfen.
- Terminzeit/Dauer werden nicht synchronisiert. Sofortlink-Gültigkeit und spätere Nutzbarkeit sind offen; keine bestätigte unbegrenzte Gültigkeit.

## Noch erforderlich

1. .NET SDK 8 bereitstellen, NuGet-Restore und **alle Core-Tests tatsächlich ausführen**, Fehler vor Freigabe beheben.
2. Unter Windows mit Visual Studio OfficeTools/.NET 4.8 Developer Pack den VSTO-Host kompilieren; Designer-/Hostreferenzen und VSTO-Targets tatsächlich validieren. Ohne diesen Schritt besteht keine bestätigte Buildfähigkeit.
3. Signiert ClickOnce-publishen; Deployment-/Application-Manifeste, Abhängigkeiten, Bootstrapper und Zertifikatsvertrauen prüfen. Saubere Installation, Update, Deinstallation auf Testmaschine.
4. Outlook/Word-Integration gemäß manuellem Testplan auf **32-Bit und 64-Bit Outlook Classic** testen. UI-Thread, tatsächliche COM-Fensteridentität, Formatierung, Serien, Rechte, Shutdown und natives Speichern/Senden nachweisen.
5. Echten API-Vertrag, Authentifizierung und Linkgültigkeit klären; Adapter ggf. anpassen. Nutzeridentität, Serverberechtigungen, Moderatorzuordnung und Nutzbarkeit zum geplanten Zeitpunkt tatsächlich testen.

Bis dahin ist dies eine überprüfbare Implementierung mit offen ausgewiesenen Abnahmegrenzen, keine produktiv freigegebene oder nachweislich installierbare Version.
