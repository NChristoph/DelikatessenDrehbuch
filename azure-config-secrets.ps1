# Azure App Service - Admin Secrets konfigurieren
# Führen Sie dieses Skript in PowerShell aus (mit Azure CLI installiert)

# === KONFIGURATION ===
$resourceGroup = "IhrResourceGroupName"      # z.B. "DelikatessenDrehbuch-RG"
$appName = "IhrAppServiceName"               # z.B. "delikatessendrehbuch-app"
$adminPasswordHash = "$2a$11$deinHash..."    # Generieren Sie diesen mit /Admin/GeneratePasswordHash
$superUserHash = "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839"

# === SECRETS SETZEN ===
Write-Host "Setze Admin-Passwort-Hash..." -ForegroundColor Cyan
az webapp config appsettings set `
    --resource-group $resourceGroup `
    --name $appName `
    --settings WorldMiniApp__AdminPassword="$adminPasswordHash"

Write-Host "Setze SuperUser-Hash..." -ForegroundColor Cyan
az webapp config appsettings set `
    --resource-group $resourceGroup `
    --name $appName `
    --settings WorldMiniApp__SuperUserHash="$superUserHash"

Write-Host "✓ Secrets erfolgreich in Azure konfiguriert!" -ForegroundColor Green
Write-Host "Die App wird automatisch neu gestartet." -ForegroundColor Yellow

# === SECRETS AUSLESEN (zum Verifizieren) ===
Write-Host "`nAktuelle Settings:" -ForegroundColor Cyan
az webapp config appsettings list `
    --resource-group $resourceGroup `
    --name $appName `
    --query "[?name=='WorldMiniApp__AdminPassword' || name=='WorldMiniApp__SuperUserHash']" `
    --output table
