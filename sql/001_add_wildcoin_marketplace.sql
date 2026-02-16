-- =====================================================
-- WildCoin Marketplace - Azure SQL Migration Script
-- Run against your Azure SQL Database
-- =====================================================

-- 1. WildCoinBalance Spalte auf WorldAppUser
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldAppUser' AND COLUMN_NAME = 'WildCoinBalance'
)
BEGIN
    ALTER TABLE [WorldAppUser]
    ADD [WildCoinBalance] DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO

-- 2. MealPlanListings Tabelle
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MealPlanListings')
BEGIN
    CREATE TABLE [MealPlanListings] (
        [Id]           INT IDENTITY(1,1) NOT NULL,
        [SellerHash]   NVARCHAR(450)     NOT NULL,
        [SellerName]   NVARCHAR(256)     NOT NULL,
        [MealPlanId]   INT               NOT NULL,
        [Title]        NVARCHAR(256)     NOT NULL,
        [Description]  NVARCHAR(1000)    NULL,
        [Price]        DECIMAL(18,2)     NOT NULL,
        [DayCount]     INT               NOT NULL DEFAULT 0,
        [RecipeCount]  INT               NOT NULL DEFAULT 0,
        [CreatedAt]    DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
        [IsActive]     BIT               NOT NULL DEFAULT 1,
        [SoldCount]    INT               NOT NULL DEFAULT 0,

        CONSTRAINT [PK_MealPlanListings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MealPlanListings_MealPlan] FOREIGN KEY ([MealPlanId])
            REFERENCES [WorldUserMealPlan]([Id])
    );

    CREATE INDEX [IX_MealPlanListings_SellerHash] ON [MealPlanListings]([SellerHash]);
    CREATE INDEX [IX_MealPlanListings_IsActive_CreatedAt] ON [MealPlanListings]([IsActive], [CreatedAt] DESC);
END
GO

-- 3. WildCoinTransactions Tabelle
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WildCoinTransactions')
BEGIN
    CREATE TABLE [WildCoinTransactions] (
        [Id]            INT IDENTITY(1,1) NOT NULL,
        [UserHash]      NVARCHAR(450)     NOT NULL,
        [Amount]        DECIMAL(18,2)     NOT NULL,
        [BalanceAfter]  DECIMAL(18,2)     NOT NULL,
        [Type]          NVARCHAR(50)      NOT NULL,
        [ReferenceInfo] NVARCHAR(500)     NULL,
        [CreatedAt]     DATETIME2         NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_WildCoinTransactions] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_WildCoinTransactions_UserHash_CreatedAt]
        ON [WildCoinTransactions]([UserHash], [CreatedAt] DESC);
END
GO

-- 4. MealPlanPurchases Tabelle
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MealPlanPurchases')
BEGIN
    CREATE TABLE [MealPlanPurchases] (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [BuyerHash]         NVARCHAR(450)     NOT NULL,
        [ListingId]         INT               NOT NULL,
        [CreatedMealPlanId] INT               NOT NULL,
        [PricePaid]         DECIMAL(18,2)     NOT NULL,
        [PurchasedAt]       DATETIME2         NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_MealPlanPurchases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MealPlanPurchases_Listing] FOREIGN KEY ([ListingId])
            REFERENCES [MealPlanListings]([Id]),
        CONSTRAINT [FK_MealPlanPurchases_MealPlan] FOREIGN KEY ([CreatedMealPlanId])
            REFERENCES [WorldUserMealPlan]([Id])
    );

    CREATE INDEX [IX_MealPlanPurchases_BuyerHash] ON [MealPlanPurchases]([BuyerHash]);
    CREATE INDEX [IX_MealPlanPurchases_ListingId] ON [MealPlanPurchases]([ListingId]);
END
GO
