CREATE TABLE [dbo].[WorldClipWatchSessions]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [WorldUserPostingId] INT NOT NULL,
    [RecipeId] INT NOT NULL,
    [ViewerUserHash] NVARCHAR(256) NOT NULL,
    [CreatorUserHash] NVARCHAR(256) NOT NULL,
    [WatchedSeconds] DECIMAL(10,2) NOT NULL,
    [IsQualifiedView] BIT NOT NULL,
    [SessionStartedAtUtc] DATETIME2 NOT NULL,
    [SessionEndedAtUtc] DATETIME2 NOT NULL,
    [CreatedAtUtc] DATETIME2 NOT NULL,
    CONSTRAINT [PK_WorldClipWatchSessions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_WorldClipWatchSessions_WorldUserPosting_WorldUserPostingId]
        FOREIGN KEY ([WorldUserPostingId]) REFERENCES [dbo].[WorldUserPosting]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_WorldClipWatchSessions_CreatorUserHash_IsQualifiedView_CreatedAtUtc]
    ON [dbo].[WorldClipWatchSessions]([CreatorUserHash], [IsQualifiedView], [CreatedAtUtc]);
GO

CREATE INDEX [IX_WorldClipWatchSessions_ViewerUserHash_CreatedAtUtc]
    ON [dbo].[WorldClipWatchSessions]([ViewerUserHash], [CreatedAtUtc]);
GO

CREATE INDEX [IX_WorldClipWatchSessions_WorldUserPostingId]
    ON [dbo].[WorldClipWatchSessions]([WorldUserPostingId]);
GO

