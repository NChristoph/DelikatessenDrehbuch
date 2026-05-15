# WorldMiniApp - Umfassende Systemdokumentation

**Projekt:** DelikatessenDrehbuch
**Bereich:** WorldMiniApp Area
**Version:** 1.0
**Stand:** Mai 2026

---

## Inhaltsverzeichnis

1. [Übersicht](#1-übersicht)
2. [Architektur und Struktur](#2-architektur-und-struktur)
3. [Authentifizierung und Autorisierung](#3-authentifizierung-und-autorisierung)
4. [Bezahlsystem](#4-bezahlsystem)
5. [Datenbankmodelle](#5-datenbankmodelle)
6. [Services und Businesslogik](#6-services-und-businesslogik)
7. [Controller und API-Endpunkte](#7-controller-und-api-endpunkte)
8. [Frontend-Integration](#8-frontend-integration)
9. [Externe Integrationen](#9-externe-integrationen)
10. [Dateiaufruf-Kette](#10-dateiaufruf-kette)
11. [Sicherheitskonzepte](#11-sicherheitskonzepte)
12. [Deployment und Konfiguration](#12-deployment-und-konfiguration)

---

## 1. Übersicht

### 1.1 Was ist die WorldMiniApp?

Die WorldMiniApp ist eine TikTok-ähnliche soziale Plattform für Rezepte, die in das DelikatessenDrehbuch-Projekt integriert ist. Sie bietet:

- **World ID Authentifizierung** (biometrisch oder Gerät-basiert)
- **Wallet-Authentifizierung** (SIWE - Sign In With Ethereum)
- **Video/Bild-Feed** mit Rezepten
- **KI-gestützter Essensplaner**
- **Marketplace** mit Blockchain-Zahlungen
- **Social Features** (Likes, Kommentare, Profile)

### 1.2 Technologie-Stack

| Komponente | Technologie |
|------------|-------------|
| Backend | ASP.NET Core MVC (.NET 8) |
| Frontend | Razor Views + JavaScript (ES6+) |
| Datenbank | Azure SQL Server + Entity Framework Core |
| Blockchain | World Chain (EVM-kompatibel, Chain ID: 480) |
| Authentifizierung | Worldcoin World ID + SIWE |
| Storage | Azure Blob Storage + Bunny.net CDN |
| AI | OpenAI API |
| Styling | Bootstrap 5 + Custom CSS |

### 1.3 Verzeichnisstruktur

```
DelikatessenDrehbuch/
├── Areas/
│   └── WorldMiniApp/
│       ├── Controllers/        # MVC-Controller (10 Dateien)
│       ├── Models/             # Datenmodelle (60+ Dateien)
│       ├── Services/           # Business-Logik (20+ Dateien)
│       ├── Views/              # Razor-Templates (30+ Dateien)
│       ├── Exceptions/         # Custom Exceptions (6 Dateien)
│       └── Extensions/         # Query-Extensions
├── Contracts/                  # Smart Contracts (Solidity)
├── Data/                       # EF Core DbContext
├── wwwroot/js/                 # Frontend JavaScript
├── sql/                        # Datenbank-Migrationen
├── appsettings.json           # Konfiguration
└── Program.cs                  # DI-Container Setup
```

---

## 2. Architektur und Struktur

### 2.1 Schichtenarchitektur

```
┌─────────────────────────────────────────────┐
│         Presentation Layer                   │
│  (Razor Views + JavaScript)                 │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Controller Layer                     │
│  (AuthController, MarketplaceController,    │
│   FeedController, HomeController, etc.)     │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Service Layer                        │
│  (AuthService, WildCoinService,             │
│   BlobUploadService, etc.)                  │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Data Layer                           │
│  (ApplicationDbContext + EF Core)           │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Database                             │
│  (Azure SQL Server)                         │
└─────────────────────────────────────────────┘
```

### 2.2 Dependency Injection Setup

**Datei:** `Program.cs`

Alle Services werden im DI-Container registriert:

```csharp
// Service-Registrierung
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManager, UserManager>();
builder.Services.AddScoped<IWildCoinService, WildCoinService>();
builder.Services.AddScoped<IBlobUploadService, BlobUploadService>();
builder.Services.AddScoped<IWorldAppMealPlanService, WorldAppMealPlanService>();
// ... weitere Services
```

### 2.3 Haupt-Controller

| Controller | Zweck | Datei |
|-----------|-------|-------|
| **AuthController** | Authentifizierung (World ID, Wallet) | `Controllers/AuthController.cs` |
| **MarketplaceController** | Marketplace & Bezahlung | `Controllers/MarketplaceController.cs` |
| **HomeController** | Essensplaner & Dashboard | `Controllers/HomeController.cs` |
| **FeedController** | Social Feed & Profile | `Controllers/FeedController.cs` |
| **RecipeController** | Rezeptverwaltung | `Controllers/RecipeController.cs` |
| **MealPlanController** | Essensplan CRUD | `Controllers/MealPlanController.cs` |

---

## 3. Authentifizierung und Autorisierung

### 3.1 Authentifizierungsmethoden

#### A. World ID Authentifizierung (Primär)

**Zweck:** Biometrische oder Gerät-basierte Verifizierung über Worldcoin

**Ablauf:**

```
Benutzer                Browser              AuthController         AuthService          Worldcoin API
   │                       │                       │                    │                      │
   │ 1. Klick "Login"     │                       │                    │                      │
   │─────────────────────>│                       │                    │                      │
   │                       │                       │                    │                      │
   │ 2. World App öffnet  │                       │                    │                      │
   │<─────────────────────│                       │                    │                      │
   │                       │                       │                    │                      │
   │ 3. Biometrische      │                       │                    │                      │
   │    Verifizierung     │                       │                    │                      │
   │                       │                       │                    │                      │
   │ 4. Proof erhalten    │                       │                    │                      │
   │─────────────────────>│                       │                    │                      │
   │                       │                       │                    │                      │
   │                       │ 5. POST /VerifyAction│                    │                      │
   │                       │──────────────────────>│                    │                      │
   │                       │                       │                    │                      │
   │                       │                       │ 6. VerifyProof()   │                      │
   │                       │                       │───────────────────>│                      │
   │                       │                       │                    │                      │
   │                       │                       │                    │ 7. API-Aufruf       │
   │                       │                       │                    │────────────────────>│
   │                       │                       │                    │                      │
   │                       │                       │                    │ 8. Verifizierung OK│
   │                       │                       │                    │<────────────────────│
   │                       │                       │                    │                      │
   │                       │                       │ 9. User erstellen/ │                      │
   │                       │                       │    aktualisieren   │                      │
   │                       │                       │<───────────────────│                      │
   │                       │                       │                    │                      │
   │                       │ 10. Session Cookie   │                    │                      │
   │                       │      setzen          │                    │                      │
   │                       │<──────────────────────│                    │                      │
   │                       │                       │                    │                      │
   │ 11. Eingeloggt       │                       │                    │                      │
   │<─────────────────────│                       │                    │                      │
```

**Dateien beteiligt:**

1. **Frontend:** `Views/Auth/` - Login-Formular
2. **Controller:** `Areas/WorldMiniApp/Controllers/AuthController.cs:VerifyAction()`
3. **Service:** `Areas/WorldMiniApp/Services/AuthService.cs:VerifyProofWithWorldcoin()`
4. **User Manager:** `Areas/WorldMiniApp/Services/UserManager.cs:CreateOrUpdateUserAsync()`
5. **Model:** `Areas/WorldMiniApp/Models/WorldAppUser.cs`

**Code-Beispiel:**

```csharp
// AuthController.cs
[HttpPost]
public async Task<IActionResult> VerifyAction([FromBody] VerifyRequestDto verifyRequest)
{
    // 1. Proof an Worldcoin API senden
    var verificationResult = await _authService.VerifyProofWithWorldcoin(verifyRequest);

    if (!verificationResult.Success)
        return BadRequest("Verifizierung fehlgeschlagen");

    // 2. Benutzer erstellen/aktualisieren
    var user = await _userManager.CreateOrUpdateUserAsync(
        verifyRequest.NullifierHash,
        verifyRequest.VerificationLevel
    );

    // 3. Session setzen
    HttpContext.Session.SetString("WorldMiniAppUserHash", user.UserHash);

    return Ok();
}
```

**Verifizierungslevel:**

| Level | Beschreibung | Features |
|-------|-------------|----------|
| `"orb"` | Biometrische Verifizierung (Orb) | Alle Features, unbegrenzte Meal Plans |
| `"device"` | Gerät-basiert | Eingeschränkt, max. 1 Meal Plan |

#### B. Wallet-Authentifizierung (SIWE)

**Zweck:** Ethereum-Wallet-basierte Anmeldung

**Ablauf:**

```
1. Benutzer klickt "Mit Wallet verbinden"
   ↓
2. AuthController.Nonce() - Nonce generieren
   Datei: AuthController.cs:121
   └─> Session["SiweNonce"] = Random.Shared.Next(100000000, 999999999)
   ↓
3. Benutzer signiert SIWE-Nachricht mit Wallet
   ↓
4. AuthController.CompleteSiwe(signedMessage, nonce, walletAddress)
   Datei: AuthController.cs:135
   ├─> Nonce validieren (Session-Vergleich)
   ├─> Signatur prüfen
   ├─> Wallet-Adresse mit Regex validieren: ^0x[a-fA-F0-9]{40}$
   └─> User erstellen/aktualisieren
   ↓
5. Session Cookie setzen
   └─> WorldMiniAppUserHash = Hash(walletAddress)
```

**Dateien beteiligt:**

- `Areas/WorldMiniApp/Controllers/AuthController.cs:Nonce()` (Zeile 121)
- `Areas/WorldMiniApp/Controllers/AuthController.cs:CompleteSiwe()` (Zeile 135)
- `Areas/WorldMiniApp/Services/UserManager.cs`

### 3.2 Session-Management

**Session-Schlüssel:**

```csharp
// WorldMiniAppUserHash - Hauptidentifikator
const string SessionKeyUserHash = "WorldMiniAppUserHash";

// Wallet-Adressen
const string SessionWalletWLD = "WorldWallet_WLD";
const string SessionWalletUSDT = "WorldWallet_USDT";

// SIWE-Nonce
const string SessionKeySiweNonce = "SiweNonce";
```

**User Hash Auflösung:**

**Datei:** `Areas/WorldMiniApp/Controllers/WorldMiniAppBaseController.cs`

```csharp
public static class WorldMiniAppUserHashHelper
{
    public static string? Resolve(HttpContext httpContext)
    {
        // 1. Aus Query-Parameter
        var queryHash = httpContext.Request.Query["u"].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryHash))
            return queryHash;

        // 2. Aus Session
        return httpContext.Session.GetString("WorldMiniAppUserHash");
    }
}
```

### 3.3 Autorisierung

**Super Admin:**

```csharp
protected const string SuperUserHash =
    "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839";

protected bool IsSuperAdmin()
{
    return UserHash == SuperUserHash;
}
```

**Permissions basierend auf Verifizierungslevel:**

```csharp
// WorldAppUser.cs
public bool CanUploadVideos => VerificationLevel == "orb";
public int MaxMealPlans => VerificationLevel == "orb" ? int.MaxValue : 1;
```

---

## 4. Bezahlsystem

### 4.1 Übersicht

Das Bezahlsystem verwendet eine hybride Architektur:

1. **Off-chain:** WildCoin (interne Währung) für Belohnungen
2. **On-chain:** World Chain (Blockchain) für Marketplace-Transaktionen

**Unterstützte Tokens:**
- **WLD** (World Token) - Native Currency
- **USDT** (Tether USD)
- **USDCE** (USD Coin Ethereum)

### 4.2 Marketplace-Architektur

```
┌─────────────────────────────────────────────────────────┐
│                  MARKETPLACE FLOW                        │
└─────────────────────────────────────────────────────────┘

Verkäufer                                           Käufer
    │                                                  │
    │ 1. Erstellt Meal Plan                           │
    │    (HomeController)                             │
    │                                                  │
    │ 2. Listet zum Verkauf                           │
    │    POST /Marketplace/CreateListing              │
    │    ├─ Preis: 1-1000 WLD                        │
    │    ├─ Wallet-Adresse (WLD/USDT)                │
    │    └─> MealPlanListing erstellt                │
    │                                                  │
    │ ◄─────────── DATENBANK ──────────────────────► │
    │              MealPlanListings                    │
    │                                                  │
    │                                       3. Durchsucht Marketplace
    │                                          GET /Marketplace/Index
    │                                                  │
    │                                       4. Wählt Listing aus
    │                                          GET /GetListingPreview
    │                                                  │
    │                                       5. Verbindet Wallet
    │                                          POST /SaveWalletAddress
    │                                          └─> Session speichert
    │                                                  │
    │                                       6. Initiiert Zahlung
    │                                          ├─ MiniKit pay()
    │                                          ├─ Blockchain TX
    │                                          └─> TxHash erhalten
    │                                                  │
    │                                       7. Finalisiert Kauf
    │                                          POST /FinalizeWorldChainPurchase
    │                                          └─> WildCoinService
    │                                                  │
    ├─────────────────────────────────────────────────┤
    │        DATABASE TRANSAKTION                      │
    │   a) Meal Plan kopieren für Käufer              │
    │   b) SoldCount erhöhen                          │
    │   c) MealPlanPurchase Datensatz                 │
    │   d) 80/20 Split berechnen                      │
    │        Creator: 80 WLD                          │
    │        Platform: 20 WLD                         │
    ├─────────────────────────────────────────────────┤
    │                                                  │
    │ 8. Erhält 80% Zahlung                9. Erhält Meal Plan
    │    (im Wallet)                           (im Account)
    │                                                  │
```

### 4.3 Verkäufer-Flow: Listing erstellen

#### Datei-Aufruf-Kette:

```
Views/Marketplace/Sell.cshtml
    │
    ├─> JavaScript: worldchain-marketplace.js
    │   └─> AJAX POST /Marketplace/CreateListing
    │
    └─> MarketplaceController.cs:CreateListing()  [Zeile 156]
        │
        ├─> 1. Validierung:
        │   ├─ UserHash aus Session
        │   ├─ Preis: 1-1000 WLD
        │   ├─ Wallet-Adresse Format: ^0x[a-fA-F0-9]{40}$
        │   └─ Meal Plan gehört dem User
        │
        ├─> 2. IWildCoinService.CreateListingAsync()
        │   │
        │   └─> WildCoinService.cs:CreateListingAsync()  [Zeile 234]
        │       │
        │       ├─> Prüfe alle Rezepte gehören Verkäufer
        │       │   (via RecipeBaseData.CreatorHash)
        │       │
        │       ├─> MealPlanListing Objekt erstellen:
        │       │   {
        │       │     SellerHash: userHash,
        │       │     MealPlanId: mealPlanId,
        │       │     Title: title,
        │       │     Price: price,
        │       │     SellerWalletAddress: walletAddress,
        │       │     SellerWalletAddressUSDT: usdtWallet,
        │       │     IsActive: true,
        │       │     CreatedAt: DateTime.UtcNow,
        │       │     SoldCount: 0
        │       │   }
        │       │
        │       └─> _context.MealPlanListings.Add(listing)
        │           _context.SaveChangesAsync()
        │
        └─> 3. Response: { success: true, listingId: xyz }
```

**Beteiligte Dateien:**

1. **View:** `Areas/WorldMiniApp/Views/Marketplace/Sell.cshtml`
2. **JavaScript:** `wwwroot/js/worldchain-marketplace.js`
3. **Controller:** `Areas/WorldMiniApp/Controllers/MarketplaceController.cs:CreateListing()` (Zeile 156)
4. **Service:** `Areas/WorldMiniApp/Services/WildCoinService.cs:CreateListingAsync()` (Zeile 234)
5. **Model:** `Areas/WorldMiniApp/Models/MealPlanListing.cs`
6. **DbContext:** `Data/ApplicationDbContext.cs`

### 4.4 Käufer-Flow: Kauf durchführen

#### Datei-Aufruf-Kette:

```
Views/Marketplace/Index.cshtml
    │
    ├─> JavaScript: worldchain-marketplace.js:purchaseMealPlan()
    │   │
    │   ├─> 1. MiniKit.pay() aufrufen
    │   │   {
    │   │     to: MARKETPLACE_CONTRACT,
    │   │     value: price,
    │   │     token: "WLD" | "USDT" | "USDCE"
    │   │   }
    │   │
    │   ├─> 2. Blockchain-Transaktion
    │   │   └─> TxHash erhalten: 0xabc123...
    │   │
    │   └─> 3. AJAX POST /Marketplace/FinalizeWorldChainPurchase
    │       {
    │         listingId: 42,
    │         txHash: "0xabc123...",
    │         walletAddress: "0x789...",
    │         paymentToken: "WLD"
    │       }
    │
    └─> MarketplaceController.cs:FinalizeWorldChainPurchase()  [Zeile 412]
        │
        ├─> 1. Input-Validierung:
        │   ├─ UserHash vorhanden?
        │   ├─ TxHash Format: ^0x[a-fA-F0-9]{64}$
        │   ├─ Wallet aus Session = Request Wallet?
        │   └─ PaymentToken valid? (WLD/USDT/USDCE)
        │
        ├─> 2. IWildCoinService.FinalizeWorldChainPurchaseAsync()
        │   │
        │   └─> WildCoinService.cs:FinalizeWorldChainPurchaseAsync()  [Zeile 456]
        │       │
        │       ├─> a) Listing laden
        │       │   var listing = await _context.MealPlanListings
        │       │       .Include(l => l.MealPlan)
        │       │       .FirstOrDefaultAsync(l => l.Id == listingId);
        │       │
        │       ├─> b) Validierung:
        │       │   ├─ Listing IsActive?
        │       │   ├─ Kein Selbstkauf (außer TestMode)
        │       │   └─ TxHash noch nicht verwendet?
        │       │       (UNIQUE INDEX auf ReferenceTxHash)
        │       │
        │       ├─> c) Meal Plan kopieren für Käufer
        │       │   var newPlan = await _mealPlanService.CopyMealPlanAsync(
        │       │       listing.MealPlanId,
        │       │       buyerHash,
        │       │       listing.Title + " (gekauft)"
        │       │   );
        │       │
        │       ├─> d) 80/20 Split berechnen
        │       │   decimal creatorAmount = listing.Price * 0.8m;
        │       │   decimal platformFee = listing.Price * 0.2m;
        │       │
        │       ├─> e) MealPlanPurchase erstellen
        │       │   var purchase = new MealPlanPurchase
        │       │   {
        │       │       BuyerHash = buyerHash,
        │       │       ListingId = listingId,
        │       │       PricePaid = listing.Price,
        │       │       CreatorAmount = creatorAmount,
        │       │       PlatformFee = platformFee,
        │       │       ReferenceTxHash = txHash,
        │       │       PaymentToken = paymentToken,
        │       │       BuyerWalletAddress = walletAddress,
        │       │       SellerWalletAddress = listing.SellerWalletAddress,
        │       │       CreatedMealPlanId = newPlan.Id,
        │       │       PurchaseDate = DateTime.UtcNow
        │       │   };
        │       │
        │       ├─> f) SoldCount erhöhen
        │       │   listing.SoldCount++;
        │       │
        │       ├─> g) Datenbank-Transaktion
        │       │   await _context.MealPlanPurchases.AddAsync(purchase);
        │       │   await _context.SaveChangesAsync();
        │       │
        │       └─> h) Async On-Chain Verifizierung (fire-and-forget)
        │           _ = Task.Run(() =>
        │               VerifyTransactionOnChainAsync(txHash)
        │           );
        │
        └─> 3. Response:
            {
              success: true,
              mealPlanId: newPlan.Id,
              message: "Kauf erfolgreich!"
            }
```

**Beteiligte Dateien:**

1. **View:** `Areas/WorldMiniApp/Views/Marketplace/Index.cshtml`
2. **JavaScript:** `wwwroot/js/worldchain-marketplace.js:purchaseMealPlan()`
3. **Controller:** `Areas/WorldMiniApp/Controllers/MarketplaceController.cs:FinalizeWorldChainPurchase()` (Zeile 412)
4. **Service:** `Areas/WorldMiniApp/Services/WildCoinService.cs:FinalizeWorldChainPurchaseAsync()` (Zeile 456)
5. **Service:** `Areas/WorldMiniApp/Services/WorldAppMealPlanService.cs:CopyMealPlanAsync()`
6. **Models:**
   - `Areas/WorldMiniApp/Models/MealPlanPurchase.cs`
   - `Areas/WorldMiniApp/Models/MealPlanListing.cs`
7. **DbContext:** `Data/ApplicationDbContext.cs`

### 4.5 Blockchain-Verifizierung

**Zweck:** On-Chain-Transaktion validieren (asynchron, nicht-blockierend)

**Datei:** `Areas/WorldMiniApp/Services/WildCoinService.cs:VerifyTransactionOnChainAsync()` (Zeile 789)

```csharp
private async Task<bool> VerifyTransactionOnChainAsync(string txHash)
{
    const string RPC_URL = "https://worldchain-mainnet.g.alchemy.com/public";

    // Retry-Logik: 3 Versuche × 3 Sekunden
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            // RPC-Request erstellen
            var rpcRequest = new
            {
                jsonrpc = "2.0",
                method = "eth_getTransactionReceipt",
                @params = new[] { txHash },
                id = 1
            };

            // API-Aufruf
            var response = await _httpClient.PostAsJsonAsync(RPC_URL, rpcRequest);
            var result = await response.Content.ReadFromJsonAsync<RpcResponse>();

            // Status prüfen
            if (result?.Result?.Status == "0x1")
            {
                _logger.LogInformation(
                    "TX {TxHash} verifiziert: SUCCESS",
                    txHash
                );
                return true;
            }

            // Retry bei Pending
            if (result?.Result == null)
            {
                await Task.Delay(3000);
                continue;
            }

            _logger.LogWarning(
                "TX {TxHash} fehlgeschlagen: Status={Status}",
                txHash,
                result.Result.Status
            );
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Fehler bei TX-Verifizierung (Versuch {Attempt}/3)",
                attempt
            );

            if (attempt < 3)
                await Task.Delay(3000);
        }
    }

    return false;
}
```

**RPC-Endpoint:**
- URL: `https://worldchain-mainnet.g.alchemy.com/public`
- Chain ID: 480 (World Chain Mainnet)
- Methode: `eth_getTransactionReceipt`

**Retry-Strategie:**
- 3 Versuche
- 3 Sekunden Wartezeit zwischen Versuchen
- Gesamtmaximum: 9 Sekunden

**Wichtig:** Verifizierung blockiert NICHT den Kauf! Der Kauf wird sofort abgeschlossen, Verifizierung läuft asynchron im Hintergrund.

### 4.6 Smart Contract

**Datei:** `Contracts/MealPlanMarketplaceWorldChain.sol`

```solidity
// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "@openzeppelin/contracts/token/ERC20/IERC20.sol";

contract MealPlanMarketplaceWorldChain {
    address public platformWallet;
    uint256 public platformFeePercent = 20; // 20%

    event Purchase(
        address indexed buyer,
        address indexed seller,
        uint256 listingId,
        uint256 amount,
        address token
    );

    constructor(address _platformWallet) {
        platformWallet = _platformWallet;
    }

    function purchaseMealPlan(
        address seller,
        uint256 listingId,
        uint256 amount,
        address tokenAddress
    ) external {
        IERC20 token = IERC20(tokenAddress);

        // Transfer von Käufer zu Contract
        require(
            token.transferFrom(msg.sender, address(this), amount),
            "Transfer fehlgeschlagen"
        );

        // 80/20 Split berechnen
        uint256 creatorAmount = (amount * 80) / 100;
        uint256 platformAmount = amount - creatorAmount;

        // Transfer zu Creator
        require(
            token.transfer(seller, creatorAmount),
            "Creator-Transfer fehlgeschlagen"
        );

        // Transfer zu Platform
        require(
            token.transfer(platformWallet, platformAmount),
            "Platform-Transfer fehlgeschlagen"
        );

        emit Purchase(msg.sender, seller, listingId, amount, tokenAddress);
    }
}
```

**Deployment:**

**Datei:** `Contracts/scripts/deploy-worldchain-marketplace.js`

```javascript
const { ethers } = require("hardhat");

async function main() {
    const platformWallet = "0x..."; // Platform Wallet-Adresse

    const MarketplaceFactory = await ethers.getContractFactory(
        "MealPlanMarketplaceWorldChain"
    );

    const marketplace = await MarketplaceFactory.deploy(platformWallet);
    await marketplace.deployed();

    console.log("Contract deployed to:", marketplace.address);
}

main();
```

**Deployed Contract:**
- Adresse: `0x9891a521dbefb815a98e5be9256cd82524fffb5d`
- Chain: World Chain (480)
- Platform Fee: 20%

### 4.7 Revenue Split

```
Gesamt-Preis: 100 WLD
    │
    ├─> Creator (Verkäufer): 80 WLD (80%)
    │   └─> Wallet: listing.SellerWalletAddress
    │
    └─> Platform (DelikatessenDrehbuch): 20 WLD (20%)
        └─> Wallet: platformWallet (aus Smart Contract)
```

**Datensatz in MealPlanPurchase:**

```csharp
{
    PricePaid: 100.00m,
    CreatorAmount: 80.00m,
    PlatformFee: 20.00m,
    PaymentToken: "WLD",
    BuyerWalletAddress: "0xKäufer...",
    SellerWalletAddress: "0xVerkäufer...",
    ReferenceTxHash: "0xabc123..."
}
```

---

## 5. Datenbankmodelle

### 5.1 Entity-Relationship-Diagramm

```
┌─────────────────────┐
│   WorldAppUser      │
│   ───────────────   │
│ PK UserHash         │◄────────────────┐
│    VerificationLvl  │                 │
│    WalletAddress    │                 │
│    CreatedAt        │                 │
└─────────────────────┘                 │
         │                              │
         │ 1:N                          │
         │                              │
         ├─────────────────────┐        │
         │                     │        │
         ▼                     ▼        │
┌──────────────────┐  ┌──────────────────────┐
│ WorldUserPosting │  │  WorldUserMealPlan   │
│ ──────────────── │  │  ──────────────────  │
│ PK Id            │  │ PK Id                │
│ FK CreatorId     │  │ FK UserHash          │
│    RecipeId      │  │    Title             │
│    VideoUrl      │  │    Description       │
│    ThumbnailUrl  │  │    CreatedAt         │
│    LikeCount     │  └──────────────────────┘
│    ViewCount     │             │
└──────────────────┘             │ 1:1
                                 │
                                 ▼
                    ┌──────────────────────────┐
                    │   MealPlanListing        │◄─────────┐
                    │   ────────────────────   │          │
                    │ PK Id                    │          │
                    │ FK SellerHash            ├──────────┘
                    │ FK MealPlanId            │ N:1
                    │    Title                 │
                    │    Price                 │
                    │    SellerWalletAddress   │
                    │    IsActive              │
                    │    SoldCount             │
                    │    CreatedAt             │
                    └──────────────────────────┘
                               │
                               │ 1:N
                               │
                               ▼
                    ┌──────────────────────────┐
                    │   MealPlanPurchase       │
                    │   ────────────────────   │
                    │ PK Id                    │
                    │ FK BuyerHash             │
                    │ FK ListingId             │
                    │    PricePaid             │
                    │    CreatorAmount         │
                    │    PlatformFee           │
                    │    ReferenceTxHash (UQ)  │
                    │    PaymentToken          │
                    │    BuyerWalletAddress    │
                    │    SellerWalletAddress   │
                    │    CreatedMealPlanId     │
                    │    PurchaseDate          │
                    └──────────────────────────┘

Legende:
PK = Primary Key
FK = Foreign Key
UQ = Unique Index
1:N = One-to-Many
N:1 = Many-to-One
```

### 5.2 Haupt-Tabellen

#### A. WorldAppUser

**Datei:** `Areas/WorldMiniApp/Models/WorldAppUser.cs`

```csharp
public class WorldAppUser
{
    [Key]
    [MaxLength(66)]
    public string UserHash { get; set; } = null!;

    [MaxLength(10)]
    public string VerificationLevel { get; set; } = "device";

    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    [MaxLength(255)]
    public string? ProfileImageUrl { get; set; }

    [MaxLength(42)]
    public string? WalletAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }

    // Navigation Properties
    public ICollection<WorldUserPosting> Postings { get; set; }
    public ICollection<WorldUserMealPlan> MealPlans { get; set; }
    public ICollection<MealPlanListing> Listings { get; set; }
}
```

**Indexes:**

```sql
CREATE INDEX IX_WorldAppUser_UserHash ON WorldAppUser(UserHash);
CREATE INDEX IX_WorldAppUser_WalletAddress ON WorldAppUser(WalletAddress);
```

#### B. MealPlanListing

**Datei:** `Areas/WorldMiniApp/Models/MealPlanListing.cs`

```csharp
public class MealPlanListing
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(66)]
    public string SellerHash { get; set; } = null!;

    [Required]
    public int MealPlanId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required]
    [MaxLength(42)]
    public string SellerWalletAddress { get; set; } = null!;

    [MaxLength(42)]
    public string? SellerWalletAddressUSDT { get; set; }

    public bool IsActive { get; set; } = true;

    public int SoldCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeactivatedAt { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(SellerHash))]
    public WorldAppUser Seller { get; set; } = null!;

    [ForeignKey(nameof(MealPlanId))]
    public WorldUserMealPlan MealPlan { get; set; } = null!;

    public ICollection<MealPlanPurchase> Purchases { get; set; }
}
```

**Indexes:**

```sql
CREATE INDEX IX_MealPlanListings_SellerHash
    ON MealPlanListings(SellerHash);

CREATE INDEX IX_MealPlanListings_IsActive_CreatedAt
    ON MealPlanListings(IsActive, CreatedAt DESC);
```

**Migration:**

**Datei:** `sql/001_add_wildcoin_marketplace.sql`

```sql
CREATE TABLE MealPlanListings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SellerHash NVARCHAR(66) NOT NULL,
    MealPlanId INT NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(2000),
    Price DECIMAL(18,2) NOT NULL,
    SellerWalletAddress NVARCHAR(42) NOT NULL,
    SellerWalletAddressUSDT NVARCHAR(42),
    IsActive BIT NOT NULL DEFAULT 1,
    SoldCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DeactivatedAt DATETIME2,

    CONSTRAINT FK_MealPlanListings_Seller
        FOREIGN KEY (SellerHash)
        REFERENCES WorldAppUser(UserHash),

    CONSTRAINT FK_MealPlanListings_MealPlan
        FOREIGN KEY (MealPlanId)
        REFERENCES WorldUserMealPlan(Id)
);

CREATE INDEX IX_MealPlanListings_SellerHash
    ON MealPlanListings(SellerHash);
```

#### C. MealPlanPurchase

**Datei:** `Areas/WorldMiniApp/Models/MealPlanPurchase.cs`

```csharp
public class MealPlanPurchase
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(66)]
    public string BuyerHash { get; set; } = null!;

    [Required]
    public int ListingId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePaid { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal CreatorAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PlatformFee { get; set; }

    [MaxLength(66)]
    public string? ReferenceTxHash { get; set; }

    [MaxLength(10)]
    public string? PaymentToken { get; set; } // "WLD", "USDT", "USDCE"

    [MaxLength(42)]
    public string? BuyerWalletAddress { get; set; }

    [MaxLength(42)]
    public string? SellerWalletAddress { get; set; }

    public int? CreatedMealPlanId { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey(nameof(BuyerHash))]
    public WorldAppUser Buyer { get; set; } = null!;

    [ForeignKey(nameof(ListingId))]
    public MealPlanListing Listing { get; set; } = null!;
}
```

**Wichtigste Indexes:**

```sql
-- Duplicate-Protection (kritisch!)
CREATE UNIQUE INDEX UX_MealPlanPurchases_ReferenceTxHash_NotNull
    ON MealPlanPurchases(ReferenceTxHash)
    WHERE ReferenceTxHash IS NOT NULL;

-- Query-Optimierung
CREATE INDEX IX_MealPlanPurchases_ListingBuyerTx
    ON MealPlanPurchases(ListingId, BuyerHash, ReferenceTxHash);

CREATE INDEX IX_MealPlanPurchases_BuyerHash
    ON MealPlanPurchases(BuyerHash);
```

**Migration:**

**Datei:** `sql/002_add_worldchain_purchase_fields.sql`

```sql
ALTER TABLE MealPlanPurchases
    ADD ReferenceTxHash NVARCHAR(66) NULL;

ALTER TABLE MealPlanPurchases
    ADD BuyerWalletAddress NVARCHAR(42) NULL;

ALTER TABLE MealPlanPurchases
    ADD SellerWalletAddress NVARCHAR(42) NULL;

-- UNIQUE constraint auf TxHash
CREATE UNIQUE INDEX UX_MealPlanPurchases_ReferenceTxHash_NotNull
    ON MealPlanPurchases(ReferenceTxHash)
    WHERE ReferenceTxHash IS NOT NULL;
```

**Datei:** `sql/003_add_payment_token_field.sql`

```sql
ALTER TABLE MealPlanPurchases
    ADD PaymentToken NVARCHAR(10) NULL;
```

### 5.3 DbContext-Konfiguration

**Datei:** `Data/ApplicationDbContext.cs`

```csharp
public class ApplicationDbContext : DbContext
{
    // DbSets
    public DbSet<WorldAppUser> WorldAppUsers { get; set; }
    public DbSet<WorldUserPosting> WorldUserPostings { get; set; }
    public DbSet<WorldUserMealPlan> WorldUserMealPlans { get; set; }
    public DbSet<MealPlanListing> MealPlanListings { get; set; }
    public DbSet<MealPlanPurchase> MealPlanPurchases { get; set; }
    public DbSet<WildCoinTransaction> WildCoinTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // WorldAppUser
        modelBuilder.Entity<WorldAppUser>()
            .HasKey(u => u.UserHash);

        modelBuilder.Entity<WorldAppUser>()
            .HasIndex(u => u.WalletAddress);

        // MealPlanListing
        modelBuilder.Entity<MealPlanListing>()
            .HasOne(l => l.Seller)
            .WithMany(u => u.Listings)
            .HasForeignKey(l => l.SellerHash)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MealPlanListing>()
            .HasOne(l => l.MealPlan)
            .WithOne()
            .HasForeignKey<MealPlanListing>(l => l.MealPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MealPlanListing>()
            .HasIndex(l => l.SellerHash);

        modelBuilder.Entity<MealPlanListing>()
            .HasIndex(l => new { l.IsActive, l.CreatedAt });

        // MealPlanPurchase
        modelBuilder.Entity<MealPlanPurchase>()
            .HasOne(p => p.Buyer)
            .WithMany()
            .HasForeignKey(p => p.BuyerHash)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MealPlanPurchase>()
            .HasOne(p => p.Listing)
            .WithMany(l => l.Purchases)
            .HasForeignKey(p => p.ListingId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE Index auf TxHash
        modelBuilder.Entity<MealPlanPurchase>()
            .HasIndex(p => p.ReferenceTxHash)
            .IsUnique()
            .HasFilter("ReferenceTxHash IS NOT NULL");

        modelBuilder.Entity<MealPlanPurchase>()
            .HasIndex(p => p.BuyerHash);
    }
}
```

---

## 6. Services und Businesslogik

### 6.1 Service-Übersicht

| Service | Datei | Zweck |
|---------|-------|-------|
| **IAuthService** | `Services/AuthService.cs` | Worldcoin World ID Verifizierung |
| **IUserManager** | `Services/UserManager.cs` | User CRUD, Duplicate Handling |
| **IWildCoinService** | `Services/WildCoinService.cs` | Marketplace, Payments, Listings |
| **IBlobUploadService** | `Services/BlobUploadService.cs` | Azure Blob Storage Upload |
| **IWorldAppMealPlanService** | `Services/WorldAppMealPlanService.cs` | Meal Plan CRUD |
| **IFeedAlgorithmService** | `Services/FeedAlgorithmService.cs` | Feed Personalisierung |
| **ISaveNewRecipeService** | `Services/SaveNewRecipeService.cs` | Rezept speichern |
| **IRecipeAiTransformService** | `Services/RecipeAiTransformService.cs` | KI-Rezept-Varianten |
| **IMarketplaceRankingService** | `Services/MarketplaceRankingService.cs` | Listing-Ranking |

### 6.2 IWildCoinService (Kern des Bezahlsystems)

**Datei:** `Areas/WorldMiniApp/Services/WildCoinService.cs`

**Interface:**

```csharp
public interface IWildCoinService
{
    // Marketplace Listings
    Task<MealPlanListing> CreateListingAsync(
        string sellerHash,
        int mealPlanId,
        string title,
        string description,
        decimal price,
        string walletAddress,
        string? usdtWalletAddress
    );

    Task<List<MealPlanListing>> GetActiveListingsAsync();
    Task<List<MealPlanListing>> GetMyListingsAsync(string userHash);
    Task DeactivateListingAsync(string userHash, int listingId);
    Task ActivateListingAsync(string userHash, int listingId);

    // Purchases
    Task<MealPlanPurchase> FinalizeWorldChainPurchaseAsync(
        string buyerHash,
        int listingId,
        string txHash,
        string walletAddress,
        string paymentToken
    );

    Task<List<MealPlanPurchase>> GetPurchasesByBuyerAsync(string buyerHash);

    // WildCoin (internal currency)
    Task<decimal> GetBalanceAsync(string userHash);
    Task TransferAsync(string fromHash, string toHash, decimal amount, string referenceInfo);
    Task RewardAsync(string userHash, decimal amount, string referenceInfo);
}
```

**Implementierung - Wichtigste Methoden:**

#### CreateListingAsync()

```csharp
public async Task<MealPlanListing> CreateListingAsync(
    string sellerHash,
    int mealPlanId,
    string title,
    string description,
    decimal price,
    string walletAddress,
    string? usdtWalletAddress)
{
    // 1. Meal Plan laden mit allen Rezepten
    var mealPlan = await _context.WorldUserMealPlans
        .Include(mp => mp.DayPlans)
            .ThenInclude(dp => dp.RecipeSelections)
                .ThenInclude(rs => rs.Recipe)
        .FirstOrDefaultAsync(mp => mp.Id == mealPlanId);

    if (mealPlan == null)
        throw new NotFoundException("Meal Plan nicht gefunden");

    // 2. Prüfen: Alle Rezepte gehören dem Verkäufer
    var recipes = mealPlan.DayPlans
        .SelectMany(dp => dp.RecipeSelections)
        .Select(rs => rs.Recipe)
        .Where(r => r != null)
        .ToList();

    foreach (var recipe in recipes)
    {
        if (recipe.CreatorHash != sellerHash)
            throw new UnauthorizedAccessException(
                $"Rezept '{recipe.Title}' gehört nicht dem Verkäufer"
            );
    }

    // 3. Listing erstellen
    var listing = new MealPlanListing
    {
        SellerHash = sellerHash,
        MealPlanId = mealPlanId,
        Title = title,
        Description = description,
        Price = price,
        SellerWalletAddress = walletAddress,
        SellerWalletAddressUSDT = usdtWalletAddress,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        SoldCount = 0
    };

    await _context.MealPlanListings.AddAsync(listing);
    await _context.SaveChangesAsync();

    _logger.LogInformation(
        "Listing erstellt: ID={ListingId}, Verkäufer={SellerHash}, Preis={Price}",
        listing.Id, sellerHash, price
    );

    return listing;
}
```

#### FinalizeWorldChainPurchaseAsync()

```csharp
public async Task<MealPlanPurchase> FinalizeWorldChainPurchaseAsync(
    string buyerHash,
    int listingId,
    string txHash,
    string walletAddress,
    string paymentToken)
{
    // 1. Listing laden
    var listing = await _context.MealPlanListings
        .Include(l => l.MealPlan)
        .FirstOrDefaultAsync(l => l.Id == listingId);

    if (listing == null)
        throw new NotFoundException("Listing nicht gefunden");

    if (!listing.IsActive)
        throw new InvalidOperationException("Listing ist nicht aktiv");

    // 2. Selbstkauf verhindern (außer TestMode)
    bool testMode = _configuration.GetValue<bool>("WorldChain:TestMode");
    if (!testMode && listing.SellerHash == buyerHash)
        throw new InvalidOperationException("Selbstkauf nicht erlaubt");

    // 3. Duplicate Check (UNIQUE Index wirft Exception bei Duplikat)
    var existingPurchase = await _context.MealPlanPurchases
        .FirstOrDefaultAsync(p => p.ReferenceTxHash == txHash);

    if (existingPurchase != null)
        throw new InvalidOperationException("Transaktion bereits verarbeitet");

    // 4. Meal Plan für Käufer kopieren
    var copiedPlan = await _mealPlanService.CopyMealPlanAsync(
        listing.MealPlanId,
        buyerHash,
        $"{listing.Title} (gekauft am {DateTime.Now:dd.MM.yyyy})"
    );

    // 5. 80/20 Split berechnen
    decimal creatorAmount = listing.Price * 0.8m;
    decimal platformFee = listing.Price * 0.2m;

    // 6. Purchase-Datensatz erstellen
    var purchase = new MealPlanPurchase
    {
        BuyerHash = buyerHash,
        ListingId = listingId,
        PricePaid = listing.Price,
        CreatorAmount = creatorAmount,
        PlatformFee = platformFee,
        ReferenceTxHash = txHash,
        PaymentToken = paymentToken,
        BuyerWalletAddress = walletAddress,
        SellerWalletAddress = listing.SellerWalletAddress,
        CreatedMealPlanId = copiedPlan.Id,
        PurchaseDate = DateTime.UtcNow
    };

    // 7. SoldCount erhöhen
    listing.SoldCount++;

    // 8. Datenbank-Transaktion
    await _context.MealPlanPurchases.AddAsync(purchase);
    await _context.SaveChangesAsync();

    _logger.LogInformation(
        "Kauf abgeschlossen: Käufer={BuyerHash}, Listing={ListingId}, " +
        "Preis={Price}, TxHash={TxHash}",
        buyerHash, listingId, listing.Price, txHash
    );

    // 9. Async On-Chain Verifizierung (fire-and-forget)
    _ = Task.Run(async () =>
    {
        bool verified = await VerifyTransactionOnChainAsync(txHash);
        _logger.LogInformation(
            "TX-Verifizierung: {TxHash} = {Result}",
            txHash, verified ? "SUCCESS" : "FAILED"
        );
    });

    return purchase;
}
```

### 6.3 IAuthService

**Datei:** `Areas/WorldMiniApp/Services/AuthService.cs`

**Interface:**

```csharp
public interface IAuthService
{
    Task<WorldcoinVerificationResult> VerifyProofWithWorldcoin(
        VerifyRequestDto verifyRequest
    );
}
```

**Implementierung:**

```csharp
public class AuthService : IAuthService
{
    private const string APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
    private const string VERIFY_URL =
        "https://developer.worldcoin.org/api/v2/verify/{APP_ID}";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;

    public async Task<WorldcoinVerificationResult> VerifyProofWithWorldcoin(
        VerifyRequestDto verifyRequest)
    {
        try
        {
            // API-Request erstellen
            var request = new
            {
                nullifier_hash = verifyRequest.NullifierHash,
                merkle_root = verifyRequest.MerkleRoot,
                proof = verifyRequest.Proof,
                verification_level = verifyRequest.VerificationLevel,
                action = verifyRequest.Action,
                signal = verifyRequest.Signal ?? ""
            };

            // Worldcoin API aufrufen
            var url = VERIFY_URL.Replace("{APP_ID}", APP_ID);
            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Worldcoin API Error: Status={Status}, Body={Body}",
                    response.StatusCode, errorContent
                );

                return new WorldcoinVerificationResult
                {
                    Success = false,
                    ErrorMessage = "Verifizierung fehlgeschlagen"
                };
            }

            var result = await response.Content
                .ReadFromJsonAsync<WorldcoinApiResponse>();

            if (result?.Success == true)
            {
                _logger.LogInformation(
                    "World ID verifiziert: Hash={Hash}, Level={Level}",
                    verifyRequest.NullifierHash,
                    verifyRequest.VerificationLevel
                );

                return new WorldcoinVerificationResult
                {
                    Success = true,
                    NullifierHash = verifyRequest.NullifierHash,
                    VerificationLevel = verifyRequest.VerificationLevel
                };
            }

            return new WorldcoinVerificationResult
            {
                Success = false,
                ErrorMessage = result?.Detail ?? "Unbekannter Fehler"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception bei World ID Verifizierung");
            return new WorldcoinVerificationResult
            {
                Success = false,
                ErrorMessage = "Serverfehler"
            };
        }
    }
}
```

**DTO:**

```csharp
public class VerifyRequestDto
{
    public string NullifierHash { get; set; } = null!;
    public string MerkleRoot { get; set; } = null!;
    public string Proof { get; set; } = null!;
    public string VerificationLevel { get; set; } = null!; // "orb" oder "device"
    public string Action { get; set; } = "login";
    public string? Signal { get; set; }
}

public class WorldcoinVerificationResult
{
    public bool Success { get; set; }
    public string? NullifierHash { get; set; }
    public string? VerificationLevel { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### 6.4 IWorldAppMealPlanService

**Datei:** `Areas/WorldMiniApp/Services/WorldAppMealPlanService.cs`

**Wichtigste Methode: CopyMealPlanAsync()**

```csharp
public async Task<WorldUserMealPlan> CopyMealPlanAsync(
    int sourceMealPlanId,
    string targetUserHash,
    string newTitle)
{
    // 1. Source Meal Plan laden
    var source = await _context.WorldUserMealPlans
        .Include(mp => mp.DayPlans)
            .ThenInclude(dp => dp.RecipeSelections)
                .ThenInclude(rs => rs.Recipe)
        .FirstOrDefaultAsync(mp => mp.Id == sourceMealPlanId);

    if (source == null)
        throw new NotFoundException("Meal Plan nicht gefunden");

    // 2. Neuen Meal Plan erstellen
    var newPlan = new WorldUserMealPlan
    {
        UserHash = targetUserHash,
        Title = newTitle,
        Description = source.Description,
        DurationDays = source.DurationDays,
        TotalCalories = source.TotalCalories,
        CreatedAt = DateTime.UtcNow,
        DayPlans = new List<WorldUserDayPlan>()
    };

    // 3. Day Plans kopieren
    foreach (var sourceDayPlan in source.DayPlans.OrderBy(dp => dp.DayNumber))
    {
        var newDayPlan = new WorldUserDayPlan
        {
            DayNumber = sourceDayPlan.DayNumber,
            DayTitle = sourceDayPlan.DayTitle,
            RecipeSelections = new List<WorldUserRecipeSelection>()
        };

        // 4. Rezept-Auswahl kopieren
        foreach (var sourceSelection in sourceDayPlan.RecipeSelections)
        {
            var newSelection = new WorldUserRecipeSelection
            {
                RecipeId = sourceSelection.RecipeId,
                MealType = sourceSelection.MealType, // Frühstück, Mittagessen, etc.
                Servings = sourceSelection.Servings
            };

            newDayPlan.RecipeSelections.Add(newSelection);
        }

        newPlan.DayPlans.Add(newDayPlan);
    }

    // 5. Speichern
    await _context.WorldUserMealPlans.AddAsync(newPlan);
    await _context.SaveChangesAsync();

    _logger.LogInformation(
        "Meal Plan kopiert: Source={SourceId}, New={NewId}, User={UserHash}",
        sourceMealPlanId, newPlan.Id, targetUserHash
    );

    return newPlan;
}
```

---

## 7. Controller und API-Endpunkte

### 7.1 MarketplaceController - Vollständige API-Referenz

**Datei:** `Areas/WorldMiniApp/Controllers/MarketplaceController.cs`

#### GET /WorldMiniApp/Marketplace/Index

**Zweck:** Marketplace-Übersicht anzeigen

```csharp
[HttpGet]
public async Task<IActionResult> Index(
    string? search = null,
    string? sort = "recent")
{
    var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);

    // Aktive Listings laden
    var query = _context.MealPlanListings
        .Include(l => l.Seller)
        .Include(l => l.MealPlan)
        .Where(l => l.IsActive);

    // Suchfilter
    if (!string.IsNullOrEmpty(search))
    {
        query = query.Where(l =>
            l.Title.Contains(search) ||
            l.Description.Contains(search)
        );
    }

    // Sortierung
    query = sort switch
    {
        "price_low" => query.OrderBy(l => l.Price),
        "price_high" => query.OrderByDescending(l => l.Price),
        "popular" => query.OrderByDescending(l => l.SoldCount),
        _ => query.OrderByDescending(l => l.CreatedAt)
    };

    var listings = await query.ToListAsync();

    // Ranking-Service anwenden (für personalisierte Sortierung)
    if (userHash != null)
    {
        listings = await _rankingService.ApplyPersonalizedRankingAsync(
            listings,
            userHash
        );
    }

    return View(listings);
}
```

**View:** `Areas/WorldMiniApp/Views/Marketplace/Index.cshtml`

#### POST /WorldMiniApp/Marketplace/CreateListing

**Zweck:** Neues Listing erstellen

```csharp
[HttpPost]
public async Task<IActionResult> CreateListing(
    [FromBody] CreateListingRequest request)
{
    var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
    if (userHash == null)
        return Unauthorized();

    // Validierung
    if (request.Price < 1 || request.Price > 1000)
        return BadRequest("Preis muss zwischen 1 und 1000 WLD liegen");

    if (!Regex.IsMatch(request.WalletAddress, @"^0x[a-fA-F0-9]{40}$"))
        return BadRequest("Ungültige Wallet-Adresse");

    // Service aufrufen
    var listing = await _wildCoinService.CreateListingAsync(
        sellerHash: userHash,
        mealPlanId: request.MealPlanId,
        title: request.Title,
        description: request.Description,
        price: request.Price,
        walletAddress: request.WalletAddress,
        usdtWalletAddress: request.UsdtWalletAddress
    );

    return Ok(new {
        success = true,
        listingId = listing.Id,
        message = "Listing erfolgreich erstellt"
    });
}
```

**DTO:**

```csharp
public class CreateListingRequest
{
    public int MealPlanId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string WalletAddress { get; set; } = null!;
    public string? UsdtWalletAddress { get; set; }
}
```

#### POST /WorldMiniApp/Marketplace/FinalizeWorldChainPurchase

**Zweck:** Blockchain-Zahlung abschließen

**Siehe Abschnitt 4.4 für vollständige Details**

```csharp
[HttpPost]
public async Task<IActionResult> FinalizeWorldChainPurchase(
    [FromBody] FinalizeWorldChainPurchaseRequest request)
{
    var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
    if (userHash == null)
        return Unauthorized();

    // Session-Wallet prüfen
    var sessionWallet = HttpContext.Session.GetString(
        request.PaymentToken == "USDT"
            ? "WorldWallet_USDT"
            : "WorldWallet_WLD"
    );

    if (sessionWallet != request.WalletAddress)
        return BadRequest("Wallet stimmt nicht mit Session überein");

    // TxHash validieren
    if (!Regex.IsMatch(request.TxHash, @"^0x[a-fA-F0-9]{64}$"))
        return BadRequest("Ungültiger TxHash");

    try
    {
        var purchase = await _wildCoinService.FinalizeWorldChainPurchaseAsync(
            buyerHash: userHash,
            listingId: request.ListingId,
            txHash: request.TxHash,
            walletAddress: request.WalletAddress,
            paymentToken: request.PaymentToken
        );

        return Ok(new
        {
            success = true,
            mealPlanId = purchase.CreatedMealPlanId,
            message = "Kauf erfolgreich abgeschlossen!"
        });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fehler beim Kaufabschluss");
        return StatusCode(500, "Interner Serverfehler");
    }
}
```

**DTO:**

```csharp
public class FinalizeWorldChainPurchaseRequest
{
    public int ListingId { get; set; }
    public string TxHash { get; set; } = null!;
    public string WalletAddress { get; set; } = null!;
    public string PaymentToken { get; set; } = "WLD"; // "WLD", "USDT", "USDCE"
}
```

#### GET /WorldMiniApp/Marketplace/MyListings

**Zweck:** Eigene Listings anzeigen

```csharp
[HttpGet]
public async Task<IActionResult> MyListings()
{
    var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
    if (userHash == null)
        return RedirectToAction("Login", "Auth");

    var listings = await _wildCoinService.GetMyListingsAsync(userHash);

    return View(listings);
}
```

#### POST /WorldMiniApp/Marketplace/SaveWalletAddress

**Zweck:** Wallet-Adresse in Session speichern

```csharp
[HttpPost]
public IActionResult SaveWalletAddress(
    [FromBody] SaveWalletRequest request)
{
    var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
    if (userHash == null)
        return Unauthorized();

    // Wallet-Adresse validieren
    if (!Regex.IsMatch(request.WalletAddress, @"^0x[a-fA-F0-9]{40}$"))
        return BadRequest("Ungültige Wallet-Adresse");

    // In Session speichern
    string sessionKey = request.Token == "USDT"
        ? "WorldWallet_USDT"
        : "WorldWallet_WLD";

    HttpContext.Session.SetString(sessionKey, request.WalletAddress);

    _logger.LogInformation(
        "Wallet gespeichert: User={UserHash}, Token={Token}, Wallet={Wallet}",
        userHash, request.Token, request.WalletAddress
    );

    return Ok(new { success = true });
}
```

**DTO:**

```csharp
public class SaveWalletRequest
{
    public string WalletAddress { get; set; } = null!;
    public string Token { get; set; } = "WLD"; // "WLD" oder "USDT"
}
```

### 7.2 AuthController

**Datei:** `Areas/WorldMiniApp/Controllers/AuthController.cs`

**Vollständige Endpunkte - siehe Abschnitt 3 für Details:**

- `GET /WorldMiniApp/Auth/Login`
- `GET /WorldMiniApp/Auth/Nonce`
- `POST /WorldMiniApp/Auth/VerifyAction`
- `POST /WorldMiniApp/Auth/CompleteSiwe`
- `POST /WorldMiniApp/Auth/SetTestHash` (nur Development)
- `GET /WorldMiniApp/Auth/Logout`

### 7.3 HomeController

**Datei:** `Areas/WorldMiniApp/Controllers/HomeController.cs`

#### GET /WorldMiniApp/Home/Index

**Zweck:** Dashboard & Meal Planner Hub

```csharp
[HttpGet]
public async Task<IActionResult> Index()
{
    var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
    if (userHash == null)
        return RedirectToAction("Login", "Auth");

    // User-Statistiken laden
    var user = await _context.WorldAppUsers
        .Include(u => u.MealPlans)
        .Include(u => u.Postings)
        .FirstOrDefaultAsync(u => u.UserHash == userHash);

    var viewModel = new DashboardViewModel
    {
        User = user,
        MealPlanCount = user.MealPlans.Count,
        PostingCount = user.Postings.Count,
        TotalLikes = user.Postings.Sum(p => p.LikeCount),
        RecentMealPlans = user.MealPlans
            .OrderByDescending(mp => mp.CreatedAt)
            .Take(5)
            .ToList()
    };

    return View(viewModel);
}
```

#### POST /WorldMiniApp/Home/Generated

**Zweck:** KI-Essensplan generieren

```csharp
[HttpPost]
public async Task<IActionResult> Generated(
    [FromBody] GenerateMealPlanRequest request)
{
    var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
    if (userHash == null)
        return Unauthorized();

    // KI-Service aufrufen
    var mealPlan = await _aiService.GenerateMealPlanAsync(
        userHash: userHash,
        days: request.Days,
        caloriesPerDay: request.CaloriesPerDay,
        dietaryPreferences: request.DietaryPreferences,
        excludedIngredients: request.ExcludedIngredients
    );

    return Ok(new
    {
        success = true,
        mealPlanId = mealPlan.Id,
        plan = mealPlan
    });
}
```

---

## 8. Frontend-Integration

### 8.1 JavaScript-Dateien

#### worldchain-marketplace.js

**Datei:** `wwwroot/js/worldchain-marketplace.js`

**Zweck:** Web3-Integration für Marketplace-Zahlungen

```javascript
// MiniKit initialisieren
import { MiniKit } from '@worldcoin/minikit-js';

const MARKETPLACE_CONTRACT = "0x9891a521dbefb815a98e5be9256cd82524fffb5d";
const WLD_TOKEN = "0x163f8C2467924be0ae7B5347228CABF260318753";

// Wallet verbinden
async function connectWallet(tokenType = 'WLD') {
    try {
        // Wallet-Adresse abrufen
        const walletAddress = await MiniKit.getWalletAddress();

        // An Backend senden
        const response = await fetch('/WorldMiniApp/Marketplace/SaveWalletAddress', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                walletAddress: walletAddress,
                token: tokenType
            })
        });

        if (response.ok) {
            showNotification('Wallet verbunden!', 'success');
            return walletAddress;
        }
    } catch (error) {
        console.error('Wallet-Verbindung fehlgeschlagen:', error);
        showNotification('Wallet-Verbindung fehlgeschlagen', 'error');
    }
}

// Meal Plan kaufen
async function purchaseMealPlan(listingId, price, tokenType = 'WLD') {
    try {
        // 1. Wallet verbinden
        const walletAddress = await connectWallet(tokenType);
        if (!walletAddress) return;

        // 2. Token-Adresse bestimmen
        const tokenAddress = tokenType === 'WLD' ? WLD_TOKEN : null;

        // 3. Zahlung durchführen
        const paymentPayload = {
            to: MARKETPLACE_CONTRACT,
            value: (price * 1e18).toString(), // Wei-Konvertierung
            token: tokenAddress,
            reference: `listing-${listingId}`
        };

        showNotification('Zahlung wird vorbereitet...', 'info');

        const paymentResult = await MiniKit.pay(paymentPayload);

        if (!paymentResult.success) {
            throw new Error('Zahlung abgebrochen');
        }

        // 4. TxHash extrahieren
        const txHash = paymentResult.txHash || paymentResult.paymentReference;

        showNotification('Kauf wird finalisiert...', 'info');

        // 5. Backend-Finalisierung
        const response = await fetch('/WorldMiniApp/Marketplace/FinalizeWorldChainPurchase', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                listingId: listingId,
                txHash: txHash,
                walletAddress: walletAddress,
                paymentToken: tokenType
            })
        });

        const result = await response.json();

        if (result.success) {
            showNotification('Kauf erfolgreich! Meal Plan wird geladen...', 'success');

            // 6. Zum gekauften Meal Plan weiterleiten
            setTimeout(() => {
                window.location.href = `/WorldMiniApp/Home/WorldPlan?id=${result.mealPlanId}`;
            }, 2000);
        } else {
            throw new Error(result.message || 'Kauf fehlgeschlagen');
        }
    } catch (error) {
        console.error('Kauffehler:', error);
        showNotification('Kauf fehlgeschlagen: ' + error.message, 'error');
    }
}

// Event-Listener für Buy-Buttons
document.querySelectorAll('.btn-buy-listing').forEach(button => {
    button.addEventListener('click', async (e) => {
        const listingId = e.target.dataset.listingId;
        const price = parseFloat(e.target.dataset.price);
        const tokenType = e.target.dataset.token || 'WLD';

        await purchaseMealPlan(listingId, price, tokenType);
    });
});

// Notification-Helper
function showNotification(message, type) {
    // Toast-Notification anzeigen
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.textContent = message;
    document.body.appendChild(toast);

    setTimeout(() => toast.remove(), 3000);
}
```

**Verwendung im View:**

```html
<!-- Areas/WorldMiniApp/Views/Marketplace/Index.cshtml -->
<div class="listing-card" data-listing-id="@listing.Id">
    <h3>@listing.Title</h3>
    <p>@listing.Description</p>
    <p class="price">@listing.Price WLD</p>

    <button class="btn btn-primary btn-buy-listing"
            data-listing-id="@listing.Id"
            data-price="@listing.Price"
            data-token="WLD">
        Jetzt kaufen
    </button>
</div>

<script src="~/js/worldchain-marketplace.js"></script>
```

#### worldminiapp-i18n.js

**Datei:** `wwwroot/js/worldminiapp-i18n.js`

**Zweck:** Internationalisierung (Deutsch/Englisch)

```javascript
const translations = {
    de: {
        'marketplace.title': 'Marketplace',
        'marketplace.buy': 'Kaufen',
        'marketplace.price': 'Preis',
        'marketplace.sold': 'Verkauft',
        'auth.login': 'Anmelden',
        'auth.connect_wallet': 'Wallet verbinden',
        'feed.like': 'Gefällt mir',
        'feed.comment': 'Kommentieren'
    },
    en: {
        'marketplace.title': 'Marketplace',
        'marketplace.buy': 'Buy',
        'marketplace.price': 'Price',
        'marketplace.sold': 'Sold',
        'auth.login': 'Login',
        'auth.connect_wallet': 'Connect Wallet',
        'feed.like': 'Like',
        'feed.comment': 'Comment'
    }
};

function t(key) {
    const locale = document.documentElement.lang || 'de';
    return translations[locale]?.[key] || key;
}

// Auto-Translation für data-i18n Attribute
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.dataset.i18n;
        el.textContent = t(key);
    });
});
```

### 8.2 Razor Views

#### Marketplace Index View

**Datei:** `Areas/WorldMiniApp/Views/Marketplace/Index.cshtml`

```cshtml
@model List<MealPlanListing>

@{
    ViewData["Title"] = "Marketplace";
}

<div class="marketplace-container">
    <div class="marketplace-header">
        <h1>🛒 Marketplace</h1>
        <p>Kaufe Premium-Essenspläne von Top-Creators</p>

        @if (User.Identity?.IsAuthenticated == true)
        {
            <a href="@Url.Action("Sell", "Marketplace")"
               class="btn btn-success">
                ➕ Eigenen Plan verkaufen
            </a>
        }
    </div>

    <!-- Filter & Suche -->
    <div class="marketplace-filters">
        <input type="text"
               id="search"
               placeholder="Suche..."
               class="form-control">

        <select id="sort" class="form-select">
            <option value="recent">Neueste</option>
            <option value="popular">Beliebteste</option>
            <option value="price_low">Preis aufsteigend</option>
            <option value="price_high">Preis absteigend</option>
        </select>
    </div>

    <!-- Listing-Grid -->
    <div class="listings-grid">
        @foreach (var listing in Model)
        {
            <div class="listing-card">
                <!-- Thumbnail -->
                @if (listing.MealPlan?.ThumbnailUrl != null)
                {
                    <img src="@listing.MealPlan.ThumbnailUrl"
                         alt="@listing.Title"
                         class="listing-thumbnail">
                }

                <!-- Info -->
                <div class="listing-info">
                    <h3>@listing.Title</h3>
                    <p class="listing-description">@listing.Description</p>

                    <div class="listing-meta">
                        <span class="creator">
                            👤 @listing.Seller.DisplayName
                        </span>
                        <span class="sold-count">
                            ✅ @listing.SoldCount verkauft
                        </span>
                    </div>

                    <div class="listing-price">
                        <span class="price">@listing.Price WLD</span>
                        <button class="btn btn-primary btn-buy-listing"
                                data-listing-id="@listing.Id"
                                data-price="@listing.Price"
                                data-token="WLD">
                            Kaufen
                        </button>
                    </div>
                </div>
            </div>
        }
    </div>
</div>

@section Scripts {
    <script src="~/js/worldchain-marketplace.js"></script>
}
```

#### Sell View (Listing erstellen)

**Datei:** `Areas/WorldMiniApp/Views/Marketplace/Sell.cshtml`

```cshtml
@model List<WorldUserMealPlan>

@{
    ViewData["Title"] = "Plan verkaufen";
}

<div class="sell-container">
    <h1>💰 Meal Plan verkaufen</h1>

    <form id="create-listing-form">
        <!-- Meal Plan auswählen -->
        <div class="form-group">
            <label>Wähle einen Meal Plan:</label>
            <select id="meal-plan-select" class="form-select" required>
                <option value="">-- Bitte wählen --</option>
                @foreach (var plan in Model)
                {
                    <option value="@plan.Id">
                        @plan.Title (@plan.DurationDays Tage)
                    </option>
                }
            </select>
        </div>

        <!-- Titel -->
        <div class="form-group">
            <label>Listing-Titel:</label>
            <input type="text"
                   id="listing-title"
                   class="form-control"
                   maxlength="200"
                   required>
        </div>

        <!-- Beschreibung -->
        <div class="form-group">
            <label>Beschreibung:</label>
            <textarea id="listing-description"
                      class="form-control"
                      rows="4"
                      maxlength="2000"></textarea>
        </div>

        <!-- Preis -->
        <div class="form-group">
            <label>Preis (WLD):</label>
            <input type="number"
                   id="listing-price"
                   class="form-control"
                   min="1"
                   max="1000"
                   step="0.01"
                   required>
            <small class="form-text text-muted">
                Du erhältst 80% des Verkaufspreises
            </small>
        </div>

        <!-- Wallet-Adresse -->
        <div class="form-group">
            <label>WLD Wallet-Adresse (für Zahlungen):</label>
            <input type="text"
                   id="wallet-address-wld"
                   class="form-control"
                   pattern="^0x[a-fA-F0-9]{40}$"
                   required>
        </div>

        <div class="form-group">
            <label>USDT Wallet-Adresse (optional):</label>
            <input type="text"
                   id="wallet-address-usdt"
                   class="form-control"
                   pattern="^0x[a-fA-F0-9]{40}$">
        </div>

        <!-- Submit -->
        <button type="submit" class="btn btn-primary btn-lg">
            Listing erstellen
        </button>
    </form>
</div>

@section Scripts {
    <script>
        document.getElementById('create-listing-form').addEventListener('submit', async (e) => {
            e.preventDefault();

            const data = {
                mealPlanId: parseInt(document.getElementById('meal-plan-select').value),
                title: document.getElementById('listing-title').value,
                description: document.getElementById('listing-description').value,
                price: parseFloat(document.getElementById('listing-price').value),
                walletAddress: document.getElementById('wallet-address-wld').value,
                usdtWalletAddress: document.getElementById('wallet-address-usdt').value || null
            };

            try {
                const response = await fetch('/WorldMiniApp/Marketplace/CreateListing', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });

                const result = await response.json();

                if (result.success) {
                    alert('Listing erfolgreich erstellt!');
                    window.location.href = '/WorldMiniApp/Marketplace/MyListings';
                } else {
                    alert('Fehler: ' + result.message);
                }
            } catch (error) {
                console.error(error);
                alert('Fehler beim Erstellen des Listings');
            }
        });
    </script>
}
```

---

## 9. Externe Integrationen

### 9.1 Worldcoin (World ID)

**API-Dokumentation:** https://developer.worldcoin.org/docs

**Endpoint:** `https://developer.worldcoin.org/api/v2/verify/{APP_ID}`

