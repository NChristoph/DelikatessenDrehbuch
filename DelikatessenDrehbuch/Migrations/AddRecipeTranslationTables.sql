-- Migration: Recipe Translation System
-- Datum: 2026-05-10
-- Beschreibung: Fügt Tabellen für mehrsprachige Rezept-Steps hinzu

-- 1. Neue Tabelle: RecipeStep (einfache Step-Struktur)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeStep')
BEGIN
    CREATE TABLE RecipeStep (
        Id INT PRIMARY KEY IDENTITY(1,1),
        RecipeId INT NOT NULL,
        StepOrder INT NOT NULL,
        StepText NVARCHAR(MAX) NOT NULL,
        SourceLanguage VARCHAR(5) NOT NULL DEFAULT 'de',
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- Foreign Key
        CONSTRAINT FK_RecipeStep_Recipe
            FOREIGN KEY (RecipeId) REFERENCES RecipeBaseData(Id) ON DELETE CASCADE
    );

    -- Index für Performance
    CREATE INDEX idx_RecipeStep_Recipe
        ON RecipeStep(RecipeId, StepOrder);

    PRINT 'Table RecipeStep created successfully';
END
ELSE
BEGIN
    PRINT 'Table RecipeStep already exists';
END
GO

-- 2. Neue Tabelle: RecipeStepTranslation
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeStepTranslation')
BEGIN
    CREATE TABLE RecipeStepTranslation (
        Id INT PRIMARY KEY IDENTITY(1,1),

        -- Referenzen
        RecipeId INT NOT NULL,
        StepId INT NOT NULL,

        -- Übersetzung
        Language VARCHAR(5) NOT NULL,
        TranslatedText NVARCHAR(MAX) NOT NULL,

        -- Metadata
        TranslatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        TranslationProvider VARCHAR(50) NOT NULL DEFAULT 'gpt-4o',
        IsCoreSprache BIT NOT NULL DEFAULT 0,

        -- Foreign Keys
        CONSTRAINT FK_RecipeStepTranslation_Recipe
            FOREIGN KEY (RecipeId) REFERENCES RecipeBaseData(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeStepTranslation_Step
            FOREIGN KEY (StepId) REFERENCES RecipeStep(Id) ON DELETE NO ACTION,

        -- Unique: Ein Step hat nur EINE Übersetzung pro Sprache
        CONSTRAINT UQ_RecipeStepTranslation_Step_Language
            UNIQUE (StepId, Language)
    );

    -- Indizes für Performance
    CREATE INDEX idx_RecipeStepTranslation_Recipe_Lang
        ON RecipeStepTranslation(RecipeId, Language);

    CREATE INDEX idx_RecipeStepTranslation_Step_Lang
        ON RecipeStepTranslation(StepId, Language);

    PRINT 'Table RecipeStepTranslation created successfully';
END
ELSE
BEGIN
    PRINT 'Table RecipeStepTranslation already exists';
END
GO

-- 3. Neue Tabelle: RecipeTranslationStatus
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeTranslationStatus')
BEGIN
    CREATE TABLE RecipeTranslationStatus (
        RecipeId INT PRIMARY KEY,

        -- 4 Hauptsprachen (sofort übersetzt)
        HasDE BIT NOT NULL DEFAULT 0,
        HasEN BIT NOT NULL DEFAULT 0,
        HasESP BIT NOT NULL DEFAULT 0,
        HasPRT BIT NOT NULL DEFAULT 0,

        -- 6 On-Demand Sprachen
        HasID BIT NOT NULL DEFAULT 0,
        HasNL BIT NOT NULL DEFAULT 0,
        HasSV BIT NOT NULL DEFAULT 0,
        HasDA BIT NOT NULL DEFAULT 0,
        HasNO BIT NOT NULL DEFAULT 0,
        HasMS BIT NOT NULL DEFAULT 0,

        LastUpdated DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- Foreign Key
        CONSTRAINT FK_RecipeTranslationStatus_Recipe
            FOREIGN KEY (RecipeId) REFERENCES RecipeBaseData(Id) ON DELETE CASCADE
    );

    PRINT 'Table RecipeTranslationStatus created successfully';
END
ELSE
BEGIN
    PRINT 'Table RecipeTranslationStatus already exists';
END
GO

PRINT 'Migration completed successfully!';
