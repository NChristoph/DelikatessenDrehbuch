# Trading Agent - Auto-Swap Feature

## 🔄 Was ist Auto-Swap?

Der Trading Agent braucht zwei Token:
- **WLD** - Zum Traden
- **ETH** - Für Gas Fees

Das Auto-Swap Feature konvertiert automatisch WLD zu ETH wenn nötig.

## 💰 Kosten

### Initial Swap (beim ersten Funding):
- **1 WLD wird zu ETH konvertiert**
- Gas: ~$0.02 (auf World Chain!)
- ETH erhalten: ~0.00065 ETH
- Reicht für: ~500+ Trades

### Auto-Refill (wenn ETH < 0.0005):
- **0.5 WLD wird zu ETH konvertiert**
- Gas: ~$0.02
- ETH erhalten: ~0.00033 ETH
- Reicht für: ~250+ weitere Trades

## 📊 Beispiel-Rechnung

**User funded Agent mit 30 WLD:**

```
1. Initial Funding:
   ✅ 30 WLD an Agent Wallet gesendet
   ⚡ Auto-Swap: 1 WLD → 0.00065 ETH (Gas: $0.02)

   Agent hat jetzt:
   - 29 WLD (für Trading)
   - 0.00065 ETH (für Gas)

2. Agent macht 400 Trades:
   - Gas verbraucht: 400 × $0.01 = $4
   - ETH Balance: 0.00065 → 0.0003 (unter Threshold!)

   ⚡ Auto-Refill: 0.5 WLD → 0.00033 ETH (Gas: $0.02)

   Agent hat jetzt:
   - 28.5 WLD + Profit/Loss aus Trades
   - 0.00063 ETH (für weitere ~300 Trades)

3. Total Kosten für 400 Trades:
   - WLD → ETH Swaps: 1.5 WLD (~$3)
   - Gas für Swaps: $0.04
   - Gas für Trades: $4
   - DEX Fees: ~0.3% pro Trade
```

## ⚙️ Einstellungen

In der Datenbank gespeichert (WorldTradingAgents Tabelle):

| Feld | Default | Beschreibung |
|------|---------|--------------|
| `AutoSwapThresholdETH` | 0.0005 | Wenn ETH < dieser Wert → Auto-Swap |
| `AutoSwapAmountWLD` | 0.5 | Wie viel WLD wird geswapped |
| `CurrentBalanceETH` | 0 | Aktueller ETH Balance |

## 🔧 Implementation

### Service Methods:

1. **SwapWLDtoETHAsync(agentId, amountWLD)**
   - Swapped WLD zu ETH
   - Aktualisiert Balances
   - Logged Trade in `WorldAgentTrades`

2. **CheckAndAutoSwapGasAsync(agentId)**
   - Prüft ETH Balance
   - Führt Auto-Swap aus wenn nötig
   - Wird vor jedem Trade aufgerufen

3. **UpdateFundingAsync (erweitert)**
   - Bei Initial Funding: Auto-Swap 1 WLD → ETH
   - Aktualisiert WLD Balance

### Database Changes:

Neue Spalten in `WorldTradingAgents`:
```sql
CurrentBalanceETH DECIMAL(18,9) -- Aktueller ETH Balance
AutoSwapThresholdETH DECIMAL(18,9) DEFAULT 0.0005
AutoSwapAmountWLD DECIMAL(18,6) DEFAULT 0.5
```

### UI Changes:

1. **Dashboard zeigt ETH Balance** (mit Warnung wenn niedrig)
2. **Funding Button** zeigt "(+ Auto-Swap)"
3. **Info Box** erklärt Auto-Swap beim Funding
4. **Trade History** zeigt AutoSwap Trades

## 🚀 Verwendung

### Für User:
1. Agent erstellen
2. 30+ WLD senden
3. **FERTIG!** Auto-Swap kümmert sich um Gas

### Für Entwickler:

```csharp
// Vor jedem Trade:
await _agentService.CheckAndAutoSwapGasAsync(agentId);

// Trade ausführen...
```

## ⚠️ WICHTIG: World Chain vs Ethereum

| | Ethereum Mainnet | World Chain |
|---|------------------|-------------|
| **Gas pro Swap** | $5-50 😱 | $0.01-0.03 ✅ |
| **Gas pro Trade** | $10-100 😱 | $0.003-0.01 ✅ |
| **Auto-Swap sinnvoll?** | NEIN! | JA! |

**World Chain ist 1000x günstiger!** Deshalb funktioniert Auto-Swap.

## 📝 SQL Scripts

Führe aus:
1. **Neue Installation:** `TradingAgent_Tables.sql`
2. **Bestehende Installation:** `TradingAgent_AddAutoSwap.sql`

## 🎯 Zusammenfassung

✅ **Vorteile:**
- User muss sich nicht um Gas kümmern
- Automatische Gas-Verwaltung
- Kosten sind minimal (~$0.02 pro Swap)
- Agent kann hunderte Trades ohne Unterbrechung machen

✅ **Kosten pro Trade:**
- Gas: ~$0.01 (auf World Chain)
- DEX Fee: 0.3%
- Auto-Swap: ~$0.02 alle 300-500 Trades

✅ **Total für 1000 Trades:**
- Gas: ~$10
- Auto-Swaps: ~$0.06 (3× Refills)
- DEX Fees: Abhängig vom Trade-Volumen
