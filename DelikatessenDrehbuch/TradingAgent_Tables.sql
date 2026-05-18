-- Trading Agent Tables
-- Execute this SQL script to create the Trading Agent tables

-- Create WorldTradingAgents table
CREATE TABLE [dbo].[WorldTradingAgents] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserHash] NVARCHAR(256) NOT NULL,
    [WalletAddress] NVARCHAR(42) NOT NULL,
    [EncryptedPrivateKey] NVARCHAR(MAX) NOT NULL,
    [Status] NVARCHAR(20) NOT NULL,
    [InitialFundingWLD] DECIMAL(18,6) NOT NULL,
    [CurrentBalanceWLD] DECIMAL(18,6) NOT NULL,
    [CurrentBalanceETH] DECIMAL(18,9) NOT NULL DEFAULT 0,
    [AutoSwapThresholdETH] DECIMAL(18,9) NOT NULL DEFAULT 0.0005,
    [AutoSwapAmountWLD] DECIMAL(18,6) NOT NULL DEFAULT 0.5,
    [TotalProfitLossWLD] DECIMAL(18,6) NOT NULL,
    [ProfitLossPercentage] DECIMAL(8,4) NOT NULL,
    [TotalTrades] INT NOT NULL,
    [SuccessfulTrades] INT NOT NULL,
    [Strategy] NVARCHAR(50) NOT NULL,
    [RiskLevel] NVARCHAR(20) NOT NULL,
    [StopLossPercentage] DECIMAL(5,2) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [StartedAt] DATETIME2 NULL,
    [StoppedAt] DATETIME2 NULL,
    [LastBalanceUpdate] DATETIME2 NULL,
    [LastTradeAt] DATETIME2 NULL,
    [AgentName] NVARCHAR(100) NULL,
    CONSTRAINT [PK_WorldTradingAgents] PRIMARY KEY ([Id])
);
GO

-- Create WorldAgentTrades table
CREATE TABLE [dbo].[WorldAgentTrades] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [AgentId] INT NOT NULL,
    [TxHash] NVARCHAR(66) NULL,
    [TradeType] NVARCHAR(20) NOT NULL,
    [FromToken] NVARCHAR(42) NOT NULL,
    [ToToken] NVARCHAR(42) NOT NULL,
    [FromAmount] DECIMAL(18,6) NOT NULL,
    [ToAmount] DECIMAL(18,6) NOT NULL,
    [GasFee] DECIMAL(18,9) NOT NULL,
    [ProfitLossWLD] DECIMAL(18,6) NOT NULL,
    [Status] NVARCHAR(20) NOT NULL,
    [ErrorMessage] NVARCHAR(MAX) NULL,
    [ExecutedAt] DATETIME2 NOT NULL,
    [DexUsed] NVARCHAR(50) NULL,
    CONSTRAINT [PK_WorldAgentTrades] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorldAgentTrades_WorldTradingAgents_AgentId] FOREIGN KEY ([AgentId])
        REFERENCES [dbo].[WorldTradingAgents] ([Id])
        ON DELETE CASCADE
);
GO

-- Create index on AgentId for faster trade lookups
CREATE INDEX [IX_WorldAgentTrades_AgentId] ON [dbo].[WorldAgentTrades] ([AgentId]);
GO

PRINT 'Trading Agent tables created successfully!';
