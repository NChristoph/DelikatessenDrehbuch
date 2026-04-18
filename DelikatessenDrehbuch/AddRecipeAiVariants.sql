BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418070001_AddRecipeAiVariants'
)
BEGIN
    CREATE TABLE [RecipeAiVariants] (
        [Id] int NOT NULL IDENTITY,
        [BaseRecipeId] int NOT NULL,
        [ParentVariantId] int NULL,
        [VariantType] nvarchar(64) NOT NULL,
        [Language] nvarchar(12) NOT NULL,
        [CreatedByUserHash] nvarchar(256) NULL,
        [AppliedChangeCount] int NOT NULL,
        [LatestUserNote] nvarchar(1000) NULL,
        [IsSharedCanonical] bit NOT NULL,
        [StepPlanJson] nvarchar(max) NOT NULL,
        [RenderedStepsJson] nvarchar(max) NOT NULL,
        [RenderedIngredientsJson] nvarchar(max) NOT NULL,
        [RenderedHighlightsJson] nvarchar(max) NOT NULL,
        [RenderedTitle] nvarchar(256) NOT NULL,
        [RenderedSummary] nvarchar(4000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RecipeAiVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecipeAiVariants_RecipeAiVariants_ParentVariantId] FOREIGN KEY ([ParentVariantId]) REFERENCES [RecipeAiVariants] ([Id]),
        CONSTRAINT [FK_RecipeAiVariants_RecipeBaseData_BaseRecipeId] FOREIGN KEY ([BaseRecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418070001_AddRecipeAiVariants'
)
BEGIN
    CREATE INDEX [IX_RecipeAiVariants_BaseRecipeId_CreatedByUserHash_VariantType_Language] ON [RecipeAiVariants] ([BaseRecipeId], [CreatedByUserHash], [VariantType], [Language]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418070001_AddRecipeAiVariants'
)
BEGIN
    CREATE INDEX [IX_RecipeAiVariants_BaseRecipeId_VariantType_Language_IsSharedCanonical] ON [RecipeAiVariants] ([BaseRecipeId], [VariantType], [Language], [IsSharedCanonical]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418070001_AddRecipeAiVariants'
)
BEGIN
    CREATE INDEX [IX_RecipeAiVariants_ParentVariantId] ON [RecipeAiVariants] ([ParentVariantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418070001_AddRecipeAiVariants'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418070001_AddRecipeAiVariants', N'8.0.23');
END;
GO

COMMIT;
GO

