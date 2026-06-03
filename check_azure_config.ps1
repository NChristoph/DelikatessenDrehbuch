# Azure App Service Configuration Checker
# Dieses Skript hilft Ihnen, die Azure-Konfiguration zu überprüfen

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Azure Configuration Checker" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Prüfen ob Azure CLI installiert ist
$azCommand = Get-Command az -ErrorAction SilentlyContinue
if (-not $azCommand) {
    Write-Host "❌ Azure CLI ist nicht installiert!" -ForegroundColor Red
    Write-Host "Installieren Sie Azure CLI von: https://aka.ms/installazurecliwindows" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "ALTERNATIVE: Manuelle Prüfung im Azure Portal" -ForegroundColor Yellow
    Write-Host "1. Öffnen Sie: https://portal.azure.com" -ForegroundColor White
    Write-Host "2. Navigieren Sie zu Ihrer App Service" -ForegroundColor White
    Write-Host "3. Settings → Configuration → Application Settings" -ForegroundColor White
    Write-Host "4. Prüfen Sie ob 'DB_ConectionString' existiert" -ForegroundColor White
    Write-Host ""
    Write-Host "5. Navigieren Sie zu Ihrer SQL Database" -ForegroundColor White
    Write-Host "6. Security → Networking" -ForegroundColor White
    Write-Host "7. Aktivieren Sie: 'Allow Azure services and resources to access this server'" -ForegroundColor White
    pause
    exit
}

Write-Host "✅ Azure CLI gefunden" -ForegroundColor Green
Write-Host ""

# Login prüfen
Write-Host "Prüfe Azure Login..." -ForegroundColor Yellow
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "❌ Nicht in Azure eingeloggt" -ForegroundColor Red
    Write-Host "Führen Sie aus: az login" -ForegroundColor Yellow
    pause
    exit
}

Write-Host "✅ Eingeloggt als: $($account.user.name)" -ForegroundColor Green
Write-Host "   Subscription: $($account.name)" -ForegroundColor Gray
Write-Host ""

# App Service Name eingeben
Write-Host "========================================" -ForegroundColor Cyan
$appServiceName = Read-Host "App Service Name (z.B. delikatessendrehbuch)"
$resourceGroup = Read-Host "Resource Group Name (z.B. DelikatessenDrehbuch-RG)"

Write-Host ""
Write-Host "Prüfe App Service Settings..." -ForegroundColor Yellow

# App Service Settings abrufen
$settings = az webapp config appsettings list --name $appServiceName --resource-group $resourceGroup 2>$null | ConvertFrom-Json

if (-not $settings) {
    Write-Host "❌ App Service nicht gefunden!" -ForegroundColor Red
    Write-Host "Prüfen Sie Name und Resource Group" -ForegroundColor Yellow
    pause
    exit
}

# Nach DB_ConectionString suchen
$dbConnString = $settings | Where-Object { $_.name -eq "DB_ConectionString" }

if ($dbConnString) {
    Write-Host "✅ DB_ConectionString gefunden!" -ForegroundColor Green
    $maskedValue = $dbConnString.value -replace '(Password=)[^;]+', '$1***'
    Write-Host "   Value: $maskedValue" -ForegroundColor Gray
} else {
    Write-Host "❌ DB_ConectionString NICHT gefunden!" -ForegroundColor Red
    Write-Host ""
    Write-Host "FIX: Fügen Sie die Setting hinzu:" -ForegroundColor Yellow
    Write-Host "az webapp config appsettings set --name $appServiceName --resource-group $resourceGroup --settings DB_ConectionString='IHR_CONNECTION_STRING'" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Weitere manuelle Prüfungen im Portal:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. SQL Firewall: Erlaubt Azure Services?" -ForegroundColor White
Write-Host "   → SQL Database → Networking → Allow Azure services: ON" -ForegroundColor Gray
Write-Host ""
Write-Host "2. SQL User Berechtigungen korrekt?" -ForegroundColor White
Write-Host "   → SQL Query Editor → Login testen" -ForegroundColor Gray
Write-Host ""
pause
