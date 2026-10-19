# Spätere API-Stufe – derzeit nicht im Host aktiv

# Manuelle Windows-/Outlook-Abnahme

**Bisher nicht ausgeführt.** Dieser Plan ist keine Ergebnisliste. Für jeden Test Datum, Windows-/Office-Version, Outlook-Bitness, Add-in-Version, Konfigurationsstand, Ergebnis und bereinigte Belege festhalten. Keine Tokens oder vollständigen Konferenzlinks in Tickets/Logs übernehmen. Zwei getrennte Systeme/VMs für 32-Bit und 64-Bit verwenden.

## Vorbereitung

Windows-Build, Core-Tests, signierten ClickOnce-Publish und Installation nach [deployment.md](deployment.md) ausführen. Beide Manifestsignaturen prüfen. Mock-Konfiguration setzen und Outlook vollständig neu starten. In „COM-Add-ins“ muss Meet Link geladen sein. Der eigene Reiter „Meet Link“ muss im Explorer sowie Termin-/Besprechungs- und separaten Mail-Verfassen-Inspector erscheinen.

Core-Tests mit `FakeUi`/`Target` bilden **keine Office-Integration** nach. Sie prüfen die Ablaufsteuerung und Dispatcher-Verwendung an einer Abstraktion. COM-Identität, tatsächliches Fensterverhalten, Word-Formatierung, native Speicherung und VSTO-Ladefähigkeit müssen hier geprüft werden.

## Mock-Abnahme

| Test | Aktion | Erwartetes Ergebnis |
|---|---|---|
| Menü | Menü öffnen und zwei MOCK-Server wählen (in getrennten Entwürfen) | Deutlicher MOCK-Hinweis, unabhängige technische IDs, `.invalid`-Beispiellinks, keine echten Konferenzen. |
| Aktualisieren | Serverliste mehrfach aktualisieren, Menü schließen/öffnen | Verwaltungsaktionen erstellen nichts; Liste entspricht aktuellem Cache. |
| Neuladen | Pfad ungültig setzen oder ungültiges JSON schreiben; neu laden | Deutsche Fehlermeldung und Neuladen erreichbar, keine Serveraktion. Gültige Datei wiederherstellen und neu laden: Liste erscheint. |
| Explorer | Kalendertermin markieren, im Hauptfenster Server wählen | Markierter Termin bleibt unverändert. Nach Erfolg neuer nativer Termin im Standardkalender mit nativen Standardwerten. Auch aus Mail-Explorer testen. |
| Leerer Termin | Neuen leeren Termin öffnen, Server wählen | Oberhalb des angehängten Blocks bleiben Leerzeilen; Link anklickbar, Body anschließend frei editierbar. |
| Bestehender Termin | Vorher Betreff, Zeit, Zone, Erinnerung, Ort, Teilnehmer, Serie sowie formatierten Body setzen; Server wählen | Nur Block und Metadaten ergänzt, alle Felder/Formatierungen unverändert. Für Einzeltermin, Serientermin und Serienausnahme separat prüfen. |
| Besprechung | Bearbeitbare eigene Besprechung ergänzen | Derselbe native Inspector bleibt Ziel, Teilnehmerliste unverändert. Empfangene/nicht bearbeitbare Besprechung wird vor POST abgelehnt. |
| HTML-/RTF-Mail | Separaten Entwurf mit Signatur, Tabelle, Farben und Links öffnen | Ergänzung am Ende über Word-Range, Signatur/Text/Formatierung erhalten, Teilnehmerlink anklickbar. |
| Klartext-Mail | Separaten Plaintext-Entwurf ergänzen | Vollständige URL am Ende, ursprünglicher Text erhalten. Kein Formatwechsel. |
| Inline-Antwort | Inline-Antwort im Explorer öffnen, Add-in benutzen | Inline-Antwort bleibt unverändert; Explorer-Aktion erstellt einen neuen Termin. |
| Doppelklick | Schnell/parallel denselben Server mehrfach anklicken | Genau eine Erstellung für denselben Kontext; verständlicher Hinweis für weitere Aktion. |
| Zwei Fenster | Dasselbe gespeicherte und dasselbe ungespeicherte Element in zwei Inspectors öffnen, gleichzeitig klicken | Gemeinsame Kontext-ID/Metadaten, höchstens ein Request. Auf getrennten Elementen dürfen unabhängige Aktionen laufen. |
| Sichtbaren Block löschen | Block entfernen und erneut klicken | Metadaten verhindern weitere Erstellung. |
| Nativ speichern | Termin mit Outlook speichern, schließen, öffnen, klicken | Keine zweite Konferenz. Outlook-Save funktioniert; Add-in löst ihn nicht aus. |
| Nativ senden | Mail/Besprechung manuell senden | Versand ausschließlich durch Nutzer, Link bleibt enthalten, keine zusätzliche API-Aktion. |
| Verwerfen | Nach Serverauswahl Entwurf verwerfen | Outlook-Standardverhalten; erstellte Konferenz würde serverseitig bestehen bleiben. Kein Bereinigungsrequest. |
| Weitere Bearbeitung | Termin verschieben/absagen/löschen oder Serie ändern | Keine weiteren Meet-Link-Requests. Keine Synchronisation von Zeitpunkt/Dauer. |
| Neustart/Update | Outlook schließen/starten, höher versioniertes ClickOnce-Update installieren | Add-in lädt, konfigurierte Werte bleiben erhalten, genau eine Registrierung. |
| Deinstallation | Meet Link entfernen | Keine Add-in-Gruppe mehr, keine Beschädigung vorhandener Elemente. Benutzerdateien gesondert prüfen. |

