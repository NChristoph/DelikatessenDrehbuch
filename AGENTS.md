# DelikatessenDrehbuch - WorldMiniApp

## Überblick

Die **WorldMiniApp** ist eine eigenständige Mini-Anwendung innerhalb von DelikatessenDrehbuch. Sie bietet eine mobile-first Social-Recipe-Plattform (ähnlich TikTok für Rezepte) mit Worldcoin-Authentifizierung, Video-/Bild-Feed, Essensplanung, Einkaufslisten sowie einem Creator-Marktplatz (Rezepte/Pläne verkaufen).

**Pfad:** `DelikatessenDrehbuch/Areas/WorldMiniApp/`

> **Hinweis zur Pflege:** Diese Datei und `AGENTS.md` (Repo-Wurzel) werden identisch gehalten. Bei Änderungen beide aktualisieren.

---

## Architektur

```
Areas/WorldMiniApp/
├── Controllers/
│   ├── AuthController.cs            # Worldcoin-Login & Session
│   ├── HomeController.cs           # Hauptmenü, Essensplaner, Upload, Rezeptansicht (ShowRecipe)
│   ├── FeedController.cs           # Video-Feed, Likes, Follows, Suche, Profil (MyProfile)
│   ├── FeedCommentsController.cs   # Kommentare + Moderation
│   ├── RecipeController.cs         # Rezept anlegen/bearbeiten/löschen (Edit, UpdateRecipeFromCreateForm)
│   ├── RecipeSwapController.cs     # KI-Zutaten-Tausch + Schritt-Rewrite
│   ├── MarketplaceController.cs    # Kauf/Verkauf, Payout, digitale Produkte
│   ├── MealPlanController.cs       # Essensplan-CRUD, geteilte Pläne
│   ├── SharedController.cs         # Geteilte Links/Feeds, Kanal-Claim, Debug (SuperUser-gated)
│   ├── AdminController.cs          # Creator-Manager, Impersonation, Premium, Claim-Links
│   ├── AdminAdsController.cs       # Werbe-Anzeigen verwalten (SuperUser)
│   ├── AdController.cs             # Ad-Impression/Click-Tracking
│   ├── AdPreferencesController.cs  # Ad-Präferenzen des Users
│   ├── AgentPaymentController.cs   # (Payment-Bestätigung; agent-payment.js derzeit inaktiv)
│   ├── WatchAnalyticsController.cs # Wiedergabe-/Watch-Time-Tracking
│   ├── BunnyWebhookController.cs   # Bunny-Stream-Webhook (Transcoding fertig → Captions)
│   └── WorldMiniAppBaseController.cs # Basisklasse: Hash-Resolve, IsCreatorAllowedAsync, MaskHash, Notifications
├── Models/                         # WorldAppUser, WorldUserPosting, WorldUserMealPlan, Likes, Abos,
│                                   #   Marketplace-/Ad-/Notification-/Caption-/PendingUpload-Modelle …
├── Services/                       # siehe unten
├── Sql/                            # Manuelle Deploy-Skripte (KEINE EF-Migrationen! Siehe „Datenbank")
└── Views/
    ├── Home/                       # Index, Generator, Generated, Upload, CreatePosting, ShowRecipe, Shared* …
    ├── Feed/                       # Feed (Fullscreen-Scroll), MyProfile
    ├── Marketplace/                # Index, Sell, MyListings, Payout, CreatorShop
    └── Admin/ , AdminAds/          # Operator-Panels (bewusst nur teilweise lokalisiert)
```

### Wichtige Services (`Areas/WorldMiniApp/Services/`)