**App ID:** `app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa`

**Request-Format:**

```json
{
  "nullifier_hash": "0xabc123...",
  "merkle_root": "0xdef456...",
  "proof": "0x...",
  "verification_level": "orb",
  "action": "login",
  "signal": ""
}
```

**Response-Format:**

```json
{
  "success": true,
  "detail": "Verification successful"
}
```

**Integration:** `Areas/WorldMiniApp/Services/AuthService.cs:VerifyProofWithWorldcoin()`

### 9.2 World Chain (Blockchain)

**Chain Details:**
- **Name:** World Chain
- **Chain ID:** 480
- **RPC-URL:** `https://worldchain-mainnet.g.alchemy.com/public`
- **Explorer:** https://worldscan.org

**WLD Token:**
- **Adresse:** `0x163f8C2467924be0ae7B5347228CABF260318753`
- **Decimals:** 18

**Integration:** `Areas/WorldMiniApp/Services/WildCoinService.cs:VerifyTransactionOnChainAsync()`

### 9.3 Azure Blob Storage

**Zweck:** Bild- und Video-Uploads

**Connection String:** Aus `appsettings.json`

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "worldminiapp-media",
    "CdnUrl": "https://DelekatesenDrehbuchCdn-beecexhdaghhacab.z01.azurefd.net"
  }
}
```

**Service:** `Areas/WorldMiniApp/Services/BlobUploadService.cs`

```csharp
public async Task<string> UploadImageAsync(
    IFormFile file,
    string userHash)
{
    // Container-Client erstellen
    var blobServiceClient = new BlobServiceClient(_connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

    // Eindeutigen Dateinamen generieren
    var fileName = $"{userHash}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
    var blobClient = containerClient.GetBlobClient(fileName);

    // Upload
    await blobClient.UploadAsync(file.OpenReadStream(), overwrite: true);

    // CDN-URL zurückgeben
    return $"{_cdnUrl}/{fileName}";
}
```

### 9.4 Bunny.net CDN

**Zweck:** Video-Hosting und Streaming

**Configuration:**

```json
{
  "BunnyNet": {
    "ApiKey": "...",
    "LibraryId": "12345",
    "StreamBaseUrl": "https://vz-12345.b-cdn.net"
  }
}
```

**Service:** `Areas/WorldMiniApp/Services/BunnyUploadService.cs`

**Webhook:** `Areas/WorldMiniApp/Controllers/BunnyWebhookController.cs` - Wird aufgerufen, wenn Video-Encoding abgeschlossen ist

---

## 10. Dateiaufruf-Kette

### 10.1 Kompletter Kaufprozess (End-to-End)

```
1. USER KLICKT "KAUFEN" BUTTON
   └─> View: Areas/WorldMiniApp/Views/Marketplace/Index.cshtml (Zeile 45)
       HTML: <button class="btn-buy-listing" data-listing-id="123">

2. JAVASCRIPT EVENT HANDLER
   └─> JavaScript: wwwroot/js/worldchain-marketplace.js:purchaseMealPlan() (Zeile 34)

       2a. Wallet verbinden
           └─> JavaScript: connectWallet() (Zeile 12)
               └─> API Call: POST /WorldMiniApp/Marketplace/SaveWalletAddress
                   └─> Controller: MarketplaceController.cs:SaveWalletAddress() (Zeile 245)
                       └─> Session: HttpContext.Session.SetString("WorldWallet_WLD", address)

       2b. Blockchain-Zahlung
           └─> JavaScript: MiniKit.pay() (Zeile 48)
               ├─> World App öffnet
               ├─> User bestätigt Zahlung
               └─> TxHash erhalten: "0xabc123..."

       2c. Backend-Finalisierung
           └─> JavaScript: fetch() (Zeile 62)
               └─> API Call: POST /WorldMiniApp/Marketplace/FinalizeWorldChainPurchase

3. CONTROLLER EMPFÄNGT REQUEST
   └─> Controller: MarketplaceController.cs:FinalizeWorldChainPurchase() (Zeile 412)

       3a. Input-Validierung
           ├─> UserHash aus Session holen
           ├─> TxHash Regex: ^0x[a-fA-F0-9]{64}$
           └─> Session-Wallet vergleichen

       3b. Service aufrufen
           └─> Service: IWildCoinService.FinalizeWorldChainPurchaseAsync()

4. WILDCOINS-SERVICE
   └─> Service: WildCoinService.cs:FinalizeWorldChainPurchaseAsync() (Zeile 456)

       4a. Listing laden
           └─> DbContext: _context.MealPlanListings
                          .Include(l => l.MealPlan)
                          .FirstOrDefaultAsync(l => l.Id == listingId)

       4b. Validierung
           ├─> Listing.IsActive? (Zeile 467)
           ├─> Kein Selbstkauf? (Zeile 472)
           └─> TxHash bereits verwendet? (Zeile 478)
               └─> Query: _context.MealPlanPurchases
                          .FirstOrDefaultAsync(p => p.ReferenceTxHash == txHash)

       4c. Meal Plan kopieren
           └─> Service: IWorldAppMealPlanService.CopyMealPlanAsync()
               └─> Service: WorldAppMealPlanService.cs:CopyMealPlanAsync() (Zeile 234)
                   ├─> Source Plan laden mit EF Core Include
                   ├─> Neuen Plan erstellen
                   ├─> DayPlans kopieren (Loop)
                   ├─> RecipeSelections kopieren (Nested Loop)
                   └─> SaveChangesAsync()

       4d. 80/20 Split berechnen (Zeile 501)
           ├─> creatorAmount = price * 0.8m
           └─> platformFee = price * 0.2m

       4e. Purchase-Datensatz erstellen (Zeile 505)
           └─> new MealPlanPurchase { ... }

       4f. SoldCount erhöhen (Zeile 520)
           └─> listing.SoldCount++

       4g. Datenbank-Transaktion (Zeile 523)
           ├─> _context.MealPlanPurchases.AddAsync(purchase)
           └─> _context.SaveChangesAsync()

       4h. Async Blockchain-Verifizierung (Zeile 530)
           └─> Task.Run(() => VerifyTransactionOnChainAsync(txHash))

5. BLOCKCHAIN-VERIFIZIERUNG (ASYNC)
   └─> Service: WildCoinService.cs:VerifyTransactionOnChainAsync() (Zeile 789)

       Retry-Loop (3 Versuche):
       └─> HTTP Client: POST https://worldchain-mainnet.g.alchemy.com/public
           Request Body:
           {
             "jsonrpc": "2.0",
             "method": "eth_getTransactionReceipt",
             "params": ["0xabc123..."],
             "id": 1
           }

           Response verarbeiten:
           ├─> Status == "0x1"? → SUCCESS
           ├─> Result == null? → RETRY (TX pending)
           └─> Else → FAILED

           Logging:
           └─> _logger.LogInformation("TX {TxHash} verifiziert: {Result}")

6. RESPONSE AN FRONTEND
   └─> Controller: MarketplaceController.cs (Zeile 445)
       └─> return Ok(new {
               success = true,
               mealPlanId = purchase.CreatedMealPlanId
           })

7. FRONTEND VERARBEITET RESPONSE
   └─> JavaScript: worldchain-marketplace.js (Zeile 78)
       ├─> showNotification("Kauf erfolgreich!")
       └─> window.location.href = `/WorldMiniApp/Home/WorldPlan?id=${mealPlanId}`

8. USER SIEHT GEKAUFTEN MEAL PLAN
   └─> Controller: HomeController.cs:WorldPlan(id) (Zeile 345)
       └─> View: Areas/WorldMiniApp/Views/Home/WorldPlan.cshtml
```

### 10.2 Authentifizierung (World ID)

```
1. USER KLICKT "MIT WORLD ID ANMELDEN"
   └─> View: Areas/WorldMiniApp/Views/Auth/Login.cshtml (Zeile 23)

2. WORLD APP ÖFFNET
   └─> JavaScript: MiniKit SDK (external)
       └─> Biometrische Verifizierung (Orb oder Device)

3. PROOF AN BACKEND SENDEN
   └─> JavaScript: POST /WorldMiniApp/Auth/VerifyAction
       Body: {
         nullifierHash: "0x...",
         merkleRoot: "0x...",
         proof: "0x...",
         verificationLevel: "orb"
       }

4. CONTROLLER
   └─> Controller: AuthController.cs:VerifyAction() (Zeile 45)
       └─> Service: IAuthService.VerifyProofWithWorldcoin()

5. AUTH-SERVICE
   └─> Service: AuthService.cs:VerifyProofWithWorldcoin() (Zeile 34)
       └─> HTTP Call: POST https://developer.worldcoin.org/api/v2/verify/{APP_ID}
           └─> Response: { "success": true }

6. USER ERSTELLEN/AKTUALISIEREN
   └─> Controller: AuthController.cs (Zeile 56)
       └─> Service: IUserManager.CreateOrUpdateUserAsync()
           └─> Service: UserManager.cs:CreateOrUpdateUserAsync() (Zeile 23)
               ├─> Check: User existiert?
               │   └─> Query: _context.WorldAppUsers.FindAsync(userHash)
               ├─> If neu: new WorldAppUser()
               ├─> If existiert: LastLoginAt updaten
               └─> SaveChangesAsync()

7. SESSION SETZEN
   └─> Controller: AuthController.cs (Zeile 67)
       └─> HttpContext.Session.SetString("WorldMiniAppUserHash", userHash)

8. REDIRECT ZU DASHBOARD
   └─> return Ok(new { redirectUrl = "/WorldMiniApp/Home/Index" })
```

### 10.3 Listing erstellen

```
1. USER FÜLLT FORMULAR AUS
   └─> View: Areas/WorldMiniApp/Views/Marketplace/Sell.cshtml

2. FORMULAR ABSENDEN
   └─> JavaScript: Form Submit Handler (Zeile 78)
       └─> API Call: POST /WorldMiniApp/Marketplace/CreateListing

3. CONTROLLER
   └─> Controller: MarketplaceController.cs:CreateListing() (Zeile 156)

       3a. Validierung
           ├─> UserHash vorhanden?
           ├─> Preis: 1-1000?
           └─> Wallet Format: ^0x[a-fA-F0-9]{40}$

       3b. Service aufrufen
           └─> Service: IWildCoinService.CreateListingAsync()

4. WILDCOINS-SERVICE
   └─> Service: WildCoinService.cs:CreateListingAsync() (Zeile 234)

       4a. Meal Plan laden
           └─> DbContext: _context.WorldUserMealPlans
                          .Include(mp => mp.DayPlans)
                              .ThenInclude(dp => dp.RecipeSelections)
                                  .ThenInclude(rs => rs.Recipe)
                          .FirstOrDefaultAsync(mp => mp.Id == mealPlanId)

       4b. Ownership-Check
           └─> Loop: foreach (var recipe in recipes)
               └─> if (recipe.CreatorHash != sellerHash)
                   └─> throw UnauthorizedAccessException

       4c. Listing erstellen
           └─> new MealPlanListing { ... }

       4d. Speichern
           ├─> _context.MealPlanListings.AddAsync(listing)
           └─> _context.SaveChangesAsync()

5. RESPONSE
   └─> return Ok(new { success = true, listingId = listing.Id })

6. REDIRECT
   └─> window.location.href = "/WorldMiniApp/Marketplace/MyListings"
```

---

## 11. Sicherheitskonzepte

### 11.1 Authentifizierung & Autorisierung

**Session-basierte Auth:**
- User Hash in Session gespeichert
- Session-Cookie: `WorldMiniAppUserHash`
- Timeout: 24 Stunden (konfigurierbar)

**Worldcoin Proof Verifizierung:**
- Zero-Knowledge-Proof wird an Worldcoin API gesendet
- Backend speichert NIEMALS biometrische Daten
- Nur NullifierHash wird gespeichert (anonymisiert)

**Wallet-Signatur (SIWE):**
- Nonce-basierter Replay-Schutz
- Nonce wird nach Verwendung invalidiert
- Signatur-Verifizierung mit Ethereum-Kryptographie

### 11.2 Payment Security

**Duplicate Prevention:**
```sql
-- UNIQUE Index verhindert doppelte Transaktionen
CREATE UNIQUE INDEX UX_MealPlanPurchases_ReferenceTxHash_NotNull
    ON MealPlanPurchases(ReferenceTxHash)
    WHERE ReferenceTxHash IS NOT NULL;
```

**Wallet-Verifizierung:**
```csharp
// Session-Wallet muss mit Request-Wallet übereinstimmen
var sessionWallet = HttpContext.Session.GetString("WorldWallet_WLD");
if (sessionWallet != request.WalletAddress)
    return BadRequest("Wallet mismatch");
```

**Self-Purchase Prevention:**
```csharp
// Verkäufer kann nicht selbst kaufen (außer TestMode)
if (!testMode && listing.SellerHash == buyerHash)
    throw new InvalidOperationException("Self-purchase not allowed");
```

**Input Validation:**
```csharp
// TxHash Format
if (!Regex.IsMatch(txHash, @"^0x[a-fA-F0-9]{64}$"))
    return BadRequest("Invalid TxHash");

// Wallet Format
if (!Regex.IsMatch(wallet, @"^0x[a-fA-F0-9]{40}$"))
    return BadRequest("Invalid wallet address");

// Price Range
if (price < 1 || price > 1000)
    return BadRequest("Price must be between 1 and 1000 WLD");
```

### 11.3 Database Security

**SQL Injection Prevention:**
- Entity Framework Core (parametrisierte Queries)
- KEINE Raw-SQL Queries mit String-Konkatenation

**Foreign Key Constraints:**
```sql
CONSTRAINT FK_MealPlanPurchases_Buyer
    FOREIGN KEY (BuyerHash)
    REFERENCES WorldAppUser(UserHash)
    ON DELETE RESTRICT;
```

**Indexes für Performance & Security:**
- UNIQUE Index auf TxHash (Duplicate Prevention)
- Index auf UserHash (schnelle Lookups)
- Composite Indexes für häufige Queries

### 11.4 Secrets Management

**Hardcoded Werte (TODO: In appsettings.json verschieben):**

```csharp
// ❌ NICHT SICHER - In Code hardcoded
private const string APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
private const string RPC_URL = "https://worldchain-mainnet.g.alchemy.com/public";

// ✅ BESSER - Aus Configuration laden
private readonly string _appId;
private readonly string _rpcUrl;

public AuthService(IConfiguration configuration)
{
    _appId = configuration["Worldcoin:AppId"];
    _rpcUrl = configuration["WorldChain:RpcUrl"];
}
```

**appsettings.json (mit Environment Variables):**

```json
{
  "Worldcoin": {
    "AppId": "${WORLDCOIN_APP_ID}",
    "ActionId": "${WORLDCOIN_ACTION_ID}"
  },
  "WorldChain": {
    "RpcUrl": "${WORLDCHAIN_RPC_URL}",
    "MarketplaceContractAddress": "${MARKETPLACE_CONTRACT}"
  },
  "AzureBlobStorage": {
    "ConnectionString": "${AZURE_STORAGE_CONNECTION}"
  }
}
```

### 11.5 Rate Limiting

**Video-Upload:**
```csharp
// BlobUploadService.cs
public async Task<string> UploadVideoAsync(...)
{
    // Max 5 Uploads pro 10 Minuten
    var recentUploads = await _context.WorldUserPostings
        .Where(p => p.CreatorId == userHash)
        .Where(p => p.CreationTime > DateTime.UtcNow.AddMinutes(-10))
        .CountAsync();

    if (recentUploads >= 5)
        throw new RateLimitException("Too many uploads");

    // ...
}
```

**TODO: API Rate Limiting hinzufügen**

---

## 12. Deployment und Konfiguration

### 12.1 Produktions-Checkliste

**Secrets aus Code entfernen:**
- [ ] World ID App ID → appsettings.json
- [ ] World Chain RPC URL → appsettings.json
- [ ] Super Admin Hash → appsettings.json
- [ ] CDN Domains → appsettings.json

**Test-Mode deaktivieren:**
```json
{
  "WorldChain": {
    "TestMode": false,
    "AllowSelfPurchaseForTesting": false
  }
}
```

**Datenbank:**
- [ ] Alle Migrationen ausgeführt (001-005)
- [ ] Indexes erstellt
- [ ] Backups konfiguriert
- [ ] Connection String mit SSL

**Azure Services:**
- [ ] Blob Storage Container erstellt
- [ ] CDN konfiguriert
- [ ] Queue für Video-Processing
- [ ] CORS richtig gesetzt

**Blockchain:**
- [ ] Smart Contract deployed (World Chain Mainnet)
- [ ] Contract-Adresse in Config
- [ ] Alchemy RPC Account (mit Rate Limits)

**Monitoring:**
- [ ] Application Insights aktiviert
- [ ] Logging-Level: Information (Production)
- [ ] Error-Tracking eingerichtet

### 12.2 Environment Variables

**Windows (PowerShell):**
```powershell
$env:WORLDCOIN_APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa"
$env:WORLDCHAIN_RPC_URL = "https://worldchain-mainnet.g.alchemy.com/public"
$env:AZURE_STORAGE_CONNECTION = "DefaultEndpointsProtocol=https;..."
```

**Linux/Mac:**
```bash
export WORLDCOIN_APP_ID="app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa"
export WORLDCHAIN_RPC_URL="https://worldchain-mainnet.g.alchemy.com/public"
export AZURE_STORAGE_CONNECTION="DefaultEndpointsProtocol=https;..."
```

**Azure App Service:**
- Configuration → Application Settings
- Alle Secrets als Environment Variables hinzufügen

### 12.3 Datenbank-Migrationen ausführen

**SQL-Dateien in Reihenfolge:**

```bash
# 1. Marketplace-Tables erstellen
sqlcmd -S your-server.database.windows.net -d DelikatessenDB -U admin -P password -i sql/001_add_wildcoin_marketplace.sql

# 2. WorldChain Purchase Fields
sqlcmd -S your-server.database.windows.net -d DelikatessenDB -U admin -P password -i sql/002_add_worldchain_purchase_fields.sql

# 3. Payment Token Field
sqlcmd -S your-server.database.windows.net -d DelikatessenDB -U admin -P password -i sql/003_add_payment_token_field.sql

# 4. USDT Wallet
sqlcmd -S your-server.database.windows.net -d DelikatessenDB -U admin -P password -i sql/004_add_listing_usdt_wallet.sql

# 5. Audit Columns
sqlcmd -S your-server.database.windows.net -d DelikatessenDB -U admin -P password -i sql/005_add_missing_purchase_audit_columns.sql
```

**Oder mit EF Core Migrations:**

```bash
cd DelikatessenDrehbuch
dotnet ef database update
```

### 12.4 Smart Contract Deployment

**Datei:** `Contracts/scripts/deploy-worldchain-marketplace.js`

```bash
# Hardhat installieren
npm install --save-dev hardhat @nomiclabs/hardhat-ethers ethers

# Contract kompilieren
npx hardhat compile

# Auf World Chain Testnet deployen
npx hardhat run scripts/deploy-worldchain-marketplace.js --network worldchain-testnet

# Auf World Chain Mainnet deployen
npx hardhat run scripts/deploy-worldchain-marketplace.js --network worldchain-mainnet
```

**hardhat.config.js:**

```javascript
module.exports = {
  solidity: "0.8.20",
  networks: {
    "worldchain-mainnet": {
      url: "https://worldchain-mainnet.g.alchemy.com/public",
      chainId: 480,
      accounts: [process.env.DEPLOYER_PRIVATE_KEY]
    }
  }
};
```

### 12.5 Konfigurationsdatei

**appsettings.Production.json:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "${AZURE_SQL_CONNECTION_STRING}"
  },
  "Worldcoin": {
    "AppId": "${WORLDCOIN_APP_ID}",
    "ActionId": "login",
    "VerifyUrl": "https://developer.worldcoin.org/api/v2/verify/${WORLDCOIN_APP_ID}"
  },
  "WorldChain": {
    "ChainId": "480",
    "RpcUrl": "${WORLDCHAIN_RPC_URL}",
    "WldTokenAddress": "0x163f8C2467924be0ae7B5347228CABF260318753",
    "UsdtTokenAddress": "0x0000000000000000000000000000000000000000",
    "MarketplaceContractAddress": "${MARKETPLACE_CONTRACT_ADDRESS}",
    "TestMode": false,
    "AllowSelfPurchaseForTesting": false
  },
  "AzureBlobStorage": {
    "ConnectionString": "${AZURE_STORAGE_CONNECTION}",
    "ContainerName": "worldminiapp-media",
    "CdnUrl": "https://DelekatesenDrehbuchCdn-beecexhdaghhacab.z01.azurefd.net"
  },
  "BunnyNet": {
    "ApiKey": "${BUNNY_API_KEY}",
    "LibraryId": "${BUNNY_LIBRARY_ID}",
    "StreamBaseUrl": "https://vz-${BUNNY_LIBRARY_ID}.b-cdn.net"
  },
  "Redis": {
    "Configuration": "${REDIS_CONNECTION_STRING}"
  }
}
```

---

## Anhang

### A. Dateien-Index (Alphabetisch)

```
Areas/WorldMiniApp/
├── Controllers/
│   ├── AdPreferencesController.cs       (Ad-Einstellungen)
│   ├── AuthController.cs                 (Authentifizierung) ⭐
│   ├── BunnyWebhookController.cs         (Video-Processing Webhooks)
│   ├── FeedController.cs                 (Social Feed)
│   ├── HomeController.cs                 (Dashboard, Meal Planner)
│   ├── MarketplaceController.cs          (Marketplace & Payments) ⭐
│   ├── MealPlanController.cs             (Meal Plan CRUD)
│   ├── RecipeController.cs               (Rezeptverwaltung)
│   ├── RecipeSwapController.cs           (Rezept-Varianten)
│   ├── WatchAnalyticsController.cs       (Video-Statistiken)
│   └── WorldMiniAppBaseController.cs     (Basis-Controller)
├── Exceptions/
│   ├── NotFoundException.cs
│   ├── RateLimitException.cs
│   └── ... (weitere Custom Exceptions)
├── Extensions/
│   └── QueryExtensions.cs
├── Models/
│   ├── Keyword.cs                        (Mehrsprachige Tags)
│   ├── MealPlanListing.cs                (Marketplace Listing) ⭐
│   ├── MealPlanPurchase.cs               (Kaufdatensatz) ⭐
│   ├── WildCoinTransaction.cs            (Transaktionshistorie)
│   ├── WorldAppUser.cs                   (Benutzer)
│   ├── WorldUserMealPlan.cs              (Essensplan)
│   ├── WorldUserPosting.cs               (Rezept-Post)
│   └── ... (60+ weitere Models)
├── Services/
│   ├── AuthService.cs                    (World ID Verifizierung) ⭐
│   ├── BackgroundTaskQueue.cs            (Async Jobs)
│   ├── BlobUploadService.cs              (Azure Blob Storage)
│   ├── BunnyUploadService.cs             (Bunny.net CDN)
│   ├── FeedAlgorithmService.cs           (Feed-Personalisierung)
│   ├── MarketplaceRankingService.cs      (Listing-Ranking)
│   ├── RecipeAiNutritionService.cs       (KI-Nährwerte)
│   ├── RecipeAiTransformService.cs       (KI-Rezept-Varianten)
│   ├── SaveNewRecipeService.cs           (Rezept speichern)
│   ├── UserManager.cs                    (User CRUD)
│   ├── WildCoinService.cs                (Marketplace & Payments) ⭐
│   ├── WorldAppMealPlanService.cs        (Meal Plan CRUD)
│   └── Interfaces/ (10+ Service-Interfaces)
└── Views/
    ├── Auth/
    │   ├── Login.cshtml
    │   └── ...
    ├── Feed/
    │   ├── Index.cshtml
    │   ├── MyProfile.cshtml
    │   └── ...
    ├── Home/
    │   ├── Index.cshtml                  (Dashboard)
    │   ├── Generator.cshtml              (AI Meal Planner)
    │   ├── ShowRecipe.cshtml
    │   ├── WorldPlan.cshtml
    │   └── ...
    ├── Marketplace/
    │   ├── Index.cshtml                  (Marketplace Übersicht) ⭐
    │   ├── Sell.cshtml                   (Listing erstellen) ⭐
    │   ├── MyListings.cshtml             (Verkäufer-Dashboard)
    │   └── ...
    └── Shared/
        ├── _Layout.cshtml
        └── ...

Contracts/
├── MealPlanMarketplaceWorldChain.sol     (Smart Contract) ⭐
└── scripts/
    └── deploy-worldchain-marketplace.js

Data/
└── ApplicationDbContext.cs               (EF Core DbContext) ⭐

sql/
├── 001_add_wildcoin_marketplace.sql      (Marketplace Tables) ⭐
├── 002_add_worldchain_purchase_fields.sql
├── 003_add_payment_token_field.sql
├── 004_add_listing_usdt_wallet.sql
└── 005_add_missing_purchase_audit_columns.sql

wwwroot/js/
├── worldchain-marketplace.js             (Web3 Integration) ⭐
├── worldminiapp-ai-resume.js
├── worldminiapp-i18n.js                  (Internationalisierung)
└── worldminiapp-notifications.js

Konfiguration:
├── appsettings.json                      (Hauptkonfiguration) ⭐
├── appsettings.Production.json
└── Program.cs                            (DI-Container Setup) ⭐

⭐ = Kritisch für Bezahlsystem
```

### B. Glossar

| Begriff | Bedeutung |
|---------|-----------|
| **World ID** | Biometrische/Gerät-basierte Identität von Worldcoin |
| **NullifierHash** | Anonymisierter eindeutiger User-Identifier (kein PII) |
| **Orb** | Biometrisches Verifizierungsgerät von Worldcoin |
| **SIWE** | Sign In With Ethereum (Wallet-Authentifizierung) |
| **WLD** | World Token (Native Token von World Chain) |
| **World Chain** | EVM-kompatible Blockchain (Chain ID 480) |
| **MiniKit** | Worldcoin JavaScript SDK |
| **TxHash** | Transaktions-Hash (Blockchain-Receipt) |
| **Meal Plan** | Essensplan mit Rezepten für mehrere Tage |
| **Listing** | Marketplace-Angebot (Meal Plan zum Verkauf) |
| **80/20 Split** | 80% an Creator, 20% an Platform |
| **WildCoin** | Interne virtuelle Währung (off-chain) |

### C. Support & Wartung

**Logging:**

Alle kritischen Operationen werden geloggt:

```csharp
_logger.LogInformation(
    "Kauf abgeschlossen: Käufer={BuyerHash}, Listing={ListingId}, Preis={Price}",
    buyerHash, listingId, price
);
```

**Application Insights Query:**

```kusto
traces
| where message contains "Kauf abgeschlossen"
| project timestamp, message, customDimensions
| order by timestamp desc
```

**Häufige Fehler:**

| Fehler | Ursache | Lösung |
|--------|---------|--------|
| "Wallet mismatch" | Session-Wallet ≠ Request-Wallet | Session neu initialisieren |
| "Transaktion bereits verarbeitet" | Doppelter TxHash | Bereits gekauft, ignorieren |
| "Listing ist nicht aktiv" | Verkäufer hat deaktiviert | Listing reaktivieren |
| "Selbstkauf nicht erlaubt" | Verkäufer = Käufer | TestMode aktivieren oder anderen Account |
| "TX-Verifizierung fehlgeschlagen" | RPC Timeout/Fehler | Nicht kritisch, läuft async |

---

**Ende der Dokumentation**

Erstellt: Mai 2026
Version: 1.0
Autor: Claude Sonnet 4.5 (basierend auf Codebase-Analyse)
