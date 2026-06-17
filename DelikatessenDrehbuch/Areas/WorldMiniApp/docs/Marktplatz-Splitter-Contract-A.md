# Marktplatz — Modell A: Splitter-Contract (vorbereitet, noch nicht aktiv)

Atomarer 80/20-Split per Smart Contract: Käufer zahlt **einmal**, der Contract leitet im selben
Tx Verkäufer-Anteil + Plattform-Gebühr weiter. Löst das Problem von Modell B (kein
Verkäufer-Auszahlungs-Rückstand) und von „zwei Zahlungen" (nicht atomar).

> **Status:** Contract + Plan liegen bereit (`contracts/MarketplaceSplitter.sol`).
> **Aktiv ist derzeit Modell B** (Plattform kassiert 100 %, Verkäufer-Guthaben intern).
> Dieser Weg ist erst nach **Deploy + Integration + Audit** scharf zu schalten.

---

## 1. Contract
Datei: `Areas/WorldMiniApp/contracts/MarketplaceSplitter.sol`
- `purchase(token, seller, amount, reference)` zieht `amount` vom Käufer (`transferFrom`) und
  überweist `sellerAmount` (80 %) an den Verkäufer + `feeAmount` (20 %) an `platformWallet` — **atomar**.
- Konstruktor: `(platformWallet, feeBps)` — z. B. `feeBps = 2000` (= 20 %).
- Emittiert `Purchase(reference, buyer, seller, token, amount, sellerAmount, feeAmount)`.
- Admin: `setPlatformWallet`, `setFeeBps`, `transferOwnership`. Reentrancy-Guard.

## 2. Deploy (World Chain)
1. Kompilieren (Remix / Foundry / Hardhat), Solidity `^0.8.20`.
2. Deployen auf **World Chain** (ChainId 480) mit Konstruktor-Args:
   - `platformWallet` = deine Plattform-Wallet
   - `feeBps` = `2000`
3. **Contract-Adresse** notieren → später in Config (`Marketplace:SplitterContractAddress`).
4. (Empfehlung) vorher auf einem Testnet testen + Audit.

## 3. Frontend-Integration (World App)
Statt `MiniKit.pay` wird `MiniKit.sendTransaction` genutzt (Contract-Call), inkl. Token-Allowance:
1. **Approve/Permit2:** dem Splitter-Contract eine Allowance über `amount` für den Token (WLD/USDC.e) geben.
2. **purchase** aufrufen:
   ```
   MiniKit.sendTransaction({
     transaction: [{
       address: SPLITTER_CONTRACT,
       abi: [...],
       functionName: "purchase",
       args: [tokenAddress, sellerWallet, amountSmallestUnit, referenceBytes32]
     }],
     permit2: [ /* Allowance für tokenAddress an SPLITTER_CONTRACT */ ]
   })
   ```
   - `referenceBytes32` = `keccak256("listing-{listingId}-{ts}")` (zum Wiederfinden im Backend).
3. Ergebnis (`transaction_id`/`txHash`) ans Backend zur Verifizierung.

## 4. Backend-Verifizierung
Zwei Optionen (eine reicht):
- **Event-Verifizierung (empfohlen):** Tx-Receipt über World-Chain-RPC holen, das `Purchase`-Event
  dekodieren und prüfen: `platformWallet` korrekt, `seller` = Verkäufer-Wallet, `reference`
  entspricht dem Listing, `amount`/`feeAmount` stimmen. Dann Plan freischalten.
- **World-Transaction-API:** wie bei Modell B (`/api/v2/minikit/transaction/{id}`), falls
  `sendTransaction` dort verifizierbar ist (reference/status prüfen).

## 5. Was sich beim Umstieg B → A im Code ändert
- **Frontend** (`Marketplace/Index.cshtml`, `buyListing`): `MiniKit.pay` → `MiniKit.sendTransaction`
  (Approve + `purchase`); `to` ist der **Contract**, nicht die Plattform-Wallet.
- **Backend** (`MarketplaceService.FinalizeWorldChainPurchaseAsync`):
  - Verifizierung gegen das **`Purchase`-Event** (statt Zahlung an Plattform-Wallet).
  - **Verkäufer-Gutschrift entfällt** — der Verkäufer wird on-chain direkt bezahlt
    (das WildCoinBalance-Credit aus Modell B fällt weg bzw. wird durch die On-Chain-Zahlung ersetzt).
  - `CreatorAmount`/`PlatformFee` weiterhin verbuchen (Reporting).
- **Config:** neu `Marketplace:SplitterContractAddress`; `PlatformWalletAddress` bleibt (= `platformWallet` des Contracts).

## 6. Trade-offs
| | Modell B (aktiv) | Modell A (Contract) |
|---|---|---|
| Bestätigungen | 1 | 1 (Approve evtl. + 1) |
| Atomar | ✅ | ✅ |
| Verkäufer-Auszahlung | intern, **manuell später** | **sofort on-chain** |
| Plattform-Gebühr | sicher (Plattform hält alles) | sicher (Contract) |
| Aufwand | gering (live) | Solidity + Deploy + Audit |
| Plattform-Verwahrung von Fremdgeld | ja (80 % gehören Verkäufern) | nein |

> Empfehlung: Solange wenige Verkäufe → B (einfach). Bei Skalierung/Compliance (kein Treuhand von
> Verkäufer-Geldern) → auf A umstellen.
