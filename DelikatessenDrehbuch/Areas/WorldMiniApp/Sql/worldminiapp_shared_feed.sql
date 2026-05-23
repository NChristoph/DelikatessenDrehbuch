IF OBJECT_ID(N'dbo.WorldSharedFeeds', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorldSharedFeeds
    (
        Id                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Title             NVARCHAR(160) NOT NULL,
        OwnerUserHash     NVARCHAR(256) NOT NULL,
        InviteToken       NVARCHAR(64) NOT NULL,
        CreatedAtUtc      DATETIME2 NOT NULL CONSTRAINT DF_WorldSharedFeeds_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        LastActivityAtUtc DATETIME2 NOT NULL CONSTRAINT DF_WorldSharedFeeds_LastActivityAtUtc DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX IX_WorldSharedFeeds_InviteToken ON dbo.WorldSharedFeeds (InviteToken);
    CREATE INDEX IX_WorldSharedFeeds_OwnerUserHash ON dbo.WorldSharedFeeds (OwnerUserHash);
    CREATE INDEX IX_WorldSharedFeeds_LastActivityAtUtc ON dbo.WorldSharedFeeds (LastActivityAtUtc DESC);
END;
GO

IF OBJECT_ID(N'dbo.WorldSharedFeedMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorldSharedFeedMembers
    (
        Id                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WorldSharedFeedId INT NOT NULL,
        UserHash          NVARCHAR(256) NOT NULL,
        [Role]            NVARCHAR(32) NOT NULL CONSTRAINT DF_WorldSharedFeedMembers_Role DEFAULT N'member',
        AddedByUserHash   NVARCHAR(256) NOT NULL,
        JoinedAtUtc       DATETIME2 NOT NULL CONSTRAINT DF_WorldSharedFeedMembers_JoinedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorldSharedFeedMembers_WorldSharedFeeds
            FOREIGN KEY (WorldSharedFeedId) REFERENCES dbo.WorldSharedFeeds(Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_WorldSharedFeedMembers_Feed_User
        ON dbo.WorldSharedFeedMembers (WorldSharedFeedId, UserHash);
    CREATE INDEX IX_WorldSharedFeedMembers_UserHash
        ON dbo.WorldSharedFeedMembers (UserHash);
END;
GO

IF OBJECT_ID(N'dbo.WorldSharedFeedItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorldSharedFeedItems
    (
        Id                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WorldSharedFeedId INT NOT NULL,
        ContentType       NVARCHAR(32) NOT NULL,
        SourceToken       NVARCHAR(64) NOT NULL,
        Title             NVARCHAR(200) NOT NULL,
        AddedByUserHash   NVARCHAR(256) NOT NULL,
        CreatedAtUtc      DATETIME2 NOT NULL CONSTRAINT DF_WorldSharedFeedItems_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorldSharedFeedItems_WorldSharedFeeds
            FOREIGN KEY (WorldSharedFeedId) REFERENCES dbo.WorldSharedFeeds(Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_WorldSharedFeedItems_Feed_Type_Token
        ON dbo.WorldSharedFeedItems (WorldSharedFeedId, ContentType, SourceToken);
    CREATE INDEX IX_WorldSharedFeedItems_Feed_CreatedAtUtc
        ON dbo.WorldSharedFeedItems (WorldSharedFeedId, CreatedAtUtc DESC);
END;
GO
