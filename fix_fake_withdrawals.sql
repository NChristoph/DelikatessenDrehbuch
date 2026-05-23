-- ===================================================
-- Fix Fake Auto-Withdrawals in Trading Agent DB
-- ===================================================
-- Diese Withdrawals wurden NIE auf der Blockchain ausgeführt!
-- Sie müssen als "Simulated" markiert werden.

USE [DelikatessenDrehbuch]; -- Passe den DB-Namen an falls nötig
GO

-- 1. Zeige die betroffenen Trades an
SELECT
    Id,
    AgentId,
    TradeType,
    FromAmount,
    ToAmount,
    Status,
    ExecutedAt,
    DexUsed
FROM WorldAgentTrades
WHERE TradeType = 'AutoWithdraw'
  AND Status = 'Success'
  AND DexUsed = 'Auto-Withdrawal';

-- 2. Markiere die Fake-Withdrawals als "Simulated"
UPDATE WorldAgentTrades
SET
    Status = 'Simulated',
    DexUsed = 'Auto-Withdrawal-SIMULATED'
WHERE TradeType = 'AutoWithdraw'
  AND Status = 'Success'
  AND DexUsed = 'Auto-Withdrawal';

PRINT '✅ Fake Auto-Withdrawals wurden als SIMULATED markiert';

-- 3. Optional: Zeige die aktualisierten Trades
SELECT
    Id,
    AgentId,
    TradeType,
    FromAmount,
    Status,
    DexUsed
FROM WorldAgentTrades
WHERE TradeType = 'AutoWithdraw';

-- 4. Balance des Agents aktualisieren (auf echte Werte zurücksetzen)
-- WICHTIG: Passe die Agent ID an (hier: AgentId = 1)
DECLARE @AgentId INT = 1;
DECLARE @RealWLDBalance DECIMAL(18,6);
DECLARE @InitialFunding DECIMAL(18,6);

-- Hole den Initial Funding Wert
SELECT @InitialFunding = InitialFundingWLD
FROM WorldTradingAgents
WHERE Id = @AgentId;

-- Berechne echte Balance (alle erfolgreichen Swaps berücksichtigen)
-- Hinweis: Du musst hier die ECHTE Balance von WorldScan eintragen!
-- Beispiel: Wenn auf WorldScan 28 WLD stehen, trage hier 28 ein
SET @RealWLDBalance = 28.0; -- ⚠️ ANPASSEN an echten Wert von WorldScan!

-- Update Agent Balance
UPDATE WorldTradingAgents
SET
    CurrentBalanceWLD = @RealWLDBalance,
    TotalProfitLossWLD = @RealWLDBalance - @InitialFunding,
    ProfitLossPercentage = CASE
        WHEN @InitialFunding > 0
        THEN ((@RealWLDBalance - @InitialFunding) / @InitialFunding) * 100
        ELSE 0
    END,
    LastBalanceUpdate = GETUTCDATE()
WHERE Id = @AgentId;

PRINT '✅ Agent Balance wurde auf echten Wert zurückgesetzt';

-- 5. Zeige die korrigierte Agent-Info
SELECT
    Id,
    WalletAddress,
    Status,
    CurrentBalanceWLD,
    CurrentBalanceETH,
    TotalProfitLossWLD,
    ProfitLossPercentage,
    InitialFundingWLD
FROM WorldTradingAgents
WHERE Id = @AgentId;

GO
