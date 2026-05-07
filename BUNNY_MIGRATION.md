# 🐰 Bunny.net Migration Guide

## ✅ Was wurde geändert?

1. **BunnyUploadService.cs** ersetzt BlobUploadService.cs
2. **Program.cs** - DI-Registration geändert
3. **appsettings.json** - Bunny.net Config hinzugefügt
4. **MigrateToBunny.ps1** - PowerShell-Script für Datenmigration

## 📋 Umgebungsvariablen setzen

### Azure App Service / Production

Gehe zu **Azure Portal** → **App Service** → **Configuration** → **Application settings**:

#### **Basis-Config (für Bilder + Videos):**
```
Bunny_Net_Storage_Adres = https://storage.bunnycdn.com/avocadon
Bunny_Net_Passwort_Lager = <WRITE_PASSWORD>
Bunny_Net_Passwort_Lager_ReadOnly = <READ_ONLY_PASSWORD>
Bunny_net_host_name = avocadon.b-cdn.net
```

#### **Bunny Stream (EMPFOHLEN für Videos):**
```
Bunny_Net_Api_Stream = <STREAM_API_KEY>
Bunny_Net_Stream_ID = <LIBRARY_ID>
Bunny_Net_Stream_Host_Name = <STREAM_HOSTNAME>
```

**📍 Wo finde ich diese Werte?**

1. **Stream API Key:** Bunny.net Dashboard → **Video Library** → ⚙️ Settings → **API**
   - Kopiere den "API Key"

2. **Library ID:** Bunny.net Dashboard → **Video Library** → ⚙️ Settings → **General**
   - Steht ganz oben: "Video Library ID: 123456"

3. **Stream Hostname:** Bunny.net Dashboard → **Video Library** → ⚙️ Settings → **Hostnames**
   - Format: `vz-xxxxx-xxx.b-cdn.net` (z.B. `vz-72df6d79-681.b-cdn.net`)
   - Falls nicht vorhanden: "Add Hostname" → Standard-Pull-Zone auswählen

**🎬 Bunny Stream Vorteile:**
- ✅ Automatisches HLS Transcoding (.m3u8 Playlists)
- ✅ Automatische Thumbnail-Generierung
- ✅ Adaptive Bitrate (bessere Performance)
- ✅ Kein FFmpeg/Azure Queue nötig!

**📦 Fallback:** Wenn Stream-Variablen fehlen, wird Bunny Storage verwendet (normale .mp4 Dateien wie Azure Blob)

### Lokale Entwicklung

Erstelle/editiere `appsettings.Development.json`:

```json
{
  "Bunny_Net_Passwort_Lager": "DEIN_BUNNY_PASSWORD_HIER",
  "Bunny_Net_Storage_Adres": "https://storage.bunnycdn.com/avocadon",
  "Queue_Connection_String": "DEINE_AZURE_QUEUE_CONNECTION_STRING"
}
```

Oder setze Umgebungsvariablen:

**Windows (PowerShell):**
```powershell
$env:Bunny_Net_Passwort_Lager = "DEIN_PASSWORD"
$env:Bunny_Net_Storage_Adres = "https://storage.bunnycdn.com/avocadon"
```

**Linux/Mac:**
```bash
export Bunny_Net_Passwort_Lager="DEIN_PASSWORD"
export Bunny_Net_Storage_Adres="https://storage.bunnycdn.com/avocadon"
```

## 🔄 Datenmigration durchführen

### Voraussetzungen

1. **Azure PowerShell Modul installieren:**
```powershell
Install-Module -Name Az.Storage -Force -AllowClobber
```

2. **Daten sammeln:**
   - Azure Blob Connection String
   - Azure Container Name (Standard: `blob-world-mini-app`)
   - Bunny Storage URL: `https://storage.bunnycdn.com/avocadon`
   - Bunny Access Key (aus Bunny.net Panel)

### Migration ausführen

```powershell
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch

.\MigrateToBunny.ps1 `
    -AzureConnectionString "DefaultEndpointsProtocol=https;AccountName=..." `
    -AzureContainerName "blob-world-mini-app" `
    -BunnyStorageUrl "https://storage.bunnycdn.com/avocadon" `
    -BunnyAccessKey "DEIN_BUNNY_PASSWORD"
```

**⏱️ Dauer:** Ca. 10 Sekunden pro 100 Dateien (wegen Rate-Limiting)

## 🎬 Video-Wiedergabe (HLS.js statt iFrame)

**✅ NEU:** Feed verwendet jetzt HLS.js für Bunny Stream Videos (`.m3u8` Playlists).

