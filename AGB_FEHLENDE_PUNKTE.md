# Fehlende AGB-Punkte basierend auf Dokumentation

## 🚨 Kritische fehlende Punkte

### 1. **Social Features (Likes, Follows, Kommentare)**

**Was fehlt:**
- Likes können gelöscht/manipuliert werden
- Follower-Zahlen sind nicht garantiert
- Follow/Unfollow-Funktion
- Kommentare sind User-Generated Content
- Kommentar-Moderation durch Plattform

**Muss in AGB:**
```
- Nutzer können Beiträge liken und anderen folgen
- Plattform kann Likes/Follows bei Manipulation entfernen
- Keine Garantie für Follower-Zahlen oder Likes
- Kommentare werden nicht vorab geprüft
- Beleidigende Kommentare können gelöscht werden
- Keine Haftung für Kommentare anderer Nutzer
```

---

### 2. **WildCoin (Off-Chain virtuelle Währung)**

**Was fehlt:**
- WildCoin ist KEINE echte Kryptowährung
- Nur plattform-interne Belohnungen
- Kein Umtausch in Echtgeld
- Kann bei Account-Sperrung verfallen

**Muss in AGB:**
```
- WildCoin ist eine interne virtuelle Währung (kein echtes Geld)
- Kein Anspruch auf Auszahlung in Fiat oder Krypto
- Bei Account-Sperrung verfällt WildCoin-Guthaben
- Plattform kann WildCoin-System jederzeit ändern/beenden
- WildCoin hat keinen Geldwert außerhalb der Plattform
```

---

### 3. **Rate Limits und Upload-Beschränkungen**

**Was fehlt:**
- 5 Uploads pro 10 Minuten (konkret!)
- Videos: max 200MB
- Bilder: max 15MB
- Formate: MP4/MOV/WEBM (Video), JPG/PNG/WEBP (Bild)

**Muss in AGB:**
```
§ X. Upload-Limits

- Videos: Max 200MB, Formate: MP4, MOV, WEBM
- Bilder: Max 15MB, Formate: JPG, PNG, WEBP
- Rate Limit: Maximal 5 Uploads pro 10 Minuten
- Bei Überschreitung: Temporäre Sperre (10-60 Minuten)
- Plattform kann Limits jederzeit anpassen
```

---

### 4. **Video-Processing und Verzögerungen**

**Was fehlt:**
- Videos werden asynchron verarbeitet (Azure Queue + FFmpeg)
- Kann mehrere Minuten/Stunden dauern
- Kein Anspruch auf sofortige Verfügbarkeit
- Processing kann fehlschlagen

**Muss in AGB:**
```
§ X. Video-Verarbeitung

- Hochgeladene Videos werden asynchron verarbeitet (Komprimierung, Thumbnail-Erstellung)
- Verarbeitung kann 5 Minuten bis mehrere Stunden dauern
- Kein Anspruch auf sofortige Verfügbarkeit
- Bei technischen Problemen kann Processing fehlschlagen
- Plattform ist nicht verantwortlich für fehlgeschlagene Uploads
```

---

### 5. **Öffentliche Links (Meal Plan Sharing)**

**Was fehlt:**
- Token-basierte öffentliche Links
- Jeder mit Link kann Meal Plan sehen
- Keine Privatheit bei geteilten Links
- Links können widerrufen werden

**Muss in AGB:**
```
§ X. Öffentliche Meal Plan Links

- Nutzer können Meal Plans über Token-basierte Links teilen
- Jeder mit dem Link kann den Meal Plan sehen (öffentlich!)
- Keine Garantie für Privatheit bei geteilten Links
- Links können vom Nutzer jederzeit widerrufen werden
- Plattform kann Links bei Verstößen deaktivieren
```

---

### 6. **AI-Generator (OpenAI)**

**Was fehlt:**
- KI-generierte Meal Plans
- OpenAI API wird verwendet
- Keine Garantie für Qualität
- Kann Fehler/ungenießbare Kombinationen enthalten

**Muss in AGB:**
```
§ X. KI-generierte Inhalte

- Plattform nutzt KI (OpenAI API) zur Meal Plan Generierung
- KI-generierte Rezept-Vorschläge sind unverbindlich
- Keine Garantie für Qualität, Geschmack oder Eignung
- KI kann ungenießbare/unsinnige Kombinationen vorschlagen
- Nutzer trägt Verantwortung für Auswahl der Rezepte
- Keine Haftung für KI-generierte Inhalte
```

---

### 7. **Verifizierungslevel-Beschränkungen**

**Was fehlt:**
- "orb" (biometrisch) hat volle Features
- "device" (Gerät) hat Einschränkungen:
  - Nur 1 Meal Plan
  - Keine Video-Uploads
  - Keine Follower-Statistiken

**Muss in AGB:**
```
§ X. Verifizierungslevel

**Orb-Verifizierung (biometrisch):**
- Unbegrenzte Meal Plans
- Video-Uploads erlaubt
- Follower-Statistiken sichtbar
- Alle Features verfügbar

**Device-Verifizierung (Gerät):**
- Maximal 1 Meal Plan
- Keine Video-Uploads (nur Bilder)
- Eingeschränkte Statistiken
- Upgrade auf Orb-Verifizierung möglich

Die Plattform kann Verifizierungslevel jederzeit anpassen.
```

