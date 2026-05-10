# 🗑️ Automated Cleanup Script
# Hilft beim Entfernen des alten RecipePreparationSteps Systems

param(
    [switch]$DryRun = $false,    # Zeigt nur was gemacht würde
    [switch]$DeleteModels = $false,
    [switch]$CheckReferences = $false,
    [switch]$Backup = $false
)

$ErrorActionPreference = "Stop"
$basePath = "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch"

Write-Host "🗑️ Old System Cleanup Script" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

# ============================================
# FUNKTION: GIT BACKUP
# ============================================
function Create-GitBackup {
    Write-Host "`n📦 GIT BACKUP" -ForegroundColor Yellow
    Write-Host "----------------------------`n" -ForegroundColor Yellow

    Set-Location "C:\Users\Free8\source\repos\DelikatessenDrehbuch"

    # Status prüfen
    $status = git status --porcelain
    if ($status) {
        Write-Host "⚠️  Uncommitted changes found!" -ForegroundColor Yellow
        Write-Host "`nCreating backup commit..." -ForegroundColor White

        if (-not $DryRun) {
            git add .
            git commit -m "[BACKUP] Before cleanup of old RecipePreparationSteps system"
            Write-Host "✅ Backup commit created!" -ForegroundColor Green
        } else {
            Write-Host "[DRY RUN] Would create backup commit" -ForegroundColor Gray
        }
    } else {
        Write-Host "✅ No uncommitted changes - git is clean" -ForegroundColor Green
    }
}

# ============================================
# FUNKTION: MODELS LÖSCHEN
# ============================================
function Remove-OldModels {
    Write-Host "`n❌ DELETE OLD MODELS" -ForegroundColor Yellow
    Write-Host "----------------------------`n" -ForegroundColor Yellow

    $modelsToDelete = @(
        "$basePath\Models\RecipePreparationSteps.cs",
        "$basePath\Models\RecipeJoinPreparationSteps.cs",
        "$basePath\Models\JoinIngredientPreparationStepViewModel.cs"
    )

    foreach ($file in $modelsToDelete) {
        if (Test-Path $file) {
            Write-Host "🔍 Found: $file" -ForegroundColor White

            if (-not $DryRun) {
                Remove-Item $file -Force
                Write-Host "   ✅ DELETED`n" -ForegroundColor Red
            } else {
                Write-Host "   [DRY RUN] Would delete this file`n" -ForegroundColor Gray
            }
        } else {
            Write-Host "⚠️  Not found: $file" -ForegroundColor Yellow
            Write-Host "   (Already deleted?)`n" -ForegroundColor Gray
        }
    }

    if (-not $DryRun) {
        Write-Host "✅ All old models deleted!`n" -ForegroundColor Green
    } else {
        Write-Host "✅ Dry run complete - use -DeleteModels without -DryRun to actually delete`n" -ForegroundColor Green
    }
}

