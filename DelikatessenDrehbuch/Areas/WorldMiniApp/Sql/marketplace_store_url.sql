-- =============================================================================
-- Profil: Externer Store-Link des Creators (nur Anzeige, keine bezahlte Werbung).
-- Erweitert dbo.WorldAppUser um StoreUrl. Einmalig ausführen. Idempotent. Kein GO.
--
-- HINWEIS: Manuell ergänzt (keine EF-Migration) — beim nächsten Migration-Scaffold
-- berücksichtigen.
-- =============================================================================

IF COL_LENGTH(N'dbo.WorldAppUser', N'StoreUrl') IS NULL
    ALTER TABLE dbo.WorldAppUser ADD StoreUrl NVARCHAR(1024) NULL;
