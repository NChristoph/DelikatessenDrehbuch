# PowerShell script to add Home menu strings to remaining 6 language files

$newStrings = @'

  <!-- Home Menu -->
  <data name="Home.WeeklyPlan" xml:space="preserve">
    <value>{WEEKLY_PLAN}</value>
  </data>
  <data name="Home.AutoCreate" xml:space="preserve">
    <value>{AUTO_CREATE}</value>
  </data>
  <data name="Home.Discover" xml:space="preserve">
    <value>{DISCOVER}</value>
  </data>
  <data name="Home.CommunityTrends" xml:space="preserve">
    <value>{COMMUNITY_TRENDS}</value>
  </data>
  <data name="Home.Marketplace" xml:space="preserve">
    <value>{MARKETPLACE}</value>
  </data>
  <data name="Home.MarketplaceSubtitle" xml:space="preserve">
    <value>{MARKETPLACE_SUBTITLE}</value>
  </data>
  <data name="Home.UploadRecipe" xml:space="preserve">
    <value>{UPLOAD_RECIPE}</value>
  </data>
  <data name="Home.VerificationStatusID" xml:space="preserve">
    <value>{VERIFICATION_STATUS_ID}</value>
  </data>
  <data name="Home.BecomeCreator" xml:space="preserve">
    <value>{BECOME_CREATOR}</value>
  </data>
  <data name="Home.MyArea" xml:space="preserve">
    <value>{MY_AREA}</value>
  </data>
  <data name="Home.MyAreaSubtitle" xml:space="preserve">
    <value>{MY_AREA_SUBTITLE}</value>
  </data>
  <data name="Home.LoginNow" xml:space="preserve">
    <value>{LOGIN_NOW}</value>
  </data>
  <data name="Home.MadeWithLove" xml:space="preserve">
    <value>{MADE_WITH_LOVE}</value>
  </data>

  <!-- Login Modal -->
  <data name="Login.SignInWithWorldID" xml:space="preserve">
    <value>{SIGN_IN_WORLD_ID}</value>
  </data>
  <data name="Login.AtDelikatessen" xml:space="preserve">
    <value>{AT_DELIKATESSEN}</value>
  </data>
  <data name="Login.AppWillSee" xml:space="preserve">
    <value>{APP_WILL_SEE}</value>
  </data>
  <data name="Login.UniqueHuman" xml:space="preserve">
    <value>{UNIQUE_HUMAN}</value>
  </data>
  <data name="Login.AppDetects" xml:space="preserve">
    <value>{APP_DETECTS}</value>
  </data>
  <data name="Login.VerificationStatus" xml:space="preserve">
    <value>{VERIFICATION_STATUS}</value>
  </data>
  <data name="Login.RememberLogin" xml:space="preserve">
    <value>{REMEMBER_LOGIN}</value>
  </data>
  <data name="Login.Approve" xml:space="preserve">
    <value>{APPROVE}</value>
  </data>
  <data name="Login.Connecting" xml:space="preserve">
    <value>{CONNECTING}</value>
  </data>

  <!-- Debug Mode -->
  <data name="Debug.ActiveHash" xml:space="preserve">
    <value>{ACTIVE_HASH}</value>
  </data>
  <data name="Debug.NoHashActive" xml:space="preserve">
    <value>{NO_HASH_ACTIVE}</value>
  </data>
  <data name="Debug.SignInTestHashA" xml:space="preserve">
    <value>{SIGN_IN_TEST_A}</value>
  </data>
  <data name="Debug.SignInTestHashB" xml:space="preserve">
    <value>{SIGN_IN_TEST_B}</value>
  </data>
  <data name="Debug.SignOutDebug" xml:space="preserve">
    <value>{SIGN_OUT_DEBUG}</value>
  </data>
  <data name="Debug.OnlyVisibleInDebug" xml:space="preserve">
    <value>{ONLY_VISIBLE_DEBUG}</value>
  </data>
  <data name="Debug.TestLoginFailed" xml:space="preserve">
    <value>{TEST_LOGIN_FAILED}</value>
  </data>
  <data name="Debug.LogoutFailed" xml:space="preserve">
    <value>{LOGOUT_FAILED}</value>
  </data>

  <!-- Toast Messages (Additional) -->
  <data name="Toast.RecipePublished" xml:space="preserve">
    <value>{RECIPE_PUBLISHED}</value>
  </data>
