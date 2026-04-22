/*
    DelikatessenDrehbuch - Azure SQL safe cleanup
    ------------------------------------------------------------
    Drops the legacy AI-variant tables that duplicated data as JSON/text.

    IMPORTANT:
    - Only run this after the app version is deployed that reads/writes ONLY v2:
      dbo.RecipeAiBaseRecipes + dbo.RecipeAiBaseRecipeIngredients/Steps/SelectedIngredients.
    - This script is intentionally defensive (IF EXISTS).
*/

SET NOCOUNT ON;

BEGIN TRY
    -- Drop children first (FKs into RecipeAiVariants)
    IF OBJECT_ID(N'dbo.RecipeAiVariantSelectedIngredients', N'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.RecipeAiVariantSelectedIngredients;
    END

    IF OBJECT_ID(N'dbo.RecipeAiVariantIngredientRows', N'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.RecipeAiVariantIngredientRows;
    END

    IF OBJECT_ID(N'dbo.RecipeAiVariants', N'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.RecipeAiVariants;
    END
END TRY
BEGIN CATCH
    DECLARE @msg nvarchar(4000) = ERROR_MESSAGE();
    RAISERROR(@msg, 16, 1);
END CATCH

