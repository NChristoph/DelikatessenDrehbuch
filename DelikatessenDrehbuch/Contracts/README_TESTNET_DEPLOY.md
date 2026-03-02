# WorldChain Testnet Deploy (WLD Verkaufslogik)

Kurz-Setup, um die Verkaufslogik gegen ein Testnetz zu prüfen.

## 1) Abhängigkeiten

```bash
cd DelikatessenDrehbuch/Contracts
npm install
```

## 2) `.env` anlegen

Beispiel:

```bash
WORLDCHAIN_RPC_URL=https://<your-worldchain-testnet-rpc>
WORLDCHAIN_CHAIN_ID=480
DEPLOYER_PRIVATE_KEY=0x<private-key>
TREASURY_ADDRESS=0x<treasury-wallet>

# Option A: echtes Test-WLD (wenn du bereits Token-Adresse hast)
WLD_TOKEN_ADDRESS=0x<test-wld-token-address>
DEPLOY_MOCK_WLD=false

# Option B: eigenes Faucet-Token deployen (MockWLD)
# DEPLOY_MOCK_WLD=true
# MOCK_FAUCET_MINT=1000000
```

> Für schnelles Testen empfehle ich **Option B** (`DEPLOY_MOCK_WLD=true`), dann bekommst du sofort Faucet-Token (`faucetMint`) für Kauf-Tests.

## 3) Kompilieren

```bash
npm run compile
```

## 4) Deploy auf WorldChain Testnet

```bash
npm run deploy:testnet
```

Das Script deployed:
1. optional `MockWLD.sol` (Faucet-Token),
2. `MealPlanMarketplaceWorldChain.sol`.

Danach gibt es die Werte für deine App aus:
- `WorldChain:ChainId`
- `WorldChain:WldTokenAddress`
- `WorldChain:MarketplaceContractAddress`

## 5) In App eintragen

In `appsettings.Development.json` oder Umgebungsvariablen eintragen:

```json
"WorldChain": {
  "ChainId": "480",
  "WldTokenAddress": "0x...",
  "MarketplaceContractAddress": "0x..."
}
```

## 6) Verkaufslogik testen

1. In Feed -> Marktplatz einen Plan listen.
2. Mit Wallet kaufen (`Mit Wallet kaufen`).
3. On-Chain TX + Backend-Finalisierung prüfen.