**Änderungen in `Feed/Index.cshtml`:**
1. ❌ **iFrame-Code entfernt** - Bunny Embed iFrames werden nicht mehr verwendet
2. ✅ **HLS.js CDN eingebunden** - `hls.js@1.5.15` von CDN
3. ✅ **Automatische HLS-Init** - Alle `<video data-video-kind="hls">` werden automatisch initialisiert
4. ✅ **Native Fallback** - Safari/iOS verwenden native HLS-Unterstützung

**Beispiel gespeicherter URLs:**
```
SourceUrl: https://vz-72df6d79-681.b-cdn.net/aaeee223-0b4b-418b-a305-5146756603c3/playlist.m3u8
ThumbnailUrl: https://vz-72df6d79-681.b-cdn.net/aaeee223-0b4b-418b-a305-5146756603c3/thumbnail.jpg
```

## 🎬 VideoProcessor (NUR für Bunny Storage Fallback)

**⚠️ HINWEIS:** Wenn Bunny Stream aktiviert ist, wird VideoProcessor **NICHT** mehr benötigt!

Bunny Stream übernimmt automatisch:
- ✅ Video Transcoding (HLS)
- ✅ Thumbnail-Generierung
- ✅ Adaptive Bitrate

**Nur bei Bunny Storage (ohne Stream):**

Der `DelikatessenDrehbuchViedeoProzessor` (Azure Function) muss angepasst werden:

```csharp
// Output zu Bunny.net statt Azure Blob
var bunnyUrl = $"https://storage.bunnycdn.com/avocadon/{processedFileName}";
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("AccessKey", bunnyAccessKey);
var content = new ByteArrayContent(processedVideoBytes);
content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
await httpClient.PutAsync(bunnyUrl, content);
```

## ✅ Testen

1. **Neues Bild hochladen:**
   - WorldMiniApp → Upload → Bild auswählen
   - Prüfen ob URL `https://avocadon.b-cdn.net/...` ist

2. **Neues Video hochladen:**
   - WorldMiniApp → Upload → Video auswählen
   - Prüfen ob Raw-Upload funktioniert
   - Warten auf FFmpeg-Processing (Queue)

3. **Alte Uploads prüfen:**
   - Nach Migration sollten alte Bilder/Videos weiterhin funktionieren
   - URLs in DB bleiben gleich (CDN-URL)

## 🔍 Troubleshooting

### Upload schlägt fehl: "401 Unauthorized"
- ✅ Prüfe `Bunny_Net_Passwort_Lager` Umgebungsvariable
- ✅ Prüfe ob Access Key korrekt ist (Bunny Panel → Storage → FTP & API Access)

### Upload schlägt fehl: "404 Not Found"
- ✅ Prüfe `Bunny_Net_Storage_Adres` (muss `https://storage.bunnycdn.com/avocadon` sein)
- ✅ Prüfe ob Storage Zone Name korrekt ist

### Bilder werden nicht angezeigt
- ✅ Prüfe CORS-Einstellungen in Bunny.net Pull Zone
- ✅ Prüfe ob CDN URL korrekt ist (`https://avocadon.b-cdn.net/...`)

### Video-Processing funktioniert nicht
- ✅ Prüfe Azure Queue Connection String
- ✅ Prüfe VideoProcessor Logs (Azure Function)
- ✅ Prüfe ob Queue-Message-Format korrekt ist (jetzt `fileName` statt `blobName`)

## 📊 Kosten-Vergleich

**Vorher (Azure):**
- Blob Storage: ~$0.02/GB/Monat
- Front Door: ~$35/Monat + Traffic
- **Gesamt:** ~$50-100/Monat

**Nachher (Bunny.net):**
- Storage: ~$0.01/GB/Monat
- CDN Traffic: ~$0.01/GB
- **Gesamt:** ~$5-15/Monat

**💰 Ersparnis: ~85%**

## 🚨 Rollback

Falls Probleme auftreten:

1. **Program.cs** ändern:
```csharp
builder.Services.AddScoped<IBlobUploadService, BlobUploadService>(); // ← Alt
```

2. **appsettings.json** zurücksetzen
3. App neu deployen

## 📝 Nächste Schritte

- [ ] Umgebungsvariablen in Azure setzen
- [ ] App neu deployen
- [ ] Migration durchführen (`MigrateToBunny.ps1`)
- [ ] VideoProcessor anpassen
- [ ] Testen (Upload + alte Dateien)
- [ ] Azure Blob Storage löschen (nach 1 Woche erfolgreicher Nutzung)

## ❓ Fragen?

Schau in die Logs:
```bash
# Azure App Service Logs
az webapp log tail --name delikatessendrehbuch --resource-group <RG_NAME>
```

Oder prüfe Bunny.net Dashboard → Storage → avocadon → Files
