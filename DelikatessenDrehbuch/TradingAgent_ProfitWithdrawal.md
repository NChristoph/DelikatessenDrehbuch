# Trading Agent - Profit Withdrawal (Auto-Withdraw bei 100%)

## 🎯 Wie es funktioniert:

### Compound Interest mit Auto-Withdraw

Der Agent nutzt **Compound Interest** (reinvestiert alle Gewinne) ABER:
- **Bei 100% Profit (Verdopplung) → 50% der Gewinne werden ausgezahlt**

## 📊 Beispiel:

### Start: 30 WLD Initial Funding

```
Woche 1-4: Trading mit Compound Interest
─────────────────────────────────────────
Start:     30.0 WLD
Woche 1:  +2.0 WLD → 32.0 WLD (trading mit 32 WLD)
Woche 2:  +2.5 WLD → 34.5 WLD (trading mit 34.5 WLD)
Woche 3:  +3.0 WLD → 37.5 WLD (trading mit 37.5 WLD)
Woche 4:  -1.0 WLD → 36.5 WLD (schlechte Woche)
...

Woche 20: Balance erreicht 60 WLD ← 100% Profit! 🎉
─────────────────────────────────────────

🚨 AUTO-WITHDRAW TRIGGER! 🚨

Berechnung:
- Initial Funding:  30 WLD
- Current Balance:  60 WLD
- Total Gain:       30 WLD (100% Profit)
- Withdraw (50%):   15 WLD → AN USER WALLET ✅
- Remaining:        45 WLD → Agent traded weiter

─────────────────────────────────────────

Weiter Trading mit 45 WLD...
Woche 21: +2.0 WLD → 47.0 WLD
Woche 22: +2.5 WLD → 49.5 WLD
...

Woche 40: Balance erreicht 90 WLD ← 100% Profit von 45! 🎉
─────────────────────────────────────────

🚨 AUTO-WITHDRAW TRIGGER! 🚨

Berechnung:
- Previous Funding: 45 WLD
- Current Balance:  90 WLD
- Total Gain:       45 WLD (100% Profit)
- Withdraw (50%):   22.5 WLD → AN USER WALLET ✅
- Remaining:        67.5 WLD → Agent traded weiter

─────────────────────────────────────────

Und so weiter...
```

## 💰 Total Auszahlungen:

| Event | Agent Balance | Profit | Withdraw | User erhält | Agent behält |
|-------|---------------|--------|----------|-------------|--------------|
| Start | 30 WLD | - | - | - | 30 WLD |
| 1. Trigger | 60 WLD | +30 WLD | 50% | 15 WLD | 45 WLD |
| 2. Trigger | 90 WLD | +45 WLD | 50% | 22.5 WLD | 67.5 WLD |
| 3. Trigger | 135 WLD | +67.5 WLD | 50% | 33.75 WLD | 101.25 WLD |

**User hat ausgezahlt bekommen:** 15 + 22.5 + 33.75 = **71.25 WLD** 💰
**Agent traded mit:** 101.25 WLD (weiter wachsend!)

## 🔧 Implementation Details:

### Service Method: `CheckProfitTargetAndWithdrawAsync`

```csharp
// Wird nach jedem Trade aufgerufen
public async Task<bool> CheckProfitTargetAndWithdrawAsync(int agentId)
{
    var agent = await _context.WorldTradingAgents.FindAsync(agentId);

    // Berechne Profit Prozentsatz
    decimal profitPercent = ((agent.CurrentBalanceWLD - agent.InitialFundingWLD)
                             / agent.InitialFundingWLD) * 100;

    // Bei 100% Profit
    if (profitPercent >= 100)
    {
        decimal totalGain = agent.CurrentBalanceWLD - agent.InitialFundingWLD;
        decimal withdrawAmount = totalGain * 0.5m; // 50% der Gewinne
        decimal remainingBalance = agent.CurrentBalanceWLD - withdrawAmount;

        // Transfer zu User Wallet (TODO: Echte Implementierung)
        // ...

        // Update Agent Balance
        agent.CurrentBalanceWLD = remainingBalance;
        agent.InitialFundingWLD = remainingBalance; // WICHTIG: Neuer Baseline!

        // Log Withdrawal in Trades
        var withdrawalTrade = new WorldAgentTrade
        {
            AgentId = agentId,
            TradeType = "AutoWithdraw",
            FromAmount = withdrawAmount,
            ToAmount = withdrawAmount,
            Status = "Success"
        };

        _context.WorldAgentTrades.Add(withdrawalTrade);
        await _context.SaveChangesAsync();

        return true;
    }

    return false;
}
```

### Wann wird gecheckt?

