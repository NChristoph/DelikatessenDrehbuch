# 🗑️ Old System Cleanup - Action Plan

## ⚠️ WICHTIG: Nur ausführen wenn du bereit bist!

---

## 📊 Zusammenfassung

**Gefunden:**
- 3 Model-Dateien zum LÖSCHEN
- 6 Model-Dateien zum UPDATEN
- 3 Controller zum UPDATEN
- 2 Services zum UPDATEN
- 1 DbContext zum UPDATEN
- 10+ Migration-Dateien (IGNORIEREN - sind historisch)

**Geschätzte Zeit:** 30-60 Minuten
**Risiko:** MITTEL (alles nur Testdaten)

---

## 📝 SCHRITT-FÜR-SCHRITT PLAN

### ✅ PHASE 1: BACKUP (5 Min)

```powershell
# 1. Git Commit (falls noch nicht gemacht)
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch
git status
git add .
git commit -m "Before cleanup of old RecipePreparationSteps system"

# 2. DB Backup
# SQL Server Management Studio:
# - Rechtsklick auf DB → Tasks → Backup
# - Oder verwende das Backup-SQL unten
```

**SQL Backup:**
```sql
-- Optional: Backup der alten Tabellen (falls Rollback nötig)
SELECT * INTO RecipePreparationSteps_BACKUP
FROM RecipePreparationSteps;

SELECT * INTO RecipeJoinPreparationSteps_BACKUP
FROM RecipeJoinPreparationSteps;
```

---

### ❌ PHASE 2: MODELS LÖSCHEN (2 Min)

**Diese 3 Dateien komplett löschen:**

```powershell
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Models

# Löschen
Remove-Item RecipePreparationSteps.cs
Remove-Item RecipeJoinPreparationSteps.cs
Remove-Item JoinIngredientPreparationStepViewModel.cs
```

**Was gelöscht wird:**
1. `Models/RecipePreparationSteps.cs` (Zeile 1-22)
2. `Models/RecipeJoinPreparationSteps.cs` (Zeile 1-13)
3. `Models/JoinIngredientPreparationStepViewModel.cs` (Zeile 1-9)

---

### 🔧 PHASE 3: MODELS UPDATEN (10 Min)

#### 1. **EditRecipesModel.cs** (Zeile 15-16)

**VORHER:**
```csharp
public List<RecipePreparationSteps> RecipePreparationSteps { get; set; }
public List<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
```

**NACHHER:**
```csharp
public List<RecipeStep> RecipeSteps { get; set; }  // Neues System
```

---

#### 2. **SaveNewRecipeModel.cs** (Zeile 8)

**VORHER:**
```csharp
public List<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
```

**NACHHER:**
```csharp
public List<RecipeStep> RecipeSteps { get; set; }  // Neues System
```

---

#### 3. **RecipeBaseData.cs** (Zeile 30)

**VORHER:**
```csharp
public virtual ICollection<RecipeJoinPreparationSteps> Steps { get; set; }
```

**NACHHER:**
```csharp
public virtual ICollection<RecipeStep> Steps { get; set; }  // Neues System
```

---

#### 4. **JoinIngredientPreparationStep.cs** (Zeile 8)

**VORHER:**
```csharp
public RecipePreparationSteps Preparation { get; set; }
```

**NACHHER:**
```csharp
public RecipeStep Preparation { get; set; }  // Neues System
```

---

### 🗄️ PHASE 4: DBCONTEXT UPDATEN (5 Min)

**Datei:** `Data/ApplicationDbContext.cs`

#### 1. DbSets entfernen (Zeile 36, 39)

**LÖSCHEN:**
```csharp
public DbSet<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
public DbSet<RecipePreparationSteps> RecipePreparationSteps { get; set; }
```

#### 2. Table-Mapping entfernen (Zeile 329, 331-334)

**LÖSCHEN:**
```csharp
builder.Entity<RecipePreparationSteps>().ToTable("RecipePreperationSteps");

builder.Entity<RecipeJoinPreparationSteps>(e =>
{
    // ... alle Konfigurationen
});
```

---

### 🎮 PHASE 5: CONTROLLERS UPDATEN (20 Min)

#### 1. **RecipeController.cs** (17 Änderungen!)

**Betroffene Zeilen:** 104, 263, 317, 330, 340, 493-566, 842-872, 1000

**Große Änderungen:**
- `ExtractCreatePostingStepsFromRequest()` → Umschreiben auf neues System
- `UpsertStep()` → Umschreiben auf `RecipeStep`
- Step-Delete Logic → Umschreiben

