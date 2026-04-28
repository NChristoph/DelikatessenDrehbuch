# PowerShell script to replace T() calls with SharedLocalizer in Home/Index.cshtml

$filePath = "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Areas\WorldMiniApp\Views\Home\Index.cshtml"

# Read file content
$content = Get-Content $filePath -Raw -Encoding UTF8

# Define replacements (German text -> Resource key)
$replacements = @{
    'T\("Mein Wochenplan", "My weekly plan", "Mi plan semanal", "Meu plano semanal"\)' = '@SharedLocalizer["Home.WeeklyPlan"]'
    'T\("Automatisch erstellen \(Device\)", "Auto create \(Device\)", "Crear automáticamente \(Device\)", "Criar automaticamente \(Device\)"\)' = '@SharedLocalizer["Home.AutoCreate"]'
    'T\("Entdecken", "Discover", "Descubrir", "Descobrir"\)' = '@SharedLocalizer["Home.Discover"]'
    'T\("Community & Trends \(Device\)", "Community & Trends \(Device\)", "Comunidad y tendencias \(Device\)", "Comunidade e tendências \(Device\)"\)' = '@SharedLocalizer["Home.CommunityTrends"]'
    'T\("Diätpläne kaufen & verkaufen", "Buy & sell diet plans", "Comprar y vender planes dietéticos", "Comprar e vender planos dietéticos"\)' = '@SharedLocalizer["Home.MarketplaceSubtitle"]'
    'T\("Rezept hochladen", "Upload recipe", "Subir receta", "Enviar receita"\)' = '@SharedLocalizer["Home.UploadRecipe"]'
    'T\("Verifikationsstatus-ID", "Verification status ID", "Estado de verificación ID", "Status de verificação ID"\)' = '@SharedLocalizer["Home.VerificationStatusID"]'
    'T\("Werde Creator & verdiene \(Orb Required\) 💰", "Become a creator & earn \(Orb required\) 💰", "Conviértete en creador y gana \(Orb requerido\) 💰", "Torne-se criador e ganhe \(Orb necessário\) 💰"\)' = '@SharedLocalizer["Home.BecomeCreator"]'
    'T\("Mein Bereich", "My area", "Mi área", "Minha área"\)' = '@SharedLocalizer["Home.MyArea"]'
    'T\("Pläne, Einkaufslisten & mehr", "Plans, shopping lists & more", "Planes, listas de compras y más", "Planos, listas de compras e mais"\)' = '@SharedLocalizer["Home.MyAreaSubtitle"]'
    'T\("Jetzt einloggen", "Log in now", "Iniciar sesión ahora", "Entrar agora"\)' = '@SharedLocalizer["Home.LoginNow"]'
    'T\("Aktiver Debug-Hash:", "Active debug hash:", "Hash debug activo:", "Hash debug ativo:"\)' = '@SharedLocalizer["Debug.ActiveHash"]'
    'T\("Kein Debug-Hash aktiv\.", "No debug hash active\.", "No hay hash debug activo\.", "Nenhum hash debug ativo\."\)' = '@SharedLocalizer["Debug.NoHashActive"]'
    'T\("Mit Test-Hash A anmelden", "Sign in with test hash A", "Iniciar con hash de prueba A", "Entrar com hash de teste A"\)' = '@SharedLocalizer["Debug.SignInTestHashA"]'
    'T\("Mit Test-Hash B anmelden", "Sign in with test hash B", "Iniciar con hash de prueba B", "Entrar com hash de teste B"\)' = '@SharedLocalizer["Debug.SignInTestHashB"]'
    'T\("Debug-Login abmelden", "Sign out debug login", "Cerrar debug login", "Sair do debug login"\)' = '@SharedLocalizer["Debug.SignOutDebug"]'
    'T\("Nur im Debug-Modus sichtbar\.", "Visible only in debug mode\.", "Visible solo en modo debug\.", "Visivel apenas no modo debug\."\)' = '@SharedLocalizer["Debug.OnlyVisibleInDebug"]'
    'T\("Mit World ID anmelden", "Sign in with World ID", "Iniciar sesión con World ID", "Entrar com World ID"\)' = '@SharedLocalizer["Login.SignInWithWorldID"]'
    'T\("bei Delikatessen Drehbuch", "at Delikatessen Drehbuch", "en Delikatessen Drehbuch", "no Delikatessen Drehbuch"\)' = '@SharedLocalizer["Login.AtDelikatessen"]'
    'T\("Die App sieht danach deine", "The app will then see your", "La app verá entonces tu", "O app verá então seu"\)' = '@SharedLocalizer["Login.AppWillSee"]'
    'T\("Unverwechselbarer Mensch", "Unique human", "Humano único", "Humano único"\)' = '@SharedLocalizer["Login.UniqueHuman"]'
    'T\("App erkennt:", "App detects:", "La app detecta:", "O app detecta:"\)' = '@SharedLocalizer["Login.AppDetects"]'
    'T\("Verifikationsstatus", "Verification status", "Estado de verificación", "Status de verificação"\)' = '@SharedLocalizer["Login.VerificationStatus"]'
    'T\("Login merken", "Remember login", "Recordar inicio", "Lembrar login"\)' = '@SharedLocalizer["Login.RememberLogin"]'
    'T\("Genehmigen", "Approve", "Aprobar", "Aprovar"\)' = '@SharedLocalizer["Login.Approve"]'
    'T\("Verbinde mit World App\.\.\.", "Connecting to World App\.\.\.", "Conectando con World App\.\.\.", "Conectando ao World App\.\.\."\)' = '@SharedLocalizer["Login.Connecting"]'
}

