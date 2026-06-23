# World-Integration — Handbuch (DelikatessenDrehbuch / WorldMiniApp)

Vollständige Referenz für die Anbindung an **World** (World ID, World App, World Chain):
**Identität/Login**, **Käufe/Marktplatz**, **Sicherheit**, **Konfiguration**, **Deployment**
und **Troubleshooting**.

Stand: 2026-06-11 · SDKs: `@worldcoin/minikit-js@2.0.3`, `@worldcoin/idkit-core@4.1.8`

---

## Inhaltsverzeichnis
1. [Überblick & Architektur](#1-überblick--architektur)
2. [Begriffe / Glossar](#2-begriffe--glossar)
3. [Identität & Login (World ID 4.0 / IDKit)](#3-identität--login-world-id-40--idkit)
4. [Wallet-Login (SIWE) — Marktplatz](#4-wallet-login-siwe--marktplatz)
5. [Käufe / Marktplatz & Zahlungsverifizierung](#5-käufe--marktplatz--zahlungsverifizierung)
6. [Auth-/Session-Architektur (Cookie-Auth)](#6-auth-session-architektur-cookie-auth)
7. [Komponenten: der Azure-Signer (Node Function)](#7-komponenten-der-azure-signer-node-function)
8. [Konfiguration — alle Keys](#8-konfiguration--alle-keys)
9. [Dateireferenz](#9-dateireferenz)
10. [Deployment & Betrieb](#10-deployment--betrieb)
11. [Debugging (On-Screen-Overlay)](#11-debugging-on-screen-overlay)
12. [Troubleshooting / bekannte Stolpersteine](#12-troubleshooting--bekannte-stolpersteine)
13. [Sicherheits-Checkliste](#13-sicherheits-checkliste)
14. [Datenbank-Schema (World-App-Teil)](#14-datenbank-schema-world-app-teil)

---

## 1. Überblick & Architektur

Die Mini-App läuft als **World App Mini App** (Webview). Drei World-Bausteine:

| Baustein | Wofür | Technik |
|---|---|---|
| **World ID** (IDKit 4.0) | Login / "echter Mensch"-Nachweis (device/orb) | `@worldcoin/idkit-core` (Frontend) + v4-Verify (Backend) |
| **MiniKit** (v2) | App-Befehle: `walletAuth`, `pay` | `@worldcoin/minikit-js` |
| **World Chain** | Zahlungen (WLD/USDC) für den Marktplatz | MiniKit `pay()` + World-Payment-API |

```
                         ┌───────────────────────── World App (Webview) ─────────────────────────┐
                         │  minikitt.js                                                            │
                         │   ├─ IDKit (Login/Identität)    ── MiniKit.install + native Bridge      │
                         │   └─ MiniKit (walletAuth, pay)                                           │
                         └───────────────┬───────────────────────────────────────────┬────────────┘
                                         │ HTTPS                                       │
              ┌──────────────────────────▼───────────────────────┐    ┌───────────────▼─────────────────┐
              │  C#-Backend (ASP.NET Core, AuthController, …)     │    │  Azure Function "WorldIdSigner"  │
              │   ├─ /Auth/WorldIdRequestContext ───────────────────► /api/worldid/rp-context (signiert) │
              │   ├─ /Auth/VerifyWorldId ── v4-Verify ──┐         │    │   liest Key aus Key Vault         │
              │   ├─ /Auth/CompleteSiwe (Wallet-Login)  │         │    └──────────────────────────────────┘
              │   └─ /Marketplace/Finalize… (Kauf) ──┐  │
              └──────────────────────────────────────┼──┼─────────┘
                                                     │  │
                          World-Payment-API ◄────────┘  └────────► World-ID v4-Verify-API
                  (developer.worldcoin.org/api/v2/...)      (developer.world.org/api/v4/...)
```

**Warum ein separater Node-Signer?** Die IDKit-4.0-RP-Signatur (`signRequest`) gibt es nur
als Node-Paket. Der geheime Signing-Key liegt **ausschließlich** in dieser Function (aus
Key Vault) — die C#-App kennt ihn nicht.

---

## 2. Begriffe / Glossar

- **app_id** — öffentliche Mini-App-ID (`app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa`). Für MiniKit **und** als World-ID-app_id.
- **rp_id** — Relying-Party-ID aus dem Developer Portal (`rp_786841d324b59552`). World ID 4.0.
- **signing_key** — geheimer RP-Signing-Key. Nur im Node-Signer (Key Vault). Env-Name: `World_4_0_SingKey`.
- **nullifier** — RP-scoped, eindeutiger Identifier eines World-ID-Nutzers für eine Action. Wird unser **UserHash**.
- **UserHash** — interne Identität. = `nullifier` (World ID) oder Wallet-Adresse (SIWE/Wallet-Login).
- **IsVerified** — Verifizierungslevel des Users: `device`, `orb`, `wallet` oder `test`.
- **Action** — World-ID-Scope (`login-delikatessendrehbuch`).
- **rp_context** — backend-signiertes Objekt `{ rp_id, nonce, created_at, expires_at, signature }`, das IDKit zwingend braucht.
- **Preset** — IDKit-Credential-Typ: `deviceLegacy` (genutzt), `orbLegacy`, `proofOfHuman`, …
- **transaction_id** — von MiniKit `pay()` gelieferte Zahlungs-ID. Wird serverseitig bei World verifiziert.
- **reference** — vom Frontend erzeugte Zahlungsreferenz (`listing-{listingId}-{timestamp}`).

---

## 3. Identität & Login (World ID 4.0 / IDKit)

### 3.1 Hintergrund
- `@worldcoin/minikit-js` **v2** hat den `verify`-Befehl **entfernt** → World-ID-Verifizierung
  läuft jetzt über **IDKit** (`@worldcoin/idkit-core`). `commandsAsync` ist ebenfalls weg
  (Befehle direkt top-level: `MiniKit.walletAuth`, `MiniKit.pay`).
- IDKit 4.0 verlangt ein **backend-signiertes `rp_context`** (RP-Signatur) — sonst öffnet der Flow nicht.

### 3.2 Identitätsmodell
- Erfolgreicher World-ID-Proof liefert den **`nullifier`** → das ist der **UserHash** in `WorldAppUser`.
- **Login = device-level** (`deviceLegacy`). Hat praktisch jeder World-App-Nutzer.
- **orb** wird nur für **Upload/Creator** verlangt (`IsCreatorAllowedAsync` prüft `IsVerified == "orb"`;
  die eigene Hash via `WorldMiniApp:SuperUserHash` ist ausgenommen).
- Ein device-Login stuft einen bestehenden **orb**-User **nicht** herab (`UserManager.CreateOrUpdateWorldIdUserAsync`).

### 3.3 Feature → benötigtes Level
| Bereich | Level |
|---|---|
| Feed | device |
| Mein Bereich / Profil | device |
| Marktplatz | device |
| Upload / Creator werden | **orb** (eigene Hash ausgenommen) |

### 3.4 Login-Flow (Schritt für Schritt)
Code: `wwwroot/js/minikitt.js` → `startLoginProcess()` + `completeVerify()`; Backend: `AuthController`.

1. **Bridge init:** `MiniKit.install({ appId })`. **Pflicht**, sonst „MiniKit version not supported"-Schwarzbild.
2. **Request-Kontext holen:** `GET /WorldMiniApp/Auth/WorldIdRequestContext`
   → Backend holt das **signierte rp_context** vom Node-Signer und gibt zurück:
   `{ app_id, action, environment, allow_legacy_proofs, rp_context }`.
3. **IDKit-Request bauen:**
   ```js
   const request = await IDKit.request({
       app_id, action, rp_context,
       allow_legacy_proofs: true,
       environment: 'production'
   }).preset(deviceLegacy({ signal: '' }));
   ```
4. **Verifizieren:** innerhalb der World App **nicht** zur `connectorURI` navigieren
   (`isInWorldApp()` → true). `await request.pollUntilCompletion()`.
   Ergebnis: `{ success: true, result: { protocol_version, nonce, action, responses:[{ identifier, nullifier }] } }`.
   - Bei `success:false` (z. B. `credential_unavailable`, `user_rejected`) → **nicht** an den Server schicken, klare Meldung anzeigen.
5. **Server-Verify:** `POST /WorldMiniApp/Auth/VerifyWorldId` mit **nur dem inneren `result`** (nicht dem Wrapper).
   - Backend leitet den Proof an `POST https://developer.world.org/api/v4/verify/{rp_id}` weiter (`{ success: true }` = ok).
   - Liest `responses[0].nullifier` (+ `identifier`), legt/aktualisiert den User an, meldet ihn per **Auth-Cookie** an.

### 3.5 Wichtige Implementierungsdetails (nicht ändern ohne Grund)
- **idkit-core über `esm.sh`** laden (`https://esm.sh/@worldcoin/idkit-core@4.1.8`), **nicht** jsDelivr `+esm`
  (sonst WASM-Ladefehler).
- C#-IDKit-Endpunkte nutzen **Newtonsoft `JObject`** (nicht `System.Text.Json.JsonElement`) — die MVC-Pipeline
  serialisiert mit Newtonsoft; sonst kommt `rp_context` verstümmelt an („cannot convert undefined to a BigInt").
- v4-Verify-Body = **nur der innere Proof** (`result.result`).

---

## 4. Wallet-Login (SIWE) — Marktplatz

Für Marktplatz-Aktionen, die eine **Wallet-Adresse** brauchen (nicht der World-ID-Login):

- Frontend `minikitt.js` → `startWalletAuth()` / `connectWalletOnly()`:
  `MiniKit.install` → `fetchNonce()` (`GET /Auth/Nonce`) → `MiniKit.walletAuth({ nonce })`
  → `result.data` (`{ status, message, signature, address }`) → `POST /Auth/CompleteSiwe`.
- Backend `AuthController.CompleteSiwe`: prüft **SIWE-Signatur** (Nethereum `EthereumMessageSigner`),
  Nonce (Replay-Schutz), Adressen-Match. Bei Erfolg: UserHash = Wallet-Adresse, `IsVerified="wallet"`.
- `MiniKit.pay`/`walletAuth` sind **top-level** v2-API (Response-Form `{ executedWith, data }`).

---

## 5. Käufe / Marktplatz & Zahlungsverifizierung

### 5.1 Kauf-Flow
Frontend: `Views/Marketplace/Index.cshtml` → `buyListing()`; Backend: `MarketplaceService.FinalizeWorldChainPurchaseAsync`.

1. Nutzer tippt „Kaufen". Frontend erzeugt **reference** = `listing-{listingId}-{timestamp}`.
2. `MiniKit.pay({ to: Plattform-Wallet, tokens:[{symbol, token_amount}], reference, description })`
   → liefert `{ status:'success', transaction_id }`. **(Modell B:** voller Preis an die Plattform-Wallet;
   Verkäufer-Anteil 80 % wird serverseitig als Guthaben verbucht. Details: `Marktplatz-Dokumentation.md`.)
3. Frontend ruft `POST /WorldMiniApp/Marketplace/FinalizeWorldChainPurchase`
   mit `listingId`, `txHash` (= `transaction_id`), `walletAddress`, `paymentToken`.
4. **Backend verifiziert die Zahlung BLOCKIEREND (fail-closed)**, bevor geliefert wird.
5. Bei Erfolg: typ-abhängige Lieferung (Essensplan/Einzelrezept → Plan-Kopie; Objekt → Lieferadresse+Bestand;
   Digital → Download-Freigabe; Dienstleistung → Verkäufer-Notification), `MealPlanPurchase`-Eintrag, `SoldCount++`.

> **Angebotstypen:** Der Marktplatz verkauft fünf Typen (Essensplan, Einzelrezept, Objekt, Digital,
> Dienstleistung). Zahlungs-/Verifizierungslogik ist für alle gleich; die Unterschiede (Pflichtfelder,
> Lieferung, Download/Versand) stehen in `Marktplatz-Dokumentation.md` §14.

### 5.2 Zahlungsverifizierung (`VerifyMiniKitPaymentAsync`)
Ruft die **World-Payment-API**:
```
GET https://developer.worldcoin.org/api/v2/minikit/transaction/{transaction_id}?app_id={app_id}&type=payment
Authorization: Bearer {WorldApiKey}
```
Prüft (alle müssen passen, sonst Kauf abgelehnt):
1. **HTTP 200** → die Transaktion gehört zu **dieser App** (App-scoped API-Key). Fremde/erfundene IDs scheitern hier.
2. **`reference`** beginnt mit `listing-{listingId}-` → bindet die Zahlung an genau dieses Listing.
3. **`transaction_status` ≠ `failed`** (pending/mined sind ok).
4. **`to`** (falls geliefert) = WLD- **oder** USDT-Wallet des Verkäufers → verhindert „an sich selbst zahlen".

**Eigenschaften:**
- **Fail-closed:** Ist der Key nicht gesetzt oder die Prüfung schlägt fehl → **kein** Kauf.
- **Duplikatschutz:** UNIQUE auf `ReferenceTxHash` (gleiche TX nicht doppelt).
- **Notfall-Schalter:** `Marketplace:VerifyPayments=false` deaktiviert die Prüfung (NUR Test, unsicher).

### 5.3 Hintergrund (warum so)
Früher lief die Prüfung als **fire-and-forget** und der Kauf wurde **immer** durchgelassen →
jeder eingeloggte Nutzer konnte mit erfundener `txHash` Bezahlinhalte gratis bekommen. Das ist
jetzt geschlossen.

> ⚠️ **ethers.js-Browser-Fallback** (Kauf außerhalb der World App, echter On-Chain-Hash) wird von
> der MiniKit-Verifizierung abgelehnt (ist keine MiniKit-Transaktion). Für eine World-App-Mini-App ok;
> falls der Fallback gebraucht wird, muss er separat streng on-chain verifiziert werden
> (Empfänger/Betrag/Token). `VerifyTransactionOnChainAsync` ist aktuell ungenutzt.

---

## 6. Auth-/Session-Architektur (Cookie-Auth)

- **Eigenes Cookie-Scheme** `WorldMiniApp` (Cookie `.WorldMiniApp.Auth`), verschlüsselt+signiert
  via ASP.NET **Data Protection**. Registriert in `Program.cs` (neben der Identity-Default-Auth der Haupt-App).
- **Identität kommt ausschließlich aus dem Claim** `WorldUserHash` im Auth-Cookie
  (`WorldMiniAppUserHashHelper.Resolve`). **Nicht** mehr aus Query/Header/Cookie/Parameter
  → schließt Impersonation/IDOR.
- Eine Middleware (nur für `/WorldMiniApp`-Pfade) hängt die Identität in den `ClaimsPrincipal`
  → `Resolve()` bleibt synchron. Keine Cross-Contamination mit der Identity-`[Authorize]` der Haupt-App.
- **Anzeige-Cookie** `WorldMiniAppUserHash` (HttpOnly=false) ist nur **kosmetisch** fürs Frontend; der Server ignoriert es.
- **Login & Logout:** `WorldMiniAppUserHashHelper.SignInAsync` / `SignOutAsync`. Frontend prüft Status via `GET /Auth/SessionStatus`.
- **„Login merken" (rememberLogin):**
  - `true` → persistentes Cookie (~30 Tage, `ExpiresUtc`).
  - `false` → **Session-Cookie** (kein `ExpiresUtc`) → beim Schließen der World App weg → nächster Start zeigt wieder den Login.
- **Data-Protection-Keys** liegen persistent in SQL (`DataProtectionKeys`, via EF Core), `SetApplicationName("DelikatessenDrehbuch")`
  → Cookies überleben Neustart/Scale-out (sonst würden alle ausgeloggt).

---

## 7. Komponenten: der Azure-Signer (Node Function)

Projekt: `WorldIdSignerFunction/` (Repo-Root, eigenständige **Node Azure Function**, Verbrauch/Windows).

- **Endpoint:** `POST /api/worldid/rp-context`, Body `{ action }` → liefert
  `{ rp_id, nonce, created_at, expires_at, signature }`.
- **Signierung:** `signRequest` aus `@worldcoin/idkit-core/signing` (re-export aus `@worldcoin/idkit-server`,
  pure JS / EIP-191, **kein WASM**).
- **Schutz:** Function-Key (`?code=…`) **und** optionaler Header `X-Agent-Api-Key` (= `INTERNAL_API_KEY`).
- **Secrets (App Settings):**
  - `World_4_0_SingKey` → Key-Vault-Referenz `@Microsoft.KeyVault(SecretUri=…/World-4-0-SingKey/)`
  - `WORLD_ID_RP_ID`, `INTERNAL_API_KEY`
- ⚠️ Env-Namen mit Punkt sind unter Linux problematisch → Setting heißt `World_4_0_SingKey` (Unterstriche);
  der Code liest `World_4.0_SingKey` **und** `World_4_0_SingKey`.

Details/Code: `Areas/WorldMiniApp/docs/worldid-idkit-node-signer.md` + `WorldIdSignerFunction/README.md`.

---

## 8. Konfiguration — alle Keys

### 8.1 C#-App (Azure App Settings — `:` als `__` schreiben)
| Key | Beispiel/Wert | Wofür |
|---|---|---|
| `WorldId:AppId` | `app_a8d8…` | Mini-App-/World-ID-app_id |
| `WorldId:RpId` | `rp_786841…` | RP-ID (World ID 4.0) |
| `WorldId:Action` | `login-delikatessendrehbuch` | Login-Action |
| `WorldId:Environment` | `production` | IDKit-Environment |
| `WorldId:NodeSignerUrl` | `https://<func>.azurewebsites.net/api/worldid/rp-context?code=…` | Signer-Function |
| `WorldId:DebugVerify` | `false` | v4-Fehlergrund an Client durchreichen (nur Debug) |
| `WorldApiKey` | *(geheim)* | **Dev-Portal-API-Key** für Marktplatz-Zahlungsverifizierung |
| `Marketplace:VerifyPayments` | `true` | Zahlungsprüfung an/aus (Default an) |
| `TradingAgent:InternalApiKey` | *(geheim)* | Shared Secret C# ↔ Node-Services |
| `WorldMiniApp:SuperUserHash` | *(geheim)* | eigene Hash, von orb-Gating ausgenommen |
| `WorldMiniApp:AdminPassword` | *(BCrypt-Hash)* | Admin-Login |
| `Bunny_Net_*` | … | Video-/Bild-Storage (separat) |

### 8.2 Azure Function (WorldIdSignerFunction)
| Setting | Wert |
|---|---|
| `World_4_0_SingKey` | Key-Vault-Referenz auf Secret `World-4-0-SingKey` |
| `WORLD_ID_RP_ID` | `rp_786841…` |
| `INTERNAL_API_KEY` | = `TradingAgent:InternalApiKey` der C#-App |

### 8.3 Woher kommen die Werte?
- `app_id`, `rp_id`, `signing_key` → **Worldcoin Developer Portal** (App → World ID 4.0 „verwaltet" aktivieren).
  *„Verwaltet"* = das Portal übernimmt die On-Chain-RP-Registrierung (keine Wallet/Gas nötig); den
  Signing-Key verwaltet ihr trotzdem selbst (→ Key Vault).
- `WorldApiKey` → Developer Portal → **API Keys**.

---

## 9. Dateireferenz

**Frontend**
- `wwwroot/js/minikitt.js` — Login (IDKit), walletAuth, pay, Debug-Overlay.
- `Views/Marketplace/Index.cshtml` — Kauf-Flow (`buyListing`).

**Backend (C#)**
- `Areas/WorldMiniApp/Controllers/AuthController.cs` — `Nonce`, `CompleteSiwe`, `VerifyAction` (legacy),
  `WorldIdRequestContext`, `VerifyWorldId`, `SessionStatus`, `SetTestHash` (nur Dev), `ClearTestHash`.
- `Areas/WorldMiniApp/Controllers/MarketplaceController.cs` — Listings, Kauf-Endpunkte.
- `Areas/WorldMiniApp/Controllers/WorldMiniAppBaseController.cs` — `ResolveUserHash`, `SuperUserHash`, `IsCreatorAllowedAsync`.
- `Areas/WorldMiniApp/Services/WorldMiniAppUserHashHelper.cs` — Cookie-Auth (SignIn/SignOut/Resolve).
- `Areas/WorldMiniApp/Services/UserManager.cs` — User anlegen/aktualisieren (`CreateOrUpdateWorldIdUserAsync` u. a.).
- `Areas/WorldMiniApp/Services/MarketplaceService.cs` — Kauf + `VerifyMiniKitPaymentAsync`.
- `Areas/WorldMiniApp/Services/AuthService.cs` — (Legacy v2-Proof-Verify).
- `Program.cs` — Cookie-Scheme, Identitäts-Middleware, Data Protection (EF/SQL).

**Node-Signer**
- `WorldIdSignerFunction/` — Azure Function (`src/functions/rpContext.js`).

**SQL-Deploy-Skripte** (`Areas/WorldMiniApp/Sql/`)
- `dataprotection_keys.sql` — DataProtection-Keys-Tabelle.
- `worlduser_notifications.sql` — Feed-Glocken-Tabelle.
- `recipe_ai_variant_jobs.sql` — AI-Job-Tabelle.
- `marketplace_payout_requests.sql` — Verkäufer-Auszahlungsanträge (Cash-out).
- (weitere: `worldminiapp_shared_feed.sql`, `CreateAdTables.sql`, …)

**Docs**
- `docs/worldid-idkit-node-signer.md`, `docs/World-Integration-Handbuch.md` (dieses).

---

## 10. Deployment & Betrieb

### 10.1 Reihenfolge bei einem frischen/neuen Environment
1. **Developer Portal:** App anlegen, World ID 4.0 „verwaltet" aktivieren → `app_id`, `rp_id`, `signing_key`, plus `WorldApiKey`.
2. **Key Vault:** Secret `World-4-0-SingKey` = `signing_key` anlegen.
3. **WorldIdSignerFunction** deployen (Node Function), App Settings + Managed Identity (Key Vault Secrets User) setzen.
4. **SQL-Skripte** auf der DB ausführen: `dataprotection_keys.sql`, `worlduser_notifications.sql`, `recipe_ai_variant_jobs.sql`, `marketplace_payout_requests.sql`.
5. **C#-App** App Settings füllen (siehe §8.1), deployen.
6. In der World App testen: Login + ein Kauf.

### 10.2 Bestehendes Environment (Updates)
- DataProtection-Tabelle muss existieren (sonst Logout bei Neustart).
- Neue SQL-Skripte sind idempotent → einmal ausführen schadet nicht.
- `WorldApiKey` muss gesetzt sein, sonst werden **Käufe fail-closed abgelehnt**.

### 10.3 Secret-Rotation
- Function-Key & `INTERNAL_API_KEY` regelmäßig rotieren (an beiden Stellen: Function + C#).
- `signing_key`/`WorldApiKey` nur über Portal/Key Vault, nie im Code/Repo.

---

## 11. Debugging (On-Screen-Overlay)

Im World-App-Webview gibt es keine DevTools → eingebautes Overlay in `minikitt.js`:
- **Einschalten:** App mit `?wdebug=on` öffnen (bleibt via localStorage). **Aus:** `?wdebug=off`. Default: aus.
- **🐞-Button** unten rechts öffnet das Log-Panel. „Copy" kopiert das gesamte Log.
- Logs werden in `localStorage` (`worldid_debug_log`) **persistiert** → überleben Schwarzbild/Reload.
- Jeder Login-Schritt wird protokolliert (Schritt 0–4, HTTP-Status, rp_context-Felder, Result, v4-Detail).
- Server-Detail des v4-Verify nur, wenn `WorldId:DebugVerify=true`.

---

## 12. Troubleshooting / bekannte Stolpersteine

| Symptom | Ursache | Lösung |
|---|---|---|
| `failed to compile WebAssembly: HTTP status code is not ok` | idkit-core-WASM über jsDelivr `+esm` nicht ausgeliefert | über **esm.sh** laden (`@worldcoin/idkit-core@4.1.8`) |
| Schwarzbild „MiniKit version is not supported, update World App" | `MiniKit.install()` fehlte vor dem IDKit-Flow | `MiniKit.install({ appId })` zuerst aufrufen |
| `cannot convert undefined to a BigInt` | C# gab `rp_context` als `System.Text.Json.JsonElement` zurück (Newtonsoft serialisiert falsch) | im Backend **Newtonsoft `JObject`** verwenden |
| `credential_unavailable` | Preset `orbLegacy` verlangt Orb, User ist nur device | Preset **`deviceLegacy`** + `allow_legacy_proofs:true` |
| v4-Verify HTTP 400 nach gültigem Proof | ganzer `{success,result}`-Wrapper statt nur `result` geschickt | nur den **inneren Proof** (`result.result`) senden |
| v4-Verify HTML **403 Forbidden** | `.NET HttpClient` ohne `User-Agent` → Cloudflare-WAF blockt | `User-Agent` + `Accept`-Header setzen |
| Kauf abgelehnt, Log `ref=null` | World-Payment-API-Feldnamen weichen ab | Feldnamen in `VerifyMiniKitPaymentAsync` anpassen |
| Käufe gehen plötzlich nicht mehr | `WorldApiKey` fehlt → fail-closed | `WorldApiKey` in App Settings setzen |
| Alle Nutzer nach Deploy ausgeloggt | DataProtection-Keys nicht persistent | `dataprotection_keys.sql` ausführen / EF-Persistenz prüfen |
| `connectorURI` öffnet falsche Seite im Webview | innerhalb World App nicht navigieren | nur `pollUntilCompletion()`, navigieren nur wenn `!isInWorldApp()` |

---

## 13. Sicherheits-Checkliste

- [x] Identität nur aus signiertem Auth-Cookie-Claim (kein Query/Header/Cookie-Vertrauen).
- [x] `SetTestHash` nur in `IsDevelopment()`.
- [x] Marktplatz-Käufe serverseitig **fail-closed** verifiziert (World-Payment-API).
- [x] Signing-Key nur in Key Vault / Node-Signer, nie in der C#-App / im Repo.
- [x] AgentApi/Node-Services per `X-Agent-Api-Key` (Shared Secret) geschützt.
- [x] Exceptions/DB-Meldungen nicht an Clients leaken (generische Meldungen + Log).
- [x] SIWE: Nonce-Replay-Schutz + Signatur-/Adress-Match.
- [ ] **TODO:** Function-Key + `INTERNAL_API_KEY` rotieren (standen mal im Chat).
- [ ] **TODO (optional):** ethers.js-Browser-Fallback im Marktplatz streng on-chain absichern.

---

## 14. Datenbank-Schema (World-App-Teil)

Nur die Tabellen der **WorldMiniApp**. Zentrale Identität ist der **`UserHash`** (String) —
die meisten Tabellen referenzieren ihn als String (nicht zwingend als FK auf `WorldAppUser.Id`).
Entities liegen in `Areas/WorldMiniApp/Models/`.

### 14.1 Beziehungsübersicht
```
WorldAppUser (UserHash) ───< WorldUserPosting        (CreatorId = UserHash)
        │                ───< WorldUserMealPlan       (UserHash)
        │                ───< WorldUserLike           (FK WorldAppUser.Id)  >─── RecipeBaseData
        │                ──┬─ WorldUserAbo            (WorldUser.Id / Creator.Id)   [Follow]
        │                  └─ (Self-N:M über WorldUserAbo)
        │                ───< WorldUserNotification    (UserHash)
        │                ───< WildCoinTransaction      (UserHash)            [Guthaben-Ledger]
        │
        ├─ MealPlanListing (SellerHash) ──> WorldUserMealPlan (MealPlanId)
        │            │
        │            └──< MealPlanPurchase (ListingId) ──> WorldUserMealPlan (CreatedMealPlanId, Kopie)
        │                         (BuyerHash, SellerHash)
        │
        └─ WorldUserPendingVideo (UserHash, PostingId)   [Video-Processing]
```

### 14.2 Identität

**`WorldAppUser`** — der Nutzer (1 Zeile pro World-ID-nullifier bzw. Wallet-Adresse).
| Spalte | Typ | Bemerkung |
|---|---|---|
| `Id` | int, PK identity | |
| `UserHash` | nvarchar | **Identität** = nullifier (World ID) oder Wallet-Adresse. Faktischer Schlüssel. |
| `UserName` | nvarchar null | Anzeigename |
| `IsVerified` | nvarchar null | `device` / `orb` / `wallet` / `test` |
| `Lastlogin` | datetime null | |
| `RememberLogin` | bit | "Login merken" |
| `WildCoinBalance` | decimal | internes Guthaben |
| `Bio` | nvarchar null | |
| `WalletAddress` | nvarchar null | verknüpfte Wallet (für Auszahlungen) |
| `StoreUrl` | nvarchar(1024) null | optionaler externer Store-Link (Profil). SQL: `Sql/marketplace_store_url.sql` |
| `CreatedAt` | datetime null | |

### 14.3 Inhalte & Social

**`WorldUserPosting`** — Rezept-Post (Video/Bild).
| Spalte | Typ | Bemerkung |
|---|---|---|
| `Id` | int, PK | |
| `CreatorId` | string | = `UserHash` des Erstellers |
| `CreatorName` | string | |
| `Source` | string | Medien-URL (Video/Bild) |
| `ThumbnailUrl` | string | |
| `Title` | string | |
| `IsOffline` | bit | versteckt |
| `CreationTime` | datetime | |
| `ReportCount` | int | Meldungen |
| `IsHiddenPendingReview` | bit | durch Moderation versteckt |
| `RecipeId` | int (FK) | → `RecipeBaseData` (Navigation `Recipe`) |

**`WorldUserLike`** — Like (User ↔ Rezept). FKs: `WorldAppUser.Id`, `RecipeBaseData.Id`.
**`WorldUserAbo`** — Follow (Self-N:M über `WorldAppUser`): `WorldUser` (Follower) ↔ `Creator`.
**`WorldUserBookmark`**, **`WorldUserComment`**, **`WorldUserCommentReaction`**, **`WorldUserReport`**,
**`WorldUserCommentReport`** — Bookmarks, Kommentare, Reaktionen, Meldungen.

### 14.4 Essenspläne & Marktplatz

**`WorldUserMealPlan`** — gespeicherter Plan eines Nutzers.
| Spalte | Typ | Bemerkung |
|---|---|---|
| `Id` | int, PK | |
| `UserHash` | string | Besitzer |
| `Settings` | nvarchar null | JSON-Einstellungen |
| `MealPlan` | nvarchar null | JSON-Plan |
| `Title` | nvarchar null | |
| `CreationTime` | datetime | |

**`MealPlanListing`** — Marktplatz-Angebot (typ-übergreifend; SQL: `Sql/marketplace_listing_types.sql`).
| Spalte | Typ | Bemerkung |
|---|---|---|
| `Id` | int, PK | |
| `SellerHash` | string | Verkäufer (UserHash) |
| `SellerName` | string | |
| `SellerWalletAddress` | nvarchar null | WLD-Empfänger |
| `SellerUsdtWalletAddress` | nvarchar null | USDT/USDC-Empfänger |
| `ListingType` | nvarchar(32) | `MealPlan`/`SingleRecipe`/`PhysicalObject`/`DigitalProduct`/`Service` (Default `MealPlan`) |
| `MealPlanId` | int **null** (FK) | → `WorldUserMealPlan` (nur Essensplan) |
| `RecipeId` | int null | nur Einzelrezept |
| `DigitalFileUrl` | nvarchar null | nur Digital (Download-Ziel) |
| `StockQuantity` | int null | nur Objekt (`null`=unbegrenzt) |
| `RequiresShipping` | bit | nur Objekt |
| `CoverImageUrl` | nvarchar null | Titelbild (Nicht-Essensplan) |
| `ImagesJson` | nvarchar(max) null | Bildergalerie (Objekt) |
| `Title` / `Description` | nvarchar | |
| `Price` | decimal | Preis (WLD) |
| `DayCount` / `RecipeCount` | int | |
| `CreatedAt` | datetime | |
| `IsActive` | bit | aktiv/inaktiv |
| `SoldCount` | int | Verkäufe |

**`MealPlanPurchase`** — abgeschlossener Kauf (nach verifizierter Zahlung).
| Spalte | Typ | Bemerkung |
|---|---|---|
| `Id` | int, PK | |
| `BuyerHash` | string | Käufer (UserHash) |
| `BuyerWalletAddress` | nvarchar null | |
| `SellerHash` | string | |
| `SellerWalletAddress` | nvarchar null | |
| `ListingId` | int (FK) | → `MealPlanListing` |
| `CreatedMealPlanId` | int **null** | kopierter Plan (`WorldUserMealPlan`) — nur Essensplan/Einzelrezept |
| `ListingType` | nvarchar(32) null | Snapshot des Angebotstyps |
| `BuyerShippingAddress` | nvarchar(max) null | Lieferadresse (nur Objekt) |
| `PricePaid` | decimal | |
| `CreatorAmount` | decimal | 80 % Verkäufer-Anteil (Modell B: als WildCoinBalance-Guthaben gutgeschrieben) |
| `PlatformFee` | decimal | Differenz |
| `ReferenceTxHash` | nvarchar null | **UNIQUE** — MiniKit `transaction_id` (Duplikatschutz) |
| `PaymentToken` | string | `WLD` / `USDCE` |
| `PurchasedAt` | datetime | |

**`WildCoinTransaction`** — Guthaben-Ledger.
`Id`, `UserHash`, `Amount` (decimal), `BalanceAfter` (decimal), `Type` (string), `ReferenceInfo` (null), `CreatedAt`.

### 14.5 Benachrichtigungen & Infra

**`WorldUserNotification`** — Feed-Glocke. Schema-Skript: `Sql/worlduser_notifications.sql`.
`Id`, `UserHash`, `Icon`, `Sender`, `Description`, `Href?`, `NotificationKey?`, `EventType?`,
`LatestActorName?`, `ContextText?`, `AggregateCount`, `UnreadEventCount`, `CreatedAtUtc`,
`IsSeen`, `SeenAtUtc?`. Indizes: `(UserHash, IsSeen, CreatedAtUtc)`, `(UserHash, NotificationKey)`.

**`WorldUserPendingVideo`** — Video-Processing-Warteschlange (Bunny-Webhook).
`Id`, `PostingId` (FK), `VideoGuid`, `UserHash`, `CreatedAt`.

**`RecipeAiVariantJobs`** — asynchrone AI-Rezept-Jobs. Schema-Skript: `Sql/recipe_ai_variant_jobs.sql`.
`Id`, `JobId` (UNIQUE), `BaseRecipeId` (FK → RecipeBaseData, cascade), `VariantType`, `Language`,
`AiProvider`, `CreatedByUserHash?`, `State`, `Message?`, `Title?`, `RequestJson`, `ResultJson?`,
`CreatedAtUtc`, `UpdatedAtUtc`.

**`DataProtectionKeys`** — ASP.NET-Auth-Schlüsselring (signiert die Auth-Cookies). Schema-Skript:
`Sql/dataprotection_keys.sql`. `Id`, `FriendlyName`, `Xml`. **Extern verwaltet** (`ExcludeFromMigrations`).

### 14.6 Weitere World-Tabellen (Kurzliste)
| Tabelle | Zweck |
|---|---|
| `WorldSharedMealPlan` / `WorldSharedShoppingList` | Token-basiert geteilte Pläne/Einkaufslisten |
| `WorldSharedFeed` / `…Member` / `…Item` / `…Message` / `…Todo` | Gruppen-Feeds (Chat/Todos) |
| `MarketplacePayoutRequest` | Verkäufer-Auszahlungsanträge (Cash-out, Modell B): SellerHash, Amount, Token, WalletAddress, Status (pending/paid/rejected), TxHash, RequestedAt, ProcessedAt/By |
| `AgentPaymentRequest` | Agent→User-Zahlungsanfragen (Status/Reference) |
| `WorldTradingAgent` / `WorldAgentTrade` / `WorldTokenPrice` | Trading-Agent-Feature (World Chain) |
| `WorldAppAd` / `WorldAdPreferenceProfile` / `WorldAdPreferenceInterest` / `WorldClipWatchSession` | Ads & Watch-Analytics |
| `Keyword` / `RecipeBaseKeyword` | mehrsprachige Rezept-Tags (N:M) |

> Quelle der Wahrheit ist immer `ApplicationDbContext` + die Entity-Klassen in `Areas/WorldMiniApp/Models/`.
> Tabellen, die per Hand-SQL verwaltet werden (`WorldUserNotifications`, `RecipeAiVariantJobs`,
> `DataProtectionKeys`, geteilte Feeds), siehe `Areas/WorldMiniApp/Sql/`.

---

*Bei Änderungen an Login-/Kauf-/Auth-Code dieses Handbuch mitpflegen.*
