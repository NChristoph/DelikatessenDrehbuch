/*
  Safe patch script for production/Azure.
  Goal:
  - Ensure RecipeAiVariants has columns: AiProvider, IngredientsText, PreparationText, SelectedIngredientKey
  - Ensure RecipeAiVariantIngredientRows has IngredientMeasureQuantityId + FK + Index
  - Optional: backfill IngredientMeasureQuantityId for existing rows (best-effort)
  - Optional: clear legacy columns and/or drop them later (ONLY after verifying all rows are linked)
*/

SET NOCOUNT ON;

/* === 1) RecipeAiVariants: add missing columns (safe) === */
IF COL_LENGTH('dbo.RecipeAiVariants', 'AiProvider') IS NULL
BEGIN
    ALTER TABLE dbo.RecipeAiVariants
    ADD AiProvider nvarchar(32) NOT NULL CONSTRAINT DF_RecipeAiVariants_AiProvider DEFAULT ('openai');
END

IF COL_LENGTH('dbo.RecipeAiVariants', 'IngredientsText') IS NULL
BEGIN
    ALTER TABLE dbo.RecipeAiVariants
    ADD IngredientsText nvarchar(max) NULL;
END

IF COL_LENGTH('dbo.RecipeAiVariants', 'PreparationText') IS NULL
BEGIN
    ALTER TABLE dbo.RecipeAiVariants
    ADD PreparationText nvarchar(max) NULL;
END

IF COL_LENGTH('dbo.RecipeAiVariants', 'SelectedIngredientKey') IS NULL
BEGIN
    ALTER TABLE dbo.RecipeAiVariants
    ADD SelectedIngredientKey nvarchar(400) NULL;
END

/* Backfill SelectedIngredientKey if missing (keeps existing behavior compatible) */
IF COL_LENGTH('dbo.RecipeAiVariants', 'SelectedIngredientKey') IS NOT NULL
BEGIN
    UPDATE v
    SET v.SelectedIngredientKey = ''
    FROM dbo.RecipeAiVariants v
    WHERE v.SelectedIngredientKey IS NULL;

    ALTER TABLE dbo.RecipeAiVariants
    ALTER COLUMN SelectedIngredientKey nvarchar(400) NOT NULL;
END

/* === 2) RecipeAiVariantIngredientRows: ensure IngredientMeasureQuantityId === */
IF OBJECT_ID('dbo.RecipeAiVariantIngredientRows', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'IngredientMeasureQuantityId') IS NULL
BEGIN
    ALTER TABLE dbo.RecipeAiVariantIngredientRows
    ADD IngredientMeasureQuantityId int NULL;
END

/* Index (only if column exists) */
IF OBJECT_ID('dbo.RecipeAiVariantIngredientRows', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'IngredientMeasureQuantityId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_RecipeAiVariantIngredientRows_IngredientMeasureQuantityId'
          AND object_id = OBJECT_ID('dbo.RecipeAiVariantIngredientRows')
   )
BEGIN
    EXEC sp_executesql N'CREATE INDEX IX_RecipeAiVariantIngredientRows_IngredientMeasureQuantityId
                         ON dbo.RecipeAiVariantIngredientRows(IngredientMeasureQuantityId);';
END

/* FK (only if column exists) */
IF OBJECT_ID('dbo.RecipeAiVariantIngredientRows', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'IngredientMeasureQuantityId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = 'FK_RecipeAiVariantIngredientRows_IngredientMeasureQuantity_IngredientMeasureQuantityId'
          AND parent_object_id = OBJECT_ID('dbo.RecipeAiVariantIngredientRows')
   )
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.RecipeAiVariantIngredientRows WITH NOCHECK
                         ADD CONSTRAINT FK_RecipeAiVariantIngredientRows_IngredientMeasureQuantity_IngredientMeasureQuantityId
                             FOREIGN KEY (IngredientMeasureQuantityId)
                             REFERENCES dbo.IngredientMeasureQuantity(Id);';
END

