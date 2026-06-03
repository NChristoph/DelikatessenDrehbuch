@echo off
REM Setze die Azure SQL Connection String Umgebungsvariable
REM WICHTIG: Ersetze IHR_PASSWORT_HIER mit dem echten Passwort!

setx AZURE_SQL_CONNECTIONSTRING "Server=tcp:delikatessendrehbuch.database.windows.net,1433;Initial Catalog=DelikatessenDrehbuch_Datenbank-2024-8-8-7-54_2025-06-17T08-19Z;Persist Security Info=False;User ID=ChristophNitsch;Password=IHR_PASSWORT_HIER;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

echo Umgebungsvariable wurde gesetzt!
echo WICHTIG: Schliessen Sie das Terminal und starten Sie Visual Studio neu, damit die Variable wirksam wird.
pause
