# 🎬 Automatische Untertitel-Generierung - Setup Guide

## 💰 Kostenvergleich

| Service | Kosten pro Video-Minute | Kosten für 100 Minuten |
|---------|-------------------------|------------------------|
| **Bunny AI Captions (10 Sprachen)** | $1.00 | $100 |
| **Whisper + DeepL (10 Sprachen)** | ~$0.06 | ~$6 |
| **Ersparnis** | **94%** | **$94** |

---

## 📋 Was wurde implementiert?

### Neue Dateien:
1. **CaptionGenerationService.cs** - Haupt-Service für Caption-Generierung
2. **BunnyWebhookController.cs** (erweitert) - Triggert Caption-Generation nach Video-Enkodierung

### Workflow:
```
1. User lädt Video hoch → Bunny Stream
2. Bunny enkodiert Video → sendet Webhook
3. Webhook triggert CaptionGenerationService
4. Service:
   a) Download Video temporär
   b) Transkription mit Whisper (Deutsch)
   c) Übersetzung in 9 Sprachen mit DeepL
   d) Upload aller .vtt-Dateien zu Bunny Storage
   e) Cleanup (Temp-File löschen)
5. Feed zeigt Untertitel automatisch an
```

---

## 🔑 Schritt 1: API-Keys besorgen

### OpenAI API Key (Whisper)
1. Gehe zu: https://platform.openai.com/api-keys
2. Klicke **"Create new secret key"**
3. Kopiere den Key: `sk-proj-...`
4. **Kosten:** $0.006 pro Minute (~1 Cent pro 2 Minuten)

### DeepL API Key (Übersetzung)
1. Gehe zu: https://www.deepl.com/pro-api
2. Wähle **"DeepL API Free"** (500,000 Zeichen/Monat kostenlos!)
3. Registriere dich
4. Kopiere den API Key: `....:fx` (endet mit `:fx` für Free-Tier)
5. **Kosten Free-Tier:**
   - 500k Zeichen/Monat kostenlos
   - ~5k Zeichen pro Video-Minute
   - = **100 Video-Minuten pro Monat kostenlos!**
6. **Kosten Pro-Tier (falls nötig):** $5 pro 500k Zeichen

### Optional: Google Translate API (für ID/MS)
DeepL unterstützt kein Indonesisch/Malay. Für diese 2 Sprachen wird Google Translate als Fallback genutzt.

1. Gehe zu: https://console.cloud.google.com/apis/library/translate.googleapis.com
2. Aktiviere die API
3. Erstelle API Key unter "Credentials"
4. **Kosten:** $20 pro 1M Zeichen (~$0.001 pro Video-Minute für 2 Sprachen)

---

## ⚙️ Schritt 2: Konfiguration in appsettings.json

Öffne `appsettings.json` und füge hinzu:

```json
{
  "OpenAI": {
    "ApiKey": "sk-proj-DEIN_OPENAI_KEY_HIER"
  },
  "DeepL": {
    "ApiKey": "dein-deepl-key-hier:fx"
  },
  "Google": {
    "TranslateApiKey": "DEIN_GOOGLE_KEY_HIER"
  },
  "BunnyNet": {
    "StorageZoneName": "delekatesendrehbuch",
    "StorageApiKey": "DEIN_BUNNY_STORAGE_KEY",
    "StoragePath": "blob-world-mini-app",
    "StreamLibraryId": "DEINE_LIBRARY_ID",
    "StreamApiKey": "DEIN_STREAM_API_KEY",
    "StreamHostname": "vz-XXXXX-xxx.b-cdn.net"
  }
}
```

### Wo finde ich die Bunny Keys?

**Storage API Key:**
1. Bunny Dashboard → Storage → deine Storage Zone
2. "FTP & API Access" → API Key kopieren

**Stream Library ID & API Key:**
1. Bunny Dashboard → Stream → deine Video Library
2. Library ID steht oben (Nummer)
3. API Key unter "API" Tab

**Stream Hostname:**
1. Bunny Dashboard → Stream → Video Library
2. "CDN Hostname" kopieren (z.B. `vz-12345678-abc.b-cdn.net`)

---

## 🚫 Schritt 3: Bunny AI Captions DEAKTIVIEREN

