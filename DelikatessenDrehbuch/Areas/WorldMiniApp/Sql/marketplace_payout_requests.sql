-- =============================================================================
-- MarketplacePayoutRequests
-- Auszahlungsanträge der Verkäufer (Marktplatz, Modell B Cash-out).
-- Einmalig auf der Azure-SQL-DB ausführen. Idempotent. Kein GO (Azure-safe).
-- =============================================================================

IF OBJECT_ID(N'dbo.MarketplacePayoutRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MarketplacePayoutRequests
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MarketplacePayoutRequests PRIMARY KEY,
        SellerHash    NVARCHAR(256) NOT NULL,
        Amount        DECIMAL(18,6) NOT NULL,
        Token         NVARCHAR(16) NOT NULL CONSTRAINT DF_MPR_Token DEFAULT (N'WLD'),
        WalletAddress NVARCHAR(128) NOT NULL,
        Status        NVARCHAR(16) NOT NULL CONSTRAINT DF_MPR_Status DEFAULT (N'pending'),
        TxHash        NVARCHAR(128) NULL,
        Note          NVARCHAR(400) NULL,
        RequestedAt   DATETIME2 NOT NULL CONSTRAINT DF_MPR_RequestedAt DEFAULT (SYSUTCDATETIME()),
        ProcessedAt   DATETIME2 NULL,
        ProcessedBy   NVARCHAR(256) NULL
    );

    CREATE INDEX IX_MarketplacePayoutRequests_SellerHash ON dbo.MarketplacePayoutRequests (SellerHash, RequestedAt DESC);
    CREATE INDEX IX_MarketplacePayoutRequests_Status ON dbo.MarketplacePayoutRequests (Status, RequestedAt);
END;
