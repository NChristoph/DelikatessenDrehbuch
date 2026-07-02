# Marktplatz — Dokumentation (WorldMiniApp)

Vollständige Referenz für den **Marktplatz**: Verkaufen, Kaufen, Wallets/Token,
Zahlungsverifizierung, Erlösverteilung, Listing-Verwaltung und Sicherheit.

Der Marktplatz unterstützt **mehrere Angebotstypen** (jedes Angebot ist genau **ein** Typ):
**Essensplan**, **Einzelrezept**, **physisches Objekt**, **digitales Produkt**, **Dienstleistung**
(siehe §14). Die Bezahllogik (Modell B, 80/20, Zahlungsverifizierung) ist für alle Typen gleich.

> Verwandt: Zahlungs-/Identitäts-Hintergrund im `World-Integration-Handbuch.md` (§5/§6),
> DB-Schema in §14 dort. Diese Datei beschreibt den Marktplatz **fachlich vollständig**.

Stand: 2026-06-23

---

## Inhalt
1. [Überblick & zwei Abläufe](#1-überblick--zwei-abläufe)
2. [Datenmodell](#2-datenmodell)
3. [Verkaufen: Listing erstellen](#3-verkaufen-listing-erstellen)
4. [Wallets & Token](#4-wallets--token)
5. [Kaufen: Buy-Flow](#5-kaufen-buy-flow)
6. [Zahlungsverifizierung (Sicherheit)](#6-zahlungsverifizierung-sicherheit)
7. [Erlösverteilung / Gebühren](#7-erlösverteilung--gebühren)
8. [Listing-Verwaltung](#8-listing-verwaltung)
9. [Anzeige, Karten & Ranking](#9-anzeige-karten--ranking)
10. [Endpunkt-Referenz](#10-endpunkt-referenz)
11. [Konfiguration](#11-konfiguration)
12. [Sicherheits-Regeln (Zusammenfassung)](#12-sicherheits-regeln-zusammenfassung)
13. [Bekannte Schwächen / Platzhalter / TODO](#13-bekannte-schwächen--platzhalter--todo)
14. [Angebotstypen & Profil-Store-Link (Erweiterung)](#14-angebotstypen--profil-store-link-erweiterung)

---

## 1. Überblick & zwei Abläufe

Creators verkaufen **Essenspläne** (`WorldUserMealPlan`) gegen On-Chain-Zahlung (WLD/USDC) an andere
Nutzer. Der Käufer erhält eine **Kopie** des Plans in seinem Bereich.

```
VERKAUFEN                                   KAUFEN
─────────                                   ──────
Sell-Formular (eigene Pläne)                Index/CreatorShop (aktive Angebote)
   │ Wallet verbinden (WLD/USDT)               │ Token wählen (WLD/USDCE)
   ▼                                            ▼ "Kaufen"
POST CreateListing                          MiniKit.pay(to=PLATTFORM-Wallet, reference=listing-{id}-…)  [Modell B]
   ├─ Preis 1–1000 WLD                         │  → transaction_id
   ├─ nur EIGENE Rezepte erlaubt               ▼
   └─ MealPlanListing (IsActive)             POST FinalizeWorldChainPurchase(listingId, txHash, …)
                                                ├─ Zahlung serverseitig verifizieren (fail-closed)
                                                ├─ Plan kopieren → Käufer
                                                ├─ MealPlanPurchase (80/20 verbucht)
                                                └─ SoldCount++
```

---

## 2. Datenmodell

- **`MealPlanListing`** — Angebot (typ-übergreifend): `SellerHash`, `SellerName`, `SellerWalletAddress`,
  `SellerUsdtWalletAddress`, **`ListingType`** (Discriminator), `MealPlanId?` (→ `WorldUserMealPlan`),
  `RecipeId?`, `DigitalFileUrl?`, `StockQuantity?`, `RequiresShipping`, `CoverImageUrl?`, `ImagesJson?`,
  `Title`, `Description`, `Price`, `DayCount`, `RecipeCount`, `IsActive`, `SoldCount`, `CreatedAt`.
  (`MealPlanId` ist jetzt **nullable** — nur Essensplan-Angebote setzen ihn.)
- **`MealPlanPurchase`** — Kauf: `BuyerHash`, `BuyerWalletAddress`, `SellerHash`, `SellerWalletAddress`,
  `ListingId`, `CreatedMealPlanId?` (Käufer-Kopie bei Essensplan/Einzelrezept), **`ListingType?`**,
  **`BuyerShippingAddress?`** (nur Objekt), `PricePaid`, `CreatorAmount`, `PlatformFee`,
  `ReferenceTxHash` (**UNIQUE**), `PaymentToken`, `PurchasedAt`.
- **`WorldUserMealPlan`** — der Plan selbst (Verkäufer-Original + Käufer-Kopie; bei Einzelrezept ein 1-Rezept-Plan).
- **`MarketplaceListingType`** (Konstanten) — `MealPlan`, `SingleRecipe`, `PhysicalObject`, `DigitalProduct`, `Service`.
- **`WorldAppUser.StoreUrl?`** — optionaler externer Store-Link des Creators (Profil).

> **Schema/SQL:** Neue Spalten werden **nicht** per EF-Migration, sondern per idempotentem SQL
> ergänzt: `Sql/marketplace_listing_types.sql` (MealPlanListings + WorldMealplanPurcase) und
> `Sql/marketplace_store_url.sql` (WorldAppUser.StoreUrl). ⚠️ Beim nächsten EF-Migration-Scaffold
> müssen diese Spalten berücksichtigt werden (sonst „column already exists").

(Spalten/Typen vollständig im Handbuch §14.)

---

## 3. Verkaufen: Listing erstellen

### 3.1 Sell-Formular (`GET Sell`)
- Listet die eigenen `WorldUserMealPlan`.
- Pro Plan wird `CanSellMealPlanAsync` geprüft; **nicht verkaufbare** Pläne werden ausgeblendet
  (`BlockedMealPlanCount` zeigt deren Anzahl).
- Zeigt verbundene Wallets (aus Session).

### 3.2 `POST CreateListing(mealPlanId, title, description, price, sellerWalletAddress, sellerUsdtWalletAddress)`
Validierung (Controller):
1. Eingeloggt (`GetUserHash`).
2. **Preis 1–1000 WLD**.
3. **Mindestens eine Wallet** (WLD oder USDT) muss angegeben sein.
4. Die übergebene Verkäufer-Wallet muss mit der **in der Session gespeicherten** übereinstimmen
   (verhindert, dass man eine fremde Wallet als Empfänger setzt).

Geschäftsregeln (Service `CreateListingAsync`):
- Der **Plan muss dem Verkäufer gehören** (`WorldUserMealPlan.UserHash == sellerHash`).
- **Nur eigene Rezepte:** alle Rezepte im Plan müssen vom Verkäufer stammen
  (`WorldUserPosting.CreatorId == sellerHash`). Sonst:
  *„Du kannst nur Essenspläne verkaufen, die ausschließlich deine eigenen Rezepte enthalten."*
- `DayCount`/`RecipeCount` werden aus dem MealPlan-JSON berechnet.

> ⚠️ Diese Eigentums-Regel ist zentral: Sie verhindert, dass jemand fremde Rezepte
> (aus dem Feed) in einem Plan bündelt und verkauft.

---

## 4. Wallets & Token

- **Unterstützte Token:** `WLD`, `USDT`, `USDCE` (`AllowedWalletTokens`). On-Chain wird `USDT` zu
  **`USDCE`** gemappt (USDC.e auf World Chain); `WLD` ist der Standard.
- **Dezimalstellen:** WLD = 18, USDCE = 6 (für `token_amount` in kleinster Einheit).
- **Wallet wird pro Token in der Session gehalten**, nicht in der DB:
  - `POST SaveWalletAddress(walletAddress, token)` → `Session["WorldWallet_WLD"]` bzw. `…_USDT`.
  - `GET GetWalletAddress(token)` → liest aus der Session.
  - Quelle der Adresse: MiniKit **walletAuth** (SIWE) bzw. `connectWalletOnly` (siehe Handbuch §4).
- **Konsequenz:** Geht die Session verloren, muss die Wallet neu verbunden werden, bevor man
  verkaufen/kaufen kann.

---

## 5. Kaufen: Buy-Flow

Frontend `Views/Marketplace/Index.cshtml` → `buyListing(listingId, title, priceWld, sellerWallet)`:
1. Login-Check + Verkäufer-Wallet vorhanden.
2. Token wählen; bei `USDCE` wird der WLD/USD-Kurs gebraucht (`getActualPrice`).
3. **`MiniKit.pay`** mit `to = Plattform-Wallet` (**Modell B** — nicht der Verkäufer!),
   `reference = listing-{listingId}-{timestamp}`, voller Preis in kleinster Einheit → `transaction_id`.
4. Käufer-Wallet sicherstellen (`ensureBuyerWalletForToken`).
5. **`POST FinalizeWorldChainPurchase`** mit `listingId`, `txHash` (= `transaction_id`), `walletAddress`, `paymentToken`.
6. Backend prüft & vergibt → `location.reload()`.
- **Fallback** (kein MiniKit / Browser): ethers.js-Pfad (`worldChainMarketplace.buyListingWithWorldChain`).
  → wird von der Zahlungsverifizierung aktuell **abgelehnt** (siehe §13).

### `POST FinalizeWorldChainPurchase` (Controller)
- Login + `txHash` vorhanden + Token in `AllowedWalletTokens`.
- **Käufer-Wallet aus Session** (`WorldWallet_WLD`/`…_USDT`); fehlt sie, aber im Request vorhanden → in Session übernehmen.
- Request-Wallet muss zur Session-Wallet passen (sonst Abbruch).
- Ruft `MarketplaceService.FinalizeWorldChainPurchaseAsync(buyerHash, listingId, txHash, wallet, allowSelfPurchase, token)`.

---

## 6. Zahlungsverifizierung (Sicherheit)

`MarketplaceService.FinalizeWorldChainPurchaseAsync` → `VerifyMiniKitPaymentAsync` (**blockierend, fail-closed**).

Aufruf der **World-Payment-API**:
```
GET https://developer.worldcoin.org/api/v2/minikit/transaction/{transaction_id}?app_id={app_id}&type=payment
Authorization: Bearer {WorldApiKey}
```
Bedingungen (alle müssen erfüllt sein, sonst **kein** Kauf):
1. **HTTP 200** → Transaktion gehört zu **dieser App** (App-scoped Key) → erfundene/fremde IDs scheitern.
2. **`reference`** beginnt mit `listing-{listingId}-` → bindet die Zahlung an genau dieses Listing.
3. **`transaction_status` ≠ `failed`**.
4. **`to`** (falls geliefert) = **Plattform-Wallet** (`Marketplace:PlatformWalletAddress[Usdt]`).

Zusätzliche Schutzschichten in `FinalizeWorldChainPurchaseAsync`:
- **Listing aktiv?** sonst `null`.
- **Kein Selbstkauf** (`SellerHash == BuyerHash`), außer `AllowSelfPurchaseForTesting`.
- **Duplikat:** `ReferenceTxHash` UNIQUE → gleiche Transaktion nicht doppelt → bestehender Kauf wird zurückgegeben.
- Schalter `Marketplace:VerifyPayments=false` deaktiviert die Prüfung (NUR Test, unsicher).

> Hintergrund: Früher lief die Prüfung als fire-and-forget und der Kauf ging **immer** durch →
> „gratis via erfundener txHash". Jetzt geschlossen. Details: Handbuch §5.

---

## 7. Erlösverteilung / Gebühren

**Aktiv: Modell B** — der Käufer zahlt den **vollen Preis** an die **Plattform-Wallet** (eine
atomare Zahlung). Bei erfolgreichem, verifiziertem Kauf (in einer DB-Transaktion):
1. **Plan kopieren:** neuer `WorldUserMealPlan` für den Käufer (`CreatedMealPlanId`).
2. `SoldCount++` am Listing.
3. **Verbuchung:** `CreatorAmount = round(Price * 0.80, 6)`, `PlatformFee = Price − CreatorAmount` (≈ 20 %).
4. `MealPlanPurchase`-Eintrag schreiben.
5. **Verkäufer-Gutschrift:** `WorldAppUser.WildCoinBalance += CreatorAmount` + `WildCoinTransaction`
   (`Type="sale_credit"`, Token in `ReferenceInfo`). → die Plattform hält den vollen Betrag,
   schuldet dem Verkäufer 80 % als **internes Guthaben**.

> ℹ️ **Auszahlung an Verkäufer** erfolgt **separat** (aktuell manuell / späteres Cash-out-Feature),
> da die Plattform on-chain den vollen Betrag erhalten hat. Das Guthaben ist token-agnostisch
> (WLD-denominiert); der tatsächliche Zahl-Token steht in `WildCoinTransaction.ReferenceInfo`.

> 🔜 **Modell A (vorbereitet):** atomarer 80/20-Split per Smart Contract — Verkäufer wird sofort
> on-chain bezahlt, kein Guthaben-Rückstand. Contract + Plan: `contracts/MarketplaceSplitter.sol`
> bzw. `docs/Marktplatz-Splitter-Contract-A.md`. Noch nicht aktiv (Deploy/Audit nötig).

### 7.1 Verkäufer-Auszahlung (Cash-out)
Das interne Guthaben wird über einen **Antrag + manuelle Auszahlung** ausgezahlt
(`MarketplacePayoutRequest`, Tabelle via `Sql/marketplace_payout_requests.sql`):

1. **Antrag (Verkäufer):** `GET Marketplace/Payout` zeigt Guthaben, Verlauf und ein Formular.
   `POST Marketplace/RequestPayout(amount, token, walletAddress)` →
   `RequestPayoutAsync` prüft `amount ≤ WildCoinBalance`, **reserviert** den Betrag sofort
   (`WildCoinBalance -= amount`, `WildCoinTransaction` `payout_request`) und legt einen
   `pending`-Antrag an.
2. **Auszahlen (Admin):** `GET Admin/Payouts` listet offene Anträge. Der Admin überweist den
   Betrag **manuell aus der Plattform-Wallet** und trägt per `POST Admin/MarkPayoutPaid(payoutId, txHash)`
   den TxHash ein → Status `paid`.
3. **Ablehnen (Admin):** `POST Admin/RejectPayout(payoutId, note)` → Status `rejected`, das
   **reservierte Guthaben wird zurückgebucht** (`WildCoinTransaction` `payout_refund`).

Status-Werte: `pending` → `paid` / `rejected`. Guthaben-Ledger: `WildCoinTransaction`
(`sale_credit`, `payout_request`, `payout_refund`).

**Einstiegspunkte (UI):**
- **Verkäufer:** „Guthaben auszahlen" im **Dashboard** (`Home/UserDashboard`, Creator-Stats) und
  „Guthaben" auf **Meine Angebote** (`Marketplace/MyListings`) → führen zu `Marketplace/Payout`.
- **Admin:** Button „Auszahlungen" im **Creator Manager** (`Admin/CreatorManager`) → `Admin/Payouts`.

> Aktuell **manuelle** On-Chain-Überweisung durch den Admin (kein Plattform-Private-Key im Backend).
> Automatische Auszahlung wäre über den Node-Signer/-Service möglich (Folgeschritt) — oder direkt
> über **Modell A** (dann entfällt das Guthaben/Cash-out ganz).

---

## 8. Listing-Verwaltung

- **`MyListings`** (GET) — eigene Angebote (aktiv & inaktiv).
- **`DeactivateListing`** / **`ActivateListing`** (POST) — nur der **Eigentümer** (`SellerHash == userHash`)
  kann sein Listing (de)aktivieren (`IsActive`).
- Es gibt **kein Hard-Delete** über die UI; Angebote werden deaktiviert.

---

## 9. Anzeige, Karten & Ranking

- **`Index`** — bis zu 50 aktive Angebote, neueste zuerst; baut `PlanCardViewModel`-Karten mit
  Hero-Bildern (aus den Rezepten des Plans) + Nährwert-Summen (`BuildNutritionTotalsAsync`).
- **`CreatorShop`** — Premium-Karten-Variante.
- **`IMarketplaceRankingService`** — injiziert für Ranking/Sortierung der Angebote.
- **Nährwerte** werden pro Listing aus den enthaltenen Rezept-IDs berechnet (Kalorien/Protein/Fett/KH).

---

## 10. Endpunkt-Referenz

Alle unter `/WorldMiniApp/Marketplace/…`, Controller `MarketplaceController`.

| Methode | Endpunkt | Zweck |
|---|---|---|
| GET | `Index` | Marktplatz-Übersicht (aktive Angebote) |
| GET | `CreatorShop` | Premium-Karten-Ansicht |
| GET | `MyListings` | eigene Angebote |
| GET | `Sell` | Verkaufs-Formular (verkaufbare Pläne) |
| POST | `SaveWalletAddress(walletAddress, token)` | Wallet pro Token in Session speichern |
| GET | `GetWalletAddress(token)` | Wallet aus Session lesen |
| POST | `UploadOfferImage(file)` | Angebots-Foto zu Bunny.net hochladen → CDN-URL (für CoverImageUrl/ImagesJson) |
| POST | `CreateListing(CreateListingRequest)` | Angebot erstellen (typ-übergreifend: `ListingType` + typ-spezifische Felder) |
| POST | `DeactivateListing(listingId)` | Angebot deaktivieren (Owner) |
| POST | `ActivateListing(listingId)` | Angebot aktivieren (Owner) |
| POST | `FinalizeWorldChainPurchase(request)` | Kauf nach Zahlung finalisieren (inkl. `ShippingAddress` bei Objekt) |
| GET | `DownloadDigitalProduct(listingId)` | Digital-Download (nur nach Kauf, fail-closed) → Redirect auf `DigitalFileUrl` |
| GET | `Payout` | Cash-out-Seite (Guthaben, Verlauf, Formular) |
| POST | `RequestPayout(amount, token, walletAddress)` | Auszahlung anfordern (Guthaben reservieren) |

**Admin** (`/WorldMiniApp/Admin/…`, nur Admin-Session):

| Methode | Endpunkt | Zweck |
|---|---|---|
| GET | `Payouts` | offene Auszahlungsanträge |
| POST | `MarkPayoutPaid(payoutId, txHash)` | nach manueller Überweisung TxHash eintragen → `paid` |
| POST | `RejectPayout(payoutId, note)` | ablehnen → Guthaben zurückbuchen |

> Alle POST-Endpunkte: `[ValidateAntiForgeryToken]`. Identität via Auth-Cookie-Claim (`GetUserHash` → Basis-`ResolveUserHash`).

---

## 11. Konfiguration

| Key | Wert/Beispiel | Wofür |
|---|---|---|
| `WorldChain:ChainId` | `480` | World Chain |
| `WorldChain:WldTokenAddress` | `0x2cFc…` | WLD-Token |
| `WorldChain:UsdtTokenAddress` | `0x79A0…` | USDC.e-Token |
| `WorldChain:WethAddress` | `0x4200…` | WETH (Gas/Agent) |
| `WorldChain:MarketplaceContractAddress` | `0x9891…` | (ethers-Fallback-Pfad) |
| `WorldChain:AllowSelfPurchaseForTesting` | `false` | Selbstkauf erlauben (nur Test!) |
| `WorldChain:TestMode` | `false` | Test-Modus für die UI |
| `WorldApiKey` | *(geheim)* | **Dev-Portal-API-Key** für die Zahlungsverifizierung |
| `Marketplace:VerifyPayments` | `true` | Zahlungsprüfung an/aus (Default an) |
| `Marketplace:PlatformWalletAddress` | *(deine Wallet)* | **Modell B:** Empfänger der Käufe (WLD). **Pflicht**, sonst Kauf abgelehnt |
| `Marketplace:PlatformWalletAddressUsdt` | *(deine USDC-Wallet)* | Empfänger für USDC.e-Käufe (Fallback: WLD-Wallet) |

---

## 12. Sicherheits-Regeln (Zusammenfassung)

- **Verkaufen:** nur eigene Pläne, **nur eigene Rezepte**; Empfänger-Wallet muss zur Session-Wallet passen; Preis 1–1000.
- **Kaufen:** Zahlung **serverseitig verifiziert** (World-Payment-API, fail-closed), reference an Listing gebunden, Empfänger = Verkäufer.
- **Kein Selbstkauf** (außer Test-Flag). **Kein Doppelkauf** (UNIQUE `ReferenceTxHash`).
- **Listing (de)aktivieren** nur durch den Eigentümer.
- Identität nur aus dem **Auth-Cookie-Claim** (kein Vertrauen auf Request-`userHash`).
- `WorldApiKey` ist Pflicht — fehlt er, werden Käufe **abgelehnt** (sicher, aber blockierend).

---

## 13. Bekannte Schwächen / Platzhalter / TODO

- **Platform-Fee:** ✅ wird eingezogen (**Modell B**). **Cash-out:** ✅ vorhanden (Antrag + Admin-Auszahlung,
  §7.1), aber die On-Chain-Überweisung erfolgt **manuell** durch den Admin → die Plattform verwahrt
  Verkäufer-Gelder (Treuhand). **Offen:** automatische Auszahlung (Node-Signer) oder Umstieg auf
  **Modell A (Splitter-Contract)** — dann keine Verwahrung/manuelle Auszahlung nötig.
- **`Marketplace:PlatformWalletAddress` ist Pflicht** (Modell B): fehlt sie, werden Käufe abgelehnt (fail-closed).
- **ethers.js-Browser-Fallback** wird von der MiniKit-Verifizierung abgelehnt (kein MiniKit-`transaction_id`).
  Für die World-App-Mini-App ok; wenn der Fallback gebraucht wird, separat **streng on-chain** verifizieren
  (Empfänger/Betrag/Token). `VerifyTransactionOnChainAsync` (alt, ungenutzt) prüft nur Existenz+Status.
- **Rating ist ein Platzhalter:** `4.8` wenn `SoldCount>0`, sonst `4.6` — **kein echtes Bewertungssystem**.
- **`ActivePlannerCount` ist fake:** `max(3, SoldCount % 17 + 3)` — reine Social-Proof-Optik.
- **Wallet & Session:** Wallet-Adresse liegt nur in der **Session** (nicht persistent) → bei Session-Verlust neu verbinden.
  Bei mehreren App-Instanzen ist die In-Memory-Session nicht geteilt.
- **Preis-Token:** Listing-Preis ist in **WLD**; bei USDC.e-Kauf wird über den WLD/USD-Kurs umgerechnet
  (clientseitig) — Kursquelle/Abweichungen beachten.
- **Feldnamen der World-Payment-API** beim ersten echten Kauf verifizieren (Log `ref=/status=/to=`),
  ggf. in `VerifyMiniKitPaymentAsync` anpassen.

---

## 14. Angebotstypen & Profil-Store-Link (Erweiterung)

Stand 2026-06-23. Der Marktplatz unterstützt fünf Angebotstypen. **Jedes Angebot ist genau ein Typ**
(`MealPlanListing.ListingType`, Discriminator als String — kein EF-TPH). Erstellt wird typ-übergreifend
über `MarketplaceService.CreateTypedListingAsync(sellerHash, CreateListingInput)`; die alte
`CreateListingAsync(...)` delegiert für Abwärtskompatibilität auf den Typ `MealPlan`.

### 14.1 Typen, Pflichtfelder & Lieferung

| Typ (`ListingType`) | Pflichtfelder bei Erstellung | Eigentums-/Validierungsregel | Lieferung beim Kauf |
|---|---|---|---|
| `MealPlan` | `MealPlanId` | Plan gehört dem Verkäufer; **nur eigene Rezepte** | Plan wird beim Käufer kopiert (`CreatedMealPlanId`) |
| `SingleRecipe` | `RecipeId` | Rezept gehört dem Verkäufer (`WorldUserPosting.CreatorId`) | 1-Rezept-Plan beim Käufer (`CreatedMealPlanId`) |
| `PhysicalObject` | `CoverImageUrl` **oder** `ImagesJson` (≥1 Bild) | `StockQuantity` ≥ 0 (optional, `null`=unbegrenzt) | `BuyerShippingAddress` gespeichert, `StockQuantity−−` (clamp 0), Verkäufer-Notification |
| `DigitalProduct` | `DigitalFileUrl` (gültige http/https) | URL-Validierung (`NormalizeOptionalUrl`) | Zugriff via `DownloadDigitalProduct` (nur nach Kauf) |
| `Service` | — (nur Titel/Beschreibung) | — | Kauf erfasst, Verkäufer-Notification (Kontaktaufnahme) |

Gemeinsam für alle Typen: Preis 1–1000 WLD, mind. eine Verkäufer-Wallet, 80/20-Gutschrift,
serverseitige Zahlungsverifizierung (Modell B). Bilder/Dateien werden in Phase 1 **als URL** angegeben
(kein eigener Upload-Pfad).

### 14.2 Kauf-Flow je Typ (Frontend)

`Views/Marketplace/Index.cshtml` → `buyListing(listingId, title, priceWld, sellerWallet, listingType, requiresShipping)`:
- **Objekt mit `RequiresShipping`**: vor der Zahlung wird die **Lieferadresse** erfragt und als
  `ShippingAddress` an `FinalizeWorldChainPurchase` gesendet.
- **Digital**: nach erfolgreichem Kauf liefert das Finalize-Ergebnis eine `downloadUrl` →
  Frontend leitet zum geschützten `DownloadDigitalProduct`-Endpunkt weiter.
- Übrige Typen: wie bisher (`location.reload`).

Das Erstellungs-Formular (`Views/Marketplace/Sell.cshtml`) zeigt eine **Typ-Auswahl** und blendet die
typ-spezifischen Felder dynamisch ein (Plan-Liste / Rezept-Dropdown / Foto-Upload+Bestand+Versand /
Download-URL / nur Beschreibung).

### 14.3 `DownloadDigitalProduct(int listingId)` (GET)
Fail-closed: nur eingeloggte Nutzer, die das Listing **gekauft** haben
(`IWildCoinService.HasPurchasedAsync`) — oder der Verkäufer selbst — bekommen einen Redirect auf
`DigitalFileUrl`. Sonst `NotFound`/`Forbid`.

### 14.4 Profil-Store-Link (externer Shop)
`WorldAppUser.StoreUrl` (nullable). Setzen/Entfernen im Profil über `FeedController.UpdateProfile`
(zusätzlicher Parameter `storeUrl`, http/https-validiert, ≤1024 Zeichen; leer = löschen). Anzeige als
Quick-Link „Mein Store" auf `MyProfile` (`target="_blank" rel="noopener nofollow"`). Reiner Link —
**keine** bezahlte Werbung/Hervorhebung.

### 14.5 Verkaufsplan-Builder (eigene Rezepte)
`GET MealPlan/BuildOwn` (Einstieg: Quick-Link „Verkaufsplan erstellen" in `MyProfile`) bietet einen
Essensplan-Builder, der **ausschließlich die eigenen Rezepte** des Nutzers zur Auswahl zeigt
(inkl. privater/offline Rezepte). Tage werden mit eigenen Rezepten gefüllt; gespeichert wird über den
bestehenden `MealPlanController.SaveMealPlanFromLayout` (Format: Array `{DayIndex, RecipeId, SlotIndex}`)
als `WorldUserMealPlan`. Da nur eigene Rezepte enthalten sind, ist der Plan direkt über
`Marketplace/Sell` (Typ Essensplan) verkaufbar (`CanSellMealPlanAsync` ✓).

Der Builder bietet pro Tag **drei Mahlzeiten-Slots** (Vorspeise/Hauptgang/Dessert → `SlotIndex` 0/1/2)
und zeigt **Live-Nährwerte** über den vorhandenen `GetMealPlanNutritionTotals`:
- **Gesamtplan** (oben) **und pro Tag** (kcal + Protein je Tageskarte).
- **Umschaltung „Gesamt / Pro Portion"**: „Gesamt" rechnet mit der gewählten Personenzahl, „Pro Portion"
  mit `personCount=1` (eine Portion je Rezept).
- Berechnung gecacht + debounced; veraltete Antworten werden per Request-Token verworfen.

Der `SlotIndex` ist nur UI-Struktur — gespeichert wird wie gehabt pro Tag (`DayIndex`).

### 14.6 Offene Punkte / Grenzen
- **Bestand bei Objekt**: Da on-chain *vor* dem Finalize bezahlt wird, kann bei limitierter Stückzahl
  theoretisch übers Limit verkauft werden (Race). Aktuell wird `StockQuantity` nur auf 0 geclamped und
  der Verkäufer benachrichtigt; manuelle Abwicklung/Erstattung. Bei Bedarf: Reservierung vor Zahlung.
- **Medien**: Fotos werden direkt zu **Bunny.net** hochgeladen (CDN/Caching) statt externer Links.
  `POST Marketplace/UploadOfferImage` (IFormFile → `IBlobUploadService.UploadContentToBlob`, WEBP) liefert die
  CDN-URL; das erste Foto ist das `CoverImageUrl`, alle Fotos landen in `ImagesJson`. Die „Angebot erstellen"-
  Seite (`Sell.cshtml`) ist im **Social-Composer-Stil** umgesetzt (Handoff `Areas/Vorlagen/design_handoff_create_offer`):
  Bild-zuerst, Typ-Pills, Titel/Beschreibung, Preis+Split, Wallet-Strip, Live-Vorschau, „Teilen" oben (aktiv erst wenn gültig).
- **Dienstleistung**: keine Termin-/Buchungslogik — nur Notification an den Verkäufer.

---

*Bei Änderungen am Marktplatz-/Kauf-/Listing-Code diese Datei mitpflegen.*
