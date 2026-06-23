# Upload-Bereich â€” Dokumentation (WorldMiniApp)

VollstÃ¤ndige Referenz fÃ¼r den **Medien-/Rezept-Upload**: vom Antippen von â€žHochladen"
bis zum fertig transkodierten Video im Feed. Deckt Endpunkte, Validierung, Rate-Limiting,
Bild- vs. Video-Pfad, Bunny-Storage/Stream und Benachrichtigungen ab.

> Abgrenzung: Hier geht es um den **Datei-Upload + Speicherung + Verarbeitung**.

Stand: 2026-06-12

---

## Inhalt
1. [Ãœberblick & Ablauf](#1-Ã¼berblick--ablauf)
2. [Einstieg / UI](#2-einstieg--ui)
3. [Upload-Endpunkt](#3-upload-endpunkt-uploadnewvideoasync)
4. [Bild-Pfad (synchron)](#4-bild-pfad-synchron)
5. [Video-Pfad (asynchron)](#5-video-pfad-asynchron)
6. [Speicher-Service: BunnyUploadService](#6-speicher-service-bunnyuploadservice)
7. [Transcoding, Untertitel (Whisper) & Benachrichtigung](#7-transcoding-untertitel-whisper--benachrichtigung)
8. [KI-Zutaten erstellen (AI)](#8-ki-zutaten-erstellen-ai)
9. [Validierung & Limits](#9-validierung--limits)
10. [Rate-Limiting & Idempotenz](#10-rate-limiting--idempotenz)
11. [Konfiguration](#11-konfiguration)
12. [Beteiligte Dateien](#12-beteiligte-dateien)
13. [Datenbank-Auswirkungen](#13-datenbank-auswirkungen)
14. [Bekannte SchwÃ¤chen / TODO](#14-bekannte-schwÃ¤chen--todo)

---

## 1. Ãœberblick & Ablauf

```
World App â”€â”€â–º CreatePosting.cshtml â”€â”€(POST UploadNewVideo)â”€â”€â–º RecipeController.UploadNewVideoAsync
                                                                    â”‚
                          â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Bild? â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”         â”‚ Video?
                          â–¼ (synchron)                   â”‚         â–¼ (asynchron, Fire-and-Forget)
        BunnyUploadService.UploadContentToBlob           â”‚   videoBytes in Memory, sofort "wird verarbeitet"
          â”œâ”€ Bild â†’ WEBP (Source + Thumb)                â”‚   _ = Task.Run(ProcessVideoUploadInBackgroundAsync)
          â””â”€ Upload â†’ Bunny Storage (CDN-URL)            â”‚         â”‚
                          â”‚                              â”‚         â”œâ”€ BunnyUploadService â†’ Bunny STREAM (VideoGuid)
        SaveNewRecipeService.SaveNewAsync                â”‚         â”œâ”€ AI-Ãœbersetzung der Schritte (10 Sprachen)
        WorldUserPosting anlegen                         â”‚         â”œâ”€ SaveNewRecipeService + WorldUserPosting
                          â”‚                              â”‚         â”œâ”€ WorldUserPendingVideo (VideoGuid)
                          â–¼                              â”‚         â””â”€ Notification "hochgeladen"
                       Feed                              â”‚                  â”‚
                                                         â”‚   (Bunny transkodiert async)
                                                         â”‚                  â–¼
                                                         â”‚   BunnyWebhookController (status=3 "finished")
                                                         â”‚     â”œâ”€ Notification "Video fertig"
                                                         â”‚     â””â”€ CaptionGenerationService (Untertitel)
```

**Kernidee:** **Bilder** sind klein â†’ synchron. **Videos** sind groÃŸ (bis 200 MB) und brauchen
Transcoding â†’ der Request gibt **sofort** â€žwird verarbeitet" zurÃ¼ck, der eigentliche Upload lÃ¤uft
im Hintergrund; Bunny meldet per Webhook, wenn das Video fertig ist.

---

## 2. Einstieg / UI

- **GET** `RecipeController.Upload(string userHash)` â†’ rendert `Views/Home/CreatePosting.cshtml`
  mit Auswahllisten (Zutaten, MaÃŸeinheiten, Keywords).
- Das Formular wird per **POST `UploadNewVideo`** abgeschickt (`[ActionName("UploadNewVideo")]`),
  Multipart mit der Datei in `posting.Content` (`IFormFile`) + Rezept-Metadaten + `StepsText`.

---

## 3. Upload-Endpunkt (`UploadNewVideoAsync`)

`POST` Â· `[ValidateAntiForgeryToken]` Â· `RecipeController.UploadNewVideoAsync(WorldUserPosting posting, string userHash)`

Ablauf (in dieser Reihenfolge):
1. **IdentitÃ¤t:** `userHash = ResolveUserHash(userHash)` (Claim-basiert, siehe Auth-Doku).
2. **Berechtigung:** `IsCreatorAllowedAsync(...)` â†’ nur **orb**-Creator (oder `SuperUserHash`) dÃ¼rfen hochladen.
   Sonst `{ success=false, error="Nicht berechtigt." }`.
3. **Datei vorhanden?** `posting.Content` nicht leer.
4. **Idempotenz:** Header `X-Idempotency-Key` â†’ bei Wiederholung (Netzwerk-Retry) wird das
   gecachte Ergebnis zurÃ¼ckgegeben (MemoryCache, 10 Min), kein Doppel-Upload.
5. **Rate-Limit:** `TryConsumeUploadSlot(userHash)` â†’ **5 Uploads / 10 Min** pro User (sonst `Retry-After`).
6. **Verzweigung Bild vs. Video:** anhand `Content.ContentType.StartsWith("video/")`.

---

## 4. Bild-Pfad (synchron)

1. `BunnyUploadService.UploadContentToBlob(posting.Content)` â†’ Bild wird zu **WEBP** konvertiert
   (Source + Thumbnail) und nach **Bunny Storage** geladen â†’ liefert `SourceUrl` + `ThumbnailUrl`.
2. **Schritte Ã¼bersetzen** (falls `StepsText` vorhanden): `RecipeTranslationService.TranslateRecipeAsync`
   erzeugt `RecipeSteps` in **10 Sprachen** (de/en/esp/prt/id/nl/sv/da/no/ms).
3. **Rezept speichern:** `SaveNewRecipeService.SaveNewAsync(recipeModel, true)`
   (Titel, Kategorie, Personen, Zubereitungszeit, `ImagePath` = Thumbnail bzw. Bild, Zutaten, Schritte).
4. **`WorldUserPosting`** anlegen (CreatorId = userHash, Source/Thumbnail, Recipe), Keywords verlinken.
5. JSON-Erfolg zurÃ¼ck.

---

## 5. Video-Pfad (asynchron)

Weil Video-Upload + Transcoding 3â€“4 Minuten dauern kÃ¶nnen, wird **nicht** im Request gewartet:

1. **Video in den Speicher lesen** (`videoBytes`), damit es nach Request-Ende verfÃ¼gbar ist.
2. **Einfache Daten extrahieren** (keine EF-Entities â†’ keine DbContext-Tracking-Probleme):
   Titel, Kategorie, Personen, Zeit, Preferences, Zutaten (Id/Menge/MaÃŸ), Keyword-IDs, StepsText, Bytes, Dateiname, ContentType.
3. **Hintergrund-Task starten:** `_ = Task.Run(() => ProcessVideoUploadInBackgroundAsync(uploadData))`
   (Fire-and-Forget; bei Fehler â†’ Fehler-Notification an den User).
4. **Sofort** `{ success=true, isAsync=true, isVideo=true, message="Upload wird verarbeitet..." }` zurÃ¼ck
   (+ Idempotenz-Cache).

### `ProcessVideoUploadInBackgroundAsync` (eigener DI-Scope!)
1. **Eigenen Scope** erstellen (`IServiceScopeFactory`) â†’ frischer `DbContext`, BlobUpload, SaveRecipe, Translation.
2. **Video hochladen:** `BunnyUploadService.UploadContentToBlob(videoFile)` â†’ **Bunny Stream** â†’ `VideoGuid` + URLs.
3. **Schritte Ã¼bersetzen** (10 Sprachen), falls vorhanden.
4. **Zutaten rekonstruieren** aus den einfachen Daten (Ingredient/Measure aus DB neu laden).
5. **Rezept speichern** (`SaveNewAsync`), **`WorldUserPosting`** anlegen, **Keywords** verlinken.
6. **`WorldUserPendingVideo`** anlegen (`VideoGuid`, PostingId, UserHash) â†’ fÃ¼r die â€žVideo fertig"-Meldung.
7. **Notification** â€žDein Video â€¦ wurde hochgeladen!" (`EventType="upload-complete"`).

---

## 6. Speicher-Service: `BunnyUploadService`

Registriert als `IBlobUploadService` (`Program.cs`). HttpClient `"BunnyStorage"` via Factory.

### Bilder
- Konvertierung zu **WEBP** mit ImageSharp:
  - **Source:** 1080Ã—1920, Ziel ~0,85 MB (QualitÃ¤t wird heruntergeregelt bis ZielgrÃ¶ÃŸe).
  - **Thumbnail:** 400Ã—711, Ziel ~250 KB.
- Upload per **PUT** zu Bunny Storage (`{Bunny_Net_Storage_Adres}/{fileName}`), RÃ¼ckgabe der **CDN-URL**.

### Videos
- Wenn **Bunny Stream** konfiguriert ist (`Bunny_Net_Api_Stream` + `Bunny_Net_Stream_ID` + `Bunny_Net_Stream_Host_Name`):
  1. `POST https://video.bunnycdn.com/library/{libraryId}/videos` â†’ erzeugt Video â†’ **`VideoGuid`**.
  2. `PUT â€¦/videos/{videoGuid}` â†’ lÃ¤dt die Bytes hoch (`waitForTranscoding: false` â†’ kehrt sofort zurÃ¼ck).
  3. Liefert `UploadContentResult { SourceUrl, ThumbnailUrl, VideoGuid }`.
- **Fallback** (Stream nicht konfiguriert): Upload nach Bunny **Storage** als `video/mp4` (kein automatisches Processing, kein `VideoGuid`).

> Hinweis: Der frÃ¼here `BlobUploadService` (Azure Blob + Azure-Queue-FFmpeg-Processor) wurde
> **entfernt** (war Dead Code) â€” aktiv ist ausschlieÃŸlich `BunnyUploadService` (siehe Abschnitt 14).

---

## 7. Transcoding, Untertitel (Whisper) & Benachrichtigung

### 7.1 Webhook
`BunnyWebhookController` (`/WorldMiniApp/Bunny/VideoWebhook`, `[AllowAnonymous]`):
1. Bunny ruft den Webhook, wenn ein Video einen Status erreicht.
2. **Optionale SignaturprÃ¼fung** (HMAC-SHA256) â€” aktiv, wenn `Bunny_Net_Validate_Webhook_Signature=true`
   **und** eine Signatur mitgeschickt wird (sonst durchgelassen).
3. Nur **Status `3` (finished/encoded)** wird verarbeitet; andere ignoriert.
4. Sucht das `WorldUserPendingVideo` per `VideoGuid`:
   - **Notification** â€žDein Video â€¦ ist fertig!" (`EventType="video-ready"`).
   - **Pending-Video entfernen.**
   - **Untertitel-Generierung** anstoÃŸen (im Hintergrund), siehe 7.2.

### 7.2 Untertitel via Whisper + Ãœbersetzung (`CaptionGenerationService`)
`GenerateCaptionsForVideoAsync(videoUrl, videoGuid)` lÃ¤uft nach dem Transcoding:

1. **Video laden** â€” die Bunny-Playlist/Datei wird heruntergeladen.
2. **Transkription (Whisper):** POST an `https://api.openai.com/v1/audio/transcriptions`
   mit Modell **`whisper-1`** â†’ **deutsches WebVTT** (Untertitel mit Zeitstempeln).
3. **DE-Untertitel hochladen:** als `captions/{videoGuid}/de.vtt` zu Bunny Storage.
4. **Ãœbersetzen:** das deutsche VTT wird per OpenAI-Chat-Modell
   (`OpenAI:CaptionTranslationModel`) in die **ZielÂ­sprachen**
   (`OpenAI:CaptionTargetLanguages`) Ã¼bersetzt â€” **nur der gesprochene Text**, Zeitstempel bleiben.
5. **Pro Sprache hochladen:** `captions/{videoGuid}/{lang}.vtt`.

So bekommt jedes hochgeladene Video automatisch **mehrsprachige Untertitel**, ohne dass der
Creator etwas tun muss. SchlÃ¤gt eine Ãœbersetzung fehl, werden die Ã¼brigen trotzdem erzeugt.

Konfig: `OpenAI:ApiKey` / `SecretKeyOpenAi`, `OpenAI:CaptionTranslationModel`, `OpenAI:CaptionTargetLanguages`.

---

## 8. KI-Zutaten erstellen (AI)

Beim Anlegen eines Rezepts kann ein Creator eine Zutat eingeben, die **noch nicht im Katalog**
(`IngredientsAndNutrients`) existiert. Dann hilft die KI, eine vollstÃ¤ndige Katalog-Zutat zu erzeugen.
Endpunkte in `RecipeController`:

### 8.1 `SuggestMissingIngredient` (POST)
1. Eingabe: `IngredientName`. Nur **orb-Creator** (`IsCreatorAllowedAsync`).
2. **Erst Katalog durchsuchen** (`FindMatchingIngredientsAsync`, bis 6 Treffer):
   â†’ gibt es passende Zutaten, kommt `Mode = "existing_match"` mit den Treffern zurÃ¼ck (keine KI nÃ¶tig).
3. **Sonst KI fragen:** `IMissingIngredientAiService.SuggestIngredientAsync` (OpenAI)
   schlÃ¤gt eine **vollstÃ¤ndige Zutat** vor (mehrsprachige Namen, NÃ¤hrwerte, Genus/Artikel-Felder)
   â†’ `Mode = "ai_suggestion"` mit `Suggestion` + `Model`.

### 8.2 `SaveSuggestedIngredient` (POST)
Speichert den (ggf. vom Creator geprÃ¼ften) Vorschlag als neue Katalog-Zutat. Schutzschichten:
- **Debug-Fallback** (kein echter OpenAI-Key) â†’ wird **nicht** gespeichert.
- **`LooksSuspiciouslyUntranslated`** â†’ abgelehnt, wenn Sprachfelder nicht sauber Ã¼bersetzt wirken.
- **`LooksMissingGenusData`** â†’ abgelehnt, wenn Genus-Felder (DE: m/f/n/pl, NL: de/het, SE/DK/NO: en/ett/et) fehlen.
- **Berechtigung** (orb-Creator).
- **Duplikat-Check** (`FindExactIngredientAsync`) â†’ vorhandene Zutat wird wiederverwendet statt doppelt angelegt.
- **Rate-Limit:** `MissingIngredientSaveRateLimit = 10` pro Fenster/User.
- Danach: Speichern in `IngredientsAndNutrients` â†’ ab sofort im Rezept-Builder nutzbar.

> Zweck: Der Zutaten-Katalog (mit NÃ¤hrwerten/Ãœbersetzungen, der u. a. die NÃ¤hrwertberechnung speist)
> wÃ¤chst kontrolliert per KI, statt dass Creators rohe Freitext-Zutaten anlegen.

---

## 9. Validierung & Limits

`BunnyUploadService.ValidateFileUpload` (vor jedem Upload):

| PrÃ¼fung | Bild | Video |
|---|---|---|
| **Erlaubte Endungen** | `.jpg .jpeg .png .webp` | `.mp4 .mov .webm` |
| **Max. GrÃ¶ÃŸe** | **15 MB** | **200 MB** |
| **Magic Bytes** | JPEG/PNG/WEBP-Header (16 B) | MP4/MOV/WEBM-Header |

- **Magic-Bytes-Check** (`MatchesMagicBytes`): die ersten 16 Byte mÃ¼ssen zum Dateityp passen
  â†’ verhindert umbenannte/gefÃ¤lschte Dateien (z. B. `.mp4`, das in Wahrheit etwas anderes ist).
- Kestrel-Limit fÃ¼r groÃŸe Uploads ist in `Program.cs` auf **250 MB** gesetzt (`MaxRequestBodySize`).

---

## 10. Rate-Limiting & Idempotenz

- **Upload-Rate-Limit:** `UploadRateLimit = 5` pro `UploadRateWindow` (10 Min), pro `userHash`,
  via `IMemoryCache` (`TryConsumeUploadSlot`). Bei Ãœberschreitung `Retry-After`-Header + Fehler-JSON.
- Weitere Limits im selben Controller: `MissingIngredientSaveRateLimit = 10`, `StepGenerationRateLimit = 20`.
- **Idempotenz:** Header `X-Idempotency-Key` â†’ gecachtes Ergebnis (10 Min) bei Retries â†’ kein Doppel-Posting.

> âš ï¸ Das MemoryCache-Rate-Limit ist **pro Instanz** (nicht geteilt). Bei mehreren App-Instanzen
> greift das Limit pro Instanz, nicht global.

---

## 11. Konfiguration

| Key | WofÃ¼r |
|---|---|
| `Bunny_Net_Passwort_Lager` | Bunny **Storage**-Passwort (Schreibzugriff) |
| `Bunny_Net_Passwort_Lager_ReadOnly` | Read-Only-Key (u. a. Webhook-Signatur) |
| `Bunny_Net_Storage_Adres` | Storage-Zone-URL (z. B. `https://storage.bunnycdn.com/avocadon`) |
| `Bunny_net_host_name` / CDN | CDN-Host fÃ¼r Auslieferung (`â€¦b-cdn.net`) |
| `Bunny_Net_Api_Stream` | Bunny **Stream** API-Key (Videos) |
| `Bunny_Net_Stream_ID` | Stream-Library-ID |
| `Bunny_Net_Stream_Host_Name` | Stream-Host (fÃ¼r Playlist/Playback) |
| `Bunny_Net_Validate_Webhook_Signature` | `true` = Webhook-Signatur erzwingen (wenn vorhanden) |
| `OpenAI:ApiKey` / `SecretKeyOpenAi` | OpenAI-Key (Whisper-Transkription, Untertitel-Ãœbersetzung, KI-Zutaten) |
| `OpenAI:CaptionTranslationModel` | Chat-Modell fÃ¼r die Untertitel-Ãœbersetzung |
| `OpenAI:CaptionTargetLanguages` | Zielsprachen der Untertitel (Liste) |

> Werte in Produktion Ã¼ber **Azure App Settings** setzen, nicht im Repo.

---

## 12. Beteiligte Dateien

| Datei | Rolle |
|---|---|
| `Controllers/RecipeController.cs` | `Upload` (GET), `UploadNewVideoAsync` (POST), `ProcessVideoUploadInBackgroundAsync`, Rate-Limit-Helfer, `UpdateRecipeFromCreateForm`, `DeletePosting` |
| `Services/BunnyUploadService.cs` | **aktiver** Upload-Service (Bilderâ†’WEBPâ†’Bunny Storage, Videosâ†’Bunny Stream), Validierung, Magic-Bytes |
| `Services/SaveNewRecipeService.cs` | Rezept (+ Bilder, Schritte, Zutaten) in DB speichern (Transaktion) |
| `Services/RecipeTranslationService.cs` | Schritte in 10 Sprachen Ã¼bersetzen (AI) |
| `Controllers/BunnyWebhookController.cs` | Transcoding-Webhook â†’ â€žVideo fertig" + Untertitel-Trigger |
| `Services/CaptionGenerationService.cs` | **Whisper**-Transkription â†’ DE-VTT â†’ Ãœbersetzung â†’ Bunny-Captions |
| `Services/MissingIngredientAiService.cs` | KI-Zutaten-Vorschlag (OpenAI) fÃ¼r `SuggestMissingIngredient` |
| `Views/Home/CreatePosting.cshtml` | Upload-/Rezept-Formular |

> Der frÃ¼here `BlobUploadService.cs` (Azure Blob + Queue) war Dead Code und wurde **entfernt**.

---

## 13. Datenbank-Auswirkungen

Ein erfolgreicher Upload erzeugt/Ã¤ndert:
- **`RecipeBaseData`** (+ Bilder, Schritte/`RecipeSteps`, Zutaten) â€” Ã¼ber `SaveNewRecipeService`.
- **`WorldUserPosting`** â€” der Feed-Eintrag (CreatorId, Source, ThumbnailUrl, Recipe).
- **`RecipeBaseKeyword`** â€” N:M-VerknÃ¼pfung der Keywords.
- **`WorldUserPendingVideo`** â€” nur Videos (bis Bunny â€žfinished" meldet).
- **`WorldUserNotification`** â€” â€žhochgeladen" und spÃ¤ter â€žVideo fertig".

(Schema-Details siehe `World-Integration-Handbuch.md`, Â§14.)

---

## 14. Bekannte SchwÃ¤chen / TODO

- **Fire-and-Forget `Task.Run`** fÃ¼r den Video-Hintergrund-Upload: lÃ¤uft auÃŸerhalb des Request-Lifecycles.
  Es wird zwar korrekt ein **eigener DI-Scope** erstellt (gut), aber der Task ist unbeobachtet und geht
  bei App-Shutdown/Recycling **verloren**. Robuster wÃ¤re eine echte Background-Queue (`IHostedService`/Channel).
- **Rate-Limit & Idempotenz im MemoryCache** sind **pro Instanz** â†’ bei Scale-out nicht global.
- **Webhook-Signatur** nur erzwungen, wenn `â€¦Validateâ€¦=true` **und** Signatur vorhanden â†’ in Produktion
  Signatur verpflichtend machen.
- **`BlobUploadService`** (Azure-Variante) war toter Code und wurde **gelÃ¶scht** âœ“.
- Magic-Bytes-Check deckt gÃ¤ngige Formate ab; exotische Container (z. B. fragmentiertes MOV) kÃ¶nnten
  abgelehnt werden â€” bei Bedarf erweitern.
- **Whisper/Ãœbersetzung** brauchen einen gÃ¼ltigen OpenAI-Key; ohne Key bleiben Videos ohne Untertitel
  und KI-Zutaten landen im Debug-Fallback (werden nicht gespeichert).

---

*Bei Ã„nderungen am Upload-/Bunny-/Processing-Code diese Datei mitpflegen.*
