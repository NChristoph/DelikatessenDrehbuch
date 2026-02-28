# WorldMiniApp – Technische Dokumentation

**Pfad:** `DelikatessenDrehbuch/Areas/WorldMiniApp/`
**Tech-Stack:** C# / .NET 8.0 / ASP.NET Core MVC / EF Core / Azure Blob & Queue / World ID / WalletAuth (SIWE) / World Chain / ImageSharp
**Stand:** 2026-02-28

---

## Inhaltsverzeichnis

1. [Überblick](#1-überblick)
2. [Verzeichnisstruktur](#2-verzeichnisstruktur)
3. [Authentifizierung](#3-authentifizierung)
   - 3.1 [World ID (Worldcoin MiniKit)](#31-world-id-worldcoin-minikit)
   - 3.2 [WalletAuth (SIWE)](#32-walletauth-siwe)
   - 3.3 [Session-Management](#33-session-management)
4. [Controller](#4-controller)
   - 4.1 [AuthController](#41-authcontroller)
   - 4.2 [HomeController](#42-homecontroller)
   - 4.3 [FeedController](#43-feedcontroller)
   - 4.4 [MarketplaceController](#44-marketplacecontroller)
5. [Datenmodell](#5-datenmodell)
   - 5.1 [Entitäten](#51-entitäten)
   - 5.2 [Beziehungen](#52-beziehungen)
   - 5.3 [DTOs & ViewModels](#53-dtos--viewmodels)
6. [Services](#6-services)
   - 6.1 [AuthService](#61-authservice)
   - 6.2 [UserManager](#62-usermanager)
   - 6.3 [BlobUploadService](#63-blobuploadservice)
   - 6.4 [SaveNewRecipeService](#64-savenewrecipeservice)
   - 6.5 [WorldAppMealPlanService](#65-worldappmealplanservice)
   - 6.6 [WildCoinService](#66-wildcoinservice)
7. [Hauptfunktionen](#7-hauptfunktionen)
   - 7.1 [Feed](#71-feed)
   - 7.2 [Upload](#72-upload)
   - 7.3 [Essensplaner](#73-essensplaner)
   - 7.4 [Einkaufsliste & Teilen](#74-einkaufsliste--teilen)
   - 7.5 [Benutzerprofil](#75-benutzerprofil)
   - 7.6 [Marketplace](#76-marketplace)
8. [Azure-Integration](#8-azure-integration)
9. [World Chain & WildCoin](#9-world-chain--wildcoin)
10. [Konfiguration](#10-konfiguration)
11. [Sicherheit](#11-sicherheit)
12. [API-Referenz](#12-api-referenz)

---

## 1. Überblick

Die **WorldMiniApp** ist eine eigenständige Mini-Anwendung innerhalb von DelikatessenDrehbuch. Sie realisiert eine mobile-first Social-Recipe-Plattform – konzeptionell ähnlich TikTok für Rezepte – mit folgenden Kernfunktionen:

| Bereich | Beschreibung |
|---|---|
| **Authentifizierung** | Worldcoin World ID (biometrisch / Gerät) + WalletAuth via SIWE |
| **Feed** | Fullscreen-Scroll-Feed mit Video/Bild-Rezepten, Likes, Follow |
| **Upload** | Video/Bild-Upload mit automatischer WEBP-Konvertierung & FFmpeg-Queue |
| **Essensplaner** | KI-generierte & manuelle Wochenpläne, Nährwertberechnung |
| **Einkaufsliste** | Automatisch aus Rezeptzutaten, skalierbar, teilbar |
| **Marketplace** | Kauf/Verkauf von Essensplänen via World Chain (WLD/USDT) |
| **Profil** | Avatar, Statistiken, Tabs für Likes / Following / Pläne / Videos |

Die App ist unter dem ASP.NET-Area-System eingebunden und teilt den `ApplicationDbContext` sowie mehrere Services mit der Haupt-App.

---

## 2. Verzeichnisstruktur

```
Areas/WorldMiniApp/
├── Controllers/
│   ├── AuthController.cs          # Worldcoin-Login, SIWE-WalletAuth, Session
│   ├── FeedController.cs          # Feed, Likes, Follow, Profil
│   ├── HomeController.cs          # Hauptmenü, Rezeptgenerator, Upload, Essensplaner
│   └── MarketplaceController.cs   # Essensplan-Marktplatz (WLD/USDT Payments)
├── Models/
│   ├── WorldAppUser.cs            # Benutzer-Entity
│   ├── WorldUserPosting.cs        # Rezept-Post (Video/Bild + Metadaten)
│   ├── WorldUserMealPlan.cs       # Gespeicherter Essensplan
│   ├── WorldSharedMealPlan.cs     # Öffentlich geteilter Essensplan (Token)
│   ├── WorldUserLike.cs           # Like-Beziehung
│   ├── WorldUserAbo.cs            # Follow-Beziehung
│   ├── MealPlanListing.cs         # Marketplace-Angebot
│   ├── WildCoinTransaction.cs     # Coin-Transaktionen & Kaufbelege
│   ├── Keyword.cs                 # Mehrsprachige Rezept-Tags
│   ├── RecipeBaseKeyword.cs       # Many-to-Many: Rezept ↔ Keyword
│   ├── MiniAppSetupModel.cs       # Einstellungen für Essensplan-Generierung
│   ├── PlanCardViewModel.cs       # Marketplace-Kartenansicht
│   ├── UserProfileViewModel.cs    # Profil-View-Daten
│   ├── VerifyRequestDto.cs        # DTOs für Auth (World ID + SIWE)
│   ├── EditPostingRecipeViewModel.cs
│   ├── JoinIngredientPreperationStep.cs
│   ├── UserDashboardViewModel.cs
│   └── WorldMealPlanViewModel.cs
├── Services/
│   ├── AuthService.cs             # Worldcoin-API Proof-Verifizierung
│   ├── UserManager.cs             # User erstellen / aktualisieren
│   ├── BlobUploadService.cs       # Azure Blob Upload (Bild→WEBP, Video→Queue)
│   ├── SaveNewRecipeService.cs    # Rezepte in DB speichern (Transaktion)
│   ├── WorldAppMealPlanService.cs # Essensplan-CRUD
│   ├── WildCoinService.cs         # WildCoin-Balance, Marketplace, On-Chain TX
│   ├── IWildCoinService.cs        # Interface
│   └── Interfaces/
│       ├── IAuthService.cs
│       ├── IBlobUploadService.cs
│       ├── ISaveNewRecipeService.cs
│       ├── IUserManager.cs
│       ├── IWorldAppMealPlanService.cs
│       └── UploadContentResult.cs
└── Views/
    ├── Home/
    │   ├── Index.cshtml           # Startseite / Login
    │   ├── Generator.cshtml       # Essensplan-Generator
    │   ├── Generated.cshtml       # Generierter Essensplan
    │   ├── Upload.cshtml          # Video/Bild-Upload
    │   ├── EditRecipe.cshtml      # Rezept bearbeiten
    │   └── ShowRecipe.cshtml      # Rezept-Detailansicht
    └── Feed/
        ├── Index.cshtml           # Fullscreen-Feed
        └── MyProfile.cshtml       # Benutzerprofil
```

---

## 3. Authentifizierung

Die WorldMiniApp unterstützt zwei Login-Wege:

### 3.1 World ID (Worldcoin MiniKit)

**Flow:**

```
1. User öffnet die App
2. Frontend lädt World App MiniKit JS-SDK
3. User klickt "Mit World ID anmelden"
4. MiniKit zeigt Verifizierungs-Dialog in der World App
5. User bestätigt (biometrisch oder Gerät)
6. Frontend erhält signierten Proof (merkle_root, nullifier_hash, proof, verification_level)
7. POST /WorldMiniApp/Auth/VerifyAction  ← sendet VerifyRequestDto
8. AuthService ruft Worldcoin Developer API auf:
   POST https://developer.worldcoin.org/api/v2/verify/{APP_ID}
9. Bei Success: UserManager.CreateNewUser() → Session-Cookie setzen
10. Redirect zur App
```

**Technische Details:**

| Parameter | Wert |
|---|---|
| App ID | `app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa` |
| Verify-Endpunkt | `https://developer.worldcoin.org/api/v2/verify/{APP_ID}` |
| Session-Key | `WorldMiniAppUserHash` |
| Verifizierungslevels | `orb` (biometrisch) · `device` (Gerät) |

**Benutzerrechte nach Level:**

| Level | Upload | Mehrere Essenspläne | Follower-Statistiken |
|---|---|---|---|
| `orb` | ✅ | ✅ | ✅ |
| `device` | ❌ | ❌ (max. 1 Plan) | ❌ |
| `wallet` | ❌ | ✅ | ❌ |

### 3.2 WalletAuth (SIWE)

Sign-In With Ethereum (EIP-4361) als zweiter Login-Weg:

```
1. Frontend ruft GET /WorldMiniApp/Auth/Nonce ab → erhält einmaligen Nonce
2. Nonce wird in der Server-Session gespeichert (Replay-Schutz)
3. MiniKit WalletAuth signiert eine SIWE-Nachricht (enthält Nonce + Ethereum-Adresse)
4. POST /WorldMiniApp/Auth/CompleteSiwe mit WalletAuthRequestDto
5. Server prüft:
   a. Payload-Status == "success"
   b. Nonce in Nachricht == gespeicherter Session-Nonce
   c. ECDSA-Signatur-Recovery (Nethereum) → Recovered Address == Claimed Address
6. Bei Erfolg: UserManager.CreateOrUpdateWalletUser() → IsVerified = "wallet"
7. Session-Cookie mit normalisierter (lowercase) Wallet-Adresse setzen
```

**Sicherheits-Garantien:**
- Nonce wird nach Verwendung aus der Session gelöscht (Replay-Schutz)
- Kein Fallback bei fehlgeschlagener Signatur-Recovery
- Recovered Address muss exakt mit Payload-Address und SIWE-Message-Address übereinstimmen

### 3.3 Session-Management

```csharp
// Session-Keys
"WorldMiniAppUserHash"  // Primärer Auth-Key (NullifierHash oder Wallet-Adresse)
"WorldMiniAppSiweNonce" // Temporär: SIWE Nonce (wird nach Login gelöscht)
"WorldWallet_WLD"       // Wallet-Adresse für WLD-Zahlungen
"WorldWallet_USDT"      // Wallet-Adresse für USDT-Zahlungen

// Session wird NIEMALS aus Request-Parametern übernommen (Session-Hijacking-Schutz)
// UserHash kommt ausschließlich aus HttpContext.Session
private string ResolveUserHash(string _ignored)
{
    return HttpContext.Session.GetString(SessionUserHashKey) ?? string.Empty;
}
```

**RefreshStatus-Endpunkt:** Beim App-Start prüft das Frontend per `POST /Auth/RefreshStatus`, ob der gespeicherte UserHash noch gültig ist, und aktualisiert `Lastlogin`.

---

## 4. Controller

### 4.1 AuthController

**Route:** `/WorldMiniApp/Auth/...`

| Methode | Route | Beschreibung |
|---|---|---|
| GET | `/Nonce` | Generiert SIWE-Nonce, speichert in Session |
| POST | `/CompleteSiwe` | SIWE-Wallet-Login (ECDSA-Verifizierung) |
| POST | `/VerifyAction` | World ID Proof-Verifizierung via Worldcoin API |
| POST | `/RefreshStatus` | Prüft Session-Gültigkeit, aktualisiert Lastlogin |
| GET | `/Index` | Login-Ansicht |

### 4.2 HomeController

**Route:** `/WorldMiniApp/Home/...`

| Methode | Route | Beschreibung |
|---|---|---|
| GET | `/Index` | Startseite / Haupt-Navigation |
| GET | `/Generator` | Essensplan-Generator-Formular |
| POST | `/Generator` | Plan generieren (Diättyp, Tage, Personen) |
| GET | `/Generated` | Ergebnis des generierten Plans |
| GET | `/Upload` | Upload-Formular |
| POST | `/Upload` | Datei hochladen (Bild/Video) + Rezept speichern |
| GET | `/EditRecipe/{id}` | Rezept bearbeiten |
| POST | `/EditRecipe/{id}` | Rezept-Änderungen speichern |
| GET | `/ShowRecipe/{id}` | Rezept-Detailansicht |
| POST | `/SaveMealPlan` | Manuellen Essensplan speichern |
| POST | `/DeleteMealPlan` | Essensplan löschen |
| GET | `/ShoppingList/{id}` | Einkaufsliste für Plan |
| POST | `/ShareMealPlan` | Essensplan als öffentlichen Link teilen |
| GET | `/SharedPlan/{token}` | Geteilten Plan ansehen |

**Upload-Rate-Limit:** 5 Uploads pro 10 Minuten pro User (In-Memory-Cache).

### 4.3 FeedController

**Route:** `/WorldMiniApp/Feed/...`

| Methode | Route | Parameter | Beschreibung |
|---|---|---|---|
| GET | `/Index` | `filter`, `userHash`, `scrollToId`, `searchTerm`, `category`, `maxPrepTime` | Feed-Ansicht |
| POST | `/ToggleLike` | `userHash`, `recipeId` | Like hinzufügen / entfernen |
| POST | `/ToggleFollow` | `userHash`, `creatorId` | Follow hinzufügen / entfernen |
| GET | `/MyProfile` | `userHash` | Profil-Seite |
| POST | `/UpdateProfile` | `userHash`, `userName` | Benutzernamen ändern |

**Filter-Modi:**

| Filter | Beschreibung |
|---|---|
| `feed` (Standard) | Alle Posts, neueste zuerst, max. 20 |
| `likes` | Nur Rezepte, die der User geliked hat |
| `myvideos` | Nur eigene Posts (erfordert Login) |

**Kategorie-Normalisierung:** Mehrsprachige Aliase werden auf kanonische Kategorien gemappt:

```csharp
"appetizer" / "vorspeise" / "entrada" → "appetizer"
"main" / "hauptspeise" / "platoprincipal" → "main"
"dessert" / "postre" / "nachspeise" → "dessert"
```

### 4.4 MarketplaceController

**Route:** `/WorldMiniApp/Marketplace/...`

| Methode | Route | Beschreibung |
|---|---|---|
| GET | `/Index` | Alle aktiven Angebote |
| GET | `/CreatorShop` | Premium-Kartenansicht der Angebote |
| GET | `/MyListings` | Eigene Angebote verwalten |
| GET | `/Sell` | Angebot erstellen (Formular) |
| POST | `/CreateListing` | Neues Angebot erstellen |
| POST | `/DeactivateListing` | Angebot deaktivieren |
| POST | `/FinalizeWorldChainPurchase` | Kauf nach On-Chain TX finalisieren |
| GET | `/GetListingPreview` | Plan-Vorschau (Rezepte + Nährwerte) als JSON |
| GET | `/Transactions` | Transaktionshistorie als JSON |
| POST | `/SaveWalletAddress` | Wallet-Adresse in Session speichern |
| GET | `/GetWalletAddress` | Gespeicherte Wallet aus Session abrufen |

---

## 5. Datenmodell

### 5.1 Entitäten

#### `WorldAppUser`
Repräsentiert einen eingeloggten Benutzer.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `UserHash` | string? | NullifierHash (World ID) oder normalisierte Wallet-Adresse |
| `UserName` | string? | Anzeigename (frei wählbar) |
| `IsVerified` | string? | Verifizierungslevel: `"orb"` · `"device"` · `"wallet"` |
| `Lastlogin` | DateTime? | Letzter Login |
| `RememberLogin` | bool | "Eingeloggt bleiben"-Flag |
| `WildCoinBalance` | decimal | Internes WildCoin-Guthaben |

#### `WorldUserPosting`
Ein hochgeladener Rezept-Post (Video oder Bild).

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `CreatorId` | string | UserHash des Erstellers |
| `CreatorName` | string | Anzeigename des Erstellers |
| `Source` | string | URL zur Bild-/Video-Datei (Azure Blob / CDN) |
| `ThumbnailUrl` | string | URL zum Thumbnail |
| `Title` | string | Titel des Posts |
| `CreationTime` | DateTime | Erstellungszeitpunkt |
| `Recipe` | RecipeBaseData | Navigation: verknüpftes Rezept |

*[NotMapped]-Felder dienen als Upload-Formulardaten und werden nicht in der DB gespeichert.*

#### `WorldUserMealPlan`
Ein gespeicherter Essensplan eines Users.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `UserHash` | string | Eigentümer-Hash |
| `Title` | string? | Plan-Bezeichnung |
| `Settings` | string? | JSON-serialisiertes `MiniAppSetupModel` |
| `MealPlan` | string? | JSON: `Dictionary<int, List<int>>` (Tag-Index → Rezept-IDs) |
| `CreationTime` | DateTime | Erstellungszeitpunkt |

**MealPlan-JSON-Format:**
```json
{
  "0": [101, 205, 312],
  "1": [88, 190, 445],
  ...
}
```
*Schlüssel = Tag-Index (0-basiert), Werte = Liste der Rezept-IDs für diesen Tag.*

#### `WorldSharedMealPlan`
Ein öffentlich geteilter Essensplan mit eindeutigem Token.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `ShareToken` | string | Eindeutiger GUID-Token für den öffentlichen Link |
| `UserHash` | string? | Ersteller-Hash |
| `Title` | string | Plan-Bezeichnung |
| `PersonCount` | int | Personenanzahl |
| `MealPlanJson` | string | Vollständiger Essensplan als JSON |
| `ShoppingListJson` | string | Einkaufsliste als JSON (`List<ShoppingListItem>`) |
| `CreatedAt` | DateTime | UTC-Erstellungszeit |

#### `WorldUserLike`
Like-Beziehung zwischen User und Rezept.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `Recipe` | RecipeBaseData | Navigation: geliktes Rezept |
| `WorldAppUser` | WorldAppUser | Navigation: likender User |

#### `WorldUserAbo`
Follow-Beziehung (User folgt einem Creator).

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `WorldUser` | WorldAppUser | Navigation: der folgende User |
| `Creator` | WorldAppUser | Navigation: der gefolgde Creator |

#### `MealPlanListing`
Ein Marketplace-Angebot für einen Essensplan.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `SellerHash` | string | UserHash des Verkäufers |
| `SellerName` | string | Anzeigename des Verkäufers |
| `SellerWalletAddress` | string? | WLD-Empfänger-Wallet |
| `SellerUsdtWalletAddress` | string? | USDT-Empfänger-Wallet |
| `MealPlanId` | int | FK → WorldUserMealPlan |
| `MealPlan` | WorldUserMealPlan | Navigation |
| `Title` | string | Angebots-Titel |
| `Description` | string? | Angebots-Beschreibung |
| `Price` | decimal | Preis in WLD |
| `DayCount` | int | Anzahl Tage im Plan |
| `RecipeCount` | int | Anzahl Rezepte im Plan |
| `IsActive` | bool | Angebot aktiv? |
| `SoldCount` | int | Verkaufsanzahl |
| `CreatedAt` | DateTime | Erstellungszeitpunkt |

#### `WildCoinTransaction`
Buchungszeile für interne WildCoin-Transaktionen.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `UserHash` | string | Betroffener User |
| `Amount` | decimal | Betrag (negativ = Ausgabe) |
| `BalanceAfter` | decimal | Kontostand nach Buchung |
| `Type` | string | `"sale"` · `"purchase"` · `"reward"` |
| `ReferenceInfo` | string? | z.B. `"MealPlanListing:42"` |
| `CreatedAt` | DateTime | Buchungszeitpunkt |

#### `MealPlanPurchase`
Kaufbeleg nach finalisiertem Marketplace-Kauf.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `BuyerHash` | string | UserHash des Käufers |
| `BuyerWalletAddress` | string? | Wallet des Käufers |
| `SellerHash` | string | UserHash des Verkäufers |
| `SellerWalletAddress` | string? | Wallet des Verkäufers |
| `ListingId` | int | FK → MealPlanListing |
| `Listing` | MealPlanListing | Navigation |
| `CreatedMealPlanId` | int | ID des kopierten Plans im Käufer-Account |
| `PricePaid` | decimal | Bezahlter Preis |
| `CreatorAmount` | decimal | Anteil des Creators (80%) |
| `PlatformFee` | decimal | Plattform-Anteil (20%) |
| `ReferenceTxHash` | string? | On-Chain TX-Hash (UNIQUE-Index) |
| `PaymentToken` | string | `"WLD"` · `"USDT"` · `"WildCoin"` |
| `PurchasedAt` | DateTime | Kaufzeitpunkt |

#### `Keyword`
Mehrsprachiges Rezept-Tag.

| Feld | Typ | Beschreibung |
|---|---|---|
| `Id` | int | Primärschlüssel |
| `Word_DE` | string | Deutsches Wort |
| `Word_EN` | string | Englisches Wort |
| `Word_ESP` | string | Spanisches Wort |
| `Word_PRT` | string | Portugiesisches Wort |
| `RecipeLinks` | ICollection\<RecipeBaseKeyword\> | Navigation: Verknüpfungen |

#### `RecipeBaseKeyword`
Verbindungstabelle für Many-to-Many Rezept ↔ Keyword.

```csharp
// Primärschlüssel: zusammengesetzt
HasKey(link => new { link.RecipeBaseDataId, link.KeywordId });
```

### 5.2 Beziehungen

```
WorldAppUser (1) ──→ (N) WorldUserPosting       [User erstellt Posts]
WorldAppUser (1) ──→ (N) WorldUserLike           [User liked Rezepte]
WorldAppUser (N) ←──→ (N) WorldAppUser            [via WorldUserAbo: Follow]
WorldAppUser (1) ──→ (N) WorldUserMealPlan       [User hat Essenspläne]
WorldUserMealPlan (1) ──→ (N) MealPlanListing    [Plan wird als Angebot gelistet]
MealPlanListing (1) ──→ (N) MealPlanPurchase     [Angebot hat Käufe]
WorldUserPosting ──→ RecipeBaseData               [Post verlinkt Rezept]
RecipeBaseData (N) ←──→ (N) Keyword               [via RecipeBaseKeyword]
RecipeBaseData (1) ──→ (N) RecipeJoinIngredientMeasureQuantity
RecipeBaseData (1) ──→ (N) RecipeBaseDataImage
```

### 5.3 DTOs & ViewModels

#### `VerifyRequestDto` / `VerifyPayloadDto`
Empfängt den World ID Proof vom Frontend.
```csharp
VerifyRequestDto {
    Payload: VerifyPayloadDto {
        Status, Proof, MerkleRoot (merkle_root),
        NullifierHash (nullifier_hash), VerificationLevel (verification_level)
    }
    Action: string
    Signal: string
    RememberLogin: bool
}
```

#### `WalletAuthRequestDto` / `WalletAuthPayloadDto`
Empfängt das SIWE-Payload vom Frontend.
```csharp
WalletAuthRequestDto {
    Payload: WalletAuthPayloadDto {
        Status, Message, Signature, Address, Version
    }
    Nonce: string
    RememberLogin: bool
}
```

#### `UserProfileViewModel`
Aggregiert alle Profil-Daten für die View.
```csharp
{
    User: WorldAppUser,
    LikedRecipes: List<WorldUserPosting>,
    Following: List<WorldAppUser>,
    MealPlans: List<WorldUserMealPlan>,
    Purchases: List<MealPlanPurchase>,
    MyVideos: List<WorldUserPosting>,   // nur bei "orb"
    FollowerCount: int                  // nur bei "orb"
}
```

#### `PlanCardViewModel`
Repräsentiert eine Marketplace-Karte in der UI.
- Enthält Preis-Label (`"Freischalten - X.XX WLD"`), Hero-Bilder, Nährwert-Flags, Verkaufsstatistiken.

---

## 6. Services

### 6.1 AuthService

**Interface:** `IAuthService`
**Zweck:** Worldcoin Developer API aufrufen und Proof verifizieren.

```csharp
Task<WorldcoinVerifyResponse> VerifyProofWithWorldcoin(VerifyRequestDto data)
```

**Verhalten:**
- Sendet `POST https://developer.worldcoin.org/api/v2/verify/{APP_ID}` mit `WorldcoinVerifyRequest`
- Leeres Signal wird durch den Standard-Hash `0x00c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a4` ersetzt
- Gibt `WorldcoinVerifyResponse { Success, Code, Detail }` zurück
- Wirft Exceptions bei Netzwerk- oder Parse-Fehlern (werden im Controller abgefangen)

### 6.2 UserManager

**Interface:** `IUserManager`
**Zweck:** Benutzer in der DB anlegen oder aktualisieren.

```csharp
Task CreateNewUser(VerifyRequestDto request)
// Legt User mit NullifierHash, VerificationLevel, Lastlogin an oder aktualisiert ihn.

Task CreateOrUpdateWalletUser(string walletAddress, bool rememberLogin)
// Legt User mit Wallet-Adresse (lowercase) und IsVerified="wallet" an oder aktualisiert ihn.
```

### 6.3 BlobUploadService

**Interface:** `IBlobUploadService`
**Zweck:** Bilder und Videos in Azure Blob Storage hochladen.

```csharp
Task<UploadContentResult> UploadContentToBlob(IFormFile file)
Task<UploadContentResult> UploadContentToBlobFromUrl(string sourceUrl)
```

**Bild-Pipeline:**
1. Validierung (Extension + Magic Bytes)
2. Konvertierung zu WEBP via ImageSharp
   - Source: 1080×1920px, Ziel ≤850 KB, Qualität 88→35 (adaptive)
   - Thumbnail: 400×711px, Ziel ≤250 KB
3. Upload beider Varianten als `image/webp` in Azure Blob

**Video-Pipeline:**
1. Validierung (Extension + Magic Bytes: MP4/MOV = `ftyp`, WEBM = EBML-Header)
2. Raw-Upload als `{name}_{uuid}.mp4`
3. Enqueue in Azure Queue (JSON-Base64) für FFmpeg-Processing:
   ```json
   { "container": "...", "blobName": "...", "uploadedAt": "..." }
   ```
4. Rückgabe der vorberechneten processed-URL (`{name}_{uuid}_processed.mp4`)

**Limits:**

| Typ | Max. Größe | Formate |
|---|---|---|
| Bild | 15 MB | JPG, PNG, WEBP |
| Video | 200 MB | MP4, MOV, WEBM |

**SSRF-Schutz** bei `UploadContentToBlobFromUrl`:
- Nur HTTPS erlaubt
- Nur Hosts `blobdelikatessendrehbuch.blob.core.windows.net` und `delekatesendrehbuchcdn-beecexhdaghhacab.z01.azurefd.net`

### 6.4 SaveNewRecipeService

**Interface:** `ISaveNewRecipeService`
**Zweck:** Neues Rezept mit allen Zutaten und Schritten in der DB persistieren (Datenbankttransaktion).

### 6.5 WorldAppMealPlanService

**Interface:** `IWorldAppMealPlanService`
**Zweck:** CRUD-Operationen für Essenspläne.

```csharp
Task CheckVerifie(MiniAppSetupModel settings, string userHash, string title)
// Prüft Verifikationslevel: "orb" darf mehrere Pläne anlegen, andere nur einen.

Task CreateWorldUserMealPlan(MiniAppSetupModel settings, string userHash, string title)
// Legt neuen Plan an. Duplikat-Titel werden automatisch nummeriert ("Plan", "Plan 1", "Plan 2", ...).

Task SaveNewMealPlan(string userHash, List<MealPlanerModel> mealPlanerModels, string title)
// Speichert Rezept-IDs in den Plan (Dictionary<Tag-Index, Rezept-IDs>).

Task EditeMealPlan(string userHash, string title, List<MealPlanerModel> mealPlaner)

Task<List<WorldUserMealPlan>> GetMealPlansByHash(string userHash)
WorldUserMealPlan GetMealPlanById(int id, string userHash)
void DeleteMealPlan(int id, string userHash)
```

### 6.6 WildCoinService

**Interface:** `IWildCoinService`
**Zweck:** Internes WildCoin-Guthaben, Marketplace-Operationen, World-Chain-Kauf-Finalisierung.

**Balance & Transfer:**
```csharp
Task<decimal> GetBalance(string userHash)
Task<bool> Transfer(string fromHash, string toHash, decimal amount, string referenceInfo)
// Atomare DB-Transaktion: Sender -= amount, Empfänger += amount, je eine Buchungszeile.
Task<bool> Reward(string userHash, decimal amount, string referenceInfo)
```

**Marketplace:**
```csharp
Task<MealPlanListing> CreateListing(...)
Task<List<MealPlanListing>> GetActiveListings(int skip, int take)
Task<List<MealPlanListing>> GetMyListings(string userHash)
Task<bool> DeactivateListing(string userHash, int listingId)
```

**Kauf-Finalisierung:**
```csharp
Task<MealPlanPurchase?> FinalizeWorldChainPurchase(
    string buyerHash, int listingId, string txHash,
    string walletAddress, bool allowSelfPurchase, string paymentToken)
```

Ablauf:
1. TX-Hash-Format-Prüfung (`^0x[a-fA-F0-9]{64}$`)
2. On-Chain-Verifizierung (World Chain Alchemy RPC, **asynchron/nicht-blockierend**, max. 3 Versuche)
3. DB-Transaktion:
   - Duplikat-Schutz via `ReferenceTxHash` (UNIQUE-Index)
   - Plan-Kopie für Käufer erstellen
   - `MealPlanPurchase` anlegen (80% Creator, 20% Plattform)
   - `SoldCount` erhöhen

---

## 7. Hauptfunktionen

### 7.1 Feed

Der Feed (`FeedController.Index`) rendert einen **Fullscreen-Scroll-Feed** im TikTok-Stil:

- **CSS Scroll-Snap** für seitenweises Scrollen
- **Video-Autoplay** mit Mute-Toggle
- **Doppeltipp-Like** mit Herz-Animation
- **Bottom-Navigation:** Home | Plan | Upload (+) | Suche | Profil

**Query-Pipeline:**
```
1. Base-Query: WorldUserPosting + Include(Recipe.RecipeKeywords.Keyword)
2. Filter anwenden (feed / likes / myvideos)
3. Suche: Titel LIKE, Kategorie LIKE + Kategorie-Alias, Keywords (DE/EN/ES/PT) LIKE
4. Kategorie-Filter (canonical mapping)
5. MaxPrepTime-Filter
6. Sortierung: myvideos → CreationTime DESC, sonst → Id DESC
7. Limit: nur im Standard-Feed ohne Filter → Take(20)
8. CDN-Pfad-Normalisierung (Blob → CDN-Domain)
```

**Like-Toggle** (`POST /ToggleLike`):
- Prüft ob Like existiert → remove oder add
- Aktualisiert `RecipeBaseData.LikeCount` atomar

**Follow-Toggle** (`POST /ToggleFollow`):
- Selbst-Follow wird abgelehnt
- Erstellt/löscht `WorldUserAbo`-Eintrag

### 7.2 Upload

`HomeController.Upload` (POST):

```
1. Rate-Limit prüfen (5/10min per UserHash via IMemoryCache)
2. Nur "orb"-User dürfen hochladen
3. BlobUploadService.UploadContentToBlob(file) aufrufen
4. SaveNewRecipeService.Save(...) mit Zutaten, Schritten, Keywords aufrufen
5. WorldUserPosting in DB speichern (CreatorId, Source, ThumbnailUrl, Recipe)
6. Redirect zu Profil / Feed
```

### 7.3 Essensplaner

Zwei Modi:

**Generator** (automatisch):
- Formular: Diättyp (Alles/Vegetarisch/Vegan), Filter, Tage (Standard 7), Personen (Standard 2)
- Abfrage zufälliger Rezepte nach Kriterien aus `IRecipesService`
- Ergebnis als `WorldMealPlanViewModel` mit berechneten Nährwerten

**Manueller Builder:**
- 7×3 Raster (Tage × Mahlzeiten: Vorspeise/Hauptspeise/Dessert)
- Rezepte per Drag & Drop / Auswahl zuweisen
- Echtzeit-Nährwertberechnung (JS-seitig, Daten via API)

**Speichern:**
- `WorldAppMealPlanService.SaveNewMealPlan(...)` → JSON als `Dictionary<int, List<int>>`
- "orb"-User: mehrere benannte Pläne
- Andere: genau ein Plan (überschreibt vorhandenen)

**Nährwertberechnung** (Server, `MarketplaceController.BuildNutritionTotalsAsync`):
- Lädt `RecipeBaseData` mit Zutaten und Nährwerten
- Berechnet Kalorien, Protein, Fett, Kohlenhydrate, Ballaststoffe
- Berücksichtigt Stückgewicht für Stück-Einheiten

### 7.4 Einkaufsliste & Teilen

**Einkaufsliste:**
- Automatisch aus Rezept-Zutaten generiert
- Gruppiert nach Kategorie (Gemüse, Proteine, Extras, etc.)
- Skalierbar nach Personenanzahl

**Teilen:**
- `POST /ShareMealPlan` erstellt `WorldSharedMealPlan` mit GUID-Token
- Öffentlicher Link: `/WorldMiniApp/Home/SharedPlan/{token}`
- Enthält Plan-JSON + Einkaufslisten-JSON (kein Login nötig)

### 7.5 Benutzerprofil

`FeedController.MyProfile` aggregiert:

| Tab | Beschreibung | Sichtbarkeit |
|---|---|---|
| **Likes** | Alle gelikten Rezepte | Alle |
| **Following** | Gefolgde Creator | Alle |
| **Essenspläne** | Eigene Pläne + Käufe | Alle |
| **Meine Videos** | Eigene Postings | Nur `orb` |

**Statistiken** (nur `orb`):
- Follower-Anzahl (CountAsync auf WorldUserAbo)
- Like-Summe aus eigenen Posts

**Einstellungen:**
- Benutzername ändern (`POST /UpdateProfile`)
- Theme-Wechsel (Color / Black / White / Rose / Lavender) – clientseitig

### 7.6 Marketplace

Vollständiger Peer-to-Peer Essensplan-Marktplatz auf World Chain:

**Angebot erstellen** (`POST /CreateListing`):
- Preis: 1–1000 WLD
- Wallet-Adresse (WLD und/oder USDT) muss mit Session-Wallet übereinstimmen
- `DayCount` und `RecipeCount` werden automatisch aus dem Plan-JSON berechnet

**Kauf-Flow:**
```
1. Käufer klickt "Freischalten" → Plan-Vorschau via GET /GetListingPreview
2. MiniKit Pay() initiiert On-Chain TX (WLD oder USDT)
3. Nach TX: POST /FinalizeWorldChainPurchase mit TxHash + PaymentToken
4. Server: Duplikat-Prüfung → Plan kopieren → Purchase anlegen → SoldCount++
5. On-Chain-Verifizierung (async, nicht-blockierend)
6. Käufer erhält Kopie des Plans in seinem Account
```

**Gebührenaufteilung:**
- 80% → Creator-Wallet
- 20% → Plattform

---

## 8. Azure-Integration

| Service | Konfigurationsschlüssel | Verwendung |
|---|---|---|
| **Blob Storage** | `Blob_Conection_String`, `World-App-Blop-Name` | Bild/Video-Speicherung |
| **Queue Storage** | `Queue_Connection_String`, `VideoProcessingQueueName` | Video-Processing-Jobs |
| **CDN (Azure Front Door)** | Hartcodierte Domain | Content Delivery |

**CDN-Pfad-Normalisierung:**
```csharp
// Alle Blob-URLs werden automatisch auf CDN umgeschrieben:
"blobdelikatessendrehbuch.blob.core.windows.net"
→ "DelekatesenDrehbuchCdn-beecexhdaghhacab.z01.azurefd.net"
```

**Video-Prozessor:** Separate Azure Functions App (`DelikatessenDrehbuchViedeoProzessor`) liest die Queue und verarbeitet Videos via FFmpeg. Das Ergebnis wird unter `{name}_processed.mp4` im gleichen Blob-Container gespeichert.

---

## 9. World Chain & WildCoin

### World Chain

**Netzwerk:** World Chain Mainnet (ChainId: `480`)
**RPC:** `https://worldchain-mainnet.g.alchemy.com/public`

Konfigurierbare Adressen:

| Config-Key | Beschreibung |
|---|---|
| `WorldChain:WldTokenAddress` | ERC-20 WLD Token-Adresse |
| `WorldChain:UsdtTokenAddress` | ERC-20 USDT Token-Adresse |
| `WorldChain:MarketplaceContractAddress` | Smart Contract für Payments |
| `WorldChain:AllowSelfPurchaseForTesting` | Selbstkauf erlauben (nur Test) |
| `WorldChain:TestMode` | Test-Modus aktiv |

**On-Chain TX-Verifizierung** (`VerifyTransactionOnChainAsync`):
- Ruft `eth_getTransactionReceipt` am Alchemy RPC ab
- Prüft `status == "0x1"` (erfolgreich)
- Max. 3 Versuche mit 3s Pause (TX evtl. noch pending)
- **Nicht-blockierend**: Kauf wird auch bei Verifizierungs-Fehler durchgeführt
- Duplikat-Schutz via UNIQUE-Index auf `ReferenceTxHash`

### WildCoin

Internes Guthaben-System (parallel zu echten On-Chain-Zahlungen):

```
Guthaben auf WorldAppUser.WildCoinBalance
Buchungen in WildCoinTransactions-Tabelle
Transaktionstypen: "sale" | "purchase" | "reward"
```

---

## 10. Konfiguration

Alle Schlüssel werden über `IConfiguration` (Dependency Injection) gelesen:

| Schlüssel | Beschreibung |
|---|---|
| `Blob_Conection_String` | Azure Blob Storage Connection String |
| `World-App-Blop-Name` | Blob-Container-Name (Standard: `blob-world-mini-app`) |
| `Queue_Connection_String` | Azure Queue Storage Connection String |
| `VideoProcessingQueueName` | Queue-Name (Standard: `video-processing-queue`) |
| `WorldChain:ChainId` | World Chain ID (Standard: `480`) |
| `WorldChain:WldTokenAddress` | WLD Token Contract |
| `WorldChain:UsdtTokenAddress` | USDT Token Contract |
| `WorldChain:MarketplaceContractAddress` | Marketplace Smart Contract |
| `WorldChain:AllowSelfPurchaseForTesting` | `"true"` für Testumgebung |
| `WorldChain:TestMode` | `"true"` für Testumgebung |

**Dependency Injection (Program.cs):**

```csharp
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManager, UserManager>();
builder.Services.AddScoped<IBlobUploadService, BlobUploadService>();
builder.Services.AddScoped<IWorldAppMealPlanService, WorldAppMealPlanService>();
builder.Services.AddScoped<ISaveNewRecipeService, SaveNewRecipeService>();
builder.Services.AddScoped<IWildCoinService, WildCoinService>();
```

Geteilte Services aus der Haupt-App (ebenfalls verfügbar):
- `IRecipesService`, `IMeasureService`, `IQuantityService`, `IIngredientService`, `INutrientService`

---

## 11. Sicherheit

| Bedrohung | Gegenmaßnahme |
|---|---|
| **Session-Hijacking** | UserHash wird ausschließlich aus `HttpContext.Session` gelesen; Request-Parameter werden ignoriert |
| **CSRF** | `[ValidateAntiForgeryToken]` auf allen POST-Endpunkten |
| **Replay-Angriff (SIWE)** | Nonce wird nach Verwendung aus Session gelöscht; Session-Nonce muss mit Nachricht-Nonce übereinstimmen |
| **Signatur-Fälschung** | Kein Fallback bei fehlgeschlagener ECDSA-Recovery; Recovered Address muss mit Claimed Address übereinstimmen |
| **SSRF** | BlobUploadService erlaubt URL-Uploads nur von Whitelist-Hosts (HTTPS only) |
| **File Upload** | Magic-Bytes-Prüfung zusätzlich zur Extension-Prüfung; separate Limits für Bild (15 MB) und Video (200 MB) |
| **Upload-Spam** | Rate Limit: 5 Uploads pro 10 Minuten pro User (IMemoryCache) |
| **Marketplace-Betrug** | Duplikat-Schutz via UNIQUE-Index auf `ReferenceTxHash`; Selbstkauf in Produktion deaktiviert |
| **Wallet-Spoofing** | Beim Listing-Erstellen und Kauf-Finalisieren: angegebene Wallet-Adresse muss Session-Wallet entsprechen |
| **Injection** | EF Core parameterisierte Queries; LIKE-Pattern über `EF.Functions.Like` |
| **Preismanipulation** | Listing-Preis aus DB; Käufer-seitige Preisangabe wird nicht akzeptiert |

---

## 12. API-Referenz

### Auth-Endpunkte

```
GET  /WorldMiniApp/Auth/Nonce
     → 200 { nonce: string }

POST /WorldMiniApp/Auth/CompleteSiwe
     Body: WalletAuthRequestDto (JSON)
     → 200 { status: "success", walletAddress: string }
     → 400 { status: "error", message: string }

POST /WorldMiniApp/Auth/VerifyAction
     Body: VerifyRequestDto (JSON)
     → 200 { status: 200, message: "Erfolg!" }
     → 400 "Verifizierung fehlgeschlagen"

POST /WorldMiniApp/Auth/RefreshStatus
     Body: RefreshLoginRequest (JSON)
     → 200 { status: "orb"|"device"|"wallet", rememberLogin: bool }
     → 404 "User not found"
```

### Feed-Endpunkte

```
GET  /WorldMiniApp/Feed/Index
     ?filter=feed|likes|myvideos
     &searchTerm=string
     &category=string
     &maxPrepTime=int
     &scrollToId=int
     → View mit List<WorldUserPosting>

POST /WorldMiniApp/Feed/ToggleLike
     Form: userHash, recipeId
     → 200 OK | 500

POST /WorldMiniApp/Feed/ToggleFollow
     Form: userHash, creatorId
     → 200 OK | 400

GET  /WorldMiniApp/Feed/MyProfile?userHash=string
     → View mit UserProfileViewModel

POST /WorldMiniApp/Feed/UpdateProfile
     Form: userHash, userName
     → Redirect zu MyProfile
```

### Marketplace-Endpunkte

```
GET  /WorldMiniApp/Marketplace/Index
     → View mit List<MealPlanListing>

GET  /WorldMiniApp/Marketplace/GetListingPreview?listingId=int
     → 200 { success, title, days[], nutrition{} }

POST /WorldMiniApp/Marketplace/CreateListing
     Form: mealPlanId, title, description, price, sellerWalletAddress, sellerUsdtWalletAddress
     → JSON { success: bool, listingId?: int, error?: string }

POST /WorldMiniApp/Marketplace/FinalizeWorldChainPurchase
     Form: FinalizeWorldChainPurchaseRequest { listingId, txHash, walletAddress, paymentToken }
     → JSON { success: bool, mealPlanId?: int, error?: string }

POST /WorldMiniApp/Marketplace/DeactivateListing
     Form: listingId
     → JSON { success: bool }

POST /WorldMiniApp/Marketplace/SaveWalletAddress
     Form: walletAddress, token (WLD|USDT|USDCE)
     → JSON { success, token, walletAddress }

GET  /WorldMiniApp/Marketplace/GetWalletAddress?token=WLD
     → JSON { walletAddress, token }

GET  /WorldMiniApp/Marketplace/Transactions
     → JSON [ { amount, balanceAfter, type, referenceInfo, date } ]
```
