# 🧪 Recipe Translation System - Test Guide

## 📋 Setup

### 1. Datenbank Migration ausführen

```powershell
# In SQL Server Management Studio oder Azure Data Studio
# Öffne und führe aus:
DelikatessenDrehbuch/Migrations/AddRecipeTranslationTables.sql
```

**Was wird erstellt:**
- ✅ Tabelle `RecipeStep` (Steps in Source-Sprache)
- ✅ Tabelle `RecipeStepTranslation` (Übersetzungen)
- ✅ Tabelle `RecipeTranslationStatus` (Sprach-Status)

### 2. OpenAI API Key konfigurieren

```json
// In appsettings.json oder appsettings.Development.json
{
  "OpenAI_API_Key": "sk-proj-DEIN_API_KEY_HIER"
}
```

**Oder als Environment Variable:**
```powershell
# Windows PowerShell
$env:OpenAI_API_Key = "sk-proj-DEIN_KEY"

# Linux/Mac
export OpenAI_API_Key="sk-proj-DEIN_KEY"
```

### 3. App starten

```powershell
cd source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch
dotnet run
```

---

## 🚀 TEST WORKFLOW

### TEST 1: Test-Rezept erstellen

**Endpoint:**
```
GET http://localhost:5000/api/translation-test/create-test-recipe
```

**Was passiert:**
- Erstellt ein Test-Rezept "Schweinebraten"
- Fügt 11 Steps hinzu (mit Emojis ♨️ und 🍲)
- Source Language: Deutsch

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "recipeId": 123,
  "message": "Test-Rezept erstellt mit 11 Schritten",
  "steps": 11
}
```

**⚠️ Notiere dir die `recipeId` für die nächsten Tests!**

---

### TEST 2: Übersetzung starten (Background)

**Endpoint:**
```
POST http://localhost:5000/api/translation-test/translate/{recipeId}
```

**Beispiel:**
```
POST http://localhost:5000/api/translation-test/translate/123
```

**Was passiert:**
- Startet Background Job
- Übersetzt in 3 Sprachen: EN, ESP, PRT
- Mit GPT-4o (bessere Qualität)
- Dauert ca. 30-60 Sekunden

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "message": "Übersetzung gestartet (läuft im Hintergrund)",
  "recipeId": 123,
  "info": "Warte ca. 30-60 Sekunden..."
}
```

---

### TEST 3: Status prüfen (nach 60 Sekunden)

**Endpoint:**
```
GET http://localhost:5000/api/translation-test/status/{recipeId}
```

**Beispiel:**
```
GET http://localhost:5000/api/translation-test/status/123
```

**Was passiert:**
- Prüft welche Sprachen fertig sind
- Zeigt Translation Count

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "recipeId": 123,
  "availableLanguages": ["DE", "EN", "ESP", "PRT"],
  "lastUpdated": "2026-05-10T10:30:00Z",
  "translationDetails": [
    { "language": "en", "count": 11 },
    { "language": "esp", "count": 11 },
    { "language": "prt", "count": 11 }
  ],
  "coreLanguagesReady": true
}
```

✅ **Wenn `coreLanguagesReady: true` → System funktioniert!**

---

### TEST 4: Steps in Deutsch abrufen

**Endpoint:**
```
GET http://localhost:5000/api/translation-test/steps/{recipeId}/de
```

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "recipeId": 123,
  "language": "de",
  "stepCount": 11,
  "steps": [
    {
      "stepNumber": 1,
      "text": "Heize ♨️ Backofen auf 180°C Ober-/Unterhitze vor."
    },
    {
      "stepNumber": 2,
      "text": "Schneide die Schweineschulter rautenförmig ein."
    },
    ...
  ]
}
```

✅ **Original Deutsch sollte perfekt sein (mit Emojis!)**

---

### TEST 5: Steps in Englisch abrufen

**Endpoint:**
```
GET http://localhost:5000/api/translation-test/steps/{recipeId}/en
```

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "recipeId": 123,
  "language": "en",
  "stepCount": 11,
  "steps": [
    {
      "stepNumber": 1,
      "text": "Preheat ♨️ oven to 180°C top/bottom heat."
    },
    {
      "stepNumber": 2,
      "text": "Score the pork shoulder in a diamond pattern."
    },
    ...
  ]
}
```

✅ **Prüfe:**
- ✅ Emojis vorhanden (♨️, 🍲)
- ✅ Grammatik korrekt (Imperative)
- ✅ Kochterminologie (Score, Diamond pattern)
- ✅ Temperaturen (180°C bleibt)

---

### TEST 6: Steps in Spanisch abrufen

**Endpoint:**
```
GET http://localhost:5000/api/translation-test/steps/{recipeId}/esp
```

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "language": "esp",
  "steps": [
    {
      "stepNumber": 1,
      "text": "Precalienta ♨️ el horno a 180°C calor superior/inferior."
    },
    ...
  ]
}
```

---

