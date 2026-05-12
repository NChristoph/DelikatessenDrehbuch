-- ===================================================
-- AI-Generated Recipe Steps Table
-- Speichert konkrete Rezeptschritte (keine Templates)
-- mit Übersetzungen in allen 10 Sprachen
-- ===================================================

CREATE TABLE [dbo].[WorldRecipeAiSteps] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [RecipeId] INT NOT NULL,
    [StepOrder] INT NOT NULL,
    [Phase] INT NOT NULL, -- 0=Basis, 1=Prep, 2=Cook, 3=Season, 4=Serve

    -- Übersetzungen (AI-generiert)
    [TextDe] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextEn] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextEsp] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextPrt] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextId] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextNl] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextSv] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextDa] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextNo] NVARCHAR(500) NOT NULL DEFAULT '',
    [TextMs] NVARCHAR(500) NOT NULL DEFAULT '',

    -- Optional: Zutat-Referenz
    [IngredientId] INT NULL,

    -- Für Smart Suggestions
    [RecipeCategory] NVARCHAR(50) NULL,
    [UsageCount] INT NOT NULL DEFAULT 1,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    -- Foreign Key zu RecipeBaseData
    CONSTRAINT [FK_WorldRecipeAiSteps_RecipeBaseData]
        FOREIGN KEY ([RecipeId])
        REFERENCES [RecipeBaseData]([Id])
        ON DELETE CASCADE
);

-- Index für schnelles Laden aller Steps eines Rezepts
CREATE NONCLUSTERED INDEX [IX_WorldRecipeAiSteps_RecipeId_StepOrder]
    ON [WorldRecipeAiSteps] ([RecipeId], [StepOrder]);

-- Index für Smart Suggestions (Kategorie-basiert)
CREATE NONCLUSTERED INDEX [IX_WorldRecipeAiSteps_Category_Usage]
    ON [WorldRecipeAiSteps] ([RecipeCategory], [UsageCount] DESC)
    WHERE [RecipeCategory] IS NOT NULL;

-- Index für Ingredient-basierte Suggestions
CREATE NONCLUSTERED INDEX [IX_WorldRecipeAiSteps_IngredientId]
    ON [WorldRecipeAiSteps] ([IngredientId])
    WHERE [IngredientId] IS NOT NULL;

GO

PRINT 'WorldRecipeAiSteps table created successfully';
