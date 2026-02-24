-- =====================================================
-- Cleanup purchase schema:
-- 1) remove SenderUserId / ReceiverUserId from WorldMealplanPurcase
--    (including dependent constraints)
-- 2) drop legacy MealPlanPurchases table (if still present)
-- =====================================================

IF OBJECT_ID(N'[dbo].[WorldMealplanPurcase]', N'U') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';

    -- Drop FK constraints that reference SenderUserId/ReceiverUserId
    ;WITH fk_to_drop AS (
        SELECT DISTINCT fk.name
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[WorldMealplanPurcase]')
          AND c.name IN (N'SenderUserId', N'ReceiverUserId')
    )
    SELECT @sql = @sql + N'ALTER TABLE [dbo].[WorldMealplanPurcase] DROP CONSTRAINT [' + name + N'];' + CHAR(10)
    FROM fk_to_drop;

    -- Drop default constraints on these columns
    ;WITH df_to_drop AS (
        SELECT dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[WorldMealplanPurcase]')
          AND c.name IN (N'SenderUserId', N'ReceiverUserId')
    )
    SELECT @sql = @sql + N'ALTER TABLE [dbo].[WorldMealplanPurcase] DROP CONSTRAINT [' + name + N'];' + CHAR(10)
    FROM df_to_drop;

    IF LEN(@sql) > 0
        EXEC sp_executesql @sql;

    -- Drop columns afterwards
    IF COL_LENGTH(N'dbo.WorldMealplanPurcase', N'SenderUserId') IS NOT NULL
        ALTER TABLE [dbo].[WorldMealplanPurcase] DROP COLUMN [SenderUserId];

    IF COL_LENGTH(N'dbo.WorldMealplanPurcase', N'ReceiverUserId') IS NOT NULL
        ALTER TABLE [dbo].[WorldMealplanPurcase] DROP COLUMN [ReceiverUserId];
END
GO

-- Optional cleanup for old table if it still exists
IF OBJECT_ID(N'[dbo].[MealPlanPurchases]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[MealPlanPurchases];
END
GO
