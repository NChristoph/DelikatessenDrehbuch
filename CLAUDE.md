# DelikatessenDrehbuch - WorldMiniApp

## Überblick

Die **WorldMiniApp** ist eine eigenständige Mini-Anwendung innerhalb von DelikatessenDrehbuch. Sie bietet eine mobile-first Social-Recipe-Plattform (ähnlich TikTok für Rezepte) mit Worldcoin-Authentifizierung, Video-/Bild-Feed, Essensplanung und Einkaufslisten.

**Pfad:** `DelikatessenDrehbuch/Areas/WorldMiniApp/`

---

## Architektur

```
Areas/WorldMiniApp/
├── Controllers/
│   ├── AuthController.cs        # Worldcoin-Login & Session
│   ├── FeedController.cs        # Video-Feed, Likes, Follows, Suche, Profil
│   └── HomeController.cs        # Hauptmenü, Essensplaner, Upload, Rezeptansicht
├── Models/
│   ├── WorldAppUser.cs          # Benutzer-Entity (Worldcoin NullifierHash)
│   ├── WorldUserPosting.cs      # Rezept-Posts (Video/Bild + Metadaten)
│   ├── WorldUserMealPlan.cs     # Gespeicherte Essenspläne
│   ├── WorldSharedMealPlan.cs   # Öffentlich geteilte Essenspläne (Token-basiert)
│   ├── WorldUserLike.cs         # Like-Beziehung (User ↔ Rezept)
│   ├── WorldUserAbo.cs          # Follow-Beziehung (User ↔ Creator)
│   ├── Keyword.cs               # Mehrsprachige Rezept-Tags (DE/EN/ES/PT)
│   ├── RecipeBaseKeyword.cs     # Many-to-Many: Rezept ↔ Keyword
│   └── ViewModels/              # VerifyRequestDto, MiniAppSetupModel, UserProfileViewModel, etc.
├── Services/
│   ├── AuthService.cs           # Worldcoin-API Proof-Verifizierung
│   ├── UserManager.cs           # Benutzer erstellen/aktualisieren
│   ├── BlobUploadService.cs     # Azure Blob Upload (Bilder→WEBP, Videos→Queue)
│   ├── SaveNewRecipeService.cs  # Rezepte in DB speichern (mit Transaktion)
│   └── WorldAppMealPlanService.cs # Essensplan-CRUD
└── Views/
    ├── Home/                    # Index, Generator, Generated, Upload, EditRecipe, ShowRecipe, etc.
    └── Feed/                    # Feed (Fullscreen-Scroll), MyProfile
```

---

## Authentifizierung (Worldcoin World ID)

Die App nutzt **Worldcoin World ID** zur Benutzeridentifikation:

1. User klickt "Connect World ID" auf der Startseite
2. World App zeigt Verifizierungs-UI an
3. Frontend sendet signierten Proof an `AuthController.VerifyAction`
4. `AuthService` verifiziert den Proof über `https://developer.worldcoin.org/api/v2/verify/{APP_ID}`
5. Bei Erfolg: `WorldAppUser` wird erstellt/aktualisiert, Session-Cookie `WorldMiniAppUserHash` gesetzt

**Verifizierungslevel:**
- `"orb"` - Biometrisch verifiziert → kann Videos hochladen, hat Follower-Statistiken
- `"device"` - Geräte-verifiziert → eingeschränkte Funktionen (nur 1 Essensplan)

**App ID:** `app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa`

---

## Hauptfunktionen

### 1. Video/Bild-Feed (`FeedController`)

- **Fullscreen-Scroll** (TikTok-Stil) mit Scroll-Snap
- Video-Autoplay mit Mute-Toggle
- **Doppeltipp zum Liken** mit Herz-Animation
- Aktionsleiste: Like-Counter, "Zum Plan hinzufügen", Rezept anzeigen
- Creator-Badge mit Follow-Button
- **Suche** nach Titel, Kategorie, Keywords, max. Zubereitungszeit
- **Filter:** Feed (alle), Likes, Meine Videos
- Mehrsprachige Kategorie-Aliase (DE/EN/ES/PT)
- Paginierung: max. 20 Items pro Laden

**Bottom-Navigation:** Home | Plan | Upload (+) | Suche | Profil

### 2. Rezept-Upload (`HomeController`)

- Upload von Videos (MP4/MOV/WEBM, max 200MB) oder Bildern (JPG/PNG/WEBP, max 15MB)
- **Rate Limit:** 5 Uploads pro 10 Minuten
- Bilder werden automatisch zu WEBP konvertiert (1080x1920 Source + 400x711 Thumbnail)
- Videos werden in Azure Queue eingereiht → FFmpeg-Verarbeitung durch `DelikatessenDrehbuchViedeoProzessor`
- Rezept-Metadaten: Titel, Kategorie, Zutaten, Zubereitungsschritte, Keywords

