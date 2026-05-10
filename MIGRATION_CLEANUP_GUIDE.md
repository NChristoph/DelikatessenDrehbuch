# 🗑️ Migration & Cleanup Guide

## ⚠️ WICHTIG: Zwei parallele Systeme!

### ALTES SYSTEM (bestehend):
```
RecipePreparationSteps        -- Nur 4 Sprachen als Spalten
RecipeJoinPreparationSteps    -- Join-Tabelle
```

### NEUES SYSTEM (implementiert):
```
RecipeStep                    -- Flexibel, beliebig Sprachen
RecipeStepTranslation         -- Separate Übersetzungen
RecipeTranslationStatus       -- Status-Tracking
```

---

## 🎯 EMPFOHLENE STRATEGIE

### PHASE 1: PARALLEL BETREIBEN (jetzt)

**Aktueller Zustand:**
- ✅ Altes System: Bleibt für bestehende Rezepte
- ✅ Neues System: Für Test-Rezepte

**Vorteil:**
- Kein Risiko
- Alles funktioniert weiter
- Neues System kann getestet werden

**Nachteile:**
- Doppelte Code-Pflege
- Doppelte Tabellen

**Dauer:** 1-2 Wochen Testing

---

### PHASE 2: MIGRATION (nach erfolgreichem Test)

**Schritt 1: Backup erstellen**
```sql
SELECT * INTO RecipePreparationSteps_BACKUP
FROM RecipePreparationSteps;

SELECT * INTO RecipeJoinPreparationSteps_BACKUP
FROM RecipeJoinPreparationSteps;
```

**Schritt 2: Migration ausführen**
```sql
-- Siehe: Migrations/MigrateOldStepsToNewSystem.sql
-- Migriert alle bestehenden Steps → neues System
```

**Schritt 3: Vergleich & Validierung**
```sql
-- Prüfe ob gleich viele Steps
SELECT
    (SELECT COUNT(*) FROM RecipePreparationSteps) as OldCount,
    (SELECT COUNT(*) FROM RecipeStep) as NewCount;

-- Sollten GLEICH sein!
```

**Schritt 4: Test in Production**
- 1 Woche parallel laufen lassen
- Beide Systeme funktionieren
- Prüfen ob neue Steps korrekt angezeigt werden

---

### PHASE 3: CLEANUP (nach 1 Woche erfolgreicher Production)

**Was kann entfernt werden:**

#### 1. Datenbank-Tabellen (❌ LÖSCHEN):
```sql
-- WARNUNG: Nur nach erfolgreichem Test!
DROP TABLE RecipeJoinPreparationSteps;
DROP TABLE RecipePreparationSteps;
```

#### 2. C# Models (❌ LÖSCHEN):
```
DelikatessenDrehbuch/Models/
├── RecipePreparationSteps.cs              ❌ Kann raus
└── RecipeJoinPreparationSteps.cs          ❌ Kann raus
```

#### 3. DbContext (🔧 UPDATE):
```csharp
// Entfernen aus ApplicationDbContext.cs:
// public DbSet<RecipePreparationSteps> RecipePreparationSteps { get; set; }
// public DbSet<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
```

#### 4. Services die altes System nutzen (🔧 UPDATE):
```bash
# Suche nach Verwendung:
grep -r "RecipePreparationSteps" --include="*.cs" DelikatessenDrehbuch/
```

**Gefundene Dateien müssen updated werden:**
- EditRecipesModel.cs
- JoinIngredientPreparationStepViewModel.cs
- Alle Services die Steps laden

---

## 📊 VERGLEICH: Alt vs. Neu

| Feature | Altes System | Neues System |
|---------|-------------|--------------|
| **Sprachen** | 4 fest (DE/EN/ESP/PRT) | Unbegrenzt |
| **Neue Sprache** | Schema-Änderung nötig | Einfach hinzufügen |
| **NULL-Werte** | Viele (ungenutzte Spalten) | Keine |
| **On-Demand** | ❌ Nicht möglich | ✅ Ja |
| **Tracking** | ❌ Nein | ✅ Ja (Provider, Datum) |
| **Flexibilität** | ⭐⭐ | ⭐⭐⭐⭐⭐ |

