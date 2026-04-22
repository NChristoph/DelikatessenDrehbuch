-- Azure-safe SQL (no GO). Creates a lightweight notification table for WorldMiniApp if missing.
-- Stores: icon, created time, seen flag + seen time, description, click href, sender string.

IF OBJECT_ID(N'[dbo].[WorldUserNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserNotifications](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserNotifications] PRIMARY KEY,
        [UserHash] NVARCHAR(256) NOT NULL,
        [Icon] NVARCHAR(64) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Icon] DEFAULT (N'bi-bell'),
        [Sender] NVARCHAR(128) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Sender] DEFAULT (N'system'),
        [Description] NVARCHAR(1000) NOT NULL,
        [Href] NVARCHAR(600) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserNotifications_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsSeen] BIT NOT NULL CONSTRAINT [DF_WorldUserNotifications_IsSeen] DEFAULT (0),
        [SeenAtUtc] DATETIME2 NULL
    );

    CREATE INDEX [IX_WorldUserNotifications_UserHash_IsSeen_CreatedAtUtc]
        ON [dbo].[WorldUserNotifications]([UserHash], [IsSeen], [CreatedAtUtc]);

    CREATE INDEX [IX_WorldUserNotifications_UserHash_CreatedAtUtc]
        ON [dbo].[WorldUserNotifications]([UserHash], [CreatedAtUtc]);
END;

