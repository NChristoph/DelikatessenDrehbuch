-- =============================================================================
-- WorldUserNotifications
-- Leichte Benachrichtigungstabelle für die Feed-Glocke (Likes, Kommentare,
-- Video-fertig, AI-Plan etc.).
--
-- Diese Tabelle wurde früher ZUR LAUFZEIT per ExecuteSqlRawAsync angelegt
-- (HomeController + RecipeAiVariantJobService). Das ist jetzt entfernt — die
-- Schema-Erzeugung gehört in den Deploy. Dieses Skript einmalig auf der
-- Azure-SQL-DB ausführen. Idempotent (IF OBJECT_ID / IF COL_LENGTH) — mehrfaches
-- Ausführen ist sicher. Kein GO (Azure-safe, ein Batch).
-- =============================================================================

IF OBJECT_ID(N'[dbo].[WorldUserNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserNotifications](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserNotifications] PRIMARY KEY,
        [UserHash] NVARCHAR(256) NOT NULL,
        [Icon] NVARCHAR(64) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Icon] DEFAULT (N'bi-bell'),
        [Sender] NVARCHAR(128) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Sender] DEFAULT (N'system'),
        [Description] NVARCHAR(1000) NOT NULL,
        [Href] NVARCHAR(600) NULL,
        [NotificationKey] NVARCHAR(300) NULL,
        [EventType] NVARCHAR(64) NULL,
        [LatestActorName] NVARCHAR(128) NULL,
        [ContextText] NVARCHAR(400) NULL,
        [AggregateCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_AggregateCount] DEFAULT (1),
        [UnreadEventCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_UnreadEventCount] DEFAULT (1),
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserNotifications_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsSeen] BIT NOT NULL CONSTRAINT [DF_WorldUserNotifications_IsSeen] DEFAULT (0),
        [SeenAtUtc] DATETIME2 NULL
    );

    CREATE INDEX [IX_WorldUserNotifications_UserHash_IsSeen_CreatedAtUtc]
        ON [dbo].[WorldUserNotifications]([UserHash], [IsSeen], [CreatedAtUtc]);
END;

-- Spalten nachziehen, falls die Tabelle aus einer älteren (schlankeren) Version stammt.
IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'NotificationKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [NotificationKey] NVARCHAR(300) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'EventType') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [EventType] NVARCHAR(64) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'LatestActorName') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [LatestActorName] NVARCHAR(128) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'ContextText') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [ContextText] NVARCHAR(400) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'AggregateCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [AggregateCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_AggregateCount_Legacy] DEFAULT (1);
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'UnreadEventCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [UnreadEventCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_UnreadEventCount_Legacy] DEFAULT (1);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_WorldUserNotifications_UserHash_NotificationKey'
      AND object_id = OBJECT_ID(N'[dbo].[WorldUserNotifications]')
)
BEGIN
    CREATE INDEX [IX_WorldUserNotifications_UserHash_NotificationKey]
        ON [dbo].[WorldUserNotifications]([UserHash], [NotificationKey]);
END;
