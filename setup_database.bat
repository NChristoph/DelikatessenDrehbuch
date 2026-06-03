@echo off
echo ========================================
echo Datenbank Setup
echo ========================================
echo.

cd /d "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch"

echo 1. Erstelle Datenbank-Migrationen...
dotnet ef migrations add InitialCreate --context ApplicationDbContext

echo.
echo 2. Aktualisiere Datenbank...
dotnet ef database update --context ApplicationDbContext

echo.
echo ========================================
echo Fertig! Die Datenbank wurde erstellt.
echo ========================================
pause
