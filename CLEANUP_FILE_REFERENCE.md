# 🗂️ File-by-File Reference Guide

Schnelle Übersicht: Was muss in welcher Datei geändert werden?

---

## ❌ ZU LÖSCHENDE DATEIEN

```
Models/RecipePreparationSteps.cs
Models/RecipeJoinPreparationSteps.cs
Models/JoinIngredientPreparationStepViewModel.cs
```

**PowerShell:**
```powershell
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Models
Remove-Item RecipePreparationSteps.cs
Remove-Item RecipeJoinPreparationSteps.cs
Remove-Item JoinIngredientPreparationStepViewModel.cs
```

---

## 🔧 ZU AKTUALISIERENDE DATEIEN

### 1. **EditRecipesModel.cs**

**Zeilen:** 15-16

**Alt:**
```csharp
public List<RecipePreparationSteps> RecipePreparationSteps { get; set; }
public List<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
```

**Neu:**
```csharp
public List<RecipeStep> RecipeSteps { get; set; } = new();
```

---

### 2. **SaveNewRecipeModel.cs**

**Zeile:** 8

**Alt:**
```csharp
public List<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
```

**Neu:**
```csharp
public List<RecipeStep> RecipeSteps { get; set; } = new();
```

---

### 3. **RecipeBaseData.cs**

**Zeile:** 30

**Alt:**
```csharp
public virtual ICollection<RecipeJoinPreparationSteps> Steps { get; set; }
```

**Neu:**
```csharp
public virtual ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
```

---

### 4. **JoinIngredientPreparationStep.cs**

**Zeile:** 8

**Alt:**
```csharp
public RecipePreparationSteps Preparation { get; set; }
```

**Neu:**
```csharp
public RecipeStep Preparation { get; set; }
```

---

### 5. **ApplicationDbContext.cs**

#### a) DbSets entfernen (Zeilen 36, 39)

**LÖSCHEN:**
```csharp
public DbSet<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
public DbSet<RecipePreparationSteps> RecipePreparationSteps { get; set; }
```

#### b) OnModelCreating - Table Mapping entfernen (Zeilen ~329-334)

**LÖSCHEN:**
```csharp
builder.Entity<RecipePreparationSteps>().ToTable("RecipePreperationSteps");

builder.Entity<RecipeJoinPreparationSteps>(e =>
{
    e.ToTable("RecipeJoinPreparationSteps");
    e.HasKey(x => x.Id);
    // ... alle weiteren Konfigurationen
});
```

---

### 6. **RecipeController.cs**

**Betroffene Zeilen:** 104, 263, 317, 330, 340, 493-566, 842-872, 1000

#### Zeile 104 - CreatePosting
**Alt:**
```csharp
RecipeJoinPreparationSteps = ExtractCreatePostingStepsFromRequest(Request.Form),
```

**Neu:**
```csharp
// Steps werden jetzt direkt erstellt - ExtractCreatePostingStepsFromRequest umschreiben
```

#### Zeilen 317-340 - UpsertStep
**Alt:**
```csharp
var existing = await _context.RecipePreparationSteps
    .FirstOrDefaultAsync(x => x.Step_DE == de && x.Step_EN == en && x.Step_ESP == esp && x.Step_PRT == prt);

if (existing != null)
    return existing.Id;

var step = new RecipePreparationSteps
{
    Step_DE = de,
    Step_EN = en,
    Step_ESP = esp,
    Step_PRT = prt
};

await _context.RecipePreparationSteps.AddAsync(step);
```

**Neu:**
```csharp
// Neues System: Source Language nur
var step = new RecipeStep
{
    RecipeId = recipeId,
    StepOrder = stepIndex,
    StepText = de,  // Source = Deutsch
    SourceLanguage = "de",
    CreatedAt = DateTime.UtcNow
};

await _context.RecipeSteps.AddAsync(step);
await _context.SaveChangesAsync();

// Optional: Translation Service aufrufen
// await _translationService.TranslateCoreLanguagesAsync(recipeId);

return step.Id;
```

#### Zeilen 493-566 - ExtractCreatePostingStepsFromRequest

**Komplette Methode umschreiben:**

**Alt:**
```csharp
private List<RecipeJoinPreparationSteps> ExtractCreatePostingStepsFromRequest(IFormCollection form)
{
    var result = new List<RecipeJoinPreparationSteps>();

    // ... komplexe Join-Logik
    var recipeStep = new RecipePreparationSteps { ... };
    result.Add(new RecipeJoinPreparationSteps { ... });

    return result;
}
```

