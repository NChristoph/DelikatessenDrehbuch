# AI-Rezept-Transformation & Zutaten-Vorschläge

## Überblick

Das AI-System der WorldMiniApp bietet zwei Hauptfunktionen:

1. **Rezept-Transformation** — Bestehende Rezepte in Varianten umwandeln (Vegan, Low Carb, High Protein, Meal Prep)
2. **Zutaten-Vorschläge** — AI-gestützte Empfehlung passender Zutaten aus der eigenen Datenbank

Beide Funktionen nutzen die **OpenAI Responses API** (`gpt-5-mini`) mit strukturiertem JSON-Output und besitzen einen vollständigen **Fallback-Modus** für Entwicklung ohne API-Key.

---

## Architektur

```
ShowRecipe.cshtml (UI)
    │
    ├── POST /PreviewAiRecipeVariant ──→ HomeController ──→ RecipeAiTransformService.BuildPreviewAsync()
    │                                                          ├── OpenAI API (Produktion)
    │                                                          └── BuildFallbackPreview() (Entwicklung)
    │
    ├── POST /SuggestAiVariantIngredients ──→ HomeController ──→ RecipeAiTransformService.BuildIngredientSuggestionsAsync()
    │                                                              ├── Phase 1: SelectIngredientCategoriesAsync() → OpenAI
    │                                                              ├── Phase 2: IngredientResolverService.GetCandidatesForGoalAsync() → DB
    │                                                              ├── Phase 3: SelectConcreteIngredientsAsync() → OpenAI
    │                                                              └── BuildIngredientSuggestionFallbackAsync() (Entwicklung)
    │
    └── POST /SaveAiRecipeVariant ──→ HomeController ──→ RecipeAiVariant Entity → DB
```

### Beteiligte Dateien

| Datei | Zweck |
|-------|-------|
| `Services/RecipeAiTransformService.cs` | Kernlogik: OpenAI-Calls, Fallback, Template-Rendering |
| `Services/Interfaces/IRecipeAiTransformService.cs` | Interface mit 2 Public-Methoden |
| `Services/IngredientResolverService.cs` | DB-basierte Zutaten-Auflösung und Scoring |
| `Services/Interfaces/IIngredientResolverService.cs` | Interface für Zutaten-Resolution |
| `Controllers/HomeController.cs` | 3 API-Endpoints (Preview, Suggest, Save) |
| `Models/RecipeAiTransformPreview.cs` | Preview-Response + Unter-Modelle |
| `Models/RecipeAiTransformRequest.cs` | Request für Rezept-Transformation |
| `Models/RecipeAiIngredientSuggestionRequest.cs` | Request für Zutaten-Vorschläge |
| `Models/RecipeAiIngredientSuggestionResponse.cs` | Response mit Zutaten-Vorschlägen |
| `Models/RecipeAiVariant.cs` | DB-Entity für persistierte AI-Varianten |
| `Models/SaveAiRecipeVariantRequest.cs` | Request zum Speichern einer Variante |
| `Models/IngredientResolutionCandidate.cs` | DTO für Zutaten-Kandidaten |
| `Models/FoodCategory.cs` | Lebensmittel-Kategorien (10 Sprachen) |
| `Views/Home/ShowRecipe.cshtml` | UI: AI-Fächer, Preview-Anzeige, Reset-Button |

---

## 1. Rezept-Transformation (`BuildPreviewAsync`)

### Ablauf

```
Request (RecipeId, VariantType, UserNote?, AppliedChangeCount)
    │
    ├── 1. Validierung (VariantType, Sprache, max. 2 Änderungsrunden)
    ├── 2. Source-StepPlan bauen (SmartSteps → MasterStep-Templates)
    ├── 3. API-Key prüfen
    │     ├── Vorhanden → OpenAI-Call mit strukturiertem Schema
    │     └── Fehlt + Dev → BuildFallbackPreview()
    ├── 4. Antwort parsen → RecipeAiTransformPreview
    └── 5. NormalizePreview() — Validierung + Step-Rendering
```

