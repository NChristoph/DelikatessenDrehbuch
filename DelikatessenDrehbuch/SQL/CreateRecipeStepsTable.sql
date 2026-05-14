-- ===================================================
-- RECIPE STEPS TABLE - Simplified Translation System
-- Branch: new-ai-translate-system
-- ===================================================

-- Steps in eigener Tabelle (normalisiert: 1 Row pro Sprache)
-- Vorteil: Nur laden wenn gebraucht, bessere Performance

CREATE TABLE [dbo].[RecipeSteps] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [RecipeId] INT NOT NULL,

    -- Sprachcode: de, en, esp, prt, id, nl, sv, da, no, ms
    [Culture] NVARCHAR(10) NOT NULL,

    -- Zubereitungsschritte als Text
    [Text] NVARCHAR(MAX) NOT NULL,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NULL,

    -- Foreign Key zu RecipeBaseData
    CONSTRAINT [FK_RecipeSteps_RecipeBaseData]
        FOREIGN KEY ([RecipeId])
        REFERENCES [RecipeBaseData]([Id])
        ON DELETE CASCADE,

    -- Unique constraint: Ein Rezept kann nur 1x pro Sprache Steps haben
    CONSTRAINT [UQ_RecipeSteps_RecipeId_Culture]
        UNIQUE ([RecipeId], [Culture])
);

-- Index für schnelles Laden der Steps eines Rezepts
CREATE NONCLUSTERED INDEX [IX_RecipeSteps_RecipeId]
    ON [RecipeSteps] ([RecipeId]);

GO

PRINT '✅ RecipeSteps table created successfully';
PRINT '';
PRINT 'Usage:';
PRINT '  - RecipeBaseData: Basis-Daten (Title, Category, Time, etc.)';
PRINT '  - RecipeJoinIngredientMeasureQuantity: Zutaten (schon mehrsprachig!)';
PRINT '  - RecipeSteps: Zubereitungsschritte (normalisiert, 1 Row pro Sprache)';
PRINT '';
PRINT 'Beispiel:';
PRINT '  RecipeId=1, Culture=de,  Text=Step 1: ...\nStep 2: ...';
PRINT '  RecipeId=1, Culture=en,  Text=Step 1: ...\nStep 2: ...';
PRINT '  RecipeId=1, Culture=esp, Text=Paso 1: ...\nPaso 2: ...';
GO