# Apply replacements
foreach ($pattern in $replacements.Keys) {
    $replacement = $replacements[$pattern]
    $content = $content -replace $pattern, $replacement
}

# Fix Html.Raw for MadeWithLove
$content = $content -replace '@Html\.Raw\(T\("Gemacht mit <i class=''bi bi-heart-fill text-danger mx-1''></i> für die World App", "Made with <i class=''bi bi-heart-fill text-danger mx-1''></i> for World App", "Hecho con <i class=''bi bi-heart-fill text-danger mx-1''></i> para World App", "Feito com <i class=''bi bi-heart-fill text-danger mx-1''></i> para World App"\)\)', '@Html.Raw(SharedLocalizer["Home.MadeWithLove"])'

# Replace hardcoded JavaScript strings
$content = $content -replace "activeDebugHashLabel\.textContent = '@T\(""Kein Debug-Hash aktiv\."", ""No debug hash active\."", ""No hay hash debug activo\."", ""Nenhum hash debug ativo\.""\)';", "activeDebugHashLabel.textContent = '@SharedLocalizer[""Debug.NoHashActive""]';"
$content = $content -replace "'Rezept erfolgreich veröffentlicht!'", "@SharedLocalizer[""Toast.RecipePublished""]"
$content = $content -replace "'Test-Login fehlgeschlagen\.'", "@SharedLocalizer[""Debug.TestLoginFailed""]"
$content = $content -replace "'Debug-Logout fehlgeschlagen\.'", "@SharedLocalizer[""Debug.LogoutFailed""]"
$content = $content -replace "alert\('Test-Login fehlgeschlagen\.'\);", "alert('@SharedLocalizer[""Debug.TestLoginFailed""]');"
$content = $content -replace "alert\('Debug-Logout fehlgeschlagen\.'\);", "alert('@SharedLocalizer[""Debug.LogoutFailed""]');"

# Write back to file
$content | Set-Content $filePath -Encoding UTF8 -NoNewline

Write-Host "Replacements completed successfully!" -ForegroundColor Green
Write-Host "Modified file: $filePath" -ForegroundColor Cyan