### Varianten-Typen

| VariantType | Label (DE) | Beschreibung |
|-------------|-----------|--------------|
| `vegan` | Vegan | Tierische Zutaten ersetzen |
| `lowcarb` | Low Carb | Stärkereiche Zutaten reduzieren |
| `highprotein` | Mehr Protein | Proteinquellen verstärken |
| `mealprep` | Meal Prep | Vorbereitung & Aufwärmen optimieren |

### OpenAI-Integration

- **Endpoint:** `https://api.openai.com/v1/responses`
- **Modell:** `gpt-5-mini`
- **Format:** Structured JSON Output (`json_schema`, `strict: true`)
- **API-Key:** Umgebungsvariable `SecretKeyOpenAi` oder `appsettings.json`

#### Request an OpenAI

```
System-Prompt:
  "Du transformierst Rezepte für eine Food-App.
   Du darfst KEINE freien Schritttexte schreiben.
   Du darfst nur masterStepKeys aus der erlaubten Liste zurückgeben."

User-Prompt:
  - Sprache, AI-Variante, bisherige Änderungsrunden
  - Rezepttitel, Portionen, Zubereitungszeit
  - Zutaten (JSON)
  - Erlaubte masterStepKeys (nur aus dem Originalrezept)
  - Aktueller StepPlan (JSON)
  - User-Hinweis (falls vorhanden)
```

#### Response-Schema (`recipe_ai_transform_preview`)

```json
{
  "variantType": "string",
  "variantLabel": "string",
  "title": "string",
  "summary": "string",
  "usedFallback": false,
  "userNoteApplied": false,
  "remainingChanges": 1,
  "ingredients": [
    { "name": "string", "quantity": "string", "changeHint": "string|null", "isModified": true }
  ],
  "stepPlan": [
    { "masterStepKey": "COOK_FRY_01", "variables": [{ "key": "ingredient", "value": "Tofu" }] }
  ],
  "highlights": ["string"]
}
```

### Änderungsrunden-System

- **Maximal 2 Änderungsrunden** pro Rezept (`MaxChangeRounds = 2`)
- `appliedChangeCount` trackt bisherige Änderungen
- `userNote` erlaubt textuelle Anpassungswünsche (z.B. "statt Reis lieber Kartoffeln")
- `remainingChanges` zeigt verbleibende Runden an
- Nach 2 Runden: Fehler "Maximal zwei AI-Änderungen sind erlaubt."

### Fallback-Modus (Entwicklung)

Wenn kein API-Key konfiguriert ist und `IsDevelopment() == true`:

| Variante | Fallback-Aktion |
|----------|----------------|
| `vegan` | Fleisch→Tofu, Milch→Hafer-Cuisine, Käse→vegane Alternative, Ei→Ei-Ersatz, Honig→Ahornsirup |
| `lowcarb` | Reis→Blumenkohlreis, Nudeln→Zucchini-Nudeln, Kartoffeln→Ofengemüse, Brot→Salatblätter |
| `highprotein` | Ergänzt "Skyr oder Extra-Protein" als neue Zutat |
| `mealprep` | Nur Highlights und Summary, keine Zutatentausche |

Die Ersetzung funktioniert via `ReplaceIngredient()` mit mehrsprachigen Suchbegriffen (DE/EN/ES/PT) und ersetzt **alle** Treffer (nicht nur den ersten).

### Master-Step-Template-Rendering

Die AI gibt `stepPlan`-Items mit `masterStepKey` + `variables` zurück. Der Service rendert diese über Templates aus `wwwroot/data/master_steps.json`.

**Template-Syntax:**
- `{{ variableName }}` — Variable einsetzen
- `(optionaler Text mit {{ var }})` — wird entfernt wenn `var` leer
- `[optionaler Text mit {{ var }}]` — wird entfernt wenn `var` leer
- `{~Fallback-Text~var1|var2~}` — wird angezeigt wenn alle genannten Variablen leer sind

