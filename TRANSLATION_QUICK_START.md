# ⚡ Translation System - Quick Start

## 🚀 In 5 Minuten loslegen

### Step 1: Datenbank Migration (2 Min)

```sql
-- Öffne SQL Server Management Studio
-- Führe aus: Migrations/AddRecipeTranslationTables.sql
-- Ergebnis: 3 neue Tabellen erstellt ✅
```

### Step 2: API Key eintragen (1 Min)

```json
// appsettings.Development.json
{
  "OpenAI_API_Key": "sk-proj-DEIN_KEY_HIER"
}
```

**Wo bekomme ich den Key?**
→ https://platform.openai.com/api-keys

### Step 3: App starten (1 Min)

```powershell
cd source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch
dotnet run
```

### Step 4: Test-Rezept erstellen (30 Sek)

**Im Browser öffnen:**
```
http://localhost:5000/api/translation-test/create-test-recipe
```

**Ergebnis kopieren:**
```json
{
  "recipeId": 123  // <-- Diese ID merken!
}
```

### Step 5: Übersetzung starten (30 Sek)

**Im Browser öffnen (recipeId einsetzen):**
```
http://localhost:5000/api/translation-test/translate/123
```

**Warte 60 Sekunden...**

### Step 6: Resultat prüfen (30 Sek)

**Im Browser öffnen:**
```
http://localhost:5000/api/translation-test/steps/123/en
```

**Erwartetes Ergebnis:**
```json
{
  "success": true,
  "steps": [
    {
      "stepNumber": 1,
      "text": "Preheat ♨️ oven to 180°C top/bottom heat."
    },
    ...
  ]
}
```

## ✅ FERTIG!

**System funktioniert wenn:**
- ✅ Englische Steps angezeigt werden
- ✅ Emojis (♨️, 🍲) vorhanden sind
- ✅ Grammatik korrekt ist

---

## 🧪 Weitere Tests

### Spanisch:
```
http://localhost:5000/api/translation-test/steps/123/esp
```

### Portugiesisch:
```
http://localhost:5000/api/translation-test/steps/123/prt
```

### On-Demand (Indonesisch):
```
http://localhost:5000/api/translation-test/translate-ondemand/123/id
```

---

## 📖 Vollständige Dokumentation

→ Siehe: `TRANSLATION_SYSTEM_TEST.md`

---

## 💡 Tipps

1. **API Key vergessen?**
   → Prüfe Console: "OpenAI_API_Key not configured"

2. **Translation dauert lange?**
   → Normal! 11 Steps in 3 Sprachen = ~60 Sekunden

3. **Kosten pro Test?**
   → ~0,3 Cent (sehr günstig!)

---

## 🎉 Next Steps

Nach erfolgreichem Test:
1. Integration in Upload-Flow
2. UI für Sprach-Anzeige
3. Production Deployment
