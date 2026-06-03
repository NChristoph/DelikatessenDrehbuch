# Add missing Keywords and AutoTranslated translations

$resourcesPath = "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Resources"

$translations = @{
    "de" = @{
        "CreatePosting.Keywords" = "Schlagwoerter"
        "Common.AutoTranslated" = "wird automatisch uebersetzt"
    }
    "en" = @{
        "CreatePosting.Keywords" = "Keywords"
        "Common.AutoTranslated" = "auto-translated"
    }
    "es" = @{
        "CreatePosting.Keywords" = "Palabras clave"
        "Common.AutoTranslated" = "traducido automaticamente"
    }
    "pt" = @{
        "CreatePosting.Keywords" = "Palavras-chave"
        "Common.AutoTranslated" = "traduzido automaticamente"
    }
    "id" = @{
        "CreatePosting.Keywords" = "Kata kunci"
        "Common.AutoTranslated" = "diterjemahkan otomatis"
    }
    "nl" = @{
        "CreatePosting.Keywords" = "Trefwoorden"
        "Common.AutoTranslated" = "automatisch vertaald"
    }
    "sv" = @{
        "CreatePosting.Keywords" = "Nyckelord"
        "Common.AutoTranslated" = "automatiskt oversatt"
    }
    "da" = @{
        "CreatePosting.Keywords" = "Noegleord"
        "Common.AutoTranslated" = "automatisk oversat"
    }
    "nb" = @{
        "CreatePosting.Keywords" = "Nokkelord"
        "Common.AutoTranslated" = "automatisk oversatt"
    }
    "ms" = @{
        "CreatePosting.Keywords" = "Kata kunci"
        "Common.AutoTranslated" = "diterjemahkan automatik"
    }
}

function Add-TranslationsToResx {
    param(
        [string]$FilePath,
        [hashtable]$Translations
    )

    Write-Host "Processing: $FilePath" -ForegroundColor Cyan

    $content = Get-Content $FilePath -Raw -Encoding UTF8
    $insertPosition = $content.LastIndexOf('</root>')

    if ($insertPosition -lt 0) {
        Write-Host "  ERROR: </root> not found!" -ForegroundColor Red
        return
    }

    $xmlEntries = ""
    foreach ($key in $Translations.Keys) {
        $value = $Translations[$key]
        $xmlEntries += "  <data name=`"$key`" xml:space=`"preserve`">`r`n    <value>$value</value>`r`n  </data>`r`n"
    }

    $newContent = $content.Insert($insertPosition, $xmlEntries)
    [System.IO.File]::WriteAllText($FilePath, $newContent, [System.Text.Encoding]::UTF8)

    Write-Host "  Done: Added $($Translations.Count) keys" -ForegroundColor Green
}

foreach ($lang in $translations.Keys) {
    $fileName = if ($lang -eq "de") {
        "SharedResources.resx"
    } else {
        "SharedResources.$lang.resx"
    }

    $filePath = Join-Path $resourcesPath $fileName

    if (Test-Path $filePath) {
        Add-TranslationsToResx -FilePath $filePath -Translations $translations[$lang]
    } else {
        Write-Host "File not found: $filePath" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Keywords and AutoTranslated added to all 10 languages!" -ForegroundColor Green
Write-Host "Please rebuild your project (Ctrl+Shift+B)" -ForegroundColor Cyan