**Rendering-Schritte:**
1. Optional-Sections auswerten (Variablen vorhanden?)
2. Fallback-Tokens ersetzen
3. Template-Variablen einsetzen
4. Whitespace/Interpunktion normalisieren

**Sprach-Lookup:** Templates existieren pro Sprache-Key (`de`, `en`, `esp`, `prt`, `id`, `nl`, `sv`, `da`, `no`, `ms`). Fallback: DE.

### NormalizePreview

Nach dem OpenAI-Call (oder Fallback) wird die Preview normalisiert:

- Fehlende Felder mit Defaults füllen
- StepPlan validieren: nur erlaubte `masterStepKeys` behalten
- Variables mergen: Source-Variablen als Basis, AI-Variablen überschreiben
- Steps rendern aus validiertem StepPlan
- `RemainingChanges` korrekt berechnen

---

## 2. Zutaten-Vorschläge (`BuildIngredientSuggestionsAsync`)

### 2-Phasen-Architektur

```
Request (RecipeId, VariantType, UserNote?)
    │
    ├── Phase 1: AI wählt Lebensmittel-Kategorien
    │     └── SelectIngredientCategoriesAsync() → OpenAI
    │           Ergebnis: preferredCategoryKeys + strategy + reasoning
    │
    ├── Zwischen-Schritt: DB-Kandidaten laden
    │     └── IngredientResolverService.GetCandidatesForGoalAsync()
    │           - Filtert nach preferredCategoryKeys
    │           - Exkludiert bereits vorhandene Zutaten
    │           - Scored nach Varianten-Ziel (Makro-Gewichtung)
    │           - Nimmt max. 18 Kandidaten
    │
    └── Phase 2: AI wählt 3 beste Zutaten aus Kandidaten
          └── SelectConcreteIngredientsAsync() → OpenAI
                Ergebnis: 3 selectedIngredientIds + reasons
```

### Phase 1: Kategorie-Auswahl

**OpenAI-Call mit Schema `recipe_ai_ingredient_category_plan`:**

```json
{
  "strategy": "add",           // "add" oder "replace"
  "reasoning": "string",       // Begründung der AI
  "preferredCategoryKeys": ["legumes", "tofu", "vegetables"]
}
```

Die AI wählt max. 3 Kategorien aus der erlaubten Liste. Falls keine gültige Auswahl → Fallback auf Default-Kategorien.

**Default-Kategorien pro Variante:**

| Variante | Default-Kategorien |
|----------|-------------------|
| `highprotein` | legumes, tofu, tempeh, yogurt, eggs, fish, poultry, cheese |
| `lowcarb` | vegetables, leafy_greens, mushrooms, fish, poultry, eggs, tofu |
| `vegan` | legumes, tofu, tempeh, vegetables, leafy_greens, mushrooms, nuts, seeds |
| `mealprep` | legumes, rice, vegetables, poultry, fish, tofu, potato |

### Zwischen-Schritt: DB-Kandidaten (`IngredientResolverService`)

Der Service lädt Zutaten aus `IngredientsAndNutrients` mit:
- **Kategorie-Filter:** Nur Zutaten deren `FoodCategory.CategoryKey` in den gewählten Kategorien liegt
- **Ausschluss:** Bereits im Rezept vorhandene Zutaten (`excludeIngredientIds`)
- **Goal-Scoring:** Makro-basierte Bewertung pro Variante

#### Scoring-Formeln

| Goal | Formel |
|------|--------|
| `highprotein` | `protein*4 + fiber*0.8 - carbs*0.35 - fat*0.1` |
| `lowcarb` | `100 - carbs*2.5 + protein*1.2 + fiber*0.6` |
| `vegan` | `protein*2 + fiber*1 - fat*0.1` |
| `mealprep` | `protein*1.5 + fiber*1 - (is_soft?2:0) - (is_liquid?1:0)` |

### Phase 2: Konkrete Zutatenwahl

