# Abnahme der Offline-Vorschau

Diese Tests wurden noch nicht in Outlook ausgeführt. Datum, Windows-/Outlook-Version, Office-Bitness, Paketversion und tatsächliches Ergebnis erfassen. Auf je einer Maschine/VM mit 32-Bit und 64-Bit Outlook Classic wiederholen.

1. GitHub-Workflow erfolgreich ausführen. Core-TRX, VSTO-Build und `mage`-Manifestprüfung müssen erfolgreich sein; nur dann darf das Installationsartefakt erscheinen.
2. ZIP herunterladen und komplett entpacken. Paket enthält Install.cmd/Uninstall.cmd, öffentliche CER, Prüfsummen und vollständige ClickOnce-Dateien; keine privaten Schlüssel. An einem anderen Pfad/auf einem anderen Rechner als dem CI-Runner testen.
3. Outlook schließen, Install.cmd starten. Ablehnung der Zertifikatsfrage muss ohne Vertrauensimport abbrechen. Bei erneuter Installation mit JA muss das Paket für den Benutzer installiert werden. Fehlende Runtime muss mit verständlicher Downloadanweisung gemeldet werden.
4. Outlook starten: Add-in geladen, Reiter Meet Link und Button Kalendereintrag erstellen vorhanden. Kein Servermenü, keine Konfigurationsanforderung und kein Anmeldedialog.
5. Ohne Internetverbindung und ohne Token/JSON-Konfiguration klicken: ein neuer nativer Termin im Standardkalender öffnet sich. Kein HTTP-Zugriff und kein Konferenz-/Mock-Link. Auch eine bereits vorhandene produktive config.json darf keine API aktivieren.
6. Vorher einen vorhandenen Termin markieren: er bleibt unverändert. Auch aus dem Mailbereich des Hauptfensters testen. Jede bewusste neue Aktion öffnet einen eigenen neuen Termin.
7. Terminwerte mit nativen Outlook-Vorgaben vergleichen. Betreff, Body, Zeit, Erinnerung, Ort und Teilnehmer selbst bearbeiten; danach mit Outlook speichern, schließen und im Kalender wieder öffnen. Das Add-in speichert/versendet selbst nicht.
8. Zweiten Entwurf öffnen und verwerfen: keine Beschädigung anderer Elemente und keine Konferenz als Nebeneffekt. Outlook beenden/neustarten; Add-in lädt weiterhin.
9. Downloadordner nach Installation verschieben/entfernen. Outlook muss aus der persistenten LOCALAPPDATA-Kopie weiter laden; keine Abhängigkeit vom CI-Runner-Pfad oder Downloadordner.
10. Neues Pipeline-Paket installieren: alte Vorschau wird deinstalliert, neue Registrierung lädt genau einmal. Neue Zertifikatsbestätigung nachvollziehen. Wiederinstallation derselben Version testen.
11. Outlook schließen, Uninstall.cmd starten: Add-in registrierungsseitig entfernt, eigene Installationskopie und nur selbst hinzugefügtes Zertifikatsvertrauen entfernt; vorhandene Termine und sonstige Konfiguration bleiben erhalten.
12. Organisationsrichtlinien und fehlendes/gesperrtes Outlook gesondert prüfen. Neues Outlook ausdrücklich nicht als kompatibel melden.

Die erhaltenen Core-Tests betreffen die spätere API-Bibliothek. Sie sind kein automatisierter Test des lokalen Outlook-Kalenderbuttons. Die frühere API-/Word-/Nebenläufigkeits-Abnahme liegt für die spätere Integration in [manual-test-plan-api.md](manual-test-plan-api.md).