</root>
'@

$translations = @{
    'id' = @{
        'WEEKLY_PLAN' = 'Rencana mingguan saya'
        'AUTO_CREATE' = 'Buat otomatis (Device)'
        'DISCOVER' = 'Jelajahi'
        'COMMUNITY_TRENDS' = 'Komunitas &amp; Tren (Device)'
        'MARKETPLACE' = 'Pasar'
        'MARKETPLACE_SUBTITLE' = 'Beli &amp; jual rencana diet'
        'UPLOAD_RECIPE' = 'Unggah resep'
        'VERIFICATION_STATUS_ID' = 'Status verifikasi ID'
        'BECOME_CREATOR' = 'Jadi kreator &amp; hasilkan (Orb diperlukan) 💰'
        'MY_AREA' = 'Area saya'
        'MY_AREA_SUBTITLE' = 'Rencana, daftar belanja &amp; lainnya'
        'LOGIN_NOW' = 'Masuk sekarang'
        'MADE_WITH_LOVE' = 'Dibuat dengan &lt;i class=''bi bi-heart-fill text-danger mx-1''&gt;&lt;/i&gt; untuk World App'
        'SIGN_IN_WORLD_ID' = 'Masuk dengan World ID'
        'AT_DELIKATESSEN' = 'di Delikatessen Drehbuch'
        'APP_WILL_SEE' = 'Aplikasi akan melihat'
        'UNIQUE_HUMAN' = 'Manusia unik'
        'APP_DETECTS' = 'Aplikasi mendeteksi:'
        'VERIFICATION_STATUS' = 'Status verifikasi'
        'REMEMBER_LOGIN' = 'Ingat login'
        'APPROVE' = 'Setuju'
        'CONNECTING' = 'Menghubungkan ke World App...'
        'ACTIVE_HASH' = 'Hash debug aktif:'
        'NO_HASH_ACTIVE' = 'Tidak ada hash debug aktif.'
        'SIGN_IN_TEST_A' = 'Masuk dengan hash tes A'
        'SIGN_IN_TEST_B' = 'Masuk dengan hash tes B'
        'SIGN_OUT_DEBUG' = 'Keluar debug login'
        'ONLY_VISIBLE_DEBUG' = 'Hanya terlihat dalam mode debug.'
        'TEST_LOGIN_FAILED' = 'Login tes gagal.'
        'LOGOUT_FAILED' = 'Logout debug gagal.'
        'RECIPE_PUBLISHED' = 'Resep berhasil dipublikasikan!'
    }
    'nl' = @{
        'WEEKLY_PLAN' = 'Mijn weekplan'
        'AUTO_CREATE' = 'Automatisch aanmaken (Device)'
        'DISCOVER' = 'Ontdekken'
        'COMMUNITY_TRENDS' = 'Community &amp; Trends (Device)'
        'MARKETPLACE' = 'Marktplaats'
        'MARKETPLACE_SUBTITLE' = 'Dieetplannen kopen &amp; verkopen'
        'UPLOAD_RECIPE' = 'Recept uploaden'
        'VERIFICATION_STATUS_ID' = 'Verificatiestatus ID'
        'BECOME_CREATOR' = 'Word maker &amp; verdien (Orb vereist) 💰'
        'MY_AREA' = 'Mijn gebied'
        'MY_AREA_SUBTITLE' = 'Plannen, boodschappenlijsten &amp; meer'
        'LOGIN_NOW' = 'Nu inloggen'
        'MADE_WITH_LOVE' = 'Gemaakt met &lt;i class=''bi bi-heart-fill text-danger mx-1''&gt;&lt;/i&gt; voor World App'
        'SIGN_IN_WORLD_ID' = 'Inloggen met World ID'
        'AT_DELIKATESSEN' = 'bij Delikatessen Drehbuch'
        'APP_WILL_SEE' = 'De app zal dan je'
        'UNIQUE_HUMAN' = 'Uniek mens'
        'APP_DETECTS' = 'App detecteert:'
        'VERIFICATION_STATUS' = 'Verificatiestatus'
        'REMEMBER_LOGIN' = 'Login onthouden'
        'APPROVE' = 'Goedkeuren'
        'CONNECTING' = 'Verbinden met World App...'
        'ACTIVE_HASH' = 'Actieve debug hash:'
        'NO_HASH_ACTIVE' = 'Geen debug hash actief.'
        'SIGN_IN_TEST_A' = 'Inloggen met test hash A'
        'SIGN_IN_TEST_B' = 'Inloggen met test hash B'
        'SIGN_OUT_DEBUG' = 'Debug login uitloggen'
        'ONLY_VISIBLE_DEBUG' = 'Alleen zichtbaar in debug-modus.'
        'TEST_LOGIN_FAILED' = 'Test login mislukt.'
        'LOGOUT_FAILED' = 'Debug logout mislukt.'
        'RECIPE_PUBLISHED' = 'Recept succesvol gepubliceerd!'
    }
    'sv' = @{
        'WEEKLY_PLAN' = 'Min veckoplan'
        'AUTO_CREATE' = 'Skapa automatiskt (Device)'
        'DISCOVER' = 'Upptäck'
        'COMMUNITY_TRENDS' = 'Community &amp; Trender (Device)'
        'MARKETPLACE' = 'Marknadsplats'
        'MARKETPLACE_SUBTITLE' = 'Köp &amp; sälj kostplaner'
        'UPLOAD_RECIPE' = 'Ladda upp recept'
        'VERIFICATION_STATUS_ID' = 'Verifieringsstatus ID'
        'BECOME_CREATOR' = 'Bli skapare &amp; tjäna (Orb krävs) 💰'
        'MY_AREA' = 'Mitt område'
        'MY_AREA_SUBTITLE' = 'Planer, inköpslistor &amp; mer'
        'LOGIN_NOW' = 'Logga in nu'
        'MADE_WITH_LOVE' = 'Gjord med &lt;i class=''bi bi-heart-fill text-danger mx-1''&gt;&lt;/i&gt; för World App'
        'SIGN_IN_WORLD_ID' = 'Logga in med World ID'
        'AT_DELIKATESSEN' = 'på Delikatessen Drehbuch'
        'APP_WILL_SEE' = 'Appen kommer då att se din'
        'UNIQUE_HUMAN' = 'Unik människa'
        'APP_DETECTS' = 'Appen upptäcker:'
        'VERIFICATION_STATUS' = 'Verifieringsstatus'
        'REMEMBER_LOGIN' = 'Kom ihåg inloggning'
        'APPROVE' = 'Godkänn'
        'CONNECTING' = 'Ansluter till World App...'
        'ACTIVE_HASH' = 'Aktiv debug hash:'
        'NO_HASH_ACTIVE' = 'Ingen debug hash aktiv.'
        'SIGN_IN_TEST_A' = 'Logga in med test hash A'
        'SIGN_IN_TEST_B' = 'Logga in med test hash B'
        'SIGN_OUT_DEBUG' = 'Logga ut debug login'
        'ONLY_VISIBLE_DEBUG' = 'Endast synlig i debug-läge.'
        'TEST_LOGIN_FAILED' = 'Test login misslyckades.'
        'LOGOUT_FAILED' = 'Debug logout misslyckades.'
        'RECIPE_PUBLISHED' = 'Recept publicerat!'
    }
    'da' = @{
        'WEEKLY_PLAN' = 'Min ugeplan'
        'AUTO_CREATE' = 'Opret automatisk (Device)'
        'DISCOVER' = 'Opdage'
        'COMMUNITY_TRENDS' = 'Community &amp; Trends (Device)'
        'MARKETPLACE' = 'Markedsplads'
        'MARKETPLACE_SUBTITLE' = 'Køb &amp; sælg kostplaner'
        'UPLOAD_RECIPE' = 'Upload opskrift'
        'VERIFICATION_STATUS_ID' = 'Verifikationsstatus ID'
        'BECOME_CREATOR' = 'Bliv skaber &amp; tjen (Orb påkrævet) 💰'
        'MY_AREA' = 'Mit område'
        'MY_AREA_SUBTITLE' = 'Planer, indkøbslister &amp; mere'
        'LOGIN_NOW' = 'Log ind nu'
        'MADE_WITH_LOVE' = 'Lavet med &lt;i class=''bi bi-heart-fill text-danger mx-1''&gt;&lt;/i&gt; til World App'
        'SIGN_IN_WORLD_ID' = 'Log ind med World ID'
        'AT_DELIKATESSEN' = 'hos Delikatessen Drehbuch'
        'APP_WILL_SEE' = 'Appen vil derefter se din'
        'UNIQUE_HUMAN' = 'Unik menneske'
        'APP_DETECTS' = 'App registrerer:'
        'VERIFICATION_STATUS' = 'Verifikationsstatus'
        'REMEMBER_LOGIN' = 'Husk login'
        'APPROVE' = 'Godkend'
        'CONNECTING' = 'Forbinder til World App...'
        'ACTIVE_HASH' = 'Aktiv debug hash:'
        'NO_HASH_ACTIVE' = 'Ingen debug hash aktiv.'
        'SIGN_IN_TEST_A' = 'Log ind med test hash A'
        'SIGN_IN_TEST_B' = 'Log ind med test hash B'
        'SIGN_OUT_DEBUG' = 'Log ud debug login'
        'ONLY_VISIBLE_DEBUG' = 'Kun synlig i debug-tilstand.'
        'TEST_LOGIN_FAILED' = 'Test login mislykkedes.'
        'LOGOUT_FAILED' = 'Debug logout mislykkedes.'
        'RECIPE_PUBLISHED' = 'Opskrift publiceret!'
    }
    'nb' = @{
        'WEEKLY_PLAN' = 'Min ukeplan'
        'AUTO_CREATE' = 'Opprett automatisk (Device)'
        'DISCOVER' = 'Oppdag'
        'COMMUNITY_TRENDS' = 'Community &amp; Trender (Device)'
        'MARKETPLACE' = 'Markedsplass'
        'MARKETPLACE_SUBTITLE' = 'Kjøp &amp; selg kostplaner'
        'UPLOAD_RECIPE' = 'Last opp oppskrift'
        'VERIFICATION_STATUS_ID' = 'Verifiseringsstatus ID'
        'BECOME_CREATOR' = 'Bli skaper &amp; tjen (Orb påkrevd) 💰'
        'MY_AREA' = 'Mitt område'
        'MY_AREA_SUBTITLE' = 'Planer, handlelister &amp; mer'
        'LOGIN_NOW' = 'Logg inn nå'
        'MADE_WITH_LOVE' = 'Laget med &lt;i class=''bi bi-heart-fill text-danger mx-1''&gt;&lt;/i&gt; for World App'
        'SIGN_IN_WORLD_ID' = 'Logg inn med World ID'
        'AT_DELIKATESSEN' = 'hos Delikatessen Drehbuch'
        'APP_WILL_SEE' = 'Appen vil da se din'
        'UNIQUE_HUMAN' = 'Unik menneske'
        'APP_DETECTS' = 'App oppdager:'
        'VERIFICATION_STATUS' = 'Verifiseringsstatus'
        'REMEMBER_LOGIN' = 'Husk innlogging'
        'APPROVE' = 'Godkjenn'
        'CONNECTING' = 'Kobler til World App...'
        'ACTIVE_HASH' = 'Aktiv debug hash:'
        'NO_HASH_ACTIVE' = 'Ingen debug hash aktiv.'
        'SIGN_IN_TEST_A' = 'Logg inn med test hash A'
        'SIGN_IN_TEST_B' = 'Logg inn med test hash B'
        'SIGN_OUT_DEBUG' = 'Logg ut debug login'
        'ONLY_VISIBLE_DEBUG' = 'Kun synlig i debug-modus.'
        'TEST_LOGIN_FAILED' = 'Test login mislyktes.'
        'LOGOUT_FAILED' = 'Debug logout mislyktes.'
        'RECIPE_PUBLISHED' = 'Oppskrift publisert!'
    }
    'ms' = @{
        'WEEKLY_PLAN' = 'Rancangan mingguan saya'
        'AUTO_CREATE' = 'Cipta automatik (Device)'
        'DISCOVER' = 'Terokai'
        'COMMUNITY_TRENDS' = 'Komuniti &amp; Trend (Device)'
        'MARKETPLACE' = 'Pasaran'
        'MARKETPLACE_SUBTITLE' = 'Beli &amp; jual pelan diet'
        'UPLOAD_RECIPE' = 'Muat naik resipi'
        'VERIFICATION_STATUS_ID' = 'Status pengesahan ID'
        'BECOME_CREATOR' = 'Jadi pencipta &amp; jana (Orb diperlukan) 💰'
        'MY_AREA' = 'Kawasan saya'
        'MY_AREA_SUBTITLE' = 'Rancangan, senarai beli-belah &amp; lagi'
        'LOGIN_NOW' = 'Log masuk sekarang'
        'MADE_WITH_LOVE' = 'Dibuat dengan &lt;i class=''bi bi-heart-fill text-danger mx-1''&gt;&lt;/i&gt; untuk World App'
        'SIGN_IN_WORLD_ID' = 'Log masuk dengan World ID'
        'AT_DELIKATESSEN' = 'di Delikatessen Drehbuch'
        'APP_WILL_SEE' = 'Aplikasi akan melihat'
        'UNIQUE_HUMAN' = 'Manusia unik'
        'APP_DETECTS' = 'Aplikasi mengesan:'
        'VERIFICATION_STATUS' = 'Status pengesahan'
        'REMEMBER_LOGIN' = 'Ingat log masuk'
        'APPROVE' = 'Luluskan'
        'CONNECTING' = 'Menyambung ke World App...'
        'ACTIVE_HASH' = 'Hash debug aktif:'
        'NO_HASH_ACTIVE' = 'Tiada hash debug aktif.'
        'SIGN_IN_TEST_A' = 'Log masuk dengan hash ujian A'
        'SIGN_IN_TEST_B' = 'Log masuk dengan hash ujian B'
        'SIGN_OUT_DEBUG' = 'Log keluar debug login'
        'ONLY_VISIBLE_DEBUG' = 'Hanya kelihatan dalam mod debug.'
        'TEST_LOGIN_FAILED' = 'Login ujian gagal.'
        'LOGOUT_FAILED' = 'Logout debug gagal.'
        'RECIPE_PUBLISHED' = 'Resipi berjaya diterbitkan!'
    }
}

$basePath = "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Resources"

foreach ($lang in $translations.Keys) {
    $filePath = "$basePath\SharedResources.$lang.resx"

    Write-Host "Processing $filePath..." -ForegroundColor Cyan

    $content = Get-Content $filePath -Raw -Encoding UTF8

    # Replace placeholders
    $localizedContent = $newStrings
    foreach ($key in $translations[$lang].Keys) {
        $localizedContent = $localizedContent -replace "\{$key\}", $translations[$lang][$key]
    }

    # Insert before </root>
    $content = $content -replace '</root>', $localizedContent

    # Write back
    $content | Set-Content $filePath -Encoding UTF8 -NoNewline

    Write-Host "✓ Updated $lang" -ForegroundColor Green
}

Write-Host "`nAll 6 language files updated successfully!" -ForegroundColor Green
