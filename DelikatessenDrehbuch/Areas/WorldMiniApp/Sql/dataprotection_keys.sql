-- =============================================================================
-- DataProtectionKeys
-- Speichert den ASP.NET-Core-DataProtection-Schlüsselring (signiert/verschlüsselt
-- u.a. die WorldMiniApp-Auth-Cookies). Ohne diese persistente, von allen
-- Instanzen geteilte Tabelle würde jeder App-Neustart / jede zusätzliche Instanz
-- neue Schlüssel erzeugen -> alle Auth-Cookies ungültig -> alle Nutzer ausgeloggt.
--
-- Spalten/Namen entsprechen exakt dem, was der EF-Core-DataProtection-Provider
-- erwartet (Entity Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey).
-- Einmalig auf der Azure-SQL-DB ausführen. Idempotent – mehrfaches Ausführen ist sicher.
-- =============================================================================

IF OBJECT_ID(N'dbo.DataProtectionKeys', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DataProtectionKeys
    (
        Id           INT IDENTITY(1,1) NOT NULL,
        FriendlyName NVARCHAR(MAX) NULL,
        Xml          NVARCHAR(MAX) NULL,
        CONSTRAINT PK_DataProtectionKeys PRIMARY KEY (Id)
    );
END;
GO
