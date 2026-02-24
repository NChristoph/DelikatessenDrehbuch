-- =====================================================
-- Add world purchase tracking fields used during finalize
-- Supports both table names:
--   - WorldMealplanPurcase (current)
--   - MealPlanPurchases (legacy)
-- =====================================================

-- ---------- WorldMealplanPurcase ----------
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'PurchaseTransactionId')
        ALTER TABLE [WorldMealplanPurcase] ADD [PurchaseTransactionId] NVARCHAR(130) NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'OrderTimestampUtc')
        ALTER TABLE [WorldMealplanPurcase] ADD [OrderTimestampUtc] DATETIME2 NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'Status')
        ALTER TABLE [WorldMealplanPurcase] ADD [Status] NVARCHAR(40) NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SellerCredited')
        ALTER TABLE [WorldMealplanPurcase] ADD [SellerCredited] BIT NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SellerCreditedAtUtc')
        ALTER TABLE [WorldMealplanPurcase] ADD [SellerCreditedAtUtc] DATETIME2 NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'CashoutTransactionId')
        ALTER TABLE [WorldMealplanPurcase] ADD [CashoutTransactionId] NVARCHAR(130) NULL;
END
GO

-- ---------- MealPlanPurchases (fallback) ----------
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MealPlanPurchases')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'PurchaseTransactionId')
        ALTER TABLE [MealPlanPurchases] ADD [PurchaseTransactionId] NVARCHAR(130) NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'OrderTimestampUtc')
        ALTER TABLE [MealPlanPurchases] ADD [OrderTimestampUtc] DATETIME2 NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'Status')
        ALTER TABLE [MealPlanPurchases] ADD [Status] NVARCHAR(40) NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'SellerCredited')
        ALTER TABLE [MealPlanPurchases] ADD [SellerCredited] BIT NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'SellerCreditedAtUtc')
        ALTER TABLE [MealPlanPurchases] ADD [SellerCreditedAtUtc] DATETIME2 NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'CashoutTransactionId')
        ALTER TABLE [MealPlanPurchases] ADD [CashoutTransactionId] NVARCHAR(130) NULL;
END
GO
