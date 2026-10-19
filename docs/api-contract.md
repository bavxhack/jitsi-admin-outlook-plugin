> **Spätere API-Stufe:** Die aktuelle installierbare Offline-Vorschau nutzt diese Funktionen und Dateien nicht. Sie benötigt weder API noch Konfiguration.

# API-Vertrag – ausdrücklich unbestätigtes Beispiel

Beim geprüften Ausgangscommit und im Arbeitsverzeichnis lag keine API-Dokumentation vor. Der Repositoryname belegt weder Endpunkte noch Authentifizierung. Kein Backend-Repository wurde geändert. Vor Produktivbetrieb sind die folgenden Annahmen mit dem Anbieter zu ersetzen oder zu bestätigen.

## Beispieladapter

`src/JitsiAdmin.OutlookPlugin.Core/Api.cs`, `ExampleApiAdapter`, implementiert genau:

```http
GET {serversEndpoint}
Authorization: Bearer <individual-user-token>
```

```json
{"servers":[{"id":"server-001","name":"Server Berlin"},{"id":"server-002","name":"Server Hamburg"}]}
```

Eine leere Liste ist gültig. IDs müssen nichtleer, eindeutig, höchstens 256 Zeichen und frei von Steuerzeichen sein. Anzeigenamen sind davon unabhängig, höchstens 200 Zeichen und einzeilig. Fehlende/wrong-type Felder werden abgelehnt. Höchstens 1000 Server und 1 MiB Antwort. Die UI verwendet generierte numerische Control-IDs und XML-Escaping über `XElement`.

```http
POST {createConferenceEndpoint}
Content-Type: application/json; charset=utf-8
Authorization: Bearer <individual-user-token>
```

```json
{"serverId":"server-001"}
```

```json
{"conferenceId":"conference-123","joinUrl":"https://meetlink.example.invalid/join/conference-123"}
```

`conferenceId` ist optional (höchstens 256 Zeichen, ohne Steuerzeichen). `joinUrl` muss eine absolute HTTPS-URL ohne eingebettete Credentials/Fragment sein. HTTP und ausführbare Schemata werden abgelehnt, auch im Mock. Host-/Moderatorfelder werden ignoriert und ersetzen niemals `joinUrl`. Ausschließlich die technische `serverId` verlässt Outlook. Kein Body, keine Mailadresse, keine Empfänger, Zeiten oder Kalenderwerte werden übertragen.

Alle HTTP-2xx gelten als Transporterfolg, anschließend folgt zwingend die Antwortvalidierung. 3xx werden abgelehnt, nie verfolgt; 401/403/429 und Serverfehler erhalten deutsche Meldungen. Keine automatische Wiederholung und keine erfundenen Idempotenzheader. Netzwerkabbruch/Timeout oder eine unbrauchbare Antwort nach POST können eine bereits erfolgte Erstellung bedeuten.

## Adapter ersetzen

`IApiAdapter` kapselt `ServersRequest`, `CreateRequest`, `ReadServers` und `ReadConference`. Dort andere Feldnamen, JSON-Hüllen oder nachgewiesene Requestformate implementieren. Adapter über den Konstruktor von `MeetLinkApi` einsetzen. Bei anderen Methoden außerdem die explizite Methodenprüfung in `Configuration.Validate` ändern und dazu Vertragstests ergänzen. Der aktuelle Adapter erlaubt nur GET ohne Body und POST mit dem obigen JSON; andere Kombinationen werden vor Netzwerkzugriff abgelehnt.

`IAuthentication.Authorize` ist austauschbar. Der aktuelle `EnvironmentBearerAuthentication` liest den Token aus der konfigurierten **benutzerspezifischen Umgebungsvariable**. Es gibt keine interaktive Anmeldung, kein OAuth-Browserfenster, keinen Refresh-Token und keine Token-Erneuerung. Ein Token wird pro Request gelesen und nicht protokolliert oder in der JSON-Konfiguration gespeichert. Eigene Authentifizierung muss weiterhin die Origin-Prüfung vor jeder Credential-Übermittlung garantieren. Der Produktionshandler deaktiviert automatische Redirects, Cookies und Default-Credentials; normale TLS-Zertifikatsprüfung bleibt aktiv.

## Vor Produktivfreigabe zu klären

- Echte URLs, Methoden, Feldnamen, Fehlerformate, Pagination und Rate Limits.
- Welche Authentifizierung belegt die individuelle Nutzeridentität? Reicht ein Bearer-Token, wie wird es sicher erneuert?
- Belegt die API die Serverberechtigung und die Moderatorzuordnung dieses Nutzers? Die Outlook-Mailadresse ist kein Anmeldenachweis; ein gemeinsames Dienstkonto garantiert diese Zuordnung nicht.
- Liefert `joinUrl` ausschließlich Teilnehmerzugang? Ist `conferenceId` stabil und nicht geheim?
- Wie lange ist ein Sofortkonferenzlink gültig? Kann er erst zur geplanten Terminzeit genutzt werden? Keine bestätigte unbegrenzte Gültigkeit.
- Gibt es dokumentierte Idempotenz oder eine Statusabfrage zur Auflösung unklarer Erstellungsergebnisse? Der aktuelle Client implementiert beides nicht.

Mock verwendet keinen HTTP-Request und keine Credentials. Er liefert zwei Server und zufällige HTTPS-Beispiellinks unter `.invalid`, verarbeitet sie aber durch dieselbe Ablaufsteuerung und Outlook-Einfügung.