**WICHTIG:** Deaktiviere Bunny's AI Captions um doppelte Kosten zu vermeiden!

1. Gehe zu: Bunny Dashboard → Stream → Video Library
2. Klicke auf **"Settings"**
3. Scroll zu **"AI Captions"**
4. **Deaktiviere** "Auto-generate captions"
5. **Speichern**

✅ Ab jetzt generiert dein Service die Captions!

---

## 🧪 Schritt 4: Testen

### Test 1: Video hochladen
1. Gehe zu CreatePosting
2. Lade ein kurzes Test-Video hoch (~30 Sekunden)
3. Veröffentliche das Rezept

### Test 2: Logs checken
Nach ~2-3 Minuten (Bunny-Enkodierung + Caption-Generation):

**In Visual Studio Output:**
```
🎬 Starting caption generation for video: abcd-1234-5678-efgh
📥 Video downloaded: 15234567 bytes
🎤 Transcribing with Whisper...
✅ Whisper transcription successful (1234 chars)
🌐 Translating to en...
✅ en captions uploaded
🌐 Translating to es...
✅ es captions uploaded
...
🎉 Caption generation completed for abcd-1234-5678-efgh
```

### Test 3: Untertitel im Feed checken
1. Öffne den Feed
2. Spiele dein Video ab
3. Untertitel sollten automatisch erscheinen (Deutsch)

**Sprach-Auswahl (optional):**
- Aktuell wird nur Deutsch geladen
- Wenn du Sprach-Auswahl willst, sag Bescheid!

---

## 📊 Monitoring & Kosten-Tracking

### OpenAI Usage
https://platform.openai.com/usage

### DeepL Usage
https://www.deepl.com/pro-account/usage

### Erwartete Kosten (10 Sprachen):
- **10 Videos à 2 Minuten = 20 Minuten**
  - Whisper: 20 × $0.006 = $0.12
  - DeepL: 20 × $0.05 = $1.00
  - **Total: ~$1.12** (Bunny: $20)

---

## 🐛 Troubleshooting

### "OpenAI API Key not configured"
→ Check appsettings.json → `"OpenAI": { "ApiKey": "..." }`

### "DeepL API Key not configured"
→ Check appsettings.json → `"DeepL": { "ApiKey": "....:fx" }`

### "Failed to download video"
→ Check Bunny Stream API Key & Library ID in appsettings.json

### Untertitel werden nicht angezeigt
1. Check Browser Console (F12) für Fehler
2. Check ob VTT-Dateien hochgeladen wurden:
   - URL: `https://delekatesendrehbuchcdn-beecexhdaghhacab.z01.azurefd.net/blob-world-mini-app/captions/{VIDEO_GUID}/de.vtt`
3. Check Bunny Storage → Ordner `captions` existiert?

### Caption-Generation startet nicht
→ Check ob Webhook korrekt konfiguriert ist:
- Bunny Dashboard → Stream → Video Library → Webhooks
- URL: `https://DEINE_DOMAIN/WorldMiniApp/Bunny/VideoWebhook`

---

## 🎯 Next Steps (Optional)

### Multi-Sprachen-Auswahl im Player
Aktuell wird nur Deutsch geladen. Wenn du möchtest:
- Alle 10 Sprachen gleichzeitig laden
- Sprach-Auswahl-Button im Player
- User kann zwischen Sprachen wechseln

→ Sag Bescheid, dann implementiere ich das!

### Caption-Qualität verbessern
- Whisper hat verschiedene Modelle (klein → groß)
- Aktuell: `whisper-1` (Standard, gute Qualität)
- Alternative: Eigener Whisper-Server (lokal, kostenlos, beste Qualität)

### Nur bestimmte Sprachen
Wenn du nicht alle 10 Sprachen brauchst:
→ Passe `SupportedLanguages` in CaptionGenerationService.cs an

---

## 📞 Support

Bei Problemen:
1. Check Logs (Visual Studio Output)
2. Check Browser Console (F12)
3. Check diese Anleitung nochmal
4. Frag Claude! 😊

---

**Geschätzte Zeit für Setup:** 15-20 Minuten
**Ersparnis pro 100 Video-Minuten:** ~$94 💰