**OpenAI-Call mit Schema `recipe_ai_ingredient_pick`:**

```json
{
  "selectedIngredientIds": [42, 117, 203],
  "reasons": ["passt gut zum Reisgericht", "proteinreich", "saisonales Gemüse"]
}
```

Die AI bekommt die kompakten Kandidaten (id, name, category, macros, score) und wählt die 3 besten.

**Sicherheitsnetz:** Falls die AI weniger als 3 gültige IDs zurückgibt, werden Kandidaten nach Score aufgefüllt.

### Fallback-Modus (Entwicklung)

`BuildIngredientSuggestionFallbackAsync()`:
- Nutzt direkt `IngredientResolverService.GetCandidatesForGoalAsync()` mit Default-Kategorien
- Nimmt die Top-3 nach Score
- Markiert `UsedFallback = true`
- Kein OpenAI-Call nötig

### Response-Modell

```json
{
  "variantType": "highprotein",
  "variantLabel": "Mehr Protein",
  "strategy": "add",
  "reasoning": "Drei passende Zutaten aus deiner Datenbank wurden ausgewählt.",
  "usedFallback": false,
  "preferredCategoryKeys": ["legumes", "tofu"],
  "preferredCategoryLabels": ["Hülsenfrüchte", "Tofu"],
  "suggestions": [
    {
      "ingredientId": 42,
      "name": "Rote Linsen",
      "categoryKey": "legumes",
      "categoryLabel": "Hülsenfrüchte",
      "proteinPer100g": 25.4,
      "carbsPer100g": 51.1,
      "fatPer100g": 1.4,
      "fiberPer100g": 10.7,
      "caloriesPer100g": 329,
      "score": 87.3,
      "aiReason": "passt gut zum Rezeptstil"
    }
  ]
}
```

---

## 3. Caching & Persistierung

### RecipeAiVariant Entity

AI-Varianten werden in der Tabelle `RecipeAiVariants` gespeichert:

| Feld | Typ | Beschreibung |
|------|-----|-------------|
| `Id` | int | PK |
| `BaseRecipeId` | int | FK → RecipeBaseData |
| `ParentVariantId` | int? | FK → RecipeAiVariant (für Ketten) |
| `VariantType` | string(64) | vegan/lowcarb/highprotein/mealprep |
| `Language` | string(12) | de/en/es/pt/id/nl/sv/da/no/ms |
| `CreatedByUserHash` | string(256)? | Ersteller |
| `AppliedChangeCount` | int | Anzahl Änderungsrunden |
| `LatestUserNote` | string(1000)? | Letzter User-Hinweis |
| `IsSharedCanonical` | bool | Gemeinsamer Cache für alle User |
| `StepPlanJson` | nvarchar(max) | Serialisierter StepPlan |
| `RenderedStepsJson` | nvarchar(max) | Gerenderte Schritt-Texte |
| `RenderedIngredientsJson` | nvarchar(max) | Zutatenliste mit Hints |
| `RenderedHighlightsJson` | nvarchar(max) | Highlight-Stichpunkte |
| `RenderedTitle` | string(256) | Transformierter Titel |
| `RenderedSummary` | string(4000) | Zusammenfassung |
| `CreatedAtUtc` | DateTime | Erstellzeitpunkt |
| `UpdatedAtUtc` | DateTime | Letzte Aktualisierung |

**DB-Indizes:**
```
IX_RecipeAiVariants_BaseRecipeId_VariantType_Language_IsSharedCanonical
IX_RecipeAiVariants_BaseRecipeId_CreatedByUserHash_VariantType_Language
```

### Cache-Strategie (`TryGetCachedAiVariantAsync`)

```
Beim Preview-Request:
  ├── appliedChangeCount > 0 → KEIN Cache (individuelle Änderung)
  ├── userNote vorhanden → KEIN Cache (individuelle Änderung)
  └── Erstaufruf (count=0, kein Note):
       ├── 1. User-spezifische Variante suchen (CreatedByUserHash = userHash, !IsSharedCanonical)
       └── 2. Gemeinsame kanonische Variante suchen (IsSharedCanonical = true)
```