---

## ⚡ SCHNELL-MIGRATION (für mutige)

Wenn du sofort migrieren willst:

```sql
-- 1. Backup
SELECT * INTO RecipePreparationSteps_BACKUP FROM RecipePreparationSteps;

-- 2. Migration (siehe MigrateOldStepsToNewSystem.sql)
-- Uncomment die Migration in der Datei

-- 3. Test
-- Öffne 5 zufällige Rezepte und prüfe ob Steps korrekt

-- 4. Wenn alles OK → Alte Tabellen löschen
-- DROP TABLE RecipeJoinPreparationSteps;
-- DROP TABLE RecipePreparationSteps;
```

---

## 🔒 SAFETY CHECKS

### Vor dem Löschen prüfen:

```sql
-- 1. Gleiche Anzahl Steps?
SELECT
    (SELECT COUNT(*) FROM RecipePreparationSteps) as OldCount,
    (SELECT COUNT(*) FROM RecipeStep) as NewCount;
-- Sollten GLEICH sein!

-- 2. Alle Rezepte migriert?
SELECT
    R.Id,
    R.Title,
    (SELECT COUNT(*) FROM RecipeJoinPreparationSteps RJ WHERE RJ.Recipe_Id = R.Id) as OldSteps,
    (SELECT COUNT(*) FROM RecipeStep RS WHERE RS.RecipeId = R.Id) as NewSteps
FROM RecipeBaseData R
WHERE EXISTS(SELECT 1 FROM RecipeJoinPreparationSteps RJ WHERE RJ.Recipe_Id = R.Id)
    AND NOT EXISTS(SELECT 1 FROM RecipeStep RS WHERE RS.RecipeId = R.Id);
-- Sollte LEER sein!

-- 3. Übersetzungen vorhanden?
SELECT
    RecipeId,
    COUNT(DISTINCT Language) as LanguageCount
FROM RecipeStepTranslation
GROUP BY RecipeId
HAVING COUNT(DISTINCT Language) < 3;
-- Sollten mindestens 3 Sprachen haben!
```

---

## 📋 CHECKLISTE

### Vor Cleanup:
- [ ] Migration ausgeführt
- [ ] Backup erstellt
- [ ] Gleiche Anzahl Steps (alt = neu)
- [ ] 5 Rezepte manuell getestet
- [ ] 1 Woche in Production gelaufen
- [ ] Keine Fehler in Logs

### Cleanup:
- [ ] Alte DB-Tabellen löschen
- [ ] Alte Models löschen
- [ ] DbContext updaten
- [ ] Services updaten
- [ ] Tests durchführen
- [ ] Deployment

---

## 🚨 ROLLBACK PLAN

Falls etwas schiefgeht:

```sql
-- 1. Restore Backup
DROP TABLE RecipePreparationSteps;
DROP TABLE RecipeJoinPreparationSteps;

SELECT * INTO RecipePreparationSteps
FROM RecipePreparationSteps_BACKUP;

SELECT * INTO RecipeJoinPreparationSteps
FROM RecipeJoinPreparationSteps_BACKUP;

-- 2. Alte Models zurück
-- git revert ...

-- 3. Deployment Rollback
```

---

## 💡 EMPFEHLUNG

### Für Production:
**PARALLEL BETREIBEN für 2 Wochen**

1. ✅ Neues System für neue Uploads
2. ✅ Altes System für bestehende Rezepte
3. ✅ Migration nach 2 Wochen
4. ✅ Cleanup nach weiteren 2 Wochen

**Vorteil:**
- Kein Risiko
- Genug Test-Zeit
- Rollback jederzeit möglich

---

## 📞 SUPPORT

**Fragen vor Cleanup:**
1. Sind ALLE Rezepte migriert?
2. Funktioniert neues System 100%?
3. Backup erstellt?
4. Rollback-Plan bereit?

**Wenn JA → Cleanup ist safe! ✅**
