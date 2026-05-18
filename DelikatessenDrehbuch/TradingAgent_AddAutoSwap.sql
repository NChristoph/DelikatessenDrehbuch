-- Add Auto-Swap columns to existing WorldTradingAgents table
-- Execute this if you already have the WorldTradingAgents table

-- Check if columns already exist before adding
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WorldTradingAgents]') AND name = 'CurrentBalanceETH')
BEGIN
    ALTER TABLE [dbo].[WorldTradingAgents]
    ADD [CurrentBalanceETH] DECIMAL(18,9) NOT NULL DEFAULT 0;
    PRINT 'Added CurrentBalanceETH column';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WorldTradingAgents]') AND name = 'AutoSwapThresholdETH')
BEGIN
    ALTER TABLE [dbo].[WorldTradingAgents]
    ADD [AutoSwapThresholdETH] DECIMAL(18,9) NOT NULL DEFAULT 0.0005;
    PRINT 'Added AutoSwapThresholdETH column';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WorldTradingAgents]') AND name = 'AutoSwapAmountWLD')
BEGIN
    ALTER TABLE [dbo].[WorldTradingAgents]
    ADD [AutoSwapAmountWLD] DECIMAL(18,6) NOT NULL DEFAULT 0.5;
    PRINT 'Added AutoSwapAmountWLD column';
END

PRINT 'Auto-Swap columns added successfully!';
GO
