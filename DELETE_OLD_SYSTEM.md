# 🗑️ Altes System komplett entfernen

## ⚠️ NUR wenn keine wichtigen Daten vorhanden sind!

---

## 1️⃣ DATENBANK: Alte Tabellen löschen

```sql
-- WARNUNG: Löscht alle Steps in altem Format!
-- Nur ausführen wenn du sicher bist!

DROP TABLE IF EXISTS RecipeJoinPreparationSteps;
DROP TABLE IF EXISTS RecipePreparationSteps;

PRINT '✅ Alte Tabellen gelöscht!';
```

---

## 2️⃣ C# MODELS: Alte Dateien löschen

### Zu löschen:
```
DelikatessenDrehbuch/Models/
├── RecipePreparationSteps.cs                    ❌ LÖSCHEN
├── RecipeJoinPreparationSteps.cs                ❌ LÖSCHEN
└── JoinIngredientPreparationStepViewModel.cs    ❌ LÖSCHEN (nutzt altes System)
```

**PowerShell:**
```powershell
cd source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Models

# Löschen
Remove-Item RecipePreparationSteps.cs
Remove-Item RecipeJoinPreparationSteps.cs
Remove-Item JoinIngredientPreparationStepViewModel.cs
```

---

## 3️⃣ DBCONTEXT: Alte DbSets entfernen

**Datei:** `Data/ApplicationDbContext.cs`

### Zu löschen:
```csharp
// DIESE ZEILEN LÖSCHEN:
public DbSet<RecipePreparationSteps> RecipePreparationSteps { get; set; }
public DbSet<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
public DbSet<JoinIngredientPreparationStep> JoinIngredientPreparationStep { get; set; }
```

---

## 4️⃣ ANDERE DATEIEN: Prüfen & Updaten

### Dateien die altes System nutzen:

```bash
# Suche nach Verwendung:
grep -r "RecipePreparationSteps" --include="*.cs" DelikatessenDrehbuch/ --exclude-dir=bin --exclude-dir=obj
```

**Gefunden:**
- `Models/EditRecipesModel.cs` → UPDATE nötig
- Services die Steps laden → UPDATE nötig

---

## 5️⃣ CLEANUP SCRIPT (Automatisch)

```powershell
# ALLES AUF EINMAL LÖSCHEN
# WARNUNG: Nur ausführen wenn sicher!

$basePath = "source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch"

# 1. Models löschen
Write-Host "Lösche alte Models..." -ForegroundColor Yellow
Remove-Item "$basePath/Models/RecipePreparationSteps.cs" -ErrorAction SilentlyContinue
Remove-Item "$basePath/Models/RecipeJoinPreparationSteps.cs" -ErrorAction SilentlyContinue
Remove-Item "$basePath/Models/JoinIngredientPreparationStepViewModel.cs" -ErrorAction SilentlyContinue

Write-Host "✅ Models gelöscht!" -ForegroundColor Green

# 2. Suche nach verbleibenden Verwendungen
Write-Host "`nPrüfe auf verbleibende Verwendungen..." -ForegroundColor Yellow
$results = Select-String -Path "$basePath/**/*.cs" -Pattern "RecipePreparationSteps" -Exclude "*.Designer.cs" -ErrorAction SilentlyContinue

if ($results) {
    Write-Host "⚠️ Folgende Dateien nutzen noch das alte System:" -ForegroundColor Red
    $results | ForEach-Object { Write-Host "  - $($_.Path):$($_.LineNumber)" }
} else {
    Write-Host "✅ Keine verbleibenden Verwendungen gefunden!" -ForegroundColor Green
}
```

---

## 6️⃣ VERBLEIBENDE DATEIEN UPDATEN

### EditRecipesModel.cs

**VORHER:**
```csharp
public List<RecipePreparationSteps> RecipePreparationSteps { get; set; }
```

**NACHHER:**
```csharp
public List<RecipeStep> RecipeSteps { get; set; }  // Neues System
```

---

## 7️⃣ VERIFY: Alles entfernt?

```powershell
# Prüfe ob wirklich alles weg ist
cd source/repos/DelikatessenDrehbuch

# Suche nach "RecipePreparationSteps"
grep -r "RecipePreparationSteps" --include="*.cs" DelikatessenDrehbuch/ --exclude-dir=bin --exclude-dir=obj

# Sollte NICHTS finden!
# Wenn doch: Dateien manuell updaten
```

---

## 8️⃣ BUILD TEST

```powershell
cd source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch

# Build
dotnet build

# Wenn Fehler → Fehlende Referenzen updaten
# Wenn OK → ✅ Cleanup erfolgreich!
```

---

## ✅ CHECKLISTE

Nach Cleanup sollte gelöscht/updated sein:

- [ ] ❌ DB-Tabellen: RecipePreparationSteps
- [ ] ❌ DB-Tabellen: RecipeJoinPreparationSteps
- [ ] ❌ Model: RecipePreparationSteps.cs
- [ ] ❌ Model: RecipeJoinPreparationSteps.cs
- [ ] ❌ Model: JoinIngredientPreparationStepViewModel.cs
- [ ] ✅ DbContext: Alte DbSets entfernt
- [ ] ✅ Build erfolgreich
- [ ] ✅ App startet ohne Fehler

---

## 🎯 SCHNELL-CLEANUP

**Alles auf einmal:**

```sql
-- 1. DB
DROP TABLE RecipeJoinPreparationSteps;
DROP TABLE RecipePreparationSteps;
```

```powershell
# 2. Files
cd source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Models
Remove-Item RecipePreparationSteps.cs
Remove-Item RecipeJoinPreparationSteps.cs
Remove-Item JoinIngredientPreparationStepViewModel.cs

# 3. Build Test
cd ..
dotnet build
```

**Wenn Build OK → Fertig! ✅**