| Service | Zweck |
|---------|-------|
| `AuthService` | Worldcoin-Proof-Verifizierung (Proof ↔ NullifierHash-Bindung) |
| `WorldMiniAppUserHashHelper` | Löst Identität NUR aus signiertem Auth-Claim (kein Hash-Forging) |
| `UserManager` | Benutzer erstellen/aktualisieren |
| `BunnyUploadService` | **Bunny.net** Upload (Bilder→WEBP, Videos→Bunny Stream). Implementiert `IBlobUploadService` (Name „Blob" ist Legacy, KEIN Azure) |
| `SaveNewRecipeService` | Rezepte in DB speichern (Transaktion) |
| `RecipeTranslationService` | Rezept-Schritte in 10 Sprachen übersetzen (OpenAI) |
| `CaptionGenerationService` | Untertitel: Whisper-Transkription + Übersetzung → Bunny `captions/{guid}/{lang}.vtt` |
| `RecipeTtsService` | **TTS-Vorlesen** der Schritte (OpenAI `gpt-4o-mini-tts`) → Bunny `tts/recipe-{id}/{lang}/step-{n}.mp3` (nur Premium-Creators) |
| `VideoUploadProcessingService` + `PendingVideoUploadResumeService` | Durabler Upload (übersteht Prozess-Neustart), Startup-Resume |
| `IngredientSwapAiService` / `RecipeSwapStepAiService` / `MissingIngredientAiService` | KI-Zutaten-Tausch + Schritt-Rewrite |
| `MarketplaceService` (`IMarketplaceService`, alias `IWildCoinService`) | Käufe, Payouts, Guthaben, Zahlungs-Verifikation |
| `MarketplaceRankingService` | Ranking der Marktplatz-Listings |
| `ChannelTransferService` | Kanal-Übergabe an echte World-ID (Claim-Link) |
| `AdInjectionService` | Werbung in den Feed einfügen |
| `WorldClipWatchService` / `WorldAdPreferenceService` | Watch-Analytics / Ad-Präferenzen |
| `RateLimitGuard` | Einfaches Fixed-Window-Rate-Limiting (IMemoryCache) |
| `BackgroundTaskQueue` + `WorldMiniAppQueuedHostedService` | Hintergrund-Task-Verarbeitung |
| `BlockchainService` | On-Chain-Hilfen (Rest aus entferntem Trading-Feature) |

---

## Authentifizierung (Worldcoin World ID)

1. User klickt "Connect World ID" auf der Startseite
2. World App zeigt Verifizierungs-UI an
3. Frontend sendet signierten Proof an `AuthController.VerifyAction`
4. `AuthService.VerifyProofWithWorldcoin` verifiziert den Proof (inkl. `NullifierHash`-Bindung) über die Worldcoin-API
5. Bei Erfolg: `WorldAppUser` wird erstellt/aktualisiert, Auth-Cookie (Scheme `WorldMiniApp`) mit signiertem `WorldUserHash`-Claim gesetzt

**Verifizierungslevel:**
- `"orb"` – Biometrisch verifiziert → darf hochladen, hat Follower-Statistiken
- `"device"` – Geräte-verifiziert → eingeschränkt (nur 1 Essensplan)

**Upload-Berechtigung:** `WorldMiniAppBaseController.IsCreatorAllowedAsync` = orb-verifiziert **ODER** `SuperUserHash` **ODER** aktive Admin-Session (serverseitiges Flag, nicht fälschbar; für „Anmelden als"/Impersonation).

**SuperUser:** `WorldMiniApp:SuperUserHash` (in Azure/Config gesetzt). Admin-Panel (`AdminController`) über PIN-Login → Session-Flag `WorldMiniAppAdminAuthenticated`.

**App ID:** `app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa`

---

## Hauptfunktionen

### 1. Video/Bild-Feed (`FeedController`)
- Fullscreen-Scroll (TikTok-Stil), Autoplay + Mute-Toggle, Doppeltipp-Like
- Untertitel (mehrsprachig) + **TTS-Vorlesen** der Schritte in der Rezeptansicht (Premium)
- Rechtsswipe → Marktplatz-Panel; **Video-Fortschrittsbalken ist wischbar (Vor-/Zurückspulen)**
- Suche (Titel/Kategorie/Keywords/max. Zubereitungszeit), Filter (Feed/Likes/Meine Videos)
- **Werbung** wird per `AdInjectionService` alle X Videos eingefügt (Vollbildkarte + Impression/Click-Tracking)
- Kommentare + Moderation (`FeedCommentsController`)

### 2. Rezept-Upload (`HomeController` / `RecipeController`)
- Videos (MP4/MOV/WEBM, max 200 MB) oder Bilder (JPG/PNG/WEBP, max 15 MB); Bilder → WEBP (1080×1920 + 400×711 Thumb)
- **Rate Limit:** 5 Uploads / 10 Min
- Videos → **Bunny Stream** (automatisches Transcoding + Thumbnails); Bilder/Untertitel/TTS → **Bunny Storage**
- **Durabler Upload:** `PendingVideoUploads`-Tabelle + Resume beim Start (übersteht Neustart/Absturz)
- **Untertitel:** optional VTT beim Upload einfügen (`captionVttText`) → sofort `de.vtt` + Übersetzungen; sonst Whisper via Bunny-Webhook. Beim Bearbeiten editier-/neu-übersetzbar.
- Schritte werden per KI in 10 Sprachen übersetzt und als `RecipeSteps` (Culture + Text) gespeichert

### 3. Essensplaner (`MealPlanController` / `HomeController`)
- Generator (Diättyp, Filter, Tage, Personen) + manueller Builder (7 Tage × 3 Gänge)
- Echtzeit-Nährwerte, Speichern, **Teilen über Token-Links**, Server-Draft-Auto-Save

### 4. Einkaufsliste
- Aus Rezept-Zutaten generiert, nach Kategorie gruppiert, skalierbar, teilbar

### 5. Creator-Marktplatz (`MarketplaceController` / `MarketplaceService`)
- Verkauf von Essensplänen/Einzelrezepten und **digitalen Produkten** (Datei-Download)
- Zahlung über **World MiniKit** (WLD/USDC); serverseitige Verifikation (`reference`-Bindung, Empfänger, Replay-Schutz)
- Guthaben (`WildCoinBalance`) + **Payout**-Anträge; Käufer erhält bei Plan/Rezept eine **eigene Kopie**
- ⚠️ Bekannt: Betrags-/Token-Prüfung der Zahlung noch offen; Käufer-Kopie speichert Rezept-IDs (kein Snapshot). Siehe Sicherheits-/Haltbarkeits-Notizen.

### 6. Premium-Creator (kostenpflichtige Funktionen)
- `WorldAppUser.PremiumUntil` (Abo-Datum) → `IsPremiumActive`. Admin setzt es im **Creator-Manager**.
- Erstes Premium-Feature: **TTS-Vorlesen** der Rezeptschritte (nur für Rezepte von Premium-Creators generiert/angezeigt).

### 7. Kanal-Übergabe (`AdminController` + `ChannelTransferService`)
- Vorgebaute Platzhalter-Kanäle per **Claim-Link** auf die echte World-ID eines Creators migrieren (Inhalte + Einnahmen).

### 8. Benutzerprofil (`FeedController.MyProfile`)
- Statistiken, Tabs (Likes/Following/Pläne/Videos), Creator-Dashboard (eingebettet), Theme-Wechsel
- **Themes:** Color, Black, White, Rose, Lavender

---

## Datenmodell (Kern-Beziehungen)

```
WorldAppUser (1) ──→ (N) WorldUserPosting     [User erstellt Posts]
WorldAppUser (1) ──→ (N) WorldUserLike        [User liked Rezepte]
WorldAppUser (N) ←──→ (N) WorldAppUser         [via WorldUserAbo: Follow-System]
WorldAppUser (1) ──→ (N) WorldUserMealPlan     [User hat Essenspläne]
WorldUserPosting ──→ RecipeBaseData            [Post verlinkt auf Rezept]
RecipeBaseData (1) ──→ (N) RecipeSteps         [Schritte je Sprache: Culture + Text]
RecipeBaseData (N) ←──→ (N) Keyword             [via RecipeBaseKeyword]
RecipeBaseData (1) ──→ (N) IngredientMeasureQuantity [Zutaten]
```
Weitere Tabellen u.a.: Marketplace-Listings/-Purchases, Payout-Requests, WildCoin-Transaktionen, Ads (+Impression/Click), Notifications, Comments, ChannelClaims, IngredientSwapHints, PendingVideoUploads, Captions/PendingVideos.

---

## Speicher & externe Dienste (Bunny.net – NICHT mehr Azure Blob/Queue)

| Dienst | Zweck | Config-Key(s) |
|--------|-------|---------------|
| **Bunny Stream** | Video-Upload + automatisches Transcoding/Thumbnails | `Bunny_Net_Api_Stream`, `Bunny_Net_Stream_ID`, `Bunny_Net_Stream_Host_Name` |
| **Bunny Storage** | Bilder (WEBP), Untertitel (`captions/…`), TTS (`tts/…`) | `Bunny_Net_Storage_Adres`, `Bunny_Net_Passwort_Lager`, `Bunny_net_host_name` |
| **OpenAI** | Übersetzung, Zutaten-Tausch, Whisper (Untertitel), **TTS** | `SecretKeyOpenAi` / `OpenAI:ApiKey` (Fallback `OPENAI_API_KEY`); optional `OpenAI:Tts*` |
| **Worldcoin / World MiniKit** | Auth-Proof + Zahlungen | `WorldId:AppId`, `WorldApiKey`, `WorldMiniApp:SuperUserHash`, `Marketplace:*` |
| **SQL Server** | Datenbank | Connection String in Config/Azure |

> Die Migration Azure→Bunny ist in `BUNNY_MIGRATION.md` beschrieben. Der alte Azure-Queue/FFmpeg-Pfad (`DelikatessenDrehbuchViedeoProzessor`, noch in der Solution) wird vom Live-Upload NICHT mehr genutzt.

---

## Datenbank (WICHTIG: keine EF-Migrationen)

Schema-Änderungen laufen über **manuelle SQL-Skripte** in `Areas/WorldMiniApp/Sql/` (idempotent, einmalig ausführen), NICHT über EF-Migrationen. Vor Deploy jeweils ausführen. Aktuelle Skripte u.a.:
`premium_until.sql`, `channel_claims.sql`, `ingredient_swap_hints.sql`, `pending_video_uploads.sql`, `marketplace_*.sql`, `worlduser_notifications.sql`, `worldminiapp_shared_feed*.sql`, `CreateAdTables.sql`, `dataprotection_keys.sql`.

---

## Lokalisierung
- Geteilte `.resx` unter `Resources/SharedResources*.resx` (11 Sprachen: invariant=de + en/es/pt/id/nl/sv/da/nb/ms).
- Views: `@inject IStringLocalizer<SharedResources> SharedLocalizer`. User-facing Views sind lokalisiert; Admin/AdminAds/Debug bewusst nicht (nur Operator).
- ⚠️ `RazorRuntimeCompilation` ist NICHT aktiv → `.cshtml`- und `.resx`-Änderungen brauchen **Rebuild + App-Neustart**.

---

## Geteilte Services aus der Haupt-App
`IRecipesService`, `IMeasureService`, `IQuantityService`, `IIngredientService` (Zutaten-/Maß-/Mengen-Stammdaten).

---

## Projektstruktur (Gesamt)

```
DelikatessenDrehbuch.sln
├── DelikatessenDrehbuch/                    # ASP.NET Core 8.0 MVC Web-App
│   ├── Areas/WorldMiniApp/                  # ← Diese Mini-App
│   ├── Areas/Identity/                      # ASP.NET Identity (Haupt-App-Login)
│   ├── Controllers/ , Models/ , Services/   # Haupt-App
│   ├── Resources/                           # SharedResources*.resx
│   ├── Data/ApplicationDbContext.cs         # EF Core DbContext
│   └── Program.cs                           # Startup & DI
├── DelikatessenDrehbuchViedeoProzessor/     # Legacy Azure Function (Video/FFmpeg) – vom Live-Pfad ersetzt durch Bunny Stream
└── DelikatessenDrehbuch.Tests/              # Tests
```

**Tech-Stack:** C# / .NET 8.0 / EF Core / SQL Server / **Bunny.net (Stream + Storage)** / OpenAI (GPT-4o-mini, gpt-4o-mini-tts, Whisper) / Worldcoin World ID + MiniKit / ImageSharp / Bootstrap 5 / jQuery
