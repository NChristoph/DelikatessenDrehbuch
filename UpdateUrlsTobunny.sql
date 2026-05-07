-- Migration Script: Azure Blob URLs → Bunny CDN URLs in Database
-- Führe dieses Script NACH der Datei-Migration aus

USE DelikatessenDrehbuch_Datenbank;
GO

BEGIN TRANSACTION;

-- Backup-Tabelle erstellen (Sicherheit!)
SELECT * INTO WorldUserPosting_Backup_BeforeBunnyMigration
FROM WorldUserPosting;

PRINT 'Backup erstellt: WorldUserPosting_Backup_BeforeBunnyMigration';

-- URLs in WorldUserPosting aktualisieren
UPDATE WorldUserPosting
SET
    -- Azure Blob URLs → Bunny CDN
    SourceUrl = REPLACE(
        REPLACE(SourceUrl,
            'https://blobdelikatessendrehbuch.blob.core.windows.net/blob-world-mini-app/',
            'https://avocadon.b-cdn.net/'),
        'https://delekatesendrehbuchcdn-beecexhdaghhacab.z01.azurefd.net/',
        'https://avocadon.b-cdn.net/'
    ),
    ThumbnailUrl = REPLACE(
        REPLACE(ThumbnailUrl,
            'https://blobdelikatessendrehbuch.blob.core.windows.net/blob-world-mini-app/',
            'https://avocadon.b-cdn.net/'),
        'https://delekatesendrehbuchcdn-beecexhdaghhacab.z01.azurefd.net/',
        'https://avocadon.b-cdn.net/'
    )
WHERE
    SourceUrl LIKE '%blob.core.windows.net%'
    OR SourceUrl LIKE '%azurefd.net%'
    OR ThumbnailUrl LIKE '%blob.core.windows.net%'
    OR ThumbnailUrl LIKE '%azurefd.net%';

PRINT CONCAT('URLs aktualisiert: ', @@ROWCOUNT, ' Zeilen geändert');

-- Prüfen ob URLs korrekt sind
SELECT TOP 10
    PostingId,
    SourceUrl,
    ThumbnailUrl
FROM WorldUserPosting
ORDER BY CreatedAt DESC;

-- Falls alles OK: COMMIT
-- Falls Fehler: ROLLBACK
-- COMMIT;
PRINT 'Transaktion bereit. Prüfe Ergebnisse und führe COMMIT aus wenn OK.';
PRINT 'Falls Fehler: ROLLBACK;';

-- Nicht automatisch committen - manuell nach Prüfung!
GO