**Neu:**
```csharp
private List<RecipeStep> ExtractCreatePostingStepsFromRequest(IFormCollection form)
{
    var result = new List<RecipeStep>();

    int stepIndex = 0;
    foreach (var key in form.Keys.Where(k => k.StartsWith("steps[")))
    {
        // Parse step index
        var match = Regex.Match(key, @"steps\[(\d+)\]");
        if (!match.Success) continue;

        int index = int.Parse(match.Groups[1].Value);

        // Get German text (Source)
        string stepText = form[$"steps[{index}][de]"].ToString();

        if (string.IsNullOrWhiteSpace(stepText))
            continue;

        result.Add(new RecipeStep
        {
            StepOrder = stepIndex++,
            StepText = stepText,
            SourceLanguage = "de",
            CreatedAt = DateTime.UtcNow
        });
    }

    return result;
}
```

#### Zeilen 842-872 - UpdateRecipeSteps (in SaveEditedRecipe)

**Alt:**
```csharp
var existingSteps = await _context.RecipeJoinPreparationSteps
    .Where(s => s.Recipe_Id == recipe.Id)
    .ToListAsync();
_context.RecipeJoinPreparationSteps.RemoveRange(existingSteps);

foreach (var stepJoin in model.Recipe.Steps)
{
    RecipePreparationSteps stepEntity;
    if (stepJoin.PreparationStepId > 0)
    {
        stepEntity = await _context.RecipePreparationSteps.FindAsync(stepJoin.PreparationStepId);
    }
    else
    {
        // ... new step logic
    }

    var newJoin = new RecipeJoinPreparationSteps { ... };
    await _context.RecipeJoinPreparationSteps.AddAsync(newJoin);
}
```

**Neu:**
```csharp
// Alte Steps entfernen
var existingSteps = await _context.RecipeSteps
    .Where(s => s.RecipeId == recipe.Id)
    .ToListAsync();
_context.RecipeSteps.RemoveRange(existingSteps);

// Neue Steps hinzufügen
int stepOrder = 0;
foreach (var step in model.Recipe.Steps)
{
    var newStep = new RecipeStep
    {
        RecipeId = recipe.Id,
        StepOrder = stepOrder++,
        StepText = step.StepText,
        SourceLanguage = step.SourceLanguage ?? "de",
        CreatedAt = DateTime.UtcNow
    };

    await _context.RecipeSteps.AddAsync(newStep);
}

// Translation neu triggern
await _translationService.TranslateCoreLanguagesAsync(recipe.Id);
```

#### Zeile 1000 - DeleteRecipe

**Alt:**
```csharp
_context.RecipeJoinPreparationSteps.RemoveRange(posting.Recipe.Steps);
```

**Neu:**
```csharp
_context.RecipeSteps.RemoveRange(posting.Recipe.Steps);
```

---

### 7. **RecipeSwapController.cs**

**Zeile:** 1173

**Alt:**
```csharp
private static string GetStepText(RecipePreparationSteps step, string language)
{
    return language switch
    {
        "de" => step.Step_DE,
        "en" => step.Step_EN,
        "esp" => step.Step_ESP,
        "prt" => step.Step_PRT,
        _ => step.Step_DE
    };
}
```

**Neu:**
```csharp
private async Task<string> GetStepText(RecipeStep step, string language)
{
    // Source Language = direkt zurück
    if (step.SourceLanguage == language)
        return step.StepText;

    // Translation abfragen
    var translation = await _context.RecipeStepTranslations
        .FirstOrDefaultAsync(t => t.StepId == step.Id && t.Language == language);

    return translation?.TranslatedText ?? step.StepText;  // Fallback
}
```

**WICHTIG:** Methode ist jetzt `async Task<string>` statt `static string`!

**Alle Aufrufe anpassen:**
```csharp
// Vorher:
var text = GetStepText(step, "en");

// Nachher:
var text = await GetStepText(step, "en");
```

---

### 8. **RecipeAiTransformService.cs**

**Zeile:** 6146

**Gleiche Änderung wie RecipeSwapController.cs:**

```csharp
private async Task<string> GetStepText(RecipeStep? step, string language)
{
    if (step == null) return string.Empty;

    if (step.SourceLanguage == language)
        return step.StepText;

    var translation = await _context.RecipeStepTranslations
        .FirstOrDefaultAsync(t => t.StepId == step.Id && t.Language == language);

    return translation?.TranslatedText ?? step.StepText;
}
```

---

### 9. **SaveNewRecipeService.cs**

**Zeilen:** 203, 212, 340-403

#### Zeilen 203-212 - SaveRecipe Method

