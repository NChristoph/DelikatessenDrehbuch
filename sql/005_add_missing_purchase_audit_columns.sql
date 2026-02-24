-- =====================================================
-- Add missing purchase audit/payout columns used by app model
-- Supports both legacy table names:
--   - WorldMealplanPurcase (current production)
--   - MealPlanPurchases (older setup)
-- =====================================================

-- ---------- WorldMealplanPurcase ----------
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SellerHash'
    )
    BEGIN
        ALTER TABLE [WorldMealplanPurcase]
        ADD [SellerHash] NVARCHAR(450) NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SellerWalletAddress'
    )
    BEGIN
        ALTER TABLE [WorldMealplanPurcase]
        ADD [SellerWalletAddress] NVARCHAR(128) NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'CreatorAmount'
    )
    BEGIN
        ALTER TABLE [WorldMealplanPurcase]
        ADD [CreatorAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    END

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'PlatformFee'
    )
    BEGIN
        ALTER TABLE [WorldMealplanPurcase]
        ADD [PlatformFee] DECIMAL(18,2) NOT NULL DEFAULT 0;
    END
END
GO

-- ---------- MealPlanPurchases (fallback) ----------
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MealPlanPurchases')
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'SellerHash'
    )
    BEGIN
        ALTER TABLE [MealPlanPurchases]
        ADD [SellerHash] NVARCHAR(450) NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'SellerWalletAddress'
    )
    BEGIN
        ALTER TABLE [MealPlanPurchases]
        ADD [SellerWalletAddress] NVARCHAR(128) NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'CreatorAmount'
    )
    BEGIN
        ALTER TABLE [MealPlanPurchases]
        ADD [CreatorAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    END

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'PlatformFee'
    )
    BEGIN
        ALTER TABLE [MealPlanPurchases]
        ADD [PlatformFee] DECIMAL(18,2) NOT NULL DEFAULT 0;
    END
END
GO
