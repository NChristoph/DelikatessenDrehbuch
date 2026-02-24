-- =====================================================
-- Cleanup purchase schema:
-- 1) remove SenderUserId / ReceiverUserId from WorldMealplanPurcase
-- 2) drop legacy MealPlanPurchases table (if still present)
-- =====================================================

-- Remove deprecated sender/receiver columns from current purchase table
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SenderUserId'
    )
    BEGIN
        ALTER TABLE [WorldMealplanPurcase] DROP COLUMN [SenderUserId];
    END

    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'ReceiverUserId'
    )
    BEGIN
        ALTER TABLE [WorldMealplanPurcase] DROP COLUMN [ReceiverUserId];
    END
END
GO

-- Optional cleanup for old table if it still exists
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MealPlanPurchases')
BEGIN
    DROP TABLE [MealPlanPurchases];
END
GO
