-- =============================================================================
-- RecipeAiVariantJobs
-- Job-Tabelle für die asynchrone AI-Rezept-Transformation (Status/Ergebnis je Job).
--
-- Wurde früher ZUR LAUFZEIT per ExecuteSqlRawAsync angelegt
-- (RecipeAiVariantJobService.EnsureSchemaAsync). Das ist jetzt entfernt — Schema
-- gehört in den Deploy. Einmalig auf der Azure-SQL-DB ausführen. Idempotent.
-- Kein GO (Azure-safe, ein Batch).
--
-- Voraussetzung: Tabelle [dbo].[RecipeBaseData] existiert (FK BaseRecipeId).
-- =============================================================================

IF OBJECT_ID(N'[dbo].[RecipeAiVariantJobs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RecipeAiVariantJobs](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RecipeAiVariantJobs] PRIMARY KEY,
        [JobId] NVARCHAR(64) NOT NULL,
        [BaseRecipeId] INT NOT NULL,
        [VariantType] NVARCHAR(64) NOT NULL,
        [Language] NVARCHAR(12) NOT NULL,
        [AiProvider] NVARCHAR(32) NOT NULL,
        [CreatedByUserHash] NVARCHAR(256) NULL,
        [State] NVARCHAR(32) NOT NULL,
        [Message] NVARCHAR(400) NULL,
        [Title] NVARCHAR(256) NULL,
        [RequestJson] NVARCHAR(MAX) NOT NULL,
        [ResultJson] NVARCHAR(MAX) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_RecipeAiVariantJobs_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_RecipeAiVariantJobs_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME())
    );

    ALTER TABLE [dbo].[RecipeAiVariantJobs] WITH CHECK
    ADD CONSTRAINT [FK_RecipeAiVariantJobs_RecipeBaseData_BaseRecipeId]
        FOREIGN KEY([BaseRecipeId]) REFERENCES [dbo].[RecipeBaseData]([Id])
        ON DELETE CASCADE;

    CREATE UNIQUE INDEX [IX_RecipeAiVariantJobs_JobId] ON [dbo].[RecipeAiVariantJobs]([JobId]);
    CREATE INDEX [IX_RecipeAiVariantJobs_BaseRecipeId_UpdatedAtUtc] ON [dbo].[RecipeAiVariantJobs]([BaseRecipeId], [UpdatedAtUtc]);
END;
