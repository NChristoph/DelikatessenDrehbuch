/*
  WorldMiniApp: Shared/public recipe variants created from ingredient swaps.

  Notes:
  - This script is intentionally idempotent (safe to run multiple times).
  - It does NOT depend on EF migrations being applied in Azure.
*/

IF OBJECT_ID(N'[dbo].[RecipeCommunityVariants]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RecipeCommunityVariants] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [OriginalRecipeId] INT NOT NULL,
        [ParentCommunityVariantId] INT NULL,
        [Title] NVARCHAR(256) NOT NULL,
        [SwapsJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_RecipeCommunityVariants_SwapsJson] DEFAULT (N'[]'),
        [CreatedByUserHash] NVARCHAR(256) NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [LikeCount] INT NOT NULL CONSTRAINT [DF_RecipeCommunityVariants_LikeCount] DEFAULT (0),
        CONSTRAINT [PK_RecipeCommunityVariants] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecipeCommunityVariants_OriginalRecipeId_CreatedAtUtc' AND object_id = OBJECT_ID(N'[dbo].[RecipeCommunityVariants]'))
BEGIN
    CREATE INDEX [IX_RecipeCommunityVariants_OriginalRecipeId_CreatedAtUtc]
    ON [dbo].[RecipeCommunityVariants] ([OriginalRecipeId], [CreatedAtUtc]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecipeCommunityVariants_ParentCommunityVariantId' AND object_id = OBJECT_ID(N'[dbo].[RecipeCommunityVariants]'))
BEGIN
    CREATE INDEX [IX_RecipeCommunityVariants_ParentCommunityVariantId]
    ON [dbo].[RecipeCommunityVariants] ([ParentCommunityVariantId]);
END
GO

IF OBJECT_ID(N'[dbo].[FK_RecipeCommunityVariants_RecipeBaseData_OriginalRecipeId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[RecipeCommunityVariants] WITH CHECK
    ADD CONSTRAINT [FK_RecipeCommunityVariants_RecipeBaseData_OriginalRecipeId]
    FOREIGN KEY ([OriginalRecipeId]) REFERENCES [dbo].[RecipeBaseData] ([Id]) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'[dbo].[FK_RecipeCommunityVariants_RecipeCommunityVariants_ParentCommunityVariantId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[RecipeCommunityVariants] WITH CHECK
    ADD CONSTRAINT [FK_RecipeCommunityVariants_RecipeCommunityVariants_ParentCommunityVariantId]
    FOREIGN KEY ([ParentCommunityVariantId]) REFERENCES [dbo].[RecipeCommunityVariants] ([Id]);
END
GO