# ============================================
# FUNKTION: REFERENZEN SUCHEN
# ============================================
function Check-RemainingReferences {
    Write-Host "`n🔍 CHECK REMAINING REFERENCES" -ForegroundColor Yellow
    Write-Host "----------------------------`n" -ForegroundColor Yellow

    Set-Location "C:\Users\Free8\source\repos\DelikatessenDrehbuch"

    Write-Host "Searching for 'RecipePreparationSteps' and 'RecipeJoinPreparationSteps'..." -ForegroundColor White
    Write-Host "(Excluding bin, obj, Migrations folders)`n" -ForegroundColor Gray

    # PowerShell-basierte Suche (funktioniert auch wenn grep nicht verfügbar)
    $found = $false
    $patterns = @("RecipePreparationSteps", "RecipeJoinPreparationSteps")

    $files = Get-ChildItem -Path "DelikatessenDrehbuch" -Recurse -Include "*.cs" -Exclude "*.Designer.cs" |
        Where-Object { $_.FullName -notmatch "\\bin\\" -and $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\Migrations\\" }

    $results = @()

    foreach ($file in $files) {
        $lineNumber = 0
        foreach ($line in Get-Content $file.FullName) {
            $lineNumber++
            foreach ($pattern in $patterns) {
                if ($line -match $pattern) {
                    $results += [PSCustomObject]@{
                        File = $file.FullName.Replace("C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\", "")
                        Line = $lineNumber
                        Pattern = $pattern
                        Text = $line.Trim()
                    }
                    $found = $true
                }
            }
        }
    }

    if ($found) {
        Write-Host "⚠️  FOUND REFERENCES - These files need updating:`n" -ForegroundColor Red

        # Gruppiere nach Datei
        $grouped = $results | Group-Object File

        foreach ($group in $grouped) {
            Write-Host "📄 $($group.Name)" -ForegroundColor Cyan
            foreach ($result in $group.Group) {
                Write-Host "   Line $($result.Line): $($result.Text)" -ForegroundColor White
            }
            Write-Host ""
        }

        Write-Host "📊 Summary:" -ForegroundColor Yellow
        Write-Host "   - $($grouped.Count) files need updating" -ForegroundColor White
        Write-Host "   - $($results.Count) references found`n" -ForegroundColor White

        Write-Host "👉 See CLEANUP_FILE_REFERENCE.md for detailed instructions`n" -ForegroundColor Green
    } else {
        Write-Host "✅ NO REFERENCES FOUND!" -ForegroundColor Green
        Write-Host "   Old system completely removed!`n" -ForegroundColor Green
    }
}

# ============================================
# FUNKTION: SQL SCRIPT GENERIEREN
# ============================================
function Generate-SqlCleanup {
    Write-Host "`n📜 SQL CLEANUP SCRIPT" -ForegroundColor Yellow
    Write-Host "----------------------------`n" -ForegroundColor Yellow

    $sqlScript = @"
-- 🗑️ Database Cleanup Script
-- Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
-- WARNING: This will delete old tables!

-- 1. Prüfe ob Tabellen existieren
IF OBJECT_ID('RecipeJoinPreparationSteps', 'U') IS NOT NULL
    PRINT '✅ RecipeJoinPreparationSteps exists';
ELSE
    PRINT '⚠️  RecipeJoinPreparationSteps does not exist';

IF OBJECT_ID('RecipePreparationSteps', 'U') IS NOT NULL
    PRINT '✅ RecipePreparationSteps exists';
ELSE
    PRINT '⚠️  RecipePreparationSteps does not exist';

GO

-- 2. Foreign Keys entfernen
DECLARE @sql NVARCHAR(MAX) = '';

SELECT @sql += 'ALTER TABLE ' + OBJECT_NAME(fk.parent_object_id) +
               ' DROP CONSTRAINT ' + fk.name + ';' + CHAR(13)
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN ('RecipeJoinPreparationSteps', 'RecipePreparationSteps')
   OR OBJECT_NAME(fk.referenced_object_id) IN ('RecipeJoinPreparationSteps', 'RecipePreparationSteps');

IF LEN(@sql) > 0
BEGIN
    PRINT 'Dropping foreign keys...';
    EXEC sp_executesql @sql;
    PRINT '✅ Foreign keys dropped';
END
ELSE
    PRINT '⚠️  No foreign keys found';

GO

-- 3. Tabellen löschen
DROP TABLE IF EXISTS RecipeJoinPreparationSteps;
PRINT '✅ RecipeJoinPreparationSteps dropped';

DROP TABLE IF EXISTS RecipePreparationSteps;
PRINT '✅ RecipePreparationSteps dropped';

GO

PRINT '';
PRINT '🎉 DATABASE CLEANUP COMPLETE!';
"@

    $outputPath = "C:\Users\Free8\source\repos\DelikatessenDrehbuch\cleanup-database.sql"

    if (-not $DryRun) {
        $sqlScript | Out-File -FilePath $outputPath -Encoding UTF8
        Write-Host "✅ SQL script generated: cleanup-database.sql`n" -ForegroundColor Green
        Write-Host "👉 Execute this script in SQL Server Management Studio after successful build!`n" -ForegroundColor Yellow
    } else {
        Write-Host "[DRY RUN] Would generate: cleanup-database.sql`n" -ForegroundColor Gray
    }
}

# ============================================
# HAUPTPROGRAMM
# ============================================

if (-not $DeleteModels -and -not $CheckReferences -and -not $Backup) {
    Write-Host "❌ No action specified!`n" -ForegroundColor Red
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\cleanup-old-system.ps1 -Backup              # Create git backup" -ForegroundColor White
    Write-Host "  .\cleanup-old-system.ps1 -DeleteModels        # Delete old model files" -ForegroundColor White
    Write-Host "  .\cleanup-old-system.ps1 -CheckReferences     # Find remaining references" -ForegroundColor White
    Write-Host "  .\cleanup-old-system.ps1 -DryRun -DeleteModels  # Preview what would be deleted`n" -ForegroundColor White
    Write-Host "Example full workflow:" -ForegroundColor Cyan
    Write-Host "  .\cleanup-old-system.ps1 -Backup" -ForegroundColor Gray
    Write-Host "  .\cleanup-old-system.ps1 -DryRun -DeleteModels" -ForegroundColor Gray
    Write-Host "  .\cleanup-old-system.ps1 -DeleteModels" -ForegroundColor Gray
    Write-Host "  .\cleanup-old-system.ps1 -CheckReferences`n" -ForegroundColor Gray
    exit 1
}

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No changes will be made`n" -ForegroundColor Magenta
}

# Aktionen ausführen
if ($Backup) {
    Create-GitBackup
}

if ($DeleteModels) {
    Remove-OldModels
    Generate-SqlCleanup
}

if ($CheckReferences) {
    Check-RemainingReferences
}

Write-Host "`n✅ Script complete!`n" -ForegroundColor Green