**STRATEGIE:**
1. Suche alle Vorkommen von `RecipePreparationSteps`
2. Ersetze durch `RecipeStep`
3. Passe Join-Logik an (kein Join mehr nötig - direkt RecipeStep)
4. Ändere Sprachabfrage: Statt `step.Step_DE` → `RecipeStepTranslation` abfragen

**Beispiel - UpsertStep VORHER:**
```csharp
var step = new RecipePreparationSteps
{
    Step_DE = de,
    Step_EN = en,
    Step_ESP = esp,
    Step_PRT = prt
};
await _context.RecipePreparationSteps.AddAsync(step);
```

**NACHHER:**
```csharp
var step = new RecipeStep
{
    RecipeId = recipeId,
    StepOrder = stepIndex,
    StepText = de,  // Source-Sprache
    SourceLanguage = "de"
};
await _context.RecipeSteps.AddAsync(step);
await _context.SaveChangesAsync();

// Übersetzungen hinzufügen (optional, oder über Translation Service)
await _translationService.TranslateCoreLanguagesAsync(recipeId);
```

---

#### 2. **RecipeSwapController.cs** (Zeile 1173)

**VORHER:**
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

**NACHHER:**
```csharp
private async Task<string> GetStepText(RecipeStep step, string language)
{
    if (step.SourceLanguage == language)
        return step.StepText;

    var translation = await _context.RecipeStepTranslations
        .FirstOrDefaultAsync(t => t.StepId == step.Id && t.Language == language);

    return translation?.TranslatedText ?? step.StepText;
}
```

---

#### 3. **AdminController.cs** (Zeile 143, 296, 304, 352, 472, 513, 515, 544, 638-639)

**Betroffene Methoden:**
- `Index()` → Step-Statistiken
- `PreparationSteps()` → Admin-Liste
- `SavePreparationStep()` → LÖSCHEN oder umschreiben
- `EditRecipe()` → Step-Loading

**STRATEGIE:**
- Ersetze alle `RecipePreparationSteps` → `RecipeStep`
- Admin-Edit kann vorerst deaktiviert werden (nur neue Steps über Upload)
- Statistiken anpassen

---

### ⚙️ PHASE 6: SERVICES UPDATEN (15 Min)

#### 1. **SaveNewRecipeService.cs** (Zeile 203, 212, 340-403)

**Große Änderung:** Komplette Step-Saving Logik umschreiben

**VORHER:**
```csharp
foreach (var step in model.RecipeJoinPreparationSteps ?? Enumerable.Empty<RecipeJoinPreparationSteps>())
{
    var resolvedStep = await ResolvePreparationStepAsync(step);
    step.PreparationStep = resolvedStep;
    _context.RecipeJoinPreparationSteps.Add(step);
}
```

**NACHHER:**
```csharp
// Steps direkt speichern
foreach (var step in model.RecipeSteps ?? Enumerable.Empty<RecipeStep>())
{
    step.RecipeId = recipe.Id;
    _context.RecipeSteps.Add(step);
}
await _context.SaveChangesAsync();

// Background Translation starten
await _translationService.TranslateCoreLanguagesAsync(recipe.Id);
```

---

#### 2. **RecipeAiTransformService.cs** (Zeile 6146)

**Gleiche Änderung wie RecipeSwapController:**

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

### 🗄️ PHASE 7: DATENBANK CLEANUP (2 Min)

**Nach erfolgreichem Build & Test:**

```sql
-- 1. Foreign Keys entfernen
ALTER TABLE RecipeJoinPreparationSteps
DROP CONSTRAINT IF EXISTS FK_RecipeJoinPreparationSteps_RecipePreparationSteps;

ALTER TABLE RecipeJoinPreparationSteps
DROP CONSTRAINT IF EXISTS FK_RecipeJoinPreparationSteps_RecipeBaseData;

-- 2. Tabellen löschen
DROP TABLE IF EXISTS RecipeJoinPreparationSteps;
DROP TABLE IF EXISTS RecipePreparationSteps;

PRINT '✅ Alte Tabellen gelöscht!';
```

---

### ✅ PHASE 8: BUILD & TEST (5 Min)

```powershell
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch

# 1. Clean
dotnet clean

# 2. Build
dotnet build

# Wenn Fehler → Siehe unten "Häufige Build-Fehler"
# Wenn OK → Weiter zu Test
```