### 3. Essensplaner (`HomeController`)

- **Generator:** Automatische Wochenplan-Erstellung basierend auf:
  - Diättyp: Alles / Vegetarisch / Vegan
  - Filter (z.B. Kochzeit)
  - Tage-Anzahl (Standard: 7)
  - Personen-Anzahl (Standard: 2)
- **Manueller Builder:** 7 Tage × 3 Mahlzeiten (Vorspeise/Hauptspeise/Dessert) Raster
- **Echtzeit-Nährwertberechnung:** Kalorien, Protein, Fett, Kohlenhydrate, Zucker, Ballaststoffe, Salz
- Speichern mit Titel (automatische Nummerierung bei Duplikaten)
- **Teilen** über Token-basierte öffentliche Links
- Draft-Auto-Save auf dem Server

### 4. Einkaufsliste

- Automatisch aus Rezept-Zutaten generiert
- Gruppiert nach Kategorie (Gemüse, Proteine, Extras, etc.)
- Skalierbar nach Personenanzahl
- Öffentlich teilbar über Link

### 5. Benutzerprofil (`FeedController.MyProfile`)

- Avatar, Benutzername, Statistiken (Following, Likes, Followers)
- **Tabs:** Likes | Following | Essenspläne | Meine Videos (nur bei orb-Verifizierung)
- Einstellungen: Theme-Wechsel, Name ändern
- **Themes:** Color, Black, White, Rose, Lavender

---

## Datenmodell (Beziehungen)

```
WorldAppUser (1) ──→ (N) WorldUserPosting     [User erstellt Posts]
WorldAppUser (1) ──→ (N) WorldUserLike         [User liked Rezepte]
WorldAppUser (N) ←──→ (N) WorldAppUser          [via WorldUserAbo: Follow-System]
WorldAppUser (1) ──→ (N) WorldUserMealPlan     [User hat Essenspläne]
WorldUserPosting ──→ RecipeBaseData             [Post verlinkt auf Rezept]
RecipeBaseData (N) ←──→ (N) Keyword              [via RecipeBaseKeyword]
RecipeBaseData (1) ──→ (N) RecipeJoinIngredientMeasureQuantity [Zutaten]
RecipeBaseData (1) ──→ (N) RecipeBaseDataImage  [Bilder]
```

---

## Azure-Integration

| Service | Zweck | Config-Key |
|---------|-------|------------|
| **Blob Storage** | Bild-/Video-Speicher | `Blob_Conection_String`, `World-App-Blop-Name` |
| **Queue Storage** | Video-Processing-Jobs | `Queue_Connection_String`, `VideoProcessingQueueName` |
| **CDN** | Content Delivery | `DelekatesenDrehbuchCdn-beecexhdaghhacab.z01.azurefd.net` |
| **SQL Server** | Datenbank | Connection String in `appsettings.json` |

---

## Geteilte Services aus der Haupt-App

Die WorldMiniApp nutzt folgende Services der Haupt-Anwendung:

- `IRecipesService` - Rezepte laden (nach ID, zufällig, Liste)
- `IMeasureService` - Maßeinheiten (g, ml, Stk., etc.)
- `IQuantityService` - Mengenangaben
- `IIngredientService` - Zutaten-Stammdaten

---

## Projektstruktur (Gesamt)

```
DelikatessenDrehbuch.sln
├── DelikatessenDrehbuch/                    # ASP.NET Core 8.0 MVC Web-App
│   ├── Areas/WorldMiniApp/                  # ← Diese Mini-App
│   ├── Areas/Identity/                      # ASP.NET Identity (Login/Register)
│   ├── Controllers/                         # Haupt-App Controller
│   ├── Models/                              # 40+ Domain-Entities
│   ├── Services/                            # 30+ Business-Logic Services
│   ├── Data/ApplicationDbContext.cs         # EF Core DbContext (49 Tabellen)
│   └── Program.cs                           # Startup & DI-Konfiguration
├── DelikatessenDrehbuchViedeoProzessor/     # Azure Functions (FFmpeg Video-Processing)
└── FoodForFun/                              # Template/Referenz-Projekt
```

**Tech-Stack:** C# / .NET 8.0 / EF Core / SQL Server / Azure Blob & Queue / Redis / Stripe / Worldcoin / ImageSharp / FFmpeg / Bootstrap 5 / jQuery