```csharp
// Nach JEDEM erfolgreichen Trade:
public async Task ExecuteTradeAsync(int agentId)
{
    // 1. Check Gas (Auto-Swap wenn nötig)
    await CheckAndAutoSwapGasAsync(agentId);

    // 2. Führe Trade aus
    // ...

    // 3. Update Balances
    // ...

    // 4. Check Profit Target
    await CheckProfitTargetAndWithdrawAsync(agentId);
}
```

## 📈 Vorteile dieser Methode:

### ✅ Compound Interest bis 100%
```
Woche 1:  30 → 32 WLD   (+2)
Woche 2:  32 → 34.5 WLD (+2.5)  ← Mehr Profit weil mehr Capital!
Woche 3:  34.5 → 37.5 WLD (+3) ← Noch mehr!
```

### ✅ Profit-Sicherung bei Verdopplung
- User bekommt regelmäßig Auszahlungen
- Risiko reduziert (Profit ist gesichert)
- Agent kann trotzdem weiter wachsen

### ✅ Exponentielles Wachstum
```
Start:        30 WLD
1. Withdraw:  15 WLD → User, 45 WLD → Agent (50% mehr Capital!)
2. Withdraw:  22.5 WLD → User, 67.5 WLD → Agent (125% mehr als Start!)
3. Withdraw:  33.75 WLD → User, 101.25 WLD → Agent (237% mehr!)
```

## 🎯 Beispiel-Szenario (Realistisch):

**Annahmen:**
- 5% durchschnittlicher monatlicher Gewinn (nach Kosten)
- Initial: 30 WLD

**Timeline:**

| Monat | Balance | Profit % | Event |
|-------|---------|----------|-------|
| 0 | 30.00 WLD | 0% | Start |
| 3 | 34.73 WLD | +15.8% | - |
| 6 | 40.18 WLD | +33.9% | - |
| 9 | 46.51 WLD | +55.0% | - |
| 12 | 53.86 WLD | +79.5% | - |
| 14 | 60.11 WLD | **+100.4%** | 🎉 1. Withdraw: 15 WLD |
| 14 | 45.11 WLD | - | Reset zu neuer Baseline |
| 26 | 90.56 WLD | **+100.8%** | 🎉 2. Withdraw: 22.7 WLD |
| 26 | 67.86 WLD | - | Reset |
| 38 | 136.27 WLD | **+100.7%** | 🎉 3. Withdraw: 34.2 WLD |

**Total nach 38 Monaten:**
- User erhält: 15 + 22.7 + 34.2 = **71.9 WLD** (~$144)
- Agent Balance: 102.07 WLD (~$204)
- **Total Value: 174 WLD (~$348) aus 30 WLD Start!** 🚀

## 📝 Trade History Anzeige:

Im Dashboard werden AutoWithdraw Trades angezeigt:

```
Typ: AutoWithdraw
Von → Nach: Agent → User
Amount: 15.00 WLD
Status: Success ✅
```

## ⚙️ Konfigurierbar?

**Aktuell:**
- Trigger: 100% Profit (fest)
- Withdraw: 50% der Gewinne (fest)

**Zukünftig könnte man Settings hinzufügen:**
```csharp
public class WorldTradingAgent
{
    public decimal WithdrawTriggerPercent { get; set; } = 100; // Default: 100%
    public decimal WithdrawAmountPercent { get; set; } = 50;   // Default: 50%
}
```

Dann User kann wählen:
- "Bei +50% Profit → 100% auszahlen" (sehr konservativ)
- "Bei +100% Profit → 50% auszahlen" (balanced - AKTUELL)
- "Bei +200% Profit → 25% auszahlen" (aggressiv)

## 🔐 Sicherheit:

**TODO für Production:**
```csharp
// Echter Transfer zum User Wallet:
var decryptedPrivateKey = DecryptPrivateKey(agent.EncryptedPrivateKey);
using var wallet = new Wallet(decryptedPrivateKey, provider);
var tx = await wldToken.transfer(agent.UserWalletAddress, withdrawAmount);
await tx.wait();
```

## 📱 Notification an User:

**Wenn Auto-Withdraw triggered:**
```javascript
// Backend sendet Notification
await _notificationService.SendNotificationAsync(userHash,
    "🎉 Auto-Withdraw!",
    $"Dein Trading Agent hat 100% Profit erreicht! {withdrawAmount} WLD wurden ausgezahlt.");
```

## 🎯 Zusammenfassung:

**Das System kombiniert:**
1. ✅ **Compound Interest** für schnelles Wachstum
2. ✅ **Auto-Withdraw** für Profit-Sicherung
3. ✅ **Kontinuierliches Trading** mit wachsendem Capital
4. ✅ **Automatisch** - User muss nichts tun

**Perfekt für:**
- Passives Einkommen
- Risiko-Management (Profit wird gesichert)
- Langfristiges Wachstum

🚀 **Ready to implement in Production!**
