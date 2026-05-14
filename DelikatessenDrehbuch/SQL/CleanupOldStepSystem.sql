-- ===================================================
-- CLEANUP: Alte Step-Systeme entfernen
-- Branch: new-ai-translate-system
-- ===================================================

-- WICHTIG: Vorher Backup machen!

PRINT '🧹 Starting cleanup of old step systems...';
GO

-- ===================================================
-- 1. SKIP: RecipeBaseData bleibt wie sie ist!
-- ===================================================

-- WICHTIG: Keine neuen Spalten in RecipeBaseData!
-- Stattdessen: Eigene RecipeSteps Tabelle (siehe CreateRecipeStepsTable.sql)
-- Ingredients sind schon mehrsprachig in Ingredient-Tabelle!

PRINT '✅ Skipping RecipeBaseData changes (using separate RecipeSteps table)';
GO

-- ===================================================
-- 2. Alte Tabellen löschen (VORSICHT!)
-- ===================================================

PRINT '🗑️  Dropping old tables...';
GO

-- WorldMiniApp: Neue AI Steps Tabelle (wurde gerade erst erstellt, wird nicht gebraucht)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WorldRecipeAiSteps')
BEGIN
    DROP TABLE WorldRecipeAiSteps;
    PRINT '  ❌ Dropped: WorldRecipeAiSteps';
END

-- SmartStep System (alt)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeJoinSmartStep')
BEGIN
    DROP TABLE RecipeJoinSmartStep;
    PRINT '  ❌ Dropped: RecipeJoinSmartStep';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SmartRecipeStep')
BEGIN
    DROP TABLE SmartRecipeStep;
    PRINT '  ❌ Dropped: SmartRecipeStep';
END

-- Alte Preparation Steps (falls nicht mehr gebraucht)
-- ACHTUNG: Nur löschen wenn sicher, dass nicht mehr gebraucht!
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeJoinPreparationSteps')
BEGIN
    DROP TABLE RecipeJoinPreparationSteps;
    PRINT '  ❌ Dropped: RecipeJoinPreparationSteps';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipePreparationSteps')
BEGIN
    DROP TABLE RecipePreparationSteps;
    PRINT '  ❌ Dropped: RecipePreparationSteps';
END

-- Join-Tabelle für Ingredient-Step-Verknüpfung (falls vorhanden)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'JoinIngredientPreparationStep')
BEGIN
    DROP TABLE JoinIngredientPreparationStep;
    PRINT '  ❌ Dropped: JoinIngredientPreparationStep';
END

GO

PRINT '✅ Old tables dropped successfully';
GO

-- ===================================================
-- 3. Zusammenfassung
-- ===================================================

PRINT '';
PRINT '====================================';
PRINT '✨ Cleanup completed!';
PRINT '';
PRINT 'Added columns:';
PRINT '  - RecipeBaseData.Ingredients[De|En|Esp|Prt|Id|Nl|Sv|Da|No|Ms]';
PRINT '  - RecipeBaseData.Steps[De|En|Esp|Prt|Id|Nl|Sv|Da|No|Ms]';
PRINT '';
PRINT 'Removed tables:';
PRINT '  - WorldRecipeAiSteps';
PRINT '  - RecipeJoinSmartStep';
PRINT '  - SmartRecipeStep';
PRINT '  - RecipeJoinPreparationSteps';
PRINT '  - RecipePreparationSteps';
PRINT '  - JoinIngredientPreparationStep';
PRINT '';
PRINT '⚠️  Next steps:';
PRINT '  1. Update C# models';
PRINT '  2. Delete unused JSON files';
PRINT '  3. Remove old services/controllers';
PRINT '  4. Simplify JavaScript';
PRINT '====================================';
GO
