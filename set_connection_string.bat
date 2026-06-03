@echo off
echo ========================================
echo Connection String in User Secrets setzen
echo ========================================
echo.
echo WICHTIG: Geben Sie Ihren kompletten Connection String ein
echo (mit Passwort aus Azure)
echo.
echo Beispiel:
echo Server=tcp:delikatessendrehbuch.database.windows.net,1433;Initial Catalog=DelikatessenDrehbuch_Datenbank-2024-8-8-7-54_2025-06-17T08-19Z;User ID=ChristophNitsch;Password=IHR_ECHTES_PASSWORT;...
echo.
echo ========================================
echo.

set /p CONN_STRING="Connection String eingeben: "

cd /d "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch"

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "%CONN_STRING%"

echo.
echo ========================================
echo Fertig! Der Connection String wurde gespeichert.
echo Er ist nur auf diesem Computer verfuegbar.
echo ========================================
pause
