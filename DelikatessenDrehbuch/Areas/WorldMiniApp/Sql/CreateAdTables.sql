-- SQL Script to create Ad tables for World Mini App
-- Run this directly in SQL Server Management Studio

-- Table: WorldAppAds
CREATE TABLE [dbo].[WorldAppAds] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Title] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [MediaUrl] NVARCHAR(1000) NOT NULL,
    [ThumbnailUrl] NVARCHAR(1000) NULL,
    [IsVideo] BIT NOT NULL DEFAULT 0,
    [TargetUrl] NVARCHAR(1000) NULL,
    [CtaText] NVARCHAR(50) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [StartDate] DATETIME2(7) NULL,
    [EndDate] DATETIME2(7) NULL,
    [ShowEveryXVideos] INT NOT NULL DEFAULT 5,
    [Priority] INT NOT NULL DEFAULT 1,
    [ViewCount] INT NOT NULL DEFAULT 0,
    [ClickCount] INT NOT NULL DEFAULT 0,
    [TargetAudience] NVARCHAR(20) NULL,
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7) NULL,
    [CreatedByUserHash] NVARCHAR(200) NULL,
    CONSTRAINT [PK_WorldAppAds] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Index for active ads query (used by feed)
CREATE NONCLUSTERED INDEX [IX_WorldAppAds_Active_Priority]
ON [dbo].[WorldAppAds] ([IsActive], [Priority] DESC, [ViewCount] ASC)
INCLUDE ([StartDate], [EndDate], [TargetAudience]);
GO

-- Table: WorldAppAdImpressions
CREATE TABLE [dbo].[WorldAppAdImpressions] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [AdId] INT NOT NULL,
    [UserHash] NVARCHAR(200) NULL,
    [WasClicked] BIT NOT NULL DEFAULT 0,
    [ViewedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [ClickedAt] DATETIME2(7) NULL,
    [UserAgent] NVARCHAR(200) NULL,
    CONSTRAINT [PK_WorldAppAdImpressions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_WorldAppAdImpressions_WorldAppAds]
        FOREIGN KEY ([AdId]) REFERENCES [dbo].[WorldAppAds] ([Id]) ON DELETE CASCADE
);
GO

-- Index for analytics queries
CREATE NONCLUSTERED INDEX [IX_WorldAppAdImpressions_AdId_ViewedAt]
ON [dbo].[WorldAppAdImpressions] ([AdId], [ViewedAt] DESC);
GO

-- Index for user tracking
CREATE NONCLUSTERED INDEX [IX_WorldAppAdImpressions_UserHash]
ON [dbo].[WorldAppAdImpressions] ([UserHash]);
GO

PRINT 'Ad tables created successfully!';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Go to /WorldMiniApp/Admin/Ads to manage ads';
PRINT '2. Create your first ad';
PRINT '3. Ads will appear in the feed every X videos (configurable per ad)';
GO
