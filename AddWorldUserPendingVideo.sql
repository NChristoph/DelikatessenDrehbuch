-- Migration: AddWorldUserPendingVideo
-- Erstellt Tabelle für Tracking von Bunny Stream Videos die noch nicht fertig sind
-- WICHTIG: Stelle sicher, dass du mit der richtigen Datenbank verbunden bist!

BEGIN TRANSACTION;

-- Tabelle erstellen
CREATE TABLE [dbo].[WorldUserPendingVideos] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [PostingId] INT NOT NULL,
    [VideoGuid] NVARCHAR(100) NOT NULL,
    [UserHash] NVARCHAR(200) NOT NULL,
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT [PK_WorldUserPendingVideos] PRIMARY KEY CLUSTERED ([Id] ASC),

    CONSTRAINT [FK_WorldUserPendingVideos_WorldUserPosting]
        FOREIGN KEY ([PostingId])
        REFERENCES [dbo].[WorldUserPosting] ([Id])
        ON DELETE CASCADE
);
GO

-- Index für schnelle VideoGuid-Lookups (Webhook-Abfragen)
CREATE NONCLUSTERED INDEX [IX_WorldUserPendingVideos_VideoGuid]
    ON [dbo].[WorldUserPendingVideos] ([VideoGuid] ASC);
GO

-- Index für User-spezifische Abfragen
CREATE NONCLUSTERED INDEX [IX_WorldUserPendingVideos_UserHash]
    ON [dbo].[WorldUserPendingVideos] ([UserHash] ASC);
GO

-- Index für alte Einträge bereinigen (optional)
CREATE NONCLUSTERED INDEX [IX_WorldUserPendingVideos_CreatedAt]
    ON [dbo].[WorldUserPendingVideos] ([CreatedAt] ASC);
GO

PRINT '✅ Tabelle WorldUserPendingVideos erfolgreich erstellt';

-- Prüfen ob Tabelle existiert
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WorldUserPendingVideos')
BEGIN
    PRINT '✅ Verification: Tabelle existiert';
    SELECT
        c.name AS ColumnName,
        t.name AS DataType,
        c.max_length AS MaxLength,
        c.is_nullable AS IsNullable
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('WorldUserPendingVideos')
    ORDER BY c.column_id;
END
ELSE
BEGIN
    PRINT '❌ ERROR: Tabelle wurde nicht erstellt!';
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Falls alles OK: COMMIT
COMMIT TRANSACTION;
PRINT '🎉 Migration abgeschlossen!';
GO