### Save-Logik (`SaveAiRecipeVariant`)

- **Kanonisch** (`isSharedCanonical = true`): Wird gespeichert wenn `appliedChangeCount == 0` UND kein `userNote`. Existierende kanonische Variante wird aktualisiert (Upsert).
- **Individuell** (`isSharedCanonical = false`): Bei jeder Änderungsrunde wird eine neue Entity erstellt.

---

## 4. FoodCategory-System

### Entity

```csharp
public sealed class FoodCategory
{
    public int Id { get; set; }
    public string CategoryKey { get; set; }     // z.B. "legumes", "tofu", "vegetables"
    public string? Name_DE { get; set; }         // Hülsenfrüchte
    public string? Name_EN { get; set; }         // Legumes
    // ... (10 Sprachen: DE, EN, PRT, ESP, ID, NL, SE, DK, NO, MS)
    public ICollection<IngredientsAndNutrients> Ingredients { get; set; }
}
```

**DB-Tabelle:** `food_categories` (mit `Category_Key` Column-Mapping)
**Unique-Index:** `CategoryKey`

### Beziehung zu Zutaten

`IngredientsAndNutrients` hat ein optionales FK `FoodCategoryId` → `FoodCategory`:
- `ON DELETE SET NULL`
- Constraint: `FK_Ingredients_FoodCategories`

---

## 5. IngredientResolverService

### Interface

```csharp
public interface IIngredientResolverService
{
    // Einzelne Zutat per Name auflösen (Fuzzy-Matching)
    Task<IngredientResolutionCandidate?> ResolveByNameAsync(
        string rawName, string? language, IEnumerable<string>? allowedCategoryKeys, CancellationToken ct);

    // Mehrere Kandidaten für ein Ziel laden (Scoring)
    Task<List<IngredientResolutionCandidate>> GetCandidatesForGoalAsync(
        string goal, string? language, IEnumerable<string>? allowedCategoryKeys,
        IEnumerable<int>? excludeIngredientIds, int take, CancellationToken ct);
}
```

### Name-Matching (`ResolveByNameAsync`)

3-stufiges Matching über alle 10 Sprach-Namen:
1. **Exakt** (Score 0) — `name == query`
2. **Prefix** (Score 1) — `name.StartsWith(query)`
3. **Contains** (Score 2) — `name.Contains(query)`

**Text-Normalisierung:**
- Lowercase
- Umlaute: ä→ae, ö→oe, ü→ue, ß→ss
- Diakritische Zeichen entfernen (é→e, ñ→n, etc.)
- Non-Word-Zeichen → Leerzeichen
- Multi-Whitespace komprimieren

### IngredientResolutionCandidate

```csharp
public sealed class IngredientResolutionCandidate
{
    public int IngredientId { get; set; }
    public string Name { get; set; }
    public string CategoryKey { get; set; }
    public string CategoryLabel { get; set; }      // Lokalisierter Kategorie-Name
    public decimal ProteinPer100g { get; set; }
    public decimal CarbsPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public decimal FiberPer100g { get; set; }
    public decimal CaloriesPer100g { get; set; }
    public decimal Score { get; set; }              // Goal-basierter Score
    public string MatchType { get; set; }           // "exact"/"prefix"/"contains"/"highprotein"/etc.
}
```

---

## 6. Controller-Endpoints

### `POST /WorldMiniApp/Home/PreviewAiRecipeVariant`

**Request:** `RecipeAiTransformRequest`
```json
{ "recipeId": 123, "variantType": "vegan", "userNote": null, "appliedChangeCount": 0 }
```

**Response:** `RecipeAiTransformPreview` (JSON)

