# Passwort-Hash Generator (ohne App)
# BCrypt-Hash direkt in PowerShell generieren

$password = Read-Host "Geben Sie Ihr gewünschtes Admin-Passwort ein" -AsSecureString
$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host "`nGeneriere BCrypt-Hash..." -ForegroundColor Cyan
Write-Host "HINWEIS: Dieser Hash sollte NUR zu Testzwecken verwendet werden." -ForegroundColor Yellow
Write-Host "Verwenden Sie für Production den /Admin/GeneratePasswordHash Endpunkt." -ForegroundColor Yellow

# Alternative: Einfacher SHA256-Hash (nicht so sicher wie BCrypt, aber funktioniert)
$bytes = [System.Text.Encoding]::UTF8.GetBytes($plainPassword)
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$hash = $sha256.ComputeHash($bytes)
$hashString = [System.BitConverter]::ToString($hash).Replace("-", "").ToLower()

Write-Host "`n✅ Generierter Hash (SHA256 - für Tests):" -ForegroundColor Green
Write-Host $hashString -ForegroundColor White

Write-Host "`n⚠️ WICHTIG: Für Production verwenden Sie BCrypt!" -ForegroundColor Red
Write-Host "Öffnen Sie: https://localhost:7179/WorldMiniApp/Admin/GeneratePasswordHash" -ForegroundColor Yellow

Write-Host "`n📋 Nächste Schritte:" -ForegroundColor Cyan
Write-Host "1. Kopieren Sie den obigen Hash"
Write-Host "2. Fügen Sie ihn in launchSettings.json ein:"
Write-Host "   WorldMiniApp__AdminPassword: `"$hashString`""