/* === 3) Optional backfill: set IngredientMeasureQuantityId from existing legacy columns ===
   - Best effort: parses leading numeric token from QuantityText (fractions like 1/2 are skipped).
   - Requires legacy columns IngredientId, MeasureId, QuantityText to exist.
*/
IF OBJECT_ID('dbo.RecipeAiVariantIngredientRows', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'IngredientMeasureQuantityId') IS NOT NULL
   AND COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'IngredientId') IS NOT NULL
   AND COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'MeasureId') IS NOT NULL
   AND COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'QuantityText') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        DECLARE @rowId int, @ingredientId int, @measureId int, @qtyText nvarchar(128);
        DECLARE @token nvarchar(64), @qty float, @quantityId int, @imqId int;

        DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT r.Id, r.IngredientId, r.MeasureId, r.QuantityText
            FROM dbo.RecipeAiVariantIngredientRows r
            WHERE r.IngredientMeasureQuantityId IS NULL
              AND r.IngredientId IS NOT NULL
              AND r.MeasureId IS NOT NULL
              AND r.QuantityText IS NOT NULL
              AND LEN(LTRIM(RTRIM(r.QuantityText))) > 0;

        OPEN cur;
        FETCH NEXT FROM cur INTO @rowId, @ingredientId, @measureId, @qtyText;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @token = LTRIM(RTRIM(LEFT(@qtyText, CHARINDEX('' '', @qtyText + '' '') - 1)));
            /* skip fractions like 1/2 */
            IF CHARINDEX(''/'', @token) = 0
            BEGIN
                SET @qty = TRY_CONVERT(float, REPLACE(@token, '','', ''.''));
                IF @qty IS NOT NULL AND @qty > 0
                BEGIN
                    /* Quantity */
                    SELECT TOP (1) @quantityId = q.Id
                    FROM dbo.Quantities q
                    WHERE ABS(q.Quantitys - @qty) < 0.0001;

                    IF @quantityId IS NULL
                    BEGIN
                        INSERT INTO dbo.Quantities(Quantitys) VALUES (@qty);
                        SET @quantityId = SCOPE_IDENTITY();
                    END

                    /* IngredientMeasureQuantity */
                    SELECT TOP (1) @imqId = imq.Id
                    FROM dbo.IngredientMeasureQuantity imq
                    WHERE imq.IngredientsAndNutrientsId = @ingredientId
                      AND imq.QuantityId = @quantityId
                      AND imq.MeasureId = @measureId;

                    IF @imqId IS NULL
                    BEGIN
                        INSERT INTO dbo.IngredientMeasureQuantity(IngredientsAndNutrientsId, QuantityId, MeasureId)
                        VALUES (@ingredientId, @quantityId, @measureId);
                        SET @imqId = SCOPE_IDENTITY();
                    END

                    UPDATE dbo.RecipeAiVariantIngredientRows
                    SET IngredientMeasureQuantityId = @imqId
                    WHERE Id = @rowId;
                END
            END

            SET @quantityId = NULL;
            SET @imqId = NULL;
            SET @qty = NULL;

            FETCH NEXT FROM cur INTO @rowId, @ingredientId, @measureId, @qtyText;
        END

        CLOSE cur;
        DEALLOCATE cur;
    ';
END

/* === 4) Optional: clear legacy columns (ONLY after verifying backfill coverage) ===
   Uncomment when you're sure you no longer need free-text fallbacks.
*/
/*
UPDATE dbo.RecipeAiVariantIngredientRows
SET IngredientId = NULL,
    MeasureId = NULL,
    DisplayName = '',
    QuantityText = ''
WHERE IngredientMeasureQuantityId IS NOT NULL;
*/

/* === 5) Optional: drop legacy columns (ONLY after app is fully migrated) ===
   This is irreversible. Do it in a later release once you confirm everything works.
*/
/*
IF COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'IngredientId') IS NOT NULL
    ALTER TABLE dbo.RecipeAiVariantIngredientRows DROP COLUMN IngredientId;
IF COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'MeasureId') IS NOT NULL
    ALTER TABLE dbo.RecipeAiVariantIngredientRows DROP COLUMN MeasureId;
IF COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'DisplayName') IS NOT NULL
    ALTER TABLE dbo.RecipeAiVariantIngredientRows DROP COLUMN DisplayName;
IF COL_LENGTH('dbo.RecipeAiVariantIngredientRows', 'QuantityText') IS NOT NULL
    ALTER TABLE dbo.RecipeAiVariantIngredientRows DROP COLUMN QuantityText;
*/
