-- Migration: Alt → Neu
-- Migriert bestehende Steps von RecipePreparationSteps → RecipeStep + RecipeStepTranslation
-- WICHTIG: Erst testen, dann in Production ausführen!

-- 1. Prüfe wie viele Steps migriert werden müssen
SELECT
    COUNT(*) as TotalSteps,
    COUNT(CASE WHEN Step_DE IS NOT NULL THEN 1 END) as HasDE,
    COUNT(CASE WHEN Step_EN IS NOT NULL THEN 1 END) as HasEN,
    COUNT(CASE WHEN Step_ESP IS NOT NULL THEN 1 END) as HasESP,
    COUNT(CASE WHEN Step_PRT IS NOT NULL THEN 1 END) as HasPRT
FROM RecipePreparationSteps;

-- 2. Migriere Steps (Source Language = Deutsch)
-- WARNUNG: Läuft nur wenn RecipeStep/RecipeStepTranslation Tabellen existieren!

-- Erst Backup erstellen!
-- Uncomment um Backup zu erstellen:
-- SELECT * INTO RecipePreparationSteps_BACKUP FROM RecipePreparationSteps;
-- SELECT * INTO RecipeJoinPreparationSteps_BACKUP FROM RecipeJoinPreparationSteps;

-- Migration starten (Kommentare entfernen wenn bereit!)
/*
BEGIN TRANSACTION;

DECLARE @Counter INT = 0;
DECLARE @TotalRecipes INT;

-- Zähle Rezepte
SELECT @TotalRecipes = COUNT(DISTINCT RecipeId)
FROM RecipeJoinPreparationSteps;

PRINT 'Migriere ' + CAST(@TotalRecipes AS VARCHAR) + ' Rezepte...';

-- Cursor für alle Rezepte
DECLARE @RecipeId INT;
DECLARE RecipeCursor CURSOR FOR
    SELECT DISTINCT Recipe.Id
    FROM RecipeBaseData Recipe
    INNER JOIN RecipeJoinPreparationSteps RJ ON RJ.Recipe_Id = Recipe.Id;

OPEN RecipeCursor;
FETCH NEXT FROM RecipeCursor INTO @RecipeId;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Cursor für Steps eines Rezepts
    DECLARE @OldStepId INT;
    DECLARE @StepIndex INT;
    DECLARE @StepDE NVARCHAR(MAX);
    DECLARE @StepEN NVARCHAR(MAX);
    DECLARE @StepESP NVARCHAR(MAX);
    DECLARE @StepPRT NVARCHAR(MAX);

    DECLARE StepCursor CURSOR FOR
        SELECT
            RPS.Id,
            RJ.StepIndex,
            RPS.Step_DE,
            RPS.Step_EN,
            RPS.Step_ESP,
            RPS.Step_PRT
        FROM RecipeJoinPreparationSteps RJ
        INNER JOIN RecipePreparationSteps RPS ON RPS.Id = RJ.PreparationStepId
        WHERE RJ.Recipe_Id = @RecipeId
        ORDER BY RJ.StepIndex;

    OPEN StepCursor;
    FETCH NEXT FROM StepCursor INTO @OldStepId, @StepIndex, @StepDE, @StepEN, @StepESP, @StepPRT;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @NewStepId INT;

        -- 1. Erstelle RecipeStep (Source = DE)
        IF @StepDE IS NOT NULL AND LEN(@StepDE) > 0
        BEGIN
            INSERT INTO RecipeStep (RecipeId, StepOrder, StepText, SourceLanguage, CreatedAt)
            VALUES (@RecipeId, @StepIndex, @StepDE, 'de', GETUTCDATE());

            SET @NewStepId = SCOPE_IDENTITY();

            -- 2. Füge Übersetzungen hinzu
            -- Englisch
            IF @StepEN IS NOT NULL AND LEN(@StepEN) > 0
            BEGIN
                INSERT INTO RecipeStepTranslation (RecipeId, StepId, Language, TranslatedText, TranslatedAt, TranslationProvider, IsCoreSprache)
                VALUES (@RecipeId, @NewStepId, 'en', @StepEN, GETUTCDATE(), 'migrated', 1);
            END

            -- Spanisch
            IF @StepESP IS NOT NULL AND LEN(@StepESP) > 0
            BEGIN
                INSERT INTO RecipeStepTranslation (RecipeId, @NewStepId, Language, TranslatedText, TranslatedAt, TranslationProvider, IsCoreSprache)
                VALUES (@RecipeId, @NewStepId, 'esp', @StepESP, GETUTCDATE(), 'migrated', 1);
            END

            -- Portugiesisch
            IF @StepPRT IS NOT NULL AND LEN(@StepPRT) > 0
            BEGIN
                INSERT INTO RecipeStepTranslation (RecipeId, StepId, Language, TranslatedText, TranslatedAt, TranslationProvider, IsCoreSprache)
                VALUES (@RecipeId, @NewStepId, 'prt', @StepPRT, GETUTCDATE(), 'migrated', 1);
            END
        END

        FETCH NEXT FROM StepCursor INTO @OldStepId, @StepIndex, @StepDE, @StepEN, @StepESP, @StepPRT;
    END

    CLOSE StepCursor;
    DEALLOCATE StepCursor;

    -- Update Translation Status
    INSERT INTO RecipeTranslationStatus (RecipeId, HasDE, HasEN, HasESP, HasPRT, LastUpdated)
    VALUES (
        @RecipeId,
        CASE WHEN EXISTS(SELECT 1 FROM RecipeStep WHERE RecipeId = @RecipeId AND SourceLanguage = 'de') THEN 1 ELSE 0 END,
        CASE WHEN EXISTS(SELECT 1 FROM RecipeStepTranslation WHERE RecipeId = @RecipeId AND Language = 'en') THEN 1 ELSE 0 END,
        CASE WHEN EXISTS(SELECT 1 FROM RecipeStepTranslation WHERE RecipeId = @RecipeId AND Language = 'esp') THEN 1 ELSE 0 END,
        CASE WHEN EXISTS(SELECT 1 FROM RecipeStepTranslation WHERE RecipeId = @RecipeId AND Language = 'prt') THEN 1 ELSE 0 END,
        GETUTCDATE()
    );

    SET @Counter = @Counter + 1;

    -- Progress
    IF @Counter % 10 = 0
    BEGIN
        PRINT 'Migriert: ' + CAST(@Counter AS VARCHAR) + '/' + CAST(@TotalRecipes AS VARCHAR);
    END

    FETCH NEXT FROM RecipeCursor INTO @RecipeId;
END

CLOSE RecipeCursor;
DEALLOCATE RecipeCursor;

COMMIT TRANSACTION;

PRINT '✅ Migration abgeschlossen! ' + CAST(@Counter AS VARCHAR) + ' Rezepte migriert.';
*/

