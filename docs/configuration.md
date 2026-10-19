> **Spätere API-Stufe:** Die aktuelle installierbare Offline-Vorschau nutzt diese Funktionen und Dateien nicht. Sie benötigt weder API noch Konfiguration.

# Konfiguration

Standard: `%APPDATA%\MeetLinkOutlookAddin\config.json`. Ein gesetztes `MEETLINK_OUTLOOK_CONFIG` ersetzt den vollständigen Dateipfad. Ein leer gesetzter Override ist ungültig und fällt nicht still auf eine andere Datei zurück. Die Datei muss lesbares JSON ohne Kommentare sein (maximal 64 KiB).

| Feld | Bedeutung und zulässiger Wert |
|---|---|
| `baseUrl` | Absolute HTTPS-Basis ohne Userinfo/Fragment. Auch mit absoluten Endpunkten erforderlich. |
| `serversEndpoint` | Nichtleer, relative oder absolute HTTPS-Zieladresse. |
| `createConferenceEndpoint` | Unabhängig konfigurierbare relative oder absolute Zieladresse. |
| `serversHttpMethod` | Genau `GET`, ohne Requestbody. |
| `createConferenceHttpMethod` | Genau `POST`, JSON `{"serverId":"..."}`. |
| `timeoutSeconds` | Ganze Zahl 1–300, umfasst Senden und Antwortlesen. |
| `serverCacheSeconds` | Ganze Zahl 0–86400; 0 bedeutet keine Cache-Lebensdauer. |
| `auth.mode` | Genau `BearerTokenEnvironment`. |
| `auth.tokenEnvironmentVariable` | Nichtleerer Name der benutzerspezifischen Token-Variable. |
| `auth.allowedOrigins` | Liste aus HTTPS-Scheme, Host, optional Port. Keine Pfade außer `/`, Query, Userinfo oder Fragmente. Im Produktivmodus müssen beide API-Ziele freigegeben sein. |
| `body.heading` | Nichtleer, einzeilig, maximal 200 Zeichen; Standard „Meet-Link-Konferenz“. |
| `body.linkLabel` | Nichtleer, einzeilig, maximal 200 Zeichen; Standard „Teilnehmen:“. |
| `mockMode` | JSON-Boolean; `true` deaktiviert Netzwerk und Credential-Zugriff. |
| `logLevel` | `Information`, `Error` oder `None`. |

Alle aufgeführten Felder sind zu setzen. JSON-Beispiele im Verzeichnis `config/` enthalten ausschließlich Beispielendpunkte. Die URL `meetlink.example.invalid` ist nicht erreichbar und nicht produktiv bestätigt.

## URI-Auflösung

Es gilt `new Uri(baseUri, endpoint)`, keine Zeichenkettenverkettung:

| Basis | Endpunkt | Ergebnis |
|---|---|---|
| `https://host.invalid/root/` | `api/servers` | `https://host.invalid/root/api/servers` |
| `https://host.invalid/root` | `api/servers` | `https://host.invalid/api/servers` |
| `https://host.invalid/root/` | `/api/servers` | `https://host.invalid/api/servers` |
| `https://host.invalid/root/` | `api/servers/` | `https://host.invalid/root/api/servers/` |
| `https://host.invalid/root/` | `https://other.invalid/list` | `https://other.invalid/list` |

Abschließender Slash der Basis und führender Slash des Endpunkts ändern also das Ziel. Absolute Ziele werden direkt verwendet; deren eigener Origin muss freigegeben sein. Scheme, IDN-Host und **effektiver** Port werden verglichen: `https://host.invalid` entspricht Port 443, aber nicht Port 444 oder `host.invalid.evil.invalid`. Credentials werden erst nach dieser Prüfung am Request gesetzt. Keine automatischen Redirects, keine Zertifikatsausnahmen.

## Neuladen und Fehler

„Konfiguration neu laden“ liest eine neue unveränderliche Momentaufnahme, verwirft Cache und laufende Cachegeneration und lädt die Server asynchron. Alte Ladeantworten dürfen den neuen Cache nicht überschreiben. Auch ein Refresh erhält eine neue Menügeneration; eine veraltete Auswahl wird abgelehnt. Laufende Erstellungsaktionen verwenden die ursprüngliche Momentaufnahme. Keine dieser Verwaltungsaktionen erstellt eine Konferenz.

Ungültige Konfiguration entfernt die Serverauswahl; Fehlermeldung, Wiederholung und Neuladen bleiben erreichbar. Nach einem Serverladefehler wird für zehn Sekunden kein automatischer neuer Ladeversuch gestartet. „Erneut versuchen“/„Serverliste aktualisieren“ können sofort erneut laden. Ribbon-Callbacks warten nicht auf Netzwerkantworten. Die Liste wird nach Cacheänderung über den UI-Dispatcher invalidiert; erneut geöffnetes Menü liest immer den aktuellen Cache.

## Credentials

Token sicher im Benutzerkontext provisionieren, beispielsweise über die Windows-Oberfläche für Benutzer-Umgebungsvariablen. Nicht in Scripts, JSON, Kommandohistorie, Logs oder Git schreiben. Danach Outlook komplett neu starten, damit der Prozess die Variable erbt. Der Variablenname ist kein Token. Keine Anmeldung durch die Outlook-E-Mail-Adresse ableiten. Mock benötigt keinerlei Token.

## Schutzmetadaten und Betrieb

`MeetLinkContextId` identifiziert auch ungespeicherte Elemente über mehrere Fenster. `MeetLinkState` (`pending-or-uncertain`, `created-insertion-pending`, `inserted`), `MeetLinkServerId` und optional `MeetLinkConferenceId` sind benutzerdefinierte Textfelder, nicht im Ordnerfeldschema angelegt. Es wird **kein explizites Speichern** ausgelöst. Natives Speichern persistiert diese Felder; Body-Löschen hebt den Schutz nicht auf. Kopierte Elemente können die Metadaten ebenfalls erben und bleiben dann absichtlich geschützt.

Der Schutz ist konservativ: auch nach fehlenden Credentials oder eindeutig abgelehntem POST bleibt ein bereits gesetzter Marker aktiv. Kein automatischer Reset und kein zweiter Request durch erneutes Klicken. Zur Freigabe erst serverseitig prüfen, ob eine Konferenz entstand. Falls sicher keine Konferenz existiert: einen neuen Entwurf verwenden oder Metadaten durch einen Administrator bewusst entfernen und Outlook neu starten (auch der Sitzungsschutz bleibt bis dahin aktiv). Bei unklarem neuem Terminablauf im Explorer bleibt dessen Sitzungsschutz bis Outlook-Neustart bestehen; nach erfolgreicher Einfügung ist ein weiterer neuer Terminablauf möglich. Ein Neustart darf nie als Ersatz für die serverseitige Prüfung verstanden werden.

Nicht gespeicherte Metadaten überleben kein Verwerfen des Elements. Verwerfen beseitigt eine remote erstellte Konferenz nicht. Logs liegen unter `%LOCALAPPDATA%\MeetLinkOutlookAddin\Logs`, maximal eine aktuelle Datei von etwa 256 KiB plus eine Rotation. Nur Zeit und feste Ereigniscodes, keine Exceptiontexte, Tokens, URLs, API-Antworten oder Outlook-Inhalte. Es gibt keine Telemetrie.
