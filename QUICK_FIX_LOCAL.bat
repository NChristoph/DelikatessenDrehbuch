@echo off
echo ========================================
echo LOKALE ENTWICKLUNG - Connection String setzen
echo ========================================
echo.
echo Dieses Skript setzt den Connection String in User Secrets.
echo User Secrets werden NICHT in Git eingecheckt - sicher!
echo.
echo ========================================
echo.

cd /d "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch"

echo Bitte geben Sie Ihren vollstaendigen Azure SQL Connection String ein:
echo.
echo Beispiel:
echo Server=tcp:delikatessendrehbuch.database.windows.net,1433;Initial Catalog=DelikatessenDrehbuch_Datenbank-2024-8-8-7-54_2025-06-17T08-19Z;User ID=ChristophNitsch;Password=IHR_PASSWORT;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
echo.
echo ========================================

set /p CONN_STR="Connection String: "

echo.
echo Setze Connection String in User Secrets...
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "%CONN_STR%"

echo.
echo ========================================
echo Fertig! Pruefen Sie die Konfiguration:
echo ========================================

dotnet user-secrets list

echo.
echo Sie koennen jetzt die Anwendung lokal starten!
echo.
pause