---

### 8. **Externe Dienste (Drittanbieter)**

**Was fehlt:**
- Bunny.net (Video-CDN)
- Azure Blob Storage
- Azure Queue Storage
- OpenAI API
- Worldcoin API
- Alchemy (World Chain RPC)

**Muss in AGB:**
```
§ X. Drittanbieter-Dienste

Die Plattform nutzt folgende Drittanbieter:

- **Bunny.net:** Video-Hosting und CDN
- **Microsoft Azure:** Speicher, Queue, SQL Database
- **OpenAI:** KI-generierte Meal Plans
- **Worldcoin:** Authentifizierung (World ID)
- **Alchemy:** Blockchain-Transaktionsverifikation

**Keine Haftung für:**
- Ausfälle von Drittanbietern
- Datenverlust bei Drittanbietern
- Änderungen der Drittanbieter-Dienste
- Datenschutz-Praktiken der Drittanbieter (siehe separate Datenschutzerklärung)
```

---

### 9. **Watch Analytics / View-Tracking**

**Was fehlt:**
- Views werden gezählt
- Creator kann Statistiken sehen
- View-Manipulation ist verboten

**Muss in AGB:**
```
§ X. Analytics und Statistiken

- Plattform trackt Views, Likes, Follower für Creator-Statistiken
- View-Counts sind Schätzungen (kein Anspruch auf Exaktheit)
- Manipulation von Views ist verboten (Bots, Fake-Accounts)
- Bei Manipulation: Account-Sperrung + Löschung der Statistiken
```

---

### 10. **Ad Preferences / Werbung**

**Was fehlt:**
- Plattform kann Werbung schalten
- Nutzer kann Präferenzen einstellen
- Keine Garantie für werbefreie Nutzung

**Muss in AGB:**
```
§ X. Werbung

- Plattform kann Werbung auf der Plattform schalten
- Nutzer kann Werbepräferenzen einstellen (keine Garantie für Wirksamkeit)
- Keine werbefreie Version garantiert
- Werbung kann personalisiert sein (siehe Datenschutzerklärung)
- Einnahmen aus Werbung gehören der Plattform
```

---

### 11. **Super Admin / Moderations-Rechte**

**Was fehlt:**
- Plattform hat Super-Admin-Zugriff
- Kann alle Inhalte sehen/bearbeiten/löschen
- Zu Moderationszwecken

**Muss in AGB:**
```
§ X. Moderation und Admin-Zugriff

- Plattform-Administratoren haben Zugriff auf alle Inhalte zu Moderationszwecken
- Administratoren können Inhalte bearbeiten/löschen bei Verstößen
- Keine Haftung für Moderationsentscheidungen
- Moderation erfolgt stichprobenartig (nicht alle Inhalte werden geprüft)
```

---

### 12. **RecipeSwap / AI-Transformationen**

**Was fehlt:**
- Rezept-Varianten können generiert werden
- KI transformiert Rezepte (vegan, glutenfrei, etc.)

**Muss in AGB:**
```
§ X. Rezept-Varianten (AI-Transformationen)

- Plattform kann Rezept-Varianten automatisch generieren (z.B. vegane Alternative)
- KI-generierte Varianten sind ungeprüft
- Keine Garantie für Geschmack, Konsistenz oder Funktionalität
- Nutzer verantwortlich für Prüfung der Varianten
```

---

### 13. **Bunny.net Webhooks**

**Was fehlt:**
- Bunny.net sendet Webhooks bei Video-Processing
- Plattform hat keine Kontrolle über Bunny.net

**Muss in AGB:**
```
§ X. Video-Processing-Webhooks

- Videos werden von Bunny.net CDN verarbeitet
- Plattform erhält Benachrichtigungen über Processing-Status
- Bei Fehlern: Plattform informiert Nutzer (best effort)
- Keine Garantie für erfolgreiche Video-Verarbeitung
```

---

### 14. **Mehrsprachigkeit**

**Was fehlt:**
- Keywords in DE/EN/ES/PT
- Automatische Übersetzungen können fehlerhaft sein
- Keine Garantie für korrekte Übersetzungen

**Muss in AGB:**
```
§ X. Mehrsprachigkeit

- Plattform unterstützt mehrere Sprachen (DE/EN/ES/PT)
- Automatische Übersetzungen können fehlerhaft sein
- Keine Garantie für korrekte Übersetzungen
- Nutzer verantwortlich für Überprüfung von Rezepten in ihrer Sprache
```

---

### 15. **Thumbnails und automatische Bildverarbeitung**

**Was fehlt:**
- Bilder werden automatisch zu WEBP konvertiert
- Thumbnails werden generiert (400x711)
- Original-Qualität geht verloren

**Muss in AGB:**
```
§ X. Bild- und Thumbnail-Verarbeitung

- Hochgeladene Bilder werden automatisch komprimiert und zu WEBP konvertiert
- Thumbnails werden automatisch generiert (400x711)
- Original-Dateien werden nicht gespeichert
- Plattform ist nicht verantwortlich für Qualitätsverlust
```

