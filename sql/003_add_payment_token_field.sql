-- =====================================================
-- PaymentToken Field - Azure SQL Migration Script
-- Run against your Azure SQL Database
-- =====================================================

-- 1) Add PaymentToken column to MealPlanPurchases
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'PaymentToken'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [PaymentToken] NVARCHAR(20) NOT NULL DEFAULT 'WLD';
END
GO