**Alt:**
```csharp
foreach (var step in model.RecipeJoinPreparationSteps ?? Enumerable.Empty<RecipeJoinPreparationSteps>())
{
    var resolvedStep = await ResolvePreparationStepAsync(step);
    step.PreparationStep = resolvedStep;
    step.Recipe = recipe;
    _context.RecipeJoinPreparationSteps.Add(step);
}
```

**Neu:**
```csharp
// Steps direkt speichern (kein Join mehr)
int stepOrder = 0;
foreach (var step in model.RecipeSteps ?? Enumerable.Empty<RecipeStep>())
{
    step.RecipeId = recipe.Id;
    step.StepOrder = stepOrder++;
    step.CreatedAt = DateTime.UtcNow;
    _context.RecipeSteps.Add(step);
}

await _context.SaveChangesAsync();

// Background Translation
await _translationService.TranslateCoreLanguagesAsync(recipe.Id);
```

#### Zeilen 340-403 - ResolvePreparationStepAsync

**LÖSCHEN - Methode wird nicht mehr gebraucht!**

---

### 10. **AdminController.cs**

**Zeilen:** 143, 296, 304, 352, 472, 513, 515, 544, 638-639

#### Option A: Admin-Funktionen deaktivieren (schnell)

```csharp
// Alle Admin-Step-Edit Funktionen auskommentieren oder returnen:
public async Task<IActionResult> PreparationSteps()
{
    return RedirectToAction("Index");  // Deaktiviert
}

public IActionResult SavePreparationStep(RecipeStep step)
{
    throw new NotImplementedException("Use new Translation System");
}
```

#### Option B: Admin-Funktionen auf neues System umschreiben (aufwändig)

Alle Vorkommen von:
- `RecipePreparationSteps` → `RecipeStep`
- `RecipeJoinPreparationSteps` → entfernen
- Step-Loading über `RecipeSteps` Table

**Empfehlung:** Option A (deaktivieren) - Admin-Edit kann später nachgezogen werden.

---

## 🗄️ DATENBANK

**Nach erfolgreichem Build:**

```sql
-- Foreign Keys prüfen
SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS TableName,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN ('RecipeJoinPreparationSteps', 'RecipePreparationSteps')
    OR OBJECT_NAME(fk.referenced_object_id) IN ('RecipeJoinPreparationSteps', 'RecipePreparationSteps');

-- Foreign Keys droppen (falls vorhanden)
-- ALTER TABLE ... DROP CONSTRAINT [FK_NAME];

-- Tabellen löschen
DROP TABLE IF EXISTS RecipeJoinPreparationSteps;
DROP TABLE IF EXISTS RecipePreparationSteps;
```

---

## ⚡ QUICK-CHECK SCRIPT

**Nach jeder Änderung ausführen:**

```powershell
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch

# Suche nach verbleibenden Verwendungen (außer Migrations)
grep -r "RecipePreparationSteps\|RecipeJoinPreparationSteps" `
    --include="*.cs" `
    DelikatessenDrehbuch/ `
    --exclude-dir=bin `
    --exclude-dir=obj `
    --exclude-dir=Migrations

# Sollte NICHTS finden wenn alles fertig!
```

---

## ✅ DONE CHECKLIST

Beim Abhaken dieser Datei:

### Models:
- [ ] RecipePreparationSteps.cs - GELÖSCHT
- [ ] RecipeJoinPreparationSteps.cs - GELÖSCHT
- [ ] JoinIngredientPreparationStepViewModel.cs - GELÖSCHT
- [ ] EditRecipesModel.cs - UPDATED
- [ ] SaveNewRecipeModel.cs - UPDATED
- [ ] RecipeBaseData.cs - UPDATED
- [ ] JoinIngredientPreparationStep.cs - UPDATED

### Data:
- [ ] ApplicationDbContext.cs - UPDATED (DbSets + OnModelCreating)

### Controllers:
- [ ] RecipeController.cs - UPDATED (17 Stellen)
- [ ] RecipeSwapController.cs - UPDATED
- [ ] AdminController.cs - DEAKTIVIERT oder UPDATED

### Services:
- [ ] SaveNewRecipeService.cs - UPDATED
- [ ] RecipeAiTransformService.cs - UPDATED

### Database:
- [ ] RecipePreparationSteps Tabelle - GELÖSCHT
- [ ] RecipeJoinPreparationSteps Tabelle - GELÖSCHT

### Build:
- [ ] dotnet build - ERFOLG
- [ ] App startet - OK
- [ ] Test-Upload - OK

---

**Status:** Bereit für Cleanup! 🚀