### TEST 7: Steps in Portugiesisch abrufen

**Endpoint:**
```
GET http://localhost:5000/api/translation-test/steps/{recipeId}/prt
```

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "language": "prt",
  "steps": [
    {
      "stepNumber": 1,
      "text": "Preaqueça ♨️ o forno a 180°C calor superior/inferior."
    },
    ...
  ]
}
```

---

### TEST 8: On-Demand Übersetzung (Indonesisch)

**Endpoint:**
```
POST http://localhost:5000/api/translation-test/translate-ondemand/{recipeId}/id
```

**Was passiert:**
- Übersetzt on-the-fly mit GPT-4o-mini (günstig!)
- Speichert in DB (cache)
- Nächster Abruf: instant aus DB

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "recipeId": 123,
  "language": "id",
  "stepCount": 11,
  "steps": [
    {
      "stepNumber": 1,
      "text": "Panaskan ♨️ oven hingga 180°C panas atas/bawah."
    },
    ...
  ],
  "message": "On-Demand Übersetzung abgeschlossen"
}
```

---

## 📊 LOGS PRÜFEN

### Console Output (während Translation)

```
🌍 Translating recipe 123 from de to 4 core languages
  → Translating to EN...
  ✅ EN: 11 steps saved
  → Translating to ESP...
  ✅ ESP: 11 steps saved
  → Translating to PRT...
  ✅ PRT: 11 steps saved
🎉 Recipe 123 now available in 4 languages
```

### Fehlersuche

**Problem: "OpenAI_API_Key not configured"**
```
❌ Lösung: API Key in appsettings.json eintragen
```

**Problem: Translation returned empty**
```
❌ Mögliche Ursachen:
- GPT API Key ungültig
- Rate Limit erreicht
- GPT Response konnte nicht geparst werden

✅ Prüfe Logs für Details
```

**Problem: Recipe has no steps**
```
❌ Lösung: Zuerst Test 1 ausführen (create-test-recipe)
```

---

## 🎯 ERFOLGS-KRITERIEN

### ✅ System funktioniert wenn:

1. **Test 1:** Rezept mit 11 Steps erstellt ✅
2. **Test 2:** Background Job gestartet ✅
3. **Test 3:** Nach 60 Sek: 4 Sprachen verfügbar (DE, EN, ESP, PRT) ✅
4. **Test 4:** Deutsche Steps korrekt (mit Emojis) ✅
5. **Test 5-7:** EN/ESP/PRT Steps grammatisch korrekt ✅
6. **Test 8:** On-Demand ID funktioniert ✅

### 📊 Qualitäts-Checks:

- ✅ **Emojis beibehalten** (♨️, 🍲)
- ✅ **Grammatik korrekt** (Imperative Form)
- ✅ **Kochterminologie** ("preheat", "score", "sear")
- ✅ **Zahlen/Einheiten** (180°C, 100-120 Minuten)
- ✅ **Artikel bei Zutaten** ("the onions", "las cebollas")

---

## 📈 PERFORMANCE TESTS

### Test: 100 Rezepte mit je 10 Steps

```powershell
# Erstelle 100 Test-Rezepte
for ($i=1; $i -le 100; $i++) {
    Invoke-RestMethod -Uri "http://localhost:5000/api/translation-test/create-test-recipe"
}

# Übersetze alle (Background)
# Gesamtzeit: ca. 50-60 Minuten
# Kosten: ca. 30 Cent (GPT-4o)
```

---

## 🔧 TROUBLESHOOTING

### Datenbank-Tabellen prüfen

```sql
-- Prüfe ob Tabellen existieren
SELECT * FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('RecipeStep', 'RecipeStepTranslation', 'RecipeTranslationStatus');

-- Prüfe Übersetzungen
SELECT
    r.Id,
    r.Title,
    COUNT(DISTINCT t.Language) as LanguageCount
FROM RecipeBaseData r
LEFT JOIN RecipeStepTranslation t ON r.Id = t.RecipeId
GROUP BY r.Id, r.Title
ORDER BY r.Id DESC;
```

### Service Status prüfen

```powershell
# Prüfe ob Service registriert ist
# Im Browser: https://localhost:5001/api/translation-test/list
# Sollte Test-Rezepte auflisten
```

---

## 📞 SUPPORT

**Bei Problemen:**
1. Prüfe Logs in Console
2. Prüfe DB-Tabellen
3. Prüfe OpenAI API Key
4. Prüfe OpenAI API Limits (Rate Limiting)

**Kosten pro Test-Durchlauf:**
- 1 Rezept (11 Steps, 3 Sprachen): ~0,3 Cent
- 100 Rezepte: ~30 Cent

---

## ✅ NÄCHSTE SCHRITTE

Nach erfolgreichen Tests:
1. ✅ Integration in normalen Upload-Flow
2. ✅ UI für Sprach-Auswahl
3. ✅ Monitoring Dashboard
4. ✅ Production Deployment
