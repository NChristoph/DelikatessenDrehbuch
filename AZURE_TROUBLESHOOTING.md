# Azure SQL Connection Problem - Troubleshooting

## Problem
`Login failed for user 'ChristophNitsch'` nach Deployment in Azure

## Mögliche Ursachen

### 1. ❌ Umgebungsvariable in Azure nicht gesetzt oder falsch
**Prüfen:**
- Azure Portal → App Service → Configuration → Application Settings
- Ist `DB_ConectionString` vorhanden?
- Ist das Passwort korrekt?

**Fix:**
```
Name: DB_ConectionString
Value: Server=tcp:delikatessendrehbuch.database.windows.net,1433;Initial Catalog=DelikatessenDrehbuch_Datenbank-2024-8-8-7-54_2025-06-17T08-19Z;User ID=ChristophNitsch;Password=ECHTES_PASSWORT;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### 2. ❌ Azure SQL Firewall blockiert App Service
**Prüfen:**
- Azure Portal → SQL Database → Networking/Firewalls and virtual networks
- Ist "Allow Azure services and resources to access this server" aktiviert?

**Fix:**
- Aktivieren Sie: "Allow Azure services and resources to access this server"
- ODER fügen Sie die Outbound IP-Adressen Ihrer App Service hinzu

### 3. ❌ SQL User hat keine Berechtigung
**Prüfen:**
- Kann sich der User 'ChristophNitsch' überhaupt anmelden?
- Hat der User Zugriff auf die Datenbank?

**Fix (in SQL Server Management Studio oder Azure Query Editor):**
```sql
-- Prüfen ob User existiert
SELECT name FROM sys.database_principals WHERE name = 'ChristophNitsch';

-- User Berechtigungen geben
ALTER ROLE db_owner ADD MEMBER ChristophNitsch;
```

### 4. ❌ Connection String wird nicht aus Umgebungsvariable geladen
**Prüfen:**
- Logging in Azure App Service aktivieren
- Application Insights Logs checken

**Debug-Option:**
Temporär Logging in Program.cs hinzufügen:
```csharp
var envConnectionString = Environment.GetEnvironmentVariable("DB_ConectionString")
                          ?? Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING");
Console.WriteLine($"ENV Connection String: {(envConnectionString != null ? "FOUND" : "NOT FOUND")}");
```

## Schnellste Lösung

### Option A: Umgebungsvariable in Azure setzen
1. Azure Portal öffnen
2. Ihre App Service auswählen
3. Settings → Configuration → Application settings
4. Neue Setting hinzufügen:
   - Name: `DB_ConectionString`
   - Value: [Kompletter Connection String mit Passwort]
5. Save klicken
6. App Service neu starten

### Option B: Azure SQL Firewall öffnen
1. Azure Portal → SQL Database
2. Networking
3. "Allow Azure services and resources to access this server" → ON
4. Save

## Test-Befehle

### Connection String in Azure App Service testen (Kudu Console)
```bash
# Öffnen Sie: https://<your-app>.scm.azurewebsites.net/DebugConsole
# Dann ausführen:
echo %DB_ConectionString%
```

### Direkt aus App Service heraus SQL Server testen
```bash
# In Kudu Console
curl -v telnet://delikatessendrehbuch.database.windows.net:1433
```

## Empfohlene Reihenfolge

1. ✅ Prüfen Sie die Firewall-Einstellungen (Option B)
2. ✅ Prüfen Sie die Umgebungsvariable in Azure (Option A)
3. ✅ Testen Sie die Verbindung in Kudu Console
4. ✅ Aktivieren Sie Application Insights für besseres Logging