Die PR_ACCESS-Änderungsberechtigung wird bei gespeicherten Elementen geprüft; gesendete Mails und empfangene Besprechungen sind ausgeschlossen. Testen: eigener Kalender, schreibgeschützter freigegebener Kalender, freigegebener Kalender mit Schreibrechten. Berechtigungs-/Editorfehler müssen vor Request bzw. mit Wiederherstellungsdialog nach bereits erfolgter Erstellung behandelt werden.

## Verzögerungen, Fehler und Wiederherstellung

Für diese Tests einen kontrollierten **HTTPS-Testserver** mit gültigem Zertifikat und dem Beispielvertrag nutzen, direkte API-Ziele in `allowedOrigins` freigeben und nur Testtokens verwenden. Mock selbst hat absichtlich keine Netzwerkverzögerung. Die automatisierten HTTP-/Workflow-Fakes simulieren Verzögerungen/Fehler ohne Netzwerk, ersetzen aber nicht diese Outlook-Prüfung.

1. GET um mehrere Sekunden verzögern: Menü zeigt „Server werden geladen …“, Outlook bleibt bedienbar; nach Antwort bzw. spätestens erneutem Menüöffnen aktuelle Server. Leerantwort `{"servers":[]}` zeigt „Keine Server verfügbar“. Fehler bietet Wiederholung.
2. Servernamen mit `&`, `<`, `>` und Anführungszeichen zurückgeben. Ribbon bleibt gültig und Auswahl sendet **ID**, keinen Anzeigenamen. Doppelte/fehlende IDs oder ungültiges JSON: deutsche Meldung, keine Erstellung.
3. POST verzögern, dann aktives Fenster wechseln. Link muss ausschließlich im ursprünglich erfassten Element landen. Auch zwei verschiedene Kontexte gleichzeitig testen.
4. Ursprünglichen Inspector während POST schließen; anderes Element aktivieren. Nach Erfolg nur Dialog mit Teilnehmerlink und Kopierbutton; kein anderes Element verändern. Entsprechenden Fall mit geschlossenem Explorer prüfen.
5. Nach erfolgreichem POST den Editor schützen/Einfügen scheitern lassen oder Editor länger als acht Sekunden nicht bereitstellen. Linkdialog erscheint, Schutz bleibt bestehen, erneuter Klick erzeugt keinen POST. Nach Ende der Prüfung dürfen keine Activate-/Timerhandler weiterlaufen.
6. POST serverseitig ausführen, Antwort danach verzögern, bis Timeout eintritt. Meldung nennt unklaren Ausgang; kein automatischer Retry. Erneuter Klick bleibt gesperrt. Serverstatus separat prüfen.
7. 401, 403, 429, 500, ungültigen Teilnehmerlink, fehlerhaftes JSON und Verbindungsabbruch simulieren. Keine sensitiven Rohantworten in Meldungen/Logs. POST-Schutz bleibt konservativ erhalten.
8. 302 auf zweiten HTTPS-Host simulieren: genau ein Request zum ersten Host, kein Folgerequest und keinerlei Token am zweiten Host. Nicht freigegebenen absoluten Endpunkt, anderen Port und ähnlich benannten Host testen: bereits lokal ablehnen.
9. Während langsamer Serverabfrage Konfiguration wechseln; alte Antwort darf neue Liste nicht ersetzen. Während POST Konfiguration wechseln: laufende Aktion muss ursprüngliche URLs/Bodytexte verwenden. Alte Menügeneration darf keinen falschen Server auswählen.
10. Outlook während POST bzw. Editor-Wartephase schließen: keine unbeobachtete Task-Exception, kein hängenbleibendes Outlook, Event-/Timercleanup. Ein Abbruch macht bereits erfolgte Servererstellung nicht rückgängig.

## Reale Meet-Link-Abnahme

Erst nach Bestätigung/Anpassung des [API-Vertrags](api-contract.md):

- Individuellen Nutzer authentifizieren, zulässige und unzulässige Server prüfen (kein gemeinsames Dienstkonto als Nachweis).
- Im Request ausschließlich `serverId` nachweisen; genau einen POST pro Aktion, keine erfundenen Idempotenzheader.
- Mit getrennten Browserprofilen prüfen: authentifizierter Ersteller ist tatsächlich Moderator; Teilnehmerlink eröffnet ausschließlich Teilnehmerzugang.
- Geplanten Outlook-Zeitpunkt bewusst in die Zukunft legen und den Sofortkonferenzlink dann erneut nutzen. Laufzeit/Gültigkeit mit dem Anbieter bestätigen und dokumentieren. Kein Versprechen unbegrenzter Gültigkeit.
- Tokenablauf testen: verständlicher Fehler; keine Behauptung implementierter Token-Erneuerung.
- Nach Verwerfen/Absage nachweisen, dass keine Löschung oder Synchronisation erfolgte.

Produktivfreigabe erst nach dokumentiertem Windows-Build, verifizierten Manifesten, erfolgreicher Installation und dieser Abnahme auf beiden Outlook-Bitnessen.