---

### 16. **Doppel-Authentifizierung (World ID + Wallet)**

**Was fehlt:**
- Zwei verschiedene Login-Methoden
- Nutzer kann zwischen beiden wechseln
- Bei Verlust beider: Account nicht wiederherstellbar

**Muss in AGB:**
```
§ X. Authentifizierungsmethoden

- Login via World ID (biometrisch/Gerät) ODER Wallet (SIWE)
- Nutzer kann nur EINE Methode pro Account nutzen
- Bei Verlust von World ID UND Wallet: Account NICHT wiederherstellbar
- Plattform kann Account nicht zurücksetzen (technisch unmöglich)
```

---

### 17. **Feed-Algorithmus**

**Was fehlt:**
- Feed ist algorithmisch personalisiert
- Keine Garantie für Reichweite
- Plattform kann Algorithmus jederzeit ändern

**Muss in AGB:**
```
§ X. Feed-Algorithmus

- Feed wird algorithmisch personalisiert
- Keine Garantie für Sichtbarkeit oder Reichweite von Posts
- Plattform kann Algorithmus jederzeit ohne Ankündigung ändern
- Kein Anspruch auf bestimmte Anzahl von Views/Likes
```

---

### 18. **Meal Plan Kopien bei Verkauf**

**Was fehlt:**
- Bei Verkauf wird Meal Plan kopiert
- Original bleibt beim Verkäufer
- Käufer erhält eigene Kopie
- Änderungen des Originals betreffen Kopie nicht

**Muss in AGB:**
```
§ X. Meal Plan Kopien

- Bei Verkauf wird eine Kopie des Meal Plans erstellt
- Original bleibt beim Verkäufer erhalten
- Käufer erhält eigenständige Kopie
- Spätere Änderungen am Original betreffen Käufer-Kopie nicht
- Bei Löschung des Originals bleibt Käufer-Kopie erhalten
```

---

### 19. **Nährwertberechnung**

**Was fehlt:**
- Nährwerte sind automatisch berechnet
- Können ungenau sein
- Keine medizinische Beratung

**Muss in AGB:**
```
§ X. Nährwertangaben

- Nährwerte werden automatisch berechnet (Schätzungen)
- Keine Garantie für Genauigkeit
- Abweichungen von realen Werten möglich
- Nährwertangaben ersetzen keine medizinische/ernährungswissenschaftliche Beratung
- Bei Diäten/Allergien: Professionelle Beratung einholen
```

---

### 20. **Session-Management und Cookies**

**Was fehlt:**
- Session-Cookies werden verwendet
- UserHash in Session gespeichert
- Wallet-Adressen in Session
- Bei Cookie-Löschung: Logout

**Muss in AGB:**
```
§ X. Session und Cookies

- Plattform nutzt Session-Cookies für Login
- UserHash und Wallet-Adressen werden in Session gespeichert
- Bei Cookie-Löschung: Automatischer Logout
- Session-Timeout: 24 Stunden (standardmäßig)
- Details siehe Datenschutzerklärung
```

---

## ✅ Zusammenfassung: Was MUSS noch rein

### Neue Paragraphen hinzufügen:

1. ✅ **§ Social Features** (Likes, Follows, Kommentare)
2. ✅ **§ WildCoin** (virtuelle Währung)
3. ✅ **§ Upload-Limits** (Rate Limits, Dateigrößen)
4. ✅ **§ Video-Processing** (asynchron, Verzögerungen)
5. ✅ **§ Öffentliche Links** (Meal Plan Sharing)
6. ✅ **§ KI-generierte Inhalte** (OpenAI, keine Garantie)
7. ✅ **§ Verifizierungslevel** (orb vs device)
8. ✅ **§ Drittanbieter-Dienste** (Bunny, Azure, etc.)
9. ✅ **§ Analytics** (View-Tracking, Statistiken)
10. ✅ **§ Werbung** (Ad-System)
11. ✅ **§ Moderation** (Super Admin Zugriff)
12. ✅ **§ Rezept-Varianten** (AI-Transformationen)
13. ✅ **§ Mehrsprachigkeit** (Übersetzungen)
14. ✅ **§ Bildverarbeitung** (WEBP, Thumbnails)
15. ✅ **§ Feed-Algorithmus** (keine Garantie für Reichweite)
16. ✅ **§ Nährwertangaben** (Schätzungen, ungenau)
17. ✅ **§ Session-Management** (Cookies, Timeout)

---

## 🔴 Besonders kritisch:

1. **WildCoin** - Muss klar sein, dass es KEIN echtes Geld ist
2. **KI-Inhalte** - Keine Haftung für OpenAI-Fehler
3. **Verifizierungslevel** - Einschränkungen müssen klar kommuniziert werden
4. **Drittanbieter** - Keine Haftung für Bunny.net, Azure, etc.
5. **Nährwerte** - KEINE medizinische Beratung

---

Soll ich die AGBs jetzt mit diesen Punkten aktualisieren?
