-- =============================================
-- Migration: AddPhaseToPreperationSteps
-- Adds Phase column and classifies existing steps
-- =============================================

-- 1) Add Phase column (default 0 = Sonstige/Unbekannt)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'RecipePreperationSteps' AND COLUMN_NAME = 'Phase'
)
BEGIN
    ALTER TABLE [RecipePreperationSteps] ADD [Phase] int NOT NULL DEFAULT 0;
END
GO

-- 2) Phase 1 = Vorbereitung
UPDATE RecipePreperationSteps SET Phase = 1
WHERE Phase = 0 AND (
    Step_DE LIKE '%schneid%' OR Step_DE LIKE N'%schäl%' OR Step_DE LIKE '%wasch%'
    OR Step_DE LIKE N'%würfel%' OR Step_DE LIKE '%reib%' OR Step_DE LIKE '%einweich%'
    OR Step_DE LIKE '%hack%' OR Step_DE LIKE '%zerklein%' OR Step_DE LIKE '%hobel%'
    OR Step_DE LIKE '%pell%' OR Step_DE LIKE '%entkern%' OR Step_DE LIKE '%filetier%'
    OR Step_DE LIKE '%vorbereite%' OR Step_DE LIKE '%halbier%' OR Step_DE LIKE '%vierteln%'
);
GO

-- 3) Phase 2 = Kochen
UPDATE RecipePreperationSteps SET Phase = 2
WHERE Phase = 0 AND (
    Step_DE LIKE '%anbrat%' OR Step_DE LIKE '%koch%' OR Step_DE LIKE N'%dünst%'
    OR Step_DE LIKE '%back%' OR Step_DE LIKE '%grill%' OR Step_DE LIKE '%frittier%'
    OR Step_DE LIKE '%blanchier%' OR Step_DE LIKE '%brat%' OR Step_DE LIKE '%schmo%'
    OR Step_DE LIKE '%gart%' OR Step_DE LIKE '%garen%' OR Step_DE LIKE '%simmern%'
    OR Step_DE LIKE '%aufkochen%' OR Step_DE LIKE N'%röst%' OR Step_DE LIKE '%dampf%'
    OR Step_DE LIKE '%reduzier%' OR Step_DE LIKE '%karamellis%'
);
GO

-- 4) Phase 3 = Würzen
UPDATE RecipePreperationSteps SET Phase = 3
WHERE Phase = 0 AND (
    Step_DE LIKE '%salz%' OR Step_DE LIKE '%pfeff%' OR Step_DE LIKE N'%würz%'
    OR Step_DE LIKE '%abschmeck%' OR Step_DE LIKE '%marinier%'
    OR Step_DE LIKE N'%beträufel%' OR Step_DE LIKE '%bestreu%'
);
GO

-- 5) Phase 4 = Finish
UPDATE RecipePreperationSteps SET Phase = 4
WHERE Phase = 0 AND (
    Step_DE LIKE '%anricht%' OR Step_DE LIKE '%garnier%' OR Step_DE LIKE '%servier%'
    OR Step_DE LIKE N'%auskühlen%' OR Step_DE LIKE '%dekorier%'
    OR Step_DE LIKE '%portionier%'
);
GO

-- 6) Verify
SELECT Phase, COUNT(*) AS Anzahl FROM RecipePreperationSteps GROUP BY Phase ORDER BY Phase;
GO
