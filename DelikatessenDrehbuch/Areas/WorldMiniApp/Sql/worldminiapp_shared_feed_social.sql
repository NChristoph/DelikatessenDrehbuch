-- =============================================================================
-- Geteilte Gruppen: Feed+Chat-Zusammenlegung
--   * Text-Postings im Feed (WorldSharedFeedItems.Text + ContentType 'text')
--   * Likes auf Feed-Items (neue Tabelle WorldSharedFeedItemLikes)
--   * Private 1:1-DMs (WorldSharedFeedMessages.RecipientUserHash)
-- Einmalig auf der DB ausführen. Idempotent.
--
-- HINWEIS: Manuell ergänzt (keine EF-Migration) -> beim nächsten Migration-Scaffold
-- berücksichtigen.
-- =============================================================================

-- ---- Text-Postings: Body-Spalte am Feed-Item ------------------------------
IF COL_LENGTH(N'dbo.WorldSharedFeedItems', N'Text') IS NULL
    ALTER TABLE dbo.WorldSharedFeedItems ADD [Text] NVARCHAR(MAX) NULL;

-- ---- Likes auf Feed-Items --------------------------------------------------
IF OBJECT_ID(N'dbo.WorldSharedFeedItemLikes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorldSharedFeedItemLikes
    (
        Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorldSharedFeedItemLikes PRIMARY KEY,
        WorldSharedFeedItemId INT NOT NULL,
        UserHash              NVARCHAR(256) NOT NULL,
        CreatedAtUtc          DATETIME2 NOT NULL CONSTRAINT DF_WSFIL_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorldSharedFeedItemLikes_Items
            FOREIGN KEY (WorldSharedFeedItemId) REFERENCES dbo.WorldSharedFeedItems(Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_WorldSharedFeedItemLikes_Item_User
        ON dbo.WorldSharedFeedItemLikes (WorldSharedFeedItemId, UserHash);
END;

-- ---- Private 1:1-DMs: Empfänger an der Nachricht ---------------------------
IF OBJECT_ID(N'dbo.WorldSharedFeedMessages', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.WorldSharedFeedMessages', N'RecipientUserHash') IS NULL
    ALTER TABLE dbo.WorldSharedFeedMessages ADD RecipientUserHash NVARCHAR(256) NULL;

IF OBJECT_ID(N'dbo.WorldSharedFeedMessages', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_WorldSharedFeedMessages_Thread'
          AND object_id = OBJECT_ID(N'dbo.WorldSharedFeedMessages'))
    CREATE INDEX IX_WorldSharedFeedMessages_Thread
        ON dbo.WorldSharedFeedMessages (WorldSharedFeedId, UserHash, RecipientUserHash, CreatedAtUtc);