**Test-Checklist:**
- [ ] App startet ohne Fehler
- [ ] Test-Rezept erstellen (Upload)
- [ ] Steps werden angezeigt
- [ ] Translation läuft
- [ ] Kein DB-Fehler

---

## 🚨 HÄUFIGE BUILD-FEHLER

### Fehler: "RecipePreparationSteps not found"

**Lösung:** Du hast noch eine Referenz übersehen.

```powershell
# Finde verbleibende Verwendungen:
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch
grep -r "RecipePreparationSteps" --include="*.cs" DelikatessenDrehbuch/ --exclude-dir=bin --exclude-dir=obj --exclude-dir=Migrations
```

---

### Fehler: "Cannot resolve IRecipeStepTranslationService"

**Lösung:** Service nicht registriert.

```csharp
// Program.cs - prüfe ob vorhanden:
builder.Services.AddScoped<IRecipeStepTranslationService, RecipeStepTranslationService>();
```

---

### Fehler: "Foreign Key Constraint"

**Lösung:** DB-Tabellen noch nicht gelöscht.

```sql
-- Tabellen manuell droppen:
DROP TABLE RecipeJoinPreparationSteps;
DROP TABLE RecipePreparationSteps;
```

---

## 📊 VERBLEIBENDE DATEIEN

**IGNORE (Migration Files - historisch):**
- `Migrations/20260321232233_AddSmartRecipeSteps.Designer.cs`
- `Migrations/20260411150052_AddIngredientTags.Designer.cs`
- `Migrations/20260413105351_AddIsPowderToIngredients.Designer.cs`
- `Migrations/20260417071725_AddWorldUserBookmark.Designer.cs`
- `Migrations/20260418070001_AddRecipeAiVariants.Designer.cs`
- `Migrations/20260418081259_AddWorldSharedShoppingList.Designer.cs`
- `Migrations/20260419135642_AddRecipeAiVariantSelections.Designer.cs`
- `Migrations/20260420212742_AddIngredientMeasureQuantityLinkToAiVariantRows.Designer.cs`
- `Migrations/20260421083237_AddRecipeAiBaseRecipesV2.Designer.cs`
- `Migrations/20260424211246_AddWorldUserCommentIsPinned.Designer.cs`
- `Migrations/ApplicationDbContextModelSnapshot.cs`

**Diese NICHT anfassen!** EF Core Migrations sind historisch.

---

## 🔄 ROLLBACK PLAN

Falls etwas schiefgeht:

```powershell
# 1. Git Rollback
git reset --hard HEAD~1

# 2. DB Restore
# SQL Server Management Studio:
# Tasks → Restore Database → Select Backup File
```

---

## ✅ FINAL CHECKLIST

Nach Cleanup sollte folgendes NICHT mehr existieren:

- [ ] ❌ Model: RecipePreparationSteps.cs
- [ ] ❌ Model: RecipeJoinPreparationSteps.cs
- [ ] ❌ Model: JoinIngredientPreparationStepViewModel.cs
- [ ] ❌ DbContext: RecipePreparationSteps DbSet
- [ ] ❌ DbContext: RecipeJoinPreparationSteps DbSet
- [ ] ❌ DB-Tabelle: RecipePreparationSteps
- [ ] ❌ DB-Tabelle: RecipeJoinPreparationSteps

Folgendes sollte funktionieren:

- [ ] ✅ Build erfolgreich (keine Errors)
- [ ] ✅ App startet
- [ ] ✅ Rezept-Upload funktioniert
- [ ] ✅ Steps werden angezeigt
- [ ] ✅ Translation System läuft

---

## 💡 EMPFEHLUNG

**Vorgehen:**

1. **Heute:** Phase 1-3 (Backup + Models löschen + Models updaten)
2. **Build testen** → Fehler sammeln
3. **Morgen:** Phase 4-6 (DbContext + Controllers + Services)
4. **Build testen** → Fehler fixen
5. **Übermorgen:** Phase 7-8 (DB Cleanup + Final Test)

**Grund:** Schrittweise vorgehen = weniger Fehlersuche

**Alternativ (für Mutige):** Alles auf einmal (1 Stunde)

---

## 📞 HILFE BENÖTIGT?

Wenn du stecken bleibst:

1. **Build-Fehler?** → Schau in "Häufige Build-Fehler"
2. **DB-Fehler?** → Prüfe ob Tabellen noch existieren
3. **Sonstiges?** → Schick mir den Error-Log

**Viel Erfolg! 🚀**
