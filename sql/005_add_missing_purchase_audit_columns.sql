-- =====================================================
-- Add missing purchase audit/payout columns used by app model
-- =====================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'SellerHash'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [SellerHash] NVARCHAR(450) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'SellerWalletAddress'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [SellerWalletAddress] NVARCHAR(128) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'CreatorAmount'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [CreatorAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'PlatformFee'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [PlatformFee] DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO
