/*
  Adds Language column for RecipeCommunityVariants (language code the variant is written in).
  Idempotent / safe to run multiple times.
*/

IF COL_LENGTH(N'dbo.RecipeCommunityVariants', N'Language') IS NULL
BEGIN
    ALTER TABLE [dbo].[RecipeCommunityVariants]
    ADD [Language] NVARCHAR(12) NOT NULL CONSTRAINT [DF_RecipeCommunityVariants_Language] DEFAULT (N'de');
END
GO

