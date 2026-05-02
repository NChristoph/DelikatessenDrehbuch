/*
  Adds Summary column for RecipeCommunityVariants (short diff description vs original).
  Idempotent / safe to run multiple times.
*/

IF COL_LENGTH(N'dbo.RecipeCommunityVariants', N'Summary') IS NULL
BEGIN
    ALTER TABLE [dbo].[RecipeCommunityVariants]
    ADD [Summary] NVARCHAR(400) NULL;
END
GO