**Ablauf:**
1. Rezept mit vollen Details laden (`IncludeFullRecipeDetails()`)
2. Sprache aus Cookie `deli-lang` lesen
3. Cache prüfen (`TryGetCachedAiVariantAsync`)
4. Falls kein Cache: `BuildPreviewAsync()` aufrufen
5. Preview als JSON zurückgeben

### `POST /WorldMiniApp/Home/SuggestAiVariantIngredients`

**Request:** `RecipeAiIngredientSuggestionRequest`
```json
{ "recipeId": 123, "variantType": "highprotein", "userNote": null }
```

**Response:** `RecipeAiIngredientSuggestionResponse` (JSON)

**Ablauf:**
1. Rezept laden
2. `BuildIngredientSuggestionsAsync()` aufrufen (2-Phasen-Flow)
3. Suggestions als JSON zurückgeben

### `POST /WorldMiniApp/Home/SaveAiRecipeVariant`

**Request:** `SaveAiRecipeVariantRequest`
```json
{
  "baseRecipeId": 123,
  "latestUserNote": "mehr Gemüse",
  "preview": { /* RecipeAiTransformPreview */ }
}
```

**Response:**
```json
{ "success": true, "variantId": 42, "isSharedCanonical": true }
```

**Ablauf:**
1. Prüfen ob Basisrezept existiert
2. Kanonisch? → Existierende Entity aktualisieren oder neue erstellen
3. Individuell? → Neue Entity erstellen
4. Alle Preview-Daten als JSON serialisieren
5. Speichern + ID zurückgeben

---

## 7. Mehrsprachigkeit

### Unterstützte Sprachen (10)

| Code | Sprache | CultureInfo |
|------|---------|-------------|
| `de` | Deutsch | de-DE |
| `en` | English | en-US |
| `es` | Español | es-ES |
| `pt` | Português | pt-PT |
| `id` | Bahasa Indonesia | id-ID |
| `nl` | Nederlands | nl-NL |
| `sv` | Svenska | sv-SE |
| `da` | Dansk | da-DK |
| `no` | Norsk | nb-NO |
| `ms` | Bahasa Melayu | ms-MY |

### Sprach-Normalisierung

Eingabe-Aliase werden normalisiert:
- `esp` → `es`, `prt` → `pt`, `se` → `sv`, `dk` → `da`

### LocalizedWord-Pattern

```csharp
LocalizedWord(language, "DE-Text", "EN-Text", "ES-Text", "PT-Text")
```
- ID, NL, SV, DA, NO, MS → Fallback auf EN
- Wird durchgehend für Labels, Hints, Summaries verwendet

### Step-Texte

Legacy-Steps (`RecipePreparationSteps`) existieren nur in 4 Sprachen (DE/EN/ESP/PRT).
Smart-Steps aus `master_steps.json` haben Templates in allen 10 Sprachen.
Neue Sprachen (ID/NL/SV/DA/NO/MS) → Fallback auf EN bei Legacy-Steps.

---

## 8. Shared OpenAI Helper

```csharp
private async Task<T?> ExecuteStructuredOpenAiCallAsync<T>(
    object requestBody, string apiKey, CancellationToken cancellationToken)
```

Generischer Helper für beide Phasen der Zutaten-Vorschläge:
- Sendet Request an OpenAI Responses API
- Parst `output_text` aus der Response
- Deserialisiert in generischen Typ `T`
- Fehlerbehandlung mit `BuildApiErrorMessage()`

### TryExtractOutputText

Robustes Parsing der OpenAI-Response:
1. Prüft `root.output_text` (direktes Feld)
2. Falls nicht vorhanden: iteriert `root.output[].content[]` und sucht `type == "output_text"`

---

## 9. UI-Integration (ShowRecipe.cshtml)

### AI-Fächer

Die ShowRecipe-View zeigt einen aufklappbaren "AI-Fächer" mit 4 Varianten-Buttons:
- Vegan, Low Carb, Mehr Protein, Meal Prep

### Preview-Anzeige