-- 3. Vergleich Alt vs. Neu (nach Migration)
SELECT
    'OLD' as System,
    COUNT(DISTINCT RJ.Recipe_Id) as RecipeCount,
    COUNT(*) as StepCount
FROM RecipeJoinPreparationSteps RJ
UNION ALL
SELECT
    'NEW' as System,
    COUNT(DISTINCT RecipeId) as RecipeCount,
    COUNT(*) as StepCount
FROM RecipeStep;

-- 4. Prüfe ob Migration erfolgreich
-- Sollten gleich viele Steps sein!
SELECT
    R.Id,
    R.Title,
    (SELECT COUNT(*) FROM RecipeJoinPreparationSteps RJ WHERE RJ.Recipe_Id = R.Id) as OldStepCount,
    (SELECT COUNT(*) FROM RecipeStep RS WHERE RS.RecipeId = R.Id) as NewStepCount
FROM RecipeBaseData R
WHERE EXISTS(SELECT 1 FROM RecipeJoinPreparationSteps RJ WHERE RJ.Recipe_Id = R.Id)
ORDER BY R.Id DESC;

-- 5. NACH ERFOLGREICHER MIGRATION + TEST:
-- Erst wenn ALLES funktioniert und getestet ist!
/*
-- ⚠️ WARNUNG: Alte Tabellen löschen (nur nach erfolgreichem Test!)
-- Uncomment nur wenn du sicher bist!

-- DROP TABLE RecipeJoinPreparationSteps;
-- DROP TABLE RecipePreparationSteps;

PRINT '⚠️ Alte Tabellen gelöscht!';
*/
