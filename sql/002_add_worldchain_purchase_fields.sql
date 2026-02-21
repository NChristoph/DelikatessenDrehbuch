-- =====================================================
-- World Chain Purchase Fields - Azure SQL Migration Script
-- Run against your Azure SQL Database
-- =====================================================

-- 1) Add optional chain transaction hash to MealPlanPurchases
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'ReferenceTxHash'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [ReferenceTxHash] NVARCHAR(130) NULL;
END
GO

-- 2) Add optional buyer wallet address to MealPlanPurchases
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'BuyerWalletAddress'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [BuyerWalletAddress] NVARCHAR(64) NULL;
END
GO

-- 3) Speed up lookup and duplicate protection by listing/buyer/tx
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MealPlanPurchases_ListingBuyerTx'
      AND object_id = OBJECT_ID('MealPlanPurchases')
)
BEGIN
    CREATE INDEX [IX_MealPlanPurchases_ListingBuyerTx]
        ON [MealPlanPurchases]([ListingId], [BuyerHash], [ReferenceTxHash]);
END
GO

-- 4) Optional uniqueness for non-null chain tx hashes
-- Prevents same transaction hash being inserted twice
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_MealPlanPurchases_ReferenceTxHash_NotNull'
      AND object_id = OBJECT_ID('MealPlanPurchases')
)
BEGIN
    CREATE UNIQUE INDEX [UX_MealPlanPurchases_ReferenceTxHash_NotNull]
        ON [MealPlanPurchases]([ReferenceTxHash])
        WHERE [ReferenceTxHash] IS NOT NULL;
END
GO