Nach Klick auf eine Variante:
1. Loading-State auf dem Button
2. `POST /PreviewAiRecipeVariant` aufrufen
3. Titel, Zutaten und Schritte im DOM ersetzen
4. Badge "AI-Vorschau aktiv" einblenden
5. Geänderte Zutaten visuell hervorheben (ChangeHint)
6. Highlights anzeigen

### Reset-Button

Nach "Vorschau übernehmen" erscheint ein Reset-Button ("Original"):
- Stellt `originalRecipeState` wieder her (Titel, Zutaten-Markup, Schritte-Markup)
- Versteckt das "AI-Vorschau aktiv"-Badge
- Setzt `aiPreviewState.preview = null`

### JavaScript-State

```javascript
var originalRecipeState = {
    title: '...',
    ingredientMarkup: '...',
    stepMarkup: '...'
};

var aiPreviewState = {
    preview: null    // aktuell angezeigte Preview
};
```

---

## 10. Datenfluss-Diagramm

```
┌──────────────┐     POST /Preview        ┌────────────────┐
│ ShowRecipe.js │ ───────────────────────→ │ HomeController  │
│              │                           │                │
│  AI-Fächer   │     JSON Response         │  Cache-Check   │
│  klicken     │ ←─────────────────────── │  ↓ miss?       │
└──────────────┘                           │  BuildPreview  │
                                           └───────┬────────┘
                                                   │
                              ┌─────────────────────┤
                              │                     │
                    ┌─────────▼──────────┐  ┌──────▼──────────┐
                    │  OpenAI API Call   │  │  Fallback (Dev) │
                    │  gpt-5-mini       │  │  Regel-basiert  │
                    │  Strict JSON      │  │  ReplaceIngr.   │
                    └─────────┬──────────┘  └──────┬──────────┘
                              │                     │
                              └──────────┬──────────┘
                                         │
                              ┌──────────▼──────────┐
                              │  NormalizePreview   │
                              │  ValidateStepPlan  │
                              │  RenderMasterSteps │
                              └──────────┬──────────┘
                                         │
                              ┌──────────▼──────────┐
                              │  RecipeAiTransform  │
                              │  Preview (JSON)     │
                              └─────────────────────┘
```

```
┌──────────────┐   POST /Suggest          ┌────────────────┐
│ ShowRecipe.js │ ──────────────────────→ │ HomeController  │
│  Zutaten-    │                          │                │
│  Vorschlag   │   JSON Response          │  BuildIngr.    │
│  anfordern   │ ←────────────────────── │  Suggestions   │
└──────────────┘                          └───────┬────────┘
                                                  │
                    Phase 1                       │               Phase 2
          ┌─────────────────────┐        ┌───────▼────────┐     ┌──────────────────┐
          │ SelectIngredient    │        │ IngredientRes. │     │ SelectConcrete   │
          │ CategoriesAsync()  │───────→│ Service (DB)   │────→│ IngredientsAsync │
          │ → 3 CategoryKeys   │        │ → 18 Kandidat. │     │ → 3 Beste        │
          └─────────────────────┘        └────────────────┘     └──────────────────┘
```

---

## 11. Fehlerbehandlung

| Szenario | Verhalten |
|----------|----------|
| Kein API-Key + Production | `InvalidOperationException` → 400 Bad Request |
| Kein API-Key + Development | Fallback-Modus (lokale Regeln) |
| OpenAI HTTP-Fehler | Logging + `InvalidOperationException` mit Status-Code und Body-Auszug |
| Leere AI-Antwort | `InvalidOperationException("AI hat keine verwertbare Vorschau geliefert.")` |
| Ungültige StepKeys von AI | Werden gefiltert, nur erlaubte Keys bleiben |
| Weniger als 3 Zutaten von AI | Auffüllung aus DB-Kandidaten nach Score |
| Keine Kandidaten für Kategorien | Fallback auf Default-Kategorien, dann auf lokalen Fallback |
| Max. Änderungsrunden erreicht | 400 Bad Request ("Maximal zwei AI-Änderungen sind erlaubt.") |
