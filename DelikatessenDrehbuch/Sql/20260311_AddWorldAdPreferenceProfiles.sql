CREATE TABLE [dbo].[WorldAdPreferenceProfiles]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserHash] NVARCHAR(256) NOT NULL,
    [AllowPersonalizedAds] BIT NOT NULL,
    [AllowCategoryTargeting] BIT NOT NULL,
    [AllowKeywordTargeting] BIT NOT NULL,
    [AllowCreatorTargeting] BIT NOT NULL,
    [PreferredLanguage] NVARCHAR(MAX) NOT NULL,
    [TopCategoriesJson] NVARCHAR(MAX) NOT NULL,
    [TopKeywordsJson] NVARCHAR(MAX) NOT NULL,
    [TopCreatorsJson] NVARCHAR(MAX) NOT NULL,
    [SegmentLabelsJson] NVARCHAR(MAX) NOT NULL,
    [QualifiedWatchCount] INT NOT NULL,
    [LikedRecipeCount] INT NOT NULL,
    [FollowedCreatorCount] INT NOT NULL,
    [LastProfileRefreshUtc] DATETIME2 NULL,
    [CreatedAtUtc] DATETIME2 NOT NULL,
    [UpdatedAtUtc] DATETIME2 NOT NULL,
    CONSTRAINT [PK_WorldAdPreferenceProfiles] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE INDEX [IX_WorldAdPreferenceProfiles_UserHash]
    ON [dbo].[WorldAdPreferenceProfiles]([UserHash]);
GO

CREATE TABLE [dbo].[WorldAdPreferenceInterests]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserHash] NVARCHAR(256) NOT NULL,
    [InterestType] NVARCHAR(64) NOT NULL,
    [InterestKey] NVARCHAR(256) NOT NULL,
    [DisplayLabel] NVARCHAR(256) NOT NULL,
    [Score] DECIMAL(10,2) NOT NULL,
    [SignalCount] INT NOT NULL,
    [LastSignalAtUtc] DATETIME2 NOT NULL,
    CONSTRAINT [PK_WorldAdPreferenceInterests] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE INDEX [IX_WorldAdPreferenceInterests_UserHash_InterestType_Score]
    ON [dbo].[WorldAdPreferenceInterests]([UserHash], [InterestType], [Score]);
GO
