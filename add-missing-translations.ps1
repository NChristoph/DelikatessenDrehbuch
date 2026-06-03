# Add missing CreatePosting translations to all .resx files
# Run this script to add the missing keys

$resourcesPath = "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Resources"

# Translations for each language
$translations = @{
    "de" = @{
        "CreatePosting.Media" = "Medien"
        "CreatePosting.Ingredients" = "Zutaten"
        "CreatePosting.Steps" = "Schritte"
        "CreatePosting.PreparationSteps" = "Zubereitungsschritte"
        "CreatePosting.WriteYourSteps" = "Schreibe deine Schritte"
        "CreatePosting.SelectedIngredients" = "Ausgewaehlte Zutaten"
        "CreatePosting.TapToEdit" = "Zum Bearbeiten tippen"
        "CreatePosting.CookingInstructions" = "Kochanleitung (wird automatisch uebersetzt)"
    }
    "en" = @{
        "CreatePosting.Media" = "Media"
        "CreatePosting.Ingredients" = "Ingredients"
        "CreatePosting.Steps" = "Steps"
        "CreatePosting.PreparationSteps" = "Preparation Steps"
        "CreatePosting.WriteYourSteps" = "Write your steps"
        "CreatePosting.SelectedIngredients" = "Selected Ingredients"
        "CreatePosting.TapToEdit" = "Tap to edit"
        "CreatePosting.CookingInstructions" = "Cooking Instructions (auto-translated)"
    }
    "es" = @{
        "CreatePosting.Media" = "Medios"
        "CreatePosting.Ingredients" = "Ingredientes"
        "CreatePosting.Steps" = "Pasos"
        "CreatePosting.PreparationSteps" = "Pasos de preparacion"
        "CreatePosting.WriteYourSteps" = "Escribe tus pasos"
        "CreatePosting.SelectedIngredients" = "Ingredientes seleccionados"
        "CreatePosting.TapToEdit" = "Toca para editar"
        "CreatePosting.CookingInstructions" = "Instrucciones de cocina (traducidas automaticamente)"
    }
    "pt" = @{
        "CreatePosting.Media" = "Midia"
        "CreatePosting.Ingredients" = "Ingredientes"
        "CreatePosting.Steps" = "Passos"
        "CreatePosting.PreparationSteps" = "Passos de preparacao"
        "CreatePosting.WriteYourSteps" = "Escreva seus passos"
        "CreatePosting.SelectedIngredients" = "Ingredientes selecionados"
        "CreatePosting.TapToEdit" = "Toque para editar"
        "CreatePosting.CookingInstructions" = "Instrucoes de cozinha (traduzidas automaticamente)"
    }
    "id" = @{
        "CreatePosting.Media" = "Media"
        "CreatePosting.Ingredients" = "Bahan"
        "CreatePosting.Steps" = "Langkah"
        "CreatePosting.PreparationSteps" = "Langkah persiapan"
        "CreatePosting.WriteYourSteps" = "Tulis langkah Anda"
        "CreatePosting.SelectedIngredients" = "Bahan yang dipilih"
        "CreatePosting.TapToEdit" = "Ketuk untuk mengedit"
        "CreatePosting.CookingInstructions" = "Instruksi Memasak (diterjemahkan otomatis)"
    }
    "nl" = @{
        "CreatePosting.Media" = "Media"
        "CreatePosting.Ingredients" = "Ingredienten"
        "CreatePosting.Steps" = "Stappen"
        "CreatePosting.PreparationSteps" = "Bereidingsstappen"
        "CreatePosting.WriteYourSteps" = "Schrijf je stappen"
        "CreatePosting.SelectedIngredients" = "Geselecteerde ingredienten"
        "CreatePosting.TapToEdit" = "Tik om te bewerken"
        "CreatePosting.CookingInstructions" = "Kookinstructies (automatisch vertaald)"
    }
    "sv" = @{
        "CreatePosting.Media" = "Media"
        "CreatePosting.Ingredients" = "Ingredienser"
        "CreatePosting.Steps" = "Steg"
        "CreatePosting.PreparationSteps" = "Forberedelsesteg"
        "CreatePosting.WriteYourSteps" = "Skriv dina steg"
        "CreatePosting.SelectedIngredients" = "Valda ingredienser"
        "CreatePosting.TapToEdit" = "Tryck for att redigera"
        "CreatePosting.CookingInstructions" = "Tillagningsinstruktioner (automatiskt oversatt)"
    }
    "da" = @{
        "CreatePosting.Media" = "Medier"
        "CreatePosting.Ingredients" = "Ingredienser"
        "CreatePosting.Steps" = "Trin"
        "CreatePosting.PreparationSteps" = "Forberedelsestrin"
        "CreatePosting.WriteYourSteps" = "Skriv dine trin"
        "CreatePosting.SelectedIngredients" = "Valgte ingredienser"
        "CreatePosting.TapToEdit" = "Tryk for at redigere"
        "CreatePosting.CookingInstructions" = "Madlavningsinstruktioner (automatisk oversaettelse)"
    }
    "nb" = @{
        "CreatePosting.Media" = "Media"
        "CreatePosting.Ingredients" = "Ingredienser"
        "CreatePosting.Steps" = "Trinn"
        "CreatePosting.PreparationSteps" = "Forberedelsestrin"
        "CreatePosting.WriteYourSteps" = "Skriv dine trinn"
        "CreatePosting.SelectedIngredients" = "Valgte ingredienser"
        "CreatePosting.TapToEdit" = "Trykk for aa redigere"
        "CreatePosting.CookingInstructions" = "Matlaginginstruksjoner (automatisk oversettelse)"
    }
    "ms" = @{
        "CreatePosting.Media" = "Media"
        "CreatePosting.Ingredients" = "Bahan-bahan"
        "CreatePosting.Steps" = "Langkah"
        "CreatePosting.PreparationSteps" = "Langkah persediaan"
        "CreatePosting.WriteYourSteps" = "Tulis langkah anda"
        "CreatePosting.SelectedIngredients" = "Bahan yang dipilih"
        "CreatePosting.TapToEdit" = "Ketik untuk edit"
        "CreatePosting.CookingInstructions" = "Arahan Memasak (diterjemahkan automatik)"
    }
}

# Function to add translations to .resx file
function Add-TranslationsToResx {
    param(
        [string]$FilePath,
        [hashtable]$Translations
    )

    Write-Host "Processing: $FilePath" -ForegroundColor Cyan

    $content = Get-Content $FilePath -Raw -Encoding UTF8

    # Find the position before </root>
    $insertPosition = $content.LastIndexOf('</root>')

    if ($insertPosition -lt 0) {
        Write-Host "  ERROR: </root> not found!" -ForegroundColor Red
        return
    }

    # Build the XML entries
    $xmlEntries = ""
    foreach ($key in $Translations.Keys) {
        $value = $Translations[$key]
        $xmlEntries += "  <data name=`"$key`" xml:space=`"preserve`">`r`n    <value>$value</value>`r`n  </data>`r`n"
    }

    # Insert before </root>
    $newContent = $content.Insert($insertPosition, $xmlEntries)

    # Save with UTF-8 encoding
    [System.IO.File]::WriteAllText($FilePath, $newContent, [System.Text.Encoding]::UTF8)

    Write-Host "  Done: Added $($Translations.Count) keys" -ForegroundColor Green
}

# Process all language files
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
Write-Host "All translations added successfully!" -ForegroundColor Green
Write-Host "Please rebuild your project (Ctrl+Shift+B)" -ForegroundColor Cyan
