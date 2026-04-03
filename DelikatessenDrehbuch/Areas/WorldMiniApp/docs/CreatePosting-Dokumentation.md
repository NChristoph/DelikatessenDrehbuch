# CreatePosting — Vollständige technische Dokumentation

> **WorldMiniApp** · Rezept-Erstellungs-Modul
> **Basispfad:** `DelikatessenDrehbuch/`
> **Stand:** 2026-04-02 · **BUGFIXES:** 11 Fixes (Multi-Ingredients, Artikel-Grammatik, Optionale Variablen)

---

## Inhaltsverzeichnis

1. [Architektur-Überblick](#1-architektur-überblick)
2. [Dateiverzeichnis](#2-dateiverzeichnis)
3. [Lade-Reihenfolge der Scripts](#3-lade-reihenfolge-der-scripts)
4. [Dependency-Graph](#4-dependency-graph)
5. [Controller & Server-Endpunkte](#5-controller--server-endpunkte)
6. [Views & HTML-Struktur](#6-views--html-struktur)
7. [CSS-Klassen & Themes](#7-css-klassen--themes)
8. [JSON-Datendateien](#8-json-datendateien)
9. [JS-Datei: create-posting-utils.js](#9-js-datei-create-posting-utilsjs)
10. [JS-Datei: create-posting-data-urls.js](#10-js-datei-create-posting-data-urlsjs)
11. [JS-Datei: create-posting-data-store.js](#11-js-datei-create-posting-data-storejs)
12. [JS-Datei: create-posting-feedback.js](#12-js-datei-create-posting-feedbackjs)
13. [JS-Datei: create-posting-page-data.js](#13-js-datei-create-posting-page-datajs)
14. [JS-Datei: create-posting-template-builder.js](#14-js-datei-create-posting-template-builderjs)
15. [JS-Datei: create-posting-step-filter-ui.js](#15-js-datei-create-posting-step-filter-uijs) **NEU**
16. [JS-Datei: master-step-renderer.js](#16-js-datei-master-step-rendererjs)
17. [JS-Datei: recipe-step-suggest.js](#17-js-datei-recipe-step-suggestjs)
18. [JS-Datei: create-posting-probability.js](#18-js-datei-create-posting-probabilityjs)
19. [JS-Datei: ingredientmanager.js](#19-js-datei-ingredientmanagerjs)
20. [JS-Datei: CreatePostingSmartStepCreator.js](#20-js-datei-createpostingsmartstepcreatjs)
21. [JS-Datei: CreatePostingPage.js](#21-js-datei-createpostingpagejs)
22. [JS-Datei: create-posting-publish-checklist.js](#22-js-datei-create-posting-publish-checklistjs)
23. [CSS-Datei: create-posting-step-filter.css](#23-css-datei-create-posting-step-filtercss) **NEU**
24. [Window-API-Übersicht](#24-window-api-übersicht)
25. [Event-Listener-Katalog](#25-event-listener-katalog)
26. [DOM-Element-Verzeichnis](#26-dom-element-verzeichnis)
27. [Datenfluss & User-Flow](#27-datenfluss--user-flow)
28. [Initialisierungs-Sequenz](#28-initialisierungs-sequenz)

---

## 1. Architektur-Überblick

```
┌──────────────────────────────────────────────────────────────┐
│                     CreatePosting.cshtml                      │
│  ┌─────────┐ ┌────────┐ ┌──────────┐ ┌──────┐ ┌──────────┐ │
│  │ Basics  │ │ Media  │ │Zutaten   │ │Steps │ │Keywords  │ │
│  │card-    │ │card-   │ │card-     │ │card- │ │card-     │ │
│  │basics   │ │media   │ │ingredien.│ │steps │ │keywords  │ │
│  └─────────┘ └────────┘ └──────────┘ └──┬───┘ └──────────┘ │
│                                          │                    │
│                    _SmartStepCreatorPartial.cshtml             │
└──────────────────────────────────────────────────────────────┘
         │                    │                    │
    ┌────▼────┐         ┌────▼────┐         ┌────▼────┐
    │ Create  │         │ Smart   │         │ Publish │
    │ Posting │         │ Step    │         │ Check-  │
    │ Page.js │         │Creator.js│        │ list.js │
    └────┬────┘         └────┬────┘         └─────────┘
         │                   │
    ┌────▼───────────────────▼────────────────────────────┐
    │              Infrastruktur-Layer                      │
    │  utils · data-urls · data-store · feedback           │
    │  page-data · template-builder · renderer             │
    │  recipe-step-suggest · probability · ingredientmgr   │
    └──────────────────────────┬───────────────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │  JSON Data Files    │
                    │  /wwwroot/data/*.json│
                    └──────────┬──────────┘
                               │
                    ┌──────────▼──────────┐
                    │ RecipeController.cs  │
                    │ UploadNewVideoAsync  │
                    │ UpdateRecipe         │
                    └─────────────────────┘
```

---

## 2. Dateiverzeichnis

### JavaScript-Dateien (14 Dateien, ~9.330 Zeilen)

| # | Datei | Pfad (unter `wwwroot/js/`) | Zeilen | Zweck |
|---|-------|---------------------------|--------|-------|
| 1 | `create-posting-utils.js` | `wwwroot/js/` | 74 | Kern-Utilities: escapeHtml, resolveLangKey, fetchJson, toast |
| 2 | `create-posting-data-urls.js` | `wwwroot/js/` | 11 | URL-Konfiguration aller JSON-Endpunkte |
| 3 | `create-posting-data-store.js` | `wwwroot/js/` | 98 | Daten-Cache mit Deduplizierung & Validierung |
| 4 | `create-posting-feedback.js` | `wwwroot/js/` | 41 | Toast-Nachrichten & Fehler-Reporting |
| 5 | `create-posting-page-data.js` | `wwwroot/js/` | 17 | Async-Loader für Article-Rules & Transforms |
| 6 | `create-posting-template-builder.js` | `wwwroot/js/` | 62 | Template-Card-UI-Builder (nur für #masterTemplateCards) |
| 7 | `create-posting-step-filter-ui.js` | `wwwroot/js/` | 86 | **NEU:** Smart Step Creator Filter-UI (Suchfeld + Phase-Tabs) |
| 8 | `master-step-renderer.js` | `wwwroot/js/` | 478 | Template-Rendering-Engine mit Variablen-Substitution |
| 9 | `recipe-step-suggest.js` | `wwwroot/js/` | 513 | Rezepttyp-Erkennung & Step-Vorschläge |
| 10 | `create-posting-probability.js` | `wwwroot/js/` | 378 | Wahrscheinlichkeits-Analyse & Template-Vorschläge |
| 11 | `ingredientmanager.js` | `wwwroot/js/` | 145 | Maßeinheiten-Verwaltung (mehrsprachig, 25 Units) |
| 12 | `CreatePostingSmartStepCreator.js` | `wwwroot/js/` | 3.897 | Haupt-State-Machine des Step-Creators + Filter-Logik |
| 13 | `CreatePostingPage.js` | `wwwroot/js/` | 5.590 | Haupt-Page-Controller |
| 14 | `create-posting-publish-checklist.js` | `wwwroot/js/` | 307 | Fortschritts-Checkliste (5-Schritte-Tracker) |

### Views (3 Dateien)

| Datei | Pfad (unter `Areas/WorldMiniApp/Views/Home/`) | Zweck |
|-------|-----------------------------------------------|-------|
| `CreatePosting.cshtml` | `Views/Home/` | Haupt-View: Rezept-Erstellung |
| `EditRecipe.cshtml` | `Views/Home/` | Rezept-Bearbeitung |
| `_SmartStepCreatorPartial.cshtml` | `Views/Home/` | Shared Partial: CSS + HTML für Step-Creator |

### Controller (1 Datei)

| Datei | Pfad | Zweck |
|-------|------|-------|
| `RecipeController.cs` | `Areas/WorldMiniApp/Controllers/` | Server-Endpunkte für Upload & Edit |

### JSON-Daten (9 Dateien)

| Datei | Pfad (unter `wwwroot/data/`) | Zweck |
|-------|------------------------------|-------|
| `master_steps.json` | `data/` | Master-Step-Templates mit Variablen |
| `master_step_variables.json` | `data/` | Variablen-Katalog (articles, fractions, etc.) |
| `master_step_option_rules.json` | `data/` | Regel-Engine für Variablen-Optionen |
| `recipe_step_mapping.json` | `data/` | Step-zu-Rezepttyp-Zuordnung |
| `recipe_type_step_variables.json` | `data/` | Variablen-Overrides nach Rezepttyp |
| `recipe_category_scoring.json` | `data/` | Scoring für Rezepttyp-Erkennung |
| `ingredient_article_rules.json` | `data/` | Grammatische Artikel-Zuordnung |
| `ingredient_transforms.json` | `data/` | Zutaten-Transformation (adjektivisch) |
| `probability_template_presets.json` | `data/` | Presets für Probability-Engine |

### CSS-Dateien (1 Datei)

| Datei | Pfad (unter `wwwroot/css/`) | Zeilen | Zweck |
|-------|----------------------------|--------|-------|
| `create-posting-step-filter.css` | `wwwroot/css/` | 247 | **NEU:** Styling für Smart Step Creator Filter UI (Suchfeld + Phase-Tabs) mit Theme-Support |

---

## 3. Lade-Reihenfolge der Scripts

Definiert in `CreatePosting.cshtml` (Zeilen 2679–2695):

```
1.  create-posting-utils.js          ← sofort (kein defer)
2.  create-posting-data-urls.js      ← sofort
3.  create-posting-data-store.js     ← sofort
4.  create-posting-feedback.js       ← sofort
5.  create-posting-page-data.js      ← sofort
6.  create-posting-template-builder.js ← sofort
7.  CreatePostingSmartStepCreator.js ← async/defer
8.  master-step-renderer.js         ← async/defer
9.  recipe-step-suggest.js          ← async/defer
10. create-posting-probability.js   ← async/defer
11. CreatePostingPage.js            ← async/defer
12. create-posting-publish-checklist.js ← async/defer
```

**Wichtig:** Die ersten 6 Dateien laden synchron und stellen die `window.*`-APIs bereit, BEVOR die async-Dateien starten.

---

## 4. Dependency-Graph

```
window.CreatePostingUtils          ← Basis für alle
    ↓
window.CreatePostingDataUrls       ← URL-Registry
    ↓
window.CreatePostingDataStore      ← Nutzt DataUrls + Utils
    ↓
window.CreatePostingFeedback       ← Nutzt Utils
    ↓
window.CreatePostingPageData       ← Nutzt DataStore
    ↓
window.CreatePostingTemplateBuilder
    ├── Nutzt: MasterStepRenderer
    ├── Nutzt: getThemeMutedTextClass()
    └── Nutzt: getCreatePostingTheme()
    ↓
window.MasterStepCreatorHelpers    (aus CreatePostingSmartStepCreator.js)
    ├── Nutzt: CreatePostingUtils
    ├── Nutzt: CreatePostingDataStore
    ├── Nutzt: CreatePostingIngredientHelpers
    └── Setzt: ingredientTransforms, stepCatalog
    ↓
window.MasterStepRenderer
    ├── Nutzt: CreatePostingDataStore
    └── Nutzt: CreatePostingUtils
    ↓
window.RecipeStepSuggest
    ├── Nutzt: MasterStepRenderer
    └── Nutzt: CreatePostingDataStore
    ↓
window.CreatePostingProbability
    ├── Nutzt: RecipeStepSuggest
    ├── Nutzt: MasterStepRenderer
    └── Nutzt: CreatePostingIngredientHelpers
    ↓
CreatePostingPage.js (Haupt-Einstiegspunkt)
    ├── Nutzt: ALLE oben genannten
    ├── Setzt: CreatePostingIngredientHelpers
    ├── Setzt: Theme-Funktionen (getCreatePostingTheme, etc.)
    └── Setzt: Globale Event-Listener
    ↓
window.IngredientManager           ← unabhängig, DOMContentLoaded
```

---

## 5. Controller & Server-Endpunkte

### RecipeController.cs

**Pfad:** `Areas/WorldMiniApp/Controllers/RecipeController.cs`
**Route:** `[Route("WorldMiniApp/Home/{action}")]`

#### Öffentliche Actions

| Action | HTTP | Zeile | Parameter | Rückgabe | Beschreibung |
|--------|------|-------|-----------|----------|-------------|
| `Upload` | GET | 40 | `userHash` | View | Lädt CreatePosting.cshtml mit Zutaten, Maßeinheiten, Keywords |
| `UploadNewVideoAsync` | POST | 55 | `WorldUserPosting posting, userHash` | IActionResult | Rezept speichern (Rate-Limit: 5/10min) |
| `EditRecipe` | GET | 129 | `postingId, userHash` | View | Lädt EditRecipe.cshtml mit vollem Rezept |
| `UpsertStep` | POST | 205 | `[FromBody] UpsertStepRequest, userHash` | JSON | Neuen Step anlegen → `{ id, reused }` |
| `UpdateRecipe` | POST | 257 | `EditPostingRecipeViewModel, userHash` | IActionResult | Rezept aktualisieren |

#### Private Helfer-Methoden

| Methode | Zeile | Rückgabe | Beschreibung |
|---------|-------|----------|-------------|
| `ExtractIngredientRowsFromRequest()` | 459 | `List<IngredientRow>` | Parst Zutatzeilen aus Form |
| `ExtractCreatePostingStepsFromRequest()` | 495 | `List<RecipeJoinPreparationSteps>` | Parst Zubereitungsschritte (DE/EN/ESP/PRT) |
| `ExtractCreatePostingSmartStepsFromRequest()` | 556 | `List<SmartStepReferenceInput>` | Parst Smart-Step-Referenzen mit Metadaten |
| `ExtractStepRowsFromRequest()` | 591 | `List<StepRow>` | Parst Step-Rows für EditRecipe |
| `ExtractKeywordIdsFromRequest()` | 617 | `List<int>` | Parst ausgewählte Keyword-IDs |

#### UploadNewVideoAsync — Ablauf (Zeile 55–127)

```
1. Rate-Limit prüfen (5 Uploads / 10 Minuten)
   └─ Bei Überschreitung: HTTP 429
2. Blob-Upload: _blobUpload.UploadContentToBlob(posting.Content)
3. Rezept anlegen: SaveNewRecipeModel mit Titel, Kategorie, Personen, Bild
4. Zutaten verarbeiten: posting.IngredientMeasureQuantity
5. Steps extrahieren: ExtractCreatePostingStepsFromRequest(Request.Form)
6. Smart Steps extrahieren: ExtractCreatePostingSmartStepsFromRequest(Request.Form)
7. Keywords verknüpfen: RecipeBaseKeyword-Einträge erstellen
8. Cookie setzen: createPostingDraftReset=1 (für Draft-Löschung)
9. Redirect → Home/Index
```

#### Form-Felder (Form → Server Binding)

**Zutaten:**
```
IngredientMeasureQuantity[i].IngredientId    → int
IngredientMeasureQuantity[i].MeasureId       → int
IngredientMeasureQuantity[i].Quantity         → decimal
```

**Steps:**
```
RecipePreperationSteps[i].PreperationStepId  → int
RecipePreperationSteps[i].StepIndex          → int
RecipePreperationSteps[i].RecipePreperationStep.Step_DE  → string
RecipePreperationSteps[i].RecipePreperationStep.Step_EN  → string
RecipePreperationSteps[i].RecipePreperationStep.Step_ESP → string
RecipePreperationSteps[i].RecipePreperationStep.Step_PRT → string
RecipePreperationSteps[i].RecipePreperationStep.Phase    → int
RecipePreperationSteps[i].RecipePreperationStep.Equipment → int
```

**Smart Steps:**
```
SmartStepReferences[i].MasterStepKey   → string
SmartStepReferences[i].MetadataJson    → string (JSON)
SmartStepReferences[i].StepIndex       → int
```

**Keywords:**
```
SelectedKeywordIds[i] → int
```

---

## 6. Views & HTML-Struktur

### CreatePosting.cshtml

**Pfad:** `Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml`
**Model:** `WorldUserPosting`

```html
<div class="creator-bg min-vh-100">
  <!-- Lade-Overlay -->
  <div id="datatableLoadingOverlay">...</div>

  <!-- Top-Bar -->
  <div class="creator-topbar">
    <!-- Theme-Switcher Buttons -->
    <!-- Fortschritts-Zähler -->
    <!-- Publish-Button -->
  </div>

  <!-- Haupt-Formular -->
  <form asp-action="UploadNewVideo" method="post" id="recipeForm"
        enctype="multipart/form-data" onsubmit="prepareBinding()">
    <input type="hidden" name="userHash" value="..." />

    <!-- ══════ SECTION 1: Basics (card-basics) ══════ -->
    <section class="creator-card" data-theme="gold" id="card-basics">
      <!-- Titel Input:        asp-for="Title" -->
      <!-- Kategorie Select:   asp-for="Recipe.Category" -->
      <!--   Options: Appetizer, Main, Dessert -->
      <!-- Präferenz Select:   asp-for="Recipe.Preferences" -->
      <!--   Options: Vegan, Vegetarian, Fish, Pork, Beef, OtherMeat -->
      <!-- Personen Input:     asp-for="Recipe.PersonCount" (number) -->
      <!-- Zeit Input:         asp-for="Recipe.PreparationTime" (number, Min.) -->
    </section>

    <!-- ══════ SECTION 2: Media (card-media) ══════ -->
    <section class="creator-card" data-theme="gold" id="card-media">
      <!-- Video/Bild Vorschau: #videoPreviewContainer -->
      <!-- Image Preview:       #imagePreview -->
      <!-- Video Preview:       #videoPreview -->
      <!-- File Input:          #videoInput (accept="video/*,image/*") -->
      <!-- Upload-Label:        #uploadLabel (Drag & Drop Zone) -->
    </section>

    <!-- ══════ SECTION 3: Zutaten (card-ingredients) ══════ -->
    <section class="smart-step-creator" data-theme="rosa" id="card-ingredients">
      <!-- Suche:               #ingredientSearch -->
      <!-- Katalog:             #ingredientsCatalog (height: 50vh) -->
      <!--   DB-Rows:           .preview-step-card.ingredient-db-row -->
      <!--     data-Attribute:  data-ingredient-id, data-name-{lang}, data-genus-{lang},
                                data-group-id, data-group-icon,
                                data-is-liquid, data-is-fat, data-is-hard, data-is-soft,
                                data-selected-qty, data-selected-unit -->
      <!--   Menge/Einheit:     .js-db-qty, .js-db-unit -->
      <!-- Ausgewählt:          #selectedIngredients (Drop-Zone) -->
      <!-- Vorschläge:          #selectedIngredientsSuggestions -->
    </section>

    <!-- ══════ SECTION 4: Wahrscheinlichkeit (card-probability) ══════ -->
    <section class="smart-step-creator" data-theme="dark" id="card-probability">
      <!-- Typ-Badges:          #ingredientProbabilityBadges -->
      <!-- Templates:           #ingredientProbabilityTemplates (hidden) -->
    </section>

    <!-- ══════ SECTION 5: Steps (card-steps) — Partial ══════ -->
    @await Html.PartialAsync("_SmartStepCreatorPartial.cshtml")
    <!-- Enthält:
      <section class="smart-step-creator" data-theme="rosa" id="card-steps">
        #sc2MasterPreviewCanvas    → Vorschau-Canvas
        #sc2MasterPreviewCard      → Vorschau-Karte (theme: gold)
        #MasterText                → Aktueller Step-Text
        #insertContainer           → Template-Bibliothek (50vh)
        #stepsChipStrip            → Zutaten-Chip-Streifen
        #stepsIngredientButtons    → Zutat-Chips für aktuellen Step
        #btnApplyStepIngredient    → "Anwenden"-Button
        #btnCancelStepIngredient   → "Abbrechen"-Button
        #selectedSteps             → Akzeptierte Steps (Drop-Zone)
      </section>
    -->

    <!-- ══════ SECTION 6: Keywords (card-keywords) ══════ -->
    <section class="creator-card" data-theme="gold" id="card-keywords">
      <!-- Keyword-Buttons:     .keyword-btn (Mehrfachauswahl) -->
      <!-- Ausgewählt:          #selectedKeywords -->
    </section>

    <!-- Publish-Button -->
    <div class="creator-bottom">...</div>
  </form>
</div>
```

### _SmartStepCreatorPartial.cshtml — HTML-Bereich (Zeilen 479–519)

```html
<section class="smart-step-creator" data-theme="rosa" id="card-steps">
  <div class="sheet-head creator-top-head">
    <div>
      <div class="sheet-title">Arbeitsschritte</div>
      <div class="sheet-sub">...</div>
    </div>
    <span class="badge ...">4/5</span>
  </div>

  <!-- Vorschau-Canvas -->
  <div class="creator-preview-canvas" id="sc2MasterPreviewCanvas">
    <div class="preview-step-card" data-theme="gold" id="sc2MasterPreviewCard">
      <div class="preview-step-text" id="MasterText">Template auswählen.</div>
    </div>
    <div id="InlineVarEditorHost"></div>     <!-- Inline-Editor Container -->
    <div id="insertContainer"                <!-- Template-Bibliothek -->
         class="template-library template-library-fixed"></div>
  </div>

  <!-- Zutaten-Chip-Streifen (initial hidden) -->
  <div class="ingredient-swiper ingredient-swiper-top" id="stepsChipStrip"
       style="display:none;">
    <div class="d-flex flex-wrap gap-2" id="stepsIngredientButtons"></div>
    <div class="d-flex gap-2 mt-2">
      <button id="btnApplyStepIngredient">Anwenden</button>
      <button id="btnCancelStepIngredient">Abbrechen</button>
    </div>
  </div>

  <!-- Akzeptierte Steps -->
  <div id="selectedSteps" class="mt-3"></div>
</section>
```

---

## 7. CSS-Klassen & Themes

### Theme-System

Definiert in `_SmartStepCreatorPartial.cshtml` (Zeilen 1–477).

#### CSS-Variablen je Theme

| Variable | Zweck |
|----------|-------|
| `--creator-sheet` | Hintergrund der Sheet-Fläche |
| `--creator-border` | Rahmenfarbe |
| `--creator-text` | Textfarbe |
| `--creator-sub` | Untertitel-/Hilfstext-Farbe |
| `--creator-accent` | Akzentfarbe |
| `--creator-bg` | Haupt-Hintergrund-Gradient |

#### Theme-Varianten

| Theme | `data-theme` | Hintergrund | Text | Besonderheit |
|-------|-------------|-------------|------|-------------|
| **Dark** (Default) | `dark` | Radial #244a88→#081528 | #f8f9ff | Standard-Dunkel |
| **Light** | `light` | Linear #e8f0ff→#ffffff | #101828 | Helle Variante |
| **Rosa** | `rosa` | Linear #ff9adf→#8a3f75 | #ffffff | Text-Shadow für Lesbarkeit |
| **Gold** | `gold` | Radial #f0c97a→#9a6b24→#FDE383 | #ffffff | Sheet: #FDEB9E→#C68F18 |
| **Navy** | `navy` | Radial #244a88→#0f234d→#081528 | #f8f9ff | Sheet: #0f234d→#1f4d92 |

### Haupt-CSS-Klassen

#### Layout & Container

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.smart-step-creator` | 4 | Container: min-height 76vh, border-radius 24px, padding 0 |
| `.creator-card` | (cshtml) | Basis-Karte mit Padding und Theme-Support |
| `.creator-card-head` | (cshtml) | Kopfzeile: 14px 16px padding |
| `.creator-card-body` | (cshtml) | Inhalt: 14px 16px 16px padding |
| `.creator-card-title` | (cshtml) | Titel: font-weight 700, 1.02rem |
| `.creator-card-sub` | (cshtml) | Untertitel: 0.84rem |
| `.creator-bg` | (cshtml) | Seiten-Hintergrund |
| `.creator-topbar` | (cshtml) | Top-Navigationsleiste |
| `.creator-bottom` | (cshtml) | Publish-Bereich unten |

#### Vorschau & Templates

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.creator-preview-canvas` | 223 | Flex-Column, padding 16px |
| `.preview-step-card` | 235 | Border-radius 22px, padding 16px, Transitions |
| `.preview-step-text` | 252 | Font: clamp(1.05rem, 2.5vw, 1.4rem) |
| `.template-library` | 328 | Overflow-y auto, flex-direction column, gap 8px |
| `.template-library-fixed` | 337 | Height 50vh, max-height 50vh |
| `.template-card` | 389 | Border-radius 14px, padding 10px, Cursor pointer |
| `.template-card.active` | 425 | Lila-Gradient-Highlight |

#### Phasen & Gruppen

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.phase-header` | 344 | Border-bottom 2px, padding-bottom 4px |
| `.phase-icon` | 348 | Font-size 1.15rem |
| `.phase-title` | 350 | Font-weight 700, 0.92rem, uppercase |
| `.sub-group-header` | 358 | Text-align center |

#### Zutaten-Chips

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.ingredient-chip` | 288 | Border 1px solid, border-radius 14px, padding 8px 12px, 0.82rem |
| `.ingredient-chip.active` | 301 | Scale 1.06, lila box-shadow, lila background |
| `.ingredient-chip.fraction-remainder` | 307 | Dashed border, orange Farbe rgba(255,180,50,0.6) |
| `.ingredient-chip.ingredient-chip-dimmed` | 313 | Opacity 0.4, grayscale(0.5), order 99 — nicht-passende Zutaten bei gefilterter Anzeige |
| `.ingredient-chip-divider` | 319 | Trennlinie (1px) zwischen passenden und restlichen Zutat-Chips |
| `.chip-icon` | (inline) | Icon-Container innerhalb des Chips |

#### Auto-Show Sub-Zutaten (CreatePosting.cshtml)

| Klasse | Beschreibung |
|--------|-------------|
| `.ingredient-row.ingredient-optional-sub` | Optionale Sub-Zutat (z.B. Eiklar/Eigelb bei Ei). Dashed lila border, eingerückt (padding-left 28px), opacity 0.82, ↳ Prefix |

#### Fraction-Picker (deprecated)

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.fraction-picker-row` | 311 | ⚠️ **DEPRECATED** - Border-top 1px solid, padding-top 8px, margin-top 6px |
| `.fraction-pick` | 316 | ⚠️ **DEPRECATED** - Font-size 0.78rem, padding 4px 10px, border-radius 12px |
| `.fraction-pick.active` | 323 | ⚠️ **DEPRECATED** - Lila border/shadow/background |

#### Multi-Ingredient Plus-Button & Chip States (NEU - StyleSheet.css ~726-740)

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.ingredient-plus-btn` | 726 | Plus-Button im Step: inline-flex, border-radius 50%, width/height 26px, padding 0, font-size 1.1rem, font-weight 600, transition 0.2s |
| `.ingredient-plus-btn:hover` | 734 | Hover: transform scale(1.1), box-shadow 0 2px 6px grün |
| `.ingredient-chip-disabled` | 738 | Deaktivierter Chip (bereits ausgewählt): opacity 0.4, cursor not-allowed, pointer-events none, filter grayscale(0.7) |

**Funktion:**
- **Plus-Button:** Erscheint im Step neben Token `{{ingredient}} [+] [↻]` wenn Zutat einen Wert hat. Öffnet Editor wenn geschlossen, fügt Zutat hinzu wenn Editor offen ist
- **Disabled Chips:** Zutaten, die bereits zur Multi-Ingredient-Liste hinzugefügt wurden, werden automatisch ausgegraut und deaktiviert

#### Buttons & CTA

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.creator-cta-primary` | 330 | Gradient-Button #8d63ff→#5f85ff, border-radius 999px |
| `.pill-like` | (JS) | Pill-Form-Button für Variablen-Auswahl |
| `.btn-outline-light.pill-like` | (JS) | Standard-Auswahl-Pill |
| `.pill-like.active` | (JS) | Aktive Pill-Auswahl |

#### Step-Rows

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.step-row` | 389 | Border-radius 14px, padding 10px, Draggable |
| `.step-row-actions` | (Partial) | Flex, gap 8px, margin-top 10px |
| `.step-badge` | (JS) | Badge-Nummer links |
| `.step-text-content` | (JS) | Step-Textinhalt |

#### Swiper & Scroll

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.ingredient-swiper` | 275 | Overflow-x auto, flex-wrap nowrap, scrollbar-width none |
| `.ingredient-swiper-top` | 286 | Margin-bottom 4px |

#### Checkliste

| Klasse | Zeile (publish-checklist.js) | Eigenschaft |
|--------|------------------------------|-------------|
| `.creator-checklist-toolbar` | (injected) | Sticky top 74px, z-index 1450 |
| `.creator-checklist` | (injected) | Flex, horizontal scroll |
| `.creator-check-item` | (injected) | Checklist-Element mit active/done States |

---

## 8. JSON-Datendateien

### master_steps.json
Enthält Master-Step-Templates mit Variablen-Platzhaltern.

```json
{
  "master_steps": [
    {
      "master_id": "PREP_WASH_01",
      "title": "Waschen",
      "templates": {
        "de": "{{ingredient}} gründlich waschen.",
        "en": "Wash {{ingredient}} thoroughly."
      },
      "phase": 1,
      "equipment": 0,
      "tags": ["prep", "washing"],
      "selection_tags": ["category:salat"]
    }
  ]
}
```

**Variablen-Syntax:** `{{variable_name}}` z.B. `{{ingredient}}`, `{{duration}}`, `{{temp}}`
**Optionale Segmente:** `[optional text {{variable}}]` — wird ausgeblendet wenn Variable leer

**Reihenfolge der PREP-Steps (bestimmt Anzeige im Smart Step Creator):**
1. PREP_HEAT_01 (Vorheizen) — equipment_prep
2. PREP_WASH_01 (Waschen) — ingredient_prep
3. PREP_PEEL_01 (Schälen) — ingredient_prep, `[mit einem {{tool}}]` optional
4. PREP_CUT_01, PREP_GRATE_01, PREP_MINCE_01 — ingredient_prep
5. PREP_MARINATE_01, PREP_SOAK_01 — marinating
6. Mixing, Dough, Coating Steps...

**Wichtige Template-Änderungen:**
- PREP_PEEL_01: Tool-Teil optional — `Schäle {{ingredient}}[ mit einem {{tool}}].`
- PREP_STUFF_01: Variablen getauscht — `Fülle {{ingredient}} gleichmäßig mit {{base}} und setze {{pronoun}} in {{equipment}}.`

### master_step_variables.json
Variablen-Katalog mit Optionen pro Variable.

**Struktur:**
```json
{
  "variables": {
    "articles": { "options": [{ "key": "der", "labels": {...}, "tags": [...] }] },
    "pronoun":  { "options": [{ "key": "es",  "labels": {...}, "tags": [...] }] },
    "state":    { "options": [{ "key": "fein","labels": {...}, "tags": [...] }] },
    "heat":     { "options": [{ "key": "mittlere", ... }] },
    "shape":    { "options": [{ "key": "würfel",   ... }] },
    "equipment": { "options": [pfanne, topf, backofen, bräter, kochfeld, mixer, grill, dampfgarer, schüssel, sieb, backform, auflaufform] },
    "ingredient_fractions": {
      "options": [
        { "key": "1/2", "numerator": 1, "denominator": 2,
          "labels": { "de": "die Hälfte", ... },
          "remainder_labels": { "de": "restlich", ... } },
        { "key": "1/3", "numerator": 1, "denominator": 3, ... },
        { "key": "2/3", ... }, { "key": "1/4", ... }, { "key": "3/4", ... }
      ]
    }
  }
}
```

**Equipment-Optionen:** Jede Option hat `labels` (10 Sprachen) und `tags` für kontextbasierte Filterung im Smart Step Creator (z.B. `technique:bake`, `heat:dry`).

### ingredient_transforms.json
Definiert wie Zutaten nach bestimmten Steps transformiert werden.

```json
{
  "adjective_patterns": {
    "diced": {
      "trigger_steps": ["PREP_CUT_01"],
      "shape_match": ["würfel", "diced"],
      "patterns": {
        "de": { "m": "gewürfelter {{noun}}", "f": "gewürfelte {{noun}}" }
      }
    }
  },
  "special_transforms": [
    {
      "trigger_step": "COOK_REDUCE_01",
      "match": { "de": ["butter", "sahne"] },
      "outputs": [{ "names": { "de": "Beurre noisette" }, "icon": "🧈" }]
    }
  ]
}
```

### Weitere JSON-Dateien

| Datei | Inhalt |
|-------|--------|
| `master_step_option_rules.json` | Konditionale Regeln: welche Optionen bei welchem Step/Zutat erscheinen |
| `recipe_step_mapping.json` | Welche Steps zu welchem Rezepttyp gehören |
| `recipe_type_step_variables.json` | Variablen-Standardwerte pro Rezepttyp |
| `recipe_category_scoring.json` | Scoring-Regeln für Rezepttyp-Erkennung anhand Zutaten |
| `ingredient_article_rules.json` | Grammatik: welcher Artikel zu welcher Zutat (der/die/das) |
| `probability_template_presets.json` | Vorgefertigte Step-Kombinationen pro Rezepttyp |

---

## 9. JS-Datei: create-posting-utils.js

**Pfad:** `wwwroot/js/create-posting-utils.js` · **74 Zeilen**
**Export:** `window.CreatePostingUtils`

| Methode | Beschreibung |
|---------|-------------|
| `escapeHtml(value)` | HTML-Entities escapen (`<`, `>`, `&`, `"`, `'`) |
| `resolveLangKey(value)` | Sprach-Codes normalisieren: `es`→`esp`, `pt`→`prt`, `se`→`sv`, `dk`→`da` |
| `getDataUrl(key)` | URL aus `window.CreatePostingDataUrls` abrufen |
| `fetchJson(key, options)` | JSON von konfigurierter URL laden |
| `showTransientMessage(message, options)` | Temporäre Toast-Nachricht anzeigen |
| `reportError(message, error, options)` | Fehler loggen und anzeigen |

---

## 10. JS-Datei: create-posting-data-urls.js

**Pfad:** `wwwroot/js/create-posting-data-urls.js` · **11 Zeilen**
**Export:** `window.CreatePostingDataUrls` (Object.freeze)

| Key | URL |
|-----|-----|
| `ingredientArticleRules` | `/data/ingredient_article_rules.json` |
| `masterSteps` | `/data/master_steps.json` |
| `masterStepOptionRules` | `/data/master_step_option_rules.json` |
| `masterStepVariables` | `/data/master_step_variables.json` |
| `probabilityTemplatePresets` | `/data/probability_template_presets.json` |
| `recipeCategoryScoring` | `/data/recipe_category_scoring.json` |
| `recipeStepMapping` | `/data/recipe_step_mapping.json` |
| `recipeTypeStepVariables` | `/data/recipe_type_step_variables.json` |
| `ingredientTransforms` | `/data/ingredient_transforms.json` |

---

## 11. JS-Datei: create-posting-data-store.js

**Pfad:** `wwwroot/js/create-posting-data-store.js` · **98 Zeilen**
**Export:** `window.CreatePostingDataStore`

| Methode | Beschreibung |
|---------|-------------|
| `load(key, options)` | Einzelnen Datensatz laden (mit Cache & Deduplizierung) |
| `loadMany(keys, options)` | Mehrere Datensätze parallel laden |
| `get(key)` | Gecachte Daten abrufen (wirft Fehler wenn nicht geladen) |
| `peek(key)` | Gecachte Daten sicher abrufen (null wenn nicht geladen) |
| `has(key)` | Prüfen ob Daten gecacht |
| `clear(key)` | Cache für Key oder komplett leeren |

**Internes Verhalten:**
- In-Memory-Cache mit `Map`
- Pending-Promise-Deduplizierung (verhindert doppelte Netzwerk-Requests)
- Validierung der geladenen Daten (muss Object sein)

---

## 12. JS-Datei: create-posting-feedback.js

**Pfad:** `wwwroot/js/create-posting-feedback.js` · **41 Zeilen**
**Export:** `window.CreatePostingFeedback`

| Methode | Beschreibung |
|---------|-------------|
| `showToast(message, options)` | Toast mit Dauer und State-Management anzeigen |
| `reportError(context, error, options)` | Fehlermeldung mit Prefix anzeigen |

---

## 13. JS-Datei: create-posting-page-data.js

**Pfad:** `wwwroot/js/create-posting-page-data.js` · **17 Zeilen**
**Export:** `window.CreatePostingPageData`

| Methode | Beschreibung |
|---------|-------------|
| `loadIngredientArticleRules()` | Async: Artikel-Regeln aus DataStore laden |
| `loadIngredientTransforms()` | Async: Zutaten-Transformationen aus DataStore laden |

---

## 14. JS-Datei: create-posting-template-builder.js

**Pfad:** `wwwroot/js/create-posting-template-builder.js` · **62 Zeilen**
**Export:** `window.CreatePostingTemplateBuilder`

| Methode | Beschreibung |
|---------|-------------|
| `setMasterTemplateError(message)` | Fehlermeldung im Template-Container anzeigen |
| `renderTemplateCards(deps)` | Alle Master-Template-Karten rendern (nur für #masterTemplateCards) |
| `refreshMasterTemplateBuilder(deps)` | Komplettes Template-Builder-UI neu aufbauen |

**Abhängigkeiten:**
- `window.MasterStepRenderer.getAllTemplates()`
- `window.MasterStepRenderer.getLastLoadError()`
- `window.getThemeMutedTextClass()`
- `window.getCreatePostingTheme()`

**WICHTIG:** `renderSc2TemplateCards()` wurde entfernt (2026-04-02). Der Smart Step Creator verwendet jetzt ausschließlich `renderStepButtons()` aus `CreatePostingSmartStepCreator.js` für komplexe Gruppierung.

---

## 15. JS-Datei: create-posting-step-filter-ui.js

**Pfad:** `wwwroot/js/create-posting-step-filter-ui.js` · **86 Zeilen** · **NEU (2026-04-02)**
**Export:** `window.CreatePostingStepFilterUI`

**Zweck:** Filter-UI für Smart Step Creator (Phase 1 Optimierung)
- Rendert Suchfeld + Phase-Tabs oberhalb der Step-Liste
- Verbindet UI-Events mit Filter-Logik in `CreatePostingSmartStepCreator.js`
- Theme-aware (dark, light, rosa, gold, navy)

| Methode | Beschreibung |
|---------|-------------|
| `init()` | Filter-UI initialisieren (wird von CreatePostingPage.js aufgerufen) |
| `injectFilterUI(containerSelector)` | HTML für Suchfeld + Phase-Tabs vor Container einfügen |
| `attachFilterEventHandlers()` | Event-Listener für input/click an Filter-Controls binden |

**DOM-Struktur:**
```html
<div class="step-filter-controls" data-theme="...">
  <div class="step-search-wrapper">
    <input class="step-search-input" placeholder="🔍 Step suchen...">
  </div>
  <div class="phase-tabs">
    <button class="phase-tab active" data-phase="all">Alle</button>
    <button class="phase-tab" data-phase="1">🔪 Vorbereitung</button>
    <button class="phase-tab" data-phase="2">🔥 Kochen</button>
    <button class="phase-tab" data-phase="3">✨ Finishing</button>
    <button class="phase-tab" data-phase="4">🍽️ Servieren</button>
  </div>
</div>
```

**Event-Flow:**
1. User tippt in `.step-search-input` → `input` Event
2. Handler ruft `MasterStepCreatorHelpers.setSearchQuery(query)` auf
3. MasterStepCreatorHelpers ruft intern `renderStepButtons()` auf
4. Step-Liste wird gefiltert neu gerendert

**Abhängigkeiten:**
- `window.MasterStepCreatorHelpers.setSearchQuery(query)`
- `window.MasterStepCreatorHelpers.setPhaseFilter(phase)`
- `window.getCreatePostingTheme()`
- jQuery

**Stylesheet:** `create-posting-step-filter.css`

---

## 15. JS-Datei: master-step-renderer.js

**Pfad:** `wwwroot/js/master-step-renderer.js` · **478 Zeilen**
**Export:** `window.MasterStepRenderer`

| Methode | Beschreibung |
|---------|-------------|
| `load()` | Master-Steps und zugehörige Daten laden |
| `getAllTemplates()` | Alle verfügbaren Master-Step-Templates abrufen |
| `render(masterId, vars, lang)` | Template mit Variablen-Substitution rendern |
| `renderAll(masterIds, langKey)` | Mehrere Templates rendern |
| `getSmartDefaults(masterId, ingredients, langKey)` | Vorgeschlagene Variablen für Template |
| `suggestForIngredient(ingredientName, masterId, langKey)` | Vorschläge für Zutat |
| `getVariablePresets(masterId)` | Preset-Werte für Template-Variablen |
| `getLastLoadError()` | Letzte Lade-Fehlermeldung |
| `findTemplate(masterId)` | Template per ID finden |
| `getRecipeTypeStepVars(typeId)` | Step-Variablen für Rezepttyp |
| `resolveParentType(typeId)` | Typ-Hierarchie auflösen |
| `getOptionRulesData()` | Alle Optionsregeln abrufen |
| `getOptionRulesForAction(masterId, lang)` | Regeln für Action-Variable |
| `getOptionRulesForFamily(masterId, lang)` | Regeln für Family-Variable |
| `getOptionRulesForStep(masterId, varName, lang)` | Regeln für spezifische Variable |

---

## 16. JS-Datei: recipe-step-suggest.js

**Pfad:** `wwwroot/js/recipe-step-suggest.js` · **513 Zeilen**
**Export:** `window.RecipeStepSuggest`

| Methode | Beschreibung |
|---------|-------------|
| `loadMapping()` | Rezept-Mapping-Daten laden (mit Cache) |
| `detectRecipeTypes(ingredients, threshold)` | Rezepttypen aus Zutaten erkennen |
| `suggestStepsForIngredients(ingredients, lang)` | Step-Vorschläge basierend auf Zutaten |
| `renderRecipeTypeBadges(recipeTypes, langKey)` | Rezepttyp-Badges rendern |
| `getMasterStepPreview(masterId, ingredients, lang)` | Vorschau-Text für Master-Step |
| `getCategoryById(categoryId)` | Kategorie per ID abrufen |
| `hasMasterSteps()` | Prüfen ob Master-Steps geladen |
| `isLoaded()` | Prüfen ob Mapping-Daten geladen |

**Zutat-Alias-Gruppen:**
- Zwiebeln: IDs 11, 12, 83, 248
- Hackfleisch: IDs 218, 219, 220
- Eier: IDs 85, 227, 231

---

## 17. JS-Datei: create-posting-probability.js

**Pfad:** `wwwroot/js/create-posting-probability.js` · **378 Zeilen**
**Export:** `window.CreatePostingProbability`, `window.ProbabilityMultiIngredients`

| Methode | Beschreibung |
|---------|-------------|
| `create(deps)` | Factory: erstellt Probability-Modul-Instanz |
| → `.refresh()` | Analyse mit aktuellen Zutaten neu berechnen |
| → `.showSuggestions()` | Template-Vorschläge nach erkanntem Rezepttyp zeigen |
| `buildInlineTemplateText(masterId, template, lang, vars, overrideVars)` | **NEU (Zeile 38-58):** Template mit Inline-Tokens rendern. Aktiviert Plus-Buttons (`enablePlusButtons: true`) und übergibt Multi-Ingredients-Data aus `window.ProbabilityMultiIngredients[masterId]` |
| `getMergedVars(masterId)` | **UPDATED (Zeile 178-201):** Merged Variablen für Template. **CRITICAL FIX:** Liest Multi-Ingredients aus `window.ProbabilityMultiIngredients[masterId]` und formatiert sie mit `helpers.formatSelectedIngredientList()` vor Template-Akzeptierung |

**Schwellenwerte:**
- `>= 60%` → Erfolg (grün)
- `>= 35%` → Primär (blau)
- `< 35%` → Niedrig

**CSS-Klassen:**
- `.probability-template-wrap.editing` → Lila Glow-Effekt auf Card wenn Inline-Editor offen (border-color + box-shadow + background)
- `.probability-template-card` → Card-Container mit Transition für border/shadow/background

### Multi-Ingredient Support (NEU 2026-03-27)

**Globaler Storage (Zeile 5-7):**
```javascript
// Structure: { "masterId": { "varName": [{name, fraction, article}, ...] } }
window.ProbabilityMultiIngredients = window.ProbabilityMultiIngredients || {};
```

**Workflow:**
1. User klickt Template-Token → Editor öffnet
2. User wählt Zutat + Artikel + Fraktion → "Einsetzen"
3. Plus-Button [+] erscheint im Template neben Token
4. User klickt [+] → Editor öffnet wieder, bereits ausgewählte Zutaten sind ausgegraut
5. User fügt weitere Zutat hinzu → "die Hälfte der Pasta und ein Drittel des Mehls"
6. Chips mit X-Button am Boden des Editors zum Entfernen
7. User klickt Template-Card → Alle Multi-Ingredients werden korrekt übernommen

**Wichtige Funktionen:**

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `buildInlineTemplateText()` | 38 | Aktiviert Plus-Buttons durch `enablePlusButtons: true` in `renderTemplateWithConfig()`. Übergibt `multiIngredientsData` aus globalem Storage |
| `getMergedVars()` | 178 | **CRITICAL:** Iteriert durch `window.ProbabilityMultiIngredients[masterId]` und formatiert jede Variable mit `helpers.formatSelectedIngredientList()`. Ohne diesen Fix werden nur Einzelzutaten übertragen! |

**Event-Handler:**
- Plus-Button Click: Siehe `CreatePostingPage.js` Zeile 4199–4254
- Remove-Chip Click: Siehe `CreatePostingPage.js` Zeile 4319–4377

---

## 18. JS-Datei: ingredientmanager.js

**Pfad:** `wwwroot/js/ingredientmanager.js` · **145 Zeilen**
**Export:** `window.IngredientManager`

| Methode | Beschreibung |
|---------|-------------|
| `getUnits()` | Gecachte Unit-Liste abrufen |
| `refreshUnits()` | Units aus DOM-Select-Optionen neu aufbauen |
| `findUnitByDe(unitDe)` | Unit per deutschem Namen finden |
| `getDefaultUnitDe()` | Standard-Unit (erste in Liste) |
| `getUnitLabel(unitObj, langKey)` | Lokalisiertes Unit-Label |
| `buildUnitOptionsHtml(selectedDe, langKey)` | HTML-Optionen für Select generieren |

**25 Fallback-Units:** Stk., EL., TL., g., ml., l., Prise, Tasse, Scheibe, etc.

---

## 19. JS-Datei: CreatePostingSmartStepCreator.js

**Pfad:** `wwwroot/js/CreatePostingSmartStepCreator.js` · **2.253 Zeilen**
**Export:** `window.MasterStepCreatorHelpers` (Object.assign, 2 Blöcke)
**Pattern:** IIFE (Immediately Invoked Function Expression)

### State-Variablen (Zeile 16–23)

```javascript
let doc = null;                      // Geladenes JSON-Dokument
let variableCatalog = { variables: {} }; // Variablen-Katalog
let steps = [];                      // Master-Steps Array
let currentLang = "de";              // Aktuelle Sprache
let activeStep = null;               // { master_id, title, templateRaw, values:{} }
let activeToken = null;              // { varName, tokenId }
```

### Funktions-Katalog

#### Initialisierung & Laden

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `init()` | 2001 | Async-Initialisierung: JSON + Catalog laden, UI rendern, Events binden |
| `loadJson()` | 193 | Master-Steps JSON laden |
| `loadVariableCatalog()` | 197 | Variablen-Katalog laden |

#### Kern-Utilities

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `escapeHtml()` | 36 | Von window.CreatePostingUtils |
| `encodeAttr(str)` | 38 | URL-encode für Attribute |
| `decodeAttr(str)` | 41 | URL-decode |
| `uid(prefix)` | 45 | Einzigartige ID generieren |
| `getVarDisplayName(varName)` | 87 | Mehrsprachiger Anzeigename für Variable |

#### Variablen-Display-Namen (VAR_DISPLAY_NAMES, Zeile 50–85)

Unterstützte Variablen-Typen (10 Sprachen: de, en, esp, prt, id, nl, sv, da, no, ms):

`ingredient`, `ingredient2`, `ingredients`, `state`, `equipment`, `tool`, `duration`, `temp`, `shape`, `grind_size`, `pronoun`, `pronoun2`, `action`, `liquid`, `fat`, `base`, `marinade`, `method`, `finish`, `seasonings`, `thickener`, `count`, `mode`, `components`, `dough`, `surface`, `heat`, `extra`, `item`, `position`, `reason`, `goal`, `balance`, `keep`

#### Fraction-Helpers (Zeile 95–189)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getFractionOptions()` | 95 | Bruch-Optionen aus JSON lesen, lokalisiert zurückgeben (Optionen: 1/2, 1/3, 2/3, 1/4, 3/4) |
| `composeFractionText(fractionDef, ingredientName, article)` | 112 | **UPDATED:** Erzeugt Fraktion-Text mit automatischer Genitiv-Konvertierung. Wandelt Nominativ-Artikel automatisch zu Genitiv um: "der"→"des", "die"→"der", "das"→"des". Beispiel: `"die Hälfte des Zuckers"` |
| `findMatchingFractionOption(num, den, opts)` | 127 | Passende Fraction-Option finden |
| `getRemainderChipsFromAcceptedSteps()` | 133 | Akzeptierte Steps scannen, Rest-Chips berechnen. **UPDATED:** Unterstützt jetzt Array-Format für _fractionData (backward compatible mit alten single-object Format) |
| `renderFractionPickerHtml(fractionOptions)` | 184 | **RESTORED:** Rendert Fraktions-Auswahl-Chips (Ganzes, 1/2, 1/3, 2/3, 1/4, 3/4) unter "Menge"-Überschrift |

#### Ingredient-Helpers (Zeile 1267–1293)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getIngredientName(item)` | 1267 | Extrahiert Namen aus Zutat-Objekt oder String |
| `getIngredientFraction(item)` | 1271 | Extrahiert Fraction-Key aus Zutat-Objekt |
| `normalizeIngredientValues(values)` | 1275 | **UPDATED:** Normalisiert Zutat-Array zu `{name, fraction, article}` Format. Unterstützt backward compatibility mit String-Arrays und `{name, fraction}` Objekten |

#### Multi-Ingredient Workflow mit Plus-Button (NEU)

**Pattern:** Schrittweises Hinzufügen von Zutaten mit individuellen Fraktionen und Artikeln. Plus-Button erscheint **im Step selbst** (neben Token).

**Datenstruktur:**
```javascript
activeStep._multiIngredients = [
  { name: "Zucker", fraction: "1/2", article: "des" },
  { name: "Butter", fraction: "1/3", article: "der" }
]
```

**UI-Struktur:**
- **Editor:** Artikel oben, Zutat-Chips mittig, Fraktions-Chips unten, nur "Einsetzen" & "Schließen"
- **Editor-Boden:** Bereits hinzugefügte Zutaten als Bootstrap badge chips (blau) mit X-Button zum Entfernen
- **Step:** `{{ingredient}} [+] [↻]` - Plus-Button erscheint neben Token wenn Zutat einen Wert hat
- **Bereits ausgewählte Zutaten:** Werden automatisch ausgegraut/deaktiviert (`.ingredient-chip-disabled`)

**Workflow:**
1. User klickt Token → Editor öffnet
2. User wählt Artikel (z.B. "des"), Zutat (z.B. "Zucker"), Fraktion (z.B. "1/2")
3. User klickt "Einsetzen" → Editor schließt
4. **Plus-Button [+] erscheint im Step** neben Token: `die Hälfte des Zuckers [+] [↻]`
5. User klickt **[+] im Step** → Editor öffnet wieder
6. "Zucker"-Chip ist jetzt ausgegraut (bereits verwendet)
7. Am Boden des Editors: Chip "die Hälfte des Zuckers" mit X-Button
8. User wählt weitere Zutat (z.B. "ein Drittel der Butter")
9. User klickt **[+] im Step** → Zutat wird hinzugefügt
10. Step zeigt: `die Hälfte des Zuckers und ein Drittel der Butter [+] [↻]`
11. Am Boden des Editors: Zwei Chips, jede mit X-Button zum Entfernen
12. User kann weitere Zutat hinzufügen oder "Einsetzen" klicken → Editor schließt, finaler Text eingefügt

**Genitiv-Konvertierung:**
Das System konvertiert Nominativ-Artikel automatisch zu Genitiv für Fraktionen:
- "der" (mask) → "des" → "die Hälfte **des** Zuckers"
- "die" (fem) → "der" → "die Hälfte **der** Butter"
- "das" (neut) → "des" → "die Hälfte **des** Mehls"
- "des" bleibt "des" (bereits Genitiv)

#### Unified Overlay System (Step Creator + Probability Area)

**Ab 2026-03-27:** Vollständig vereinheitlichtes Overlay-System für beide Bereiche mit objekt-basierter Architektur.

**Architektur:**
```javascript
// ══════════════════════════════════════════════════════════════
// UNIFIED OVERLAY SYSTEM - Gleiche Logik für beide Bereiche
// ══════════════════════════════════════════════════════════════

// 1. Objekt-basierte State-Verwaltung
activeStep = {                    // Smart Step Creator (unten)
  masterId, templateRaw, values, _multiIngredients
}

probabilityStates[masterId] = {   // Probability Area (oben)
  masterId, templateRaw, values, _multiIngredients
}
// → Identische Datenstruktur! Einziger Unterschied: Dictionary vs. Single Object

// 2. Einheitliche Overlay-Öffnung
openUniversalVariableEditor({
  varName, currentVal, masterId,
  context: { type: 'step' | 'probability', masterId, ... },
  onApply, onClose
})

// 3. Einheitliche Update-Logik
updateContextValue(context, varName, value, extras) {
  if (context.type === 'step') {
    activeStep.values[varName] = value;
    renderMasterText();  // Re-render step from object
  } else if (context.type === 'probability') {
    probabilityStates[masterId].values[varName] = value;
    renderProbabilityTemplate(masterId);  // Re-render template from object
  }
}

// 4. LIVE Preview-Updates im Overlay-Header
createUnifiedOverlayHtml(editorHtml, context) {
  if (context.type === 'step') {
    // LIVE aus DOM holen
    previewHtml = document.querySelector(".current-step-wrap").innerHTML;
  } else if (context.type === 'probability') {
    // LIVE aus DOM holen (zeigt immer aktuelle Werte!)
    const card = document.querySelector(`.probability-template-wrap[data-master-id="${masterId}"] .probability-template-card`);
    previewHtml = card.innerHTML;
  }
}
```

**Kritische CSS-Klassen für Event-Delegation:**
```javascript
// Probability Area: Tokens MÜSSEN diese Klasse haben!
renderTemplateWithConfig(templateRaw, masterId, values, {
  tokenExtraClasses: 'js-probability-var',  // ← Ohne diese Klasse funktioniert Klick nicht!
  includeVarKey: true,
  enablePlusButtons: true
})
```

**Vorteile:**
- ✅ **100% identische Business-Logik** für beide Bereiche (Step Creator + Probability Area)
- ✅ Keine Code-Duplikation (vorher >400 Zeilen dupliziert)
- ✅ Objekt-basiert statt DOM-basiert → Single Source of Truth
- ✅ LIVE Preview-Updates: Overlay-Header zeigt immer aktuelle Werte
- ✅ Multi-Ingredient-Workflow funktioniert identisch in beiden Bereichen
- ✅ Bugs müssen nur an einer Stelle gefixt werden

**Implementierung:**

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `openUniversalVariableEditor(config)` | ~2120 | **Unified Overlay Entry Point**. Öffnet Editor-Overlay für beide Bereiche. Parameter: `{ varName, currentVal, masterId, context, onApply, onClose }`. Context: `{ type: 'step' \| 'probability', masterId, tokenElement, templatePreviewHtml }` |
| `createUnifiedOverlayHtml(editorHtml, context)` | ~2192 | **3-Section Layout Builder**. Erzeugt Fullscreen-Overlay mit: Fixed Header (Preview), Scrollable Middle (Editor), Fixed Footer (Buttons). Preview wird **LIVE aus DOM** geholt für aktuelle Werte |
| `bindUnifiedOverlayEventHandlers(overlayEl, editorEl, config)` | ~2248 | **Unified Event Binding**. Bindet alle Event-Handler mit `.universal` Namespace: Close, Apply, Quick-Apply, Pick-Mode, Duration-Unit, Plus-Button (Multi-Ingredient), Remove-Chip |
| `applyUnifiedEditorValue(editorEl, config)` | ~2402 | **Unified Apply Logic**. Extrahiert Wert, behandelt Multi-Ingredient-Workflow (Add-to-List + Re-open), ruft `updateContextValue()` auf, schließt Overlay |
| `handleUnifiedPlusButtonClick(editorEl, config)` | ~2510 | **Multi-Ingredient Plus Handler**. Fügt neue Zutat zu Liste hinzu, aktualisiert Context, re-öffnet Editor mit aktualisierten Chips |
| `updateContextValue(context, varName, value, extras)` | ~2333 | **Unified Update Logic**. Aktualisiert `activeStep.values` oder `probabilityStates[masterId].values`, ruft passende Render-Funktion auf |
| `closeUnifiedOverlay()` | ~2549 | **Overlay Cleanup**. Entfernt Overlay aus DOM, stellt Body-Scroll wieder her, entfernt ESC-Handler |
| `_generateEditorHtml(varName, currentVal, opts)` | ~1434 | **Shared HTML Generator**. Erzeugt komplettes Editor-HTML inkl. Multi-Ingredients-Chips (oben), Fraction-Picker, Artikel-Buttons, Zutat-Chips (mit Disabled-State), Apply/Close-Buttons. Parameter `useClassBasedIds`: false → IDs (`#ArticleBtnRow`), true → Klassen (`.js-article-btn-row`) |
| `_handlePickModeClick(pickBtn, host, useClassBasedIds)` | ~1648 | **Shared Pick-Mode Handler**. Behandelt alle Pick-Modes: `fraction` (Menge-Chips), `ingredient-value` (Multi-Select Zutat-Chips), `article` (Artikel-Buttons), `pronoun` (Pronomen), `value` (generische Werte). Setzt `.active` Klasse und speichert Selektion in `host.dataset` |
| `_handleDurationUnitClick(btn, host)` | ~1629 | **Shared Duration-Unit Handler**. Zeigt Zahlenfelder (`#DurationValueInput`, `#DurationValueToInput`) bei `minute`/`hour`, versteckt bei `per_package`/`short` |
| `getMultiIngredientsForContext(context, varName)` | ~2350 | **Multi-Ingredient Getter**. Holt Multi-Ingredient-Array für Step oder Probability basierend auf Context-Type |
| `saveMultiIngredientsForContext(context, varName, list)` | ~2361 | **Multi-Ingredient Setter**. Speichert Multi-Ingredient-Array in `activeStep._multiIngredients` oder `probabilityStates[masterId]._multiIngredients` |

**Verwendung in beiden Bereichen:**

```javascript
// Step Creator (unterer Bereich) - Nutzt IDs
const editorHtml = _generateEditorHtml(varName, currentVal, {
  useClassBasedIds: false,  // IDs: #FractionPickerRow, #ArticleBtnRow
  multiIngredients: activeStep._multiIngredients || []
});

// Probability Area (oberer Bereich) - Nutzt Klassen
const editorHtml = helpers.buildInlineEditorHtml(varName, currentVal, {
  useClassBasedIds: true,   // Klassen: .js-fraction-picker-row, .js-article-btn-row
  multiIngredients: (window.ProbabilityMultiIngredients[masterId] || {})[varName] || []
});

// Event Handler - beide Bereiche
$host.on('click', 'button[data-pick-mode]', function() {
  helpers.handlePickModeClick(this, $host[0], useClassBasedIds);
});
```

---

#### 🆕 UPDATE 2026-03-28: Overlay-offen-Workflow + Vereinheitlichte Accept-Logik

**NEUE FEATURES:**
1. ✅ **Overlay bleibt nach "Einsetzen" offen** - User kann mehrere Variablen nacheinander bearbeiten
2. ✅ **Token-Klicks im Preview-Header** - Wechsel zwischen Variablen ohne Overlay zu schließen
3. ✅ **Preview-Update im Overlay** - Header-Preview aktualisiert sich sofort nach Wertauswahl
4. ✅ **Vereinheitlichte Accept-Logik** - Step Creator & Probability Area nutzen gemeinsame Funktion
5. ✅ **Plus-Button entfernt** - Kein "+"-Button mehr neben Tokens (alles im Overlay)
6. ✅ **"Würfel" in shape-Vorauswahl** - Bug-Fix: Würfel erscheint in Top-3 Quick-Select-Buttons

**Neue Funktionen:**

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `switchEditorVariable(newVarName, config)` | ~2565 | **Variable Switcher**. Ersetzt Editor-Content mit neuem Variable-Editor ohne Overlay zu schließen. Holt aktuellen Wert aus `activeStep.values` oder `probabilityStates[masterId].values`, generiert neues Editor-HTML, aktualisiert DOM und config |
| `updateOverlayPreview(context)` | ~2432 | **Preview Updater**. Kopiert frisch gerenderten Preview-HTML aus DOM (`.current-step-wrap` oder `.probability-template-card`) in Overlay-Header (`.creator-preview-canvas > div`). Wird nach jedem `updateContextValue()` aufgerufen |
| `saveCurrentEditorValueBeforeAccept(contextType, contextId)` | ~3302 | **Shared Accept-Helper**. Gemeinsame Funktion für Step Creator (`acceptActiveStep`) und Probability Area (`getMergedVars`). Prüft ob Overlay offen, extrahiert aktuellen Editor-Wert, speichert in `activeStep.values` oder `probabilityStates[masterId].values`, rendert Update. Export via `window.MasterStepCreatorHelpers` |

**Geänderte Funktionen:**

| Funktion | Zeile | Änderung |
|----------|-------|----------|
| `applyUnifiedEditorValue(editorEl, config)` | ~2570 | **Overlay bleibt offen!** Entfernt `closeUnifiedOverlay()` nach Apply. Ruft `updateOverlayPreview(context)` auf für Preview-Update im Header |
| `handleUnifiedPlusButtonClick(editorEl, config)` | ~2607 | Nutzt `switchEditorVariable()` statt Close+Reopen. Ruft `updateOverlayPreview(context)` auf |
| **Remove-Chip Handler** | ~2312 | Nutzt `switchEditorVariable()` statt Close+Reopen. Ruft `updateOverlayPreview(context)` auf |
| `acceptActiveStep()` | ~3341 | Nutzt gemeinsame Funktion `saveCurrentEditorValueBeforeAccept('step', null)` statt dupliziertem Code |
| `getMergedVars(masterId)` (in create-posting-probability.js) | ~178 | Nutzt gemeinsame Funktion `window.MasterStepCreatorHelpers.saveCurrentEditorValueBeforeAccept('probability', masterId)` statt dupliziertem Code |
| `getRankedVarOptionEntries(varName, ...)` | ~435 | Bug-Fix: Prüft jetzt auf `normalizedKey === "shape"` statt `"form"` für Würfel-Bevorzugung |

**Entfernte Features:**

| Feature | Grund |
|---------|-------|
| **Plus-Button neben Tokens** (`ingredient-plus-btn`) | Alle Multi-Ingredient-Bearbeitung läuft jetzt über Overlay. Plus-Button im Overlay-Footer bleibt. Entfernt aus: `renderTemplate()` (~782), `renderTemplateTokens()` (~694), `renderTemplateWithConfig()` (~835) |
| **Plus-Button Event-Handler** | ~3481 | Auskommentiert, da Button nicht mehr existiert |

**Neuer User-Flow:**

```javascript
// 1. User öffnet Overlay für Variable A
openUniversalVariableEditor({ varName: 'size', ... });

// 2. User wählt Wert "feine"
_handlePickModeClick(btn, editorEl);  // Setzt dataset.selectedValue = "feine"

// 3. User klickt "Einsetzen"
applyUnifiedEditorValue(editorEl, config);
// → updateContextValue() speichert in activeStep.values['size'] = "feine"
// → updateOverlayPreview() aktualisiert Preview im Overlay-Header ✅ NEU!
// → Overlay BLEIBT OFFEN ✅ NEU!

// 4. User klickt Token "shape" im Preview-Header ✅ NEU!
$overlay.on('click.universal', '.creator-preview-canvas .template-var[data-var]', ...);
// → switchEditorVariable('shape', config) ersetzt Editor-Content
// → Zeigt shape-Editor mit aktuellem Wert aus activeStep.values['shape']

// 5. User wählt "Würfel"
_handlePickModeClick(btn, editorEl);  // Setzt dataset.selectedValue = "Würfel"

// 6. User klickt "Einsetzen"
// → speichert "Würfel", Preview aktualisiert sich, Overlay bleibt offen

// 7. User klickt "Schließen"-Button ODER
//    User klickt "Step akzeptieren"
acceptActiveStep();
// → saveCurrentEditorValueBeforeAccept('step') ✅ NEU!
//    speichert AUCH den aktuell im Editor sichtbaren Wert (falls nicht "Einsetzen" geklickt)
// → closeUnifiedOverlay()
// → ALLE Werte werden in finalen Step übernommen
```

**Event-Listener-Änderungen:**

| Event | Selector | Handler | Beschreibung |
|-------|----------|---------|-------------|
| `click.universal` | `.creator-preview-canvas .template-var[data-var]` | Token-Click im Preview | ✅ NEU! Wechselt Variable im Editor via `switchEditorVariable()`. Overlay bleibt offen. |
| `click.universal` | `.creator-preview-canvas .js-probability-var[data-var]` | Token-Click im Preview (Probability) | ✅ NEU! Identisch zu Step Creator Token-Click |

**Vorteile:**

- ✅ **Effizienterer Workflow** - Keine wiederholten Close/Open-Zyklen mehr
- ✅ **Bessere UX** - User sieht Preview-Updates sofort im Overlay-Header
- ✅ **Keine verlorenen Werte** - Auch ohne "Einsetzen"-Klick werden Werte beim Accept gespeichert
- ✅ **Weniger Code-Duplikation** - `saveCurrentEditorValueBeforeAccept()` wird von beiden Bereichen genutzt
- ✅ **Sauberere UI** - Kein "+"-Button mehr, der verwirren könnte
- ✅ **Konsistente Vorauswahl** - "Würfel" erscheint korrekt in shape-Quick-Select

**HTML-Struktur-Unterschiede:**

| Element | Step Creator (IDs) | Probability Area (Klassen) |
|---------|-------------------|---------------------------|
| Fraction Row | `<div id="FractionPickerRow">` | `<div class="js-fraction-picker-row">` |
| Article Row | `<div id="ArticleBtnRow">` | `<div class="js-article-btn-row">` |
| Pronoun Row | `<div id="PronounBtnRow">` | `<div class="js-pronoun-btn-row">` |
| Value Row | `<div id="ValueBtnRow">` | `<div class="js-value-btn-row">` |

**Selector-Logik im Handler:**
```javascript
if (mode === "article") {
  row = useClassBasedIds
    ? host.querySelector(".js-article-btn-row")   // Probability Area
    : host.querySelector("#ArticleBtnRow");        // Step Creator
}
```

#### Variablen-Lookup

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `normalizeVarKey(value)` | 209 | Variable normalisieren (mit Alias-Map) |
| `normalizeOptionValue(value)` | 219 | Optionswert normalisieren |
| `isSemanticTag(tag)` | 225 | Prüft ob Tag semantisch ist |
| `getStepById(masterId)` | 230 | Step per ID finden |
| `getStepSelectionTags(masterId)` | 235 | Selection-Tags für Step |
| `getComparableTags(rawTags)` | 254 | Vergleichbare Tag-Varianten |
| `getVariableCatalogEntry(varName)` | 268 | Katalog-Eintrag für Variable |
| `getLocalizedOptionLabel(option)` | 280 | Lokalisiertes Label |
| `getCatalogOptionEntries(varName)` | 291 | Optionen für Variable |
| `filterOptionEntriesByStepTags(entries, stepTags)` | 302 | Optionen nach Tags filtern |
| `getStepAction(masterId)` | 324 | Action-Label für Step |

#### Scoring & Ranking

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getOptionRulesContext(masterId)` | 329 | Kontext für Optionsregeln |
| `buildCurrentScoringContext()` | 344 | Aktuellen Scoring-Kontext aufbauen |
| `detectIngredientFamily()` | 352 | Zutat-Familie erkennen |
| `getRankedVarOptionEntries(varName, ...)` | 382 | Gerankte Optionen |
| `getRawVarOptions(varName, ...)` | 505 | Rohe Optionsliste |
| `getVarOptions(varName, ...)` | 510 | Verarbeitete Optionsliste |
| `getArticleOptions()` | 526 | **UPDATED:** Artikel-Optionen aus Katalog, fügt automatisch "des" (Genitiv) hinzu falls nicht vorhanden. Standard: ["ohne", "der", "die", "das", "des"] |
| `getDurationUnits()` | 532 | Dauer-Einheiten |

#### Template-Rendering

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getTemplateVariables(templateRaw)` | 546 | Variablen aus Template extrahieren |
| `resolveOptionalTemplateSegments(templateRaw, values)` | 556 | Optionale Segmente auflösen |
| `buildOptionalSegmentButtons(templateRaw, values)` | 576 | Optional-Buttons bauen |
| `renderTemplateTokens(templateRaw, stepId, values)` | 611 | Variablen-Tokens rendern |
| `renderTemplate(templateRaw, stepId, values)` | 643 | Komplettes Template rendern |
| `renderTemplateWithConfig(config)` | 700 | Template mit Config rendern |
| `renderAssignedPlaceholderTemplate(config)` | 766 | Template mit Zuweisungen rendern |
| `snippetPreview(templateRaw, values, maxLen)` | 872 | Kurze Vorschau erstellen |

#### Text-Verarbeitung

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `splitLeadingArticleByOptions(text, articleOptions)` | 796 | Artikel von Nomen trennen |
| `composeArticleAndNoun(article, noun)` | 809 | Artikel + Nomen zusammensetzen |
| `getNounOptionsFromValues(values, articleOptions)` | 816 | Nomen-Optionen ableiten |
| `splitEditorPrefillValue(value, config)` | 823 | Wert für Editor-Prefill aufteilen |

#### Step-Button-Rendering

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getPhaseMeta(phase)` | 892 | Phasen-Metadaten (Icon + Titel, mehrsprachig) |
| `getSubGroupMeta(subGroup)` | 904 | Untergruppen-Metadaten |
| `renderStepButtons()` | 919 | Step-Auswahl-Buttons rendern |
| `renderMasterText()` | ~960 | Aktuellen Step-Text rendern |

#### Zutaten-Auswahl

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getSelectedIngredientNamesFromPage()` | 1076 | Zutat-Namen von Seite lesen |
| `getSpecialTransformChipsForStep(masterId)` | 1084 | Spezial-Transform-Chips holen |
| `hasMatchingIngredientForTransform(items, t)` | 1101 | Prüft ob Zutat zum Transform passt |
| `getDerivedChipsFromAcceptedSteps()` | 1110 | Abgeleitete Chips aus akzeptierten Steps |
| `getSelectedIngredientsFromPage()` | 1139 | ALLE ausgewählten Zutaten (inkl. Remainder, Genus-Daten für Auto-Artikel) |
| `parseSelectedIngredientValues(rawValue, options)` | 1193 | Zutat-Werte parsen (backward compatible) |
| `formatSelectedIngredientList(names, langKey)` | 1296 | **UPDATED:** Zutaten-Liste formatieren ("X und Y"). Unterstützt per-ingredient articles: Verwendet `item.article` wenn gesetzt, sonst auto-detect aus genus. Rendert Fraktionen mit individuellen Artikeln (z.B. "die Hälfte des Mehls und zwei Drittel der Butter"). Backward compatible mit String-Arrays |

#### Variablen-Typ-Prüfung

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `isIngredientVariable(varName)` | 1051 | Prüft: ingredient, ingredient2, ingredients, liquid, fat, seasonings, marinade, thickener, components, extra. **Nicht:** base, dough (zeigen Options-Chips). **Hybrid:** extra zeigt Ingredient-Chips UND Options-Chips |
| `isHybridIngredientVariable(varName)` | 1056 | Prüft: extra. Hybrid-Variablen zeigen sowohl Zutaten-Chips als auch Options-Chips aus master_step_variables.json. Klick auf Option deselektiert Zutaten und umgekehrt |
| `filterIngredientsByVarType(items, varName, masterId)` | ~1058 | Filtert Zutaten nach Variable-Typ und Step-Kontext. Variable-Filter: liquid→isLiquid, fat→isFat, seasonings→GroupId 5, thickener→GroupId 8. Step-Filter: PREP_CUT_01/GRATE_01/MINCE_01/PEEL_01 + ingredient→isHard\|\|isSoft. Fallback auf alle Items wenn keine Matches |
| `isGrindSizeVariable(varName)` | 1055 | Prüft: grind_size |
| `isNoArticleVariable(varName)` | 1060 | Prüft: state, duration, count, mode, component, pronoun, pronoun2, pronomen, shape, finish, marinade, method, thickener, action, grindsize |
| `isStateVariable(varName)` | 1064 | Prüft: state |
| `isCompactSpecialVariable(varName)` | 1071 | Prüft: duration, temp, count |

#### Inline-Editor

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `preserveWindowScroll(callback)` | 1038 | Scroll-Position bewahren |
| `closeInlineEditor()` | 1044 | Editor schließen |
| `openInlineEditor(varName, tokenId)` | 1229 | Editor öffnen (Haupt-Funktion!) |
| `renderPillButtons(list, mode, currentVal)` | 1344 | Pill-Buttons rendern |
| `renderFallbackSection(varName, options, ...)` | 1327 | "Weitere anzeigen"-Bereich |
| `renderSpecialEditor(varName, currentVal)` | 1418 | Spezial-Editor (duration/temp/count). Duration-Editor hat Von-Bis-Range-Felder (`DurationValueInput` + `DurationValueToInput`). Layout: Row 1 = Inputs, Row 2 = Unit-Chips (Minute/Stunde/Pro Packung), Row 3 = Actions (Einsetzen/Schließen). Parst Range-Werte wie "8-10" beim Öffnen. Output: "8-10 Minuten" wenn Bis-Feld gefüllt, sonst "10 Minuten". State-Editor zeigt Pronomen immer oben (wie Artikel), auch bei separatem `{{pronoun}}`-Token |

#### Wert-Anwendung

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `extractLeadingNumber(text)` | 1443 | Führende Zahl extrahieren |
| `applyCurrentEditorSelection()` | 1451 | Editor-Auswahl anwenden (inkl. Fraction!) |
| `rerenderAfterValueSet()` | 1568 | Nach Wert-Setzung neu rendern |

#### Step-Referenz

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getRenderedTextForLang(step, lang)` | 1578 | Gerenderter Text für Sprache |
| `resolveAcceptedIngredientName()` | 1596 | Zutat-Name für akzeptierten Step |
| `slugifyStableKey(value)` | 1612 | Stabilen Slug erstellen |
| `normalizeToArray(value)` | 1623 | Zu Array normalisieren |
| `getStableOptionReference(varName, value)` | 1630 | Stabile Referenz für Option |
| `buildStableStepReference(step)` | 1669 | Komplette stabile Step-Referenz |

#### Step-Annahme & Events

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `acceptActiveStep()` | 1706 | Aktiven Step akzeptieren → `window.addStep()` aufrufen |
| `onStepCardClick(btn)` | 1753 | Step-Karte angeklickt |
| `setActiveButton(stepId)` | 1772 | Aktiven Button markieren |
| `wireEvents()` | 1778 | ALLE Event-Listener binden |

#### Exportierte Probability-Helpers (Zeile 2019–2180, 2819–2837)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `buildInlineEditorHtml(varName, currentVal, opts)` | 2021 | **UPDATED:** Editor-HTML generieren (für Probability-View). Wrapper um `_generateEditorHtml()` mit `useClassBasedIds: true`. Unterstützt `multiIngredients` Array in opts |
| `applyEditorValue(editorEl)` | 2089 | **UPDATED:** Editor-Wert auslesen und zusammensetzen. Wendet globale Fraction + Artikel auf alle Ingredient-Items an (Zeile 2757–2784) |
| `applyEditorExtras(editorEl)` | 2136 | Zusätzliche Werte (z.B. Pronomen) |
| `triggerPronounBeforeState(config)` | 2148 | Pronomen-vor-State-Logik |
| `handlePickModeClick(pickBtn, host, useClassBasedIds)` | 2819 | **NEU:** Shared Pick-Mode Handler (siehe "Shared Editor System") |
| `handleDurationUnitClick(btn, host)` | 2819 | **NEU:** Shared Duration-Unit Handler (siehe "Shared Editor System") |
| `normalizeIngredientValues(values)` | 2819 | **NEU:** Normalisiert Zutat-Array zu `{name, fraction, article}` Format |
| `formatSelectedIngredientList(names, langKey)` | 2819 | **NEU:** Formatiert Multi-Ingredient-Liste mit individuellen Artikeln/Fraktionen |
| `isIngredientVariable(varName)` | 2819 | **NEU:** Prüft ob Variable eine Zutat ist (ingredient, liquid, fat, etc.) |

#### Zweites IIFE: Format/Chip-Helpers (Zeile 2182–2253)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `formatIngredientList(names, langKey)` | 2185 | Zutatenliste formatieren |
| `buildIngredientChipsHtml(ingredients, selectedNames, langKey)` | 2200 | Chip-HTML bauen |
| `resolveIngredientInsertValue(selectedNames, langKey, fallback)` | 2220 | Einsetzwert auflösen |

---

## 20. JS-Datei: CreatePostingPage.js

**Pfad:** `wwwroot/js/CreatePostingPage.js` · **5.112 Zeilen**
**Pattern:** jQuery Document Ready IIFE
**Haupt-Einstiegspunkt** der gesamten CreatePosting-Seite

### Draft & Storage (Zeile 19–200)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getCookieValue(name)` | 19 | Cookie-Wert lesen |
| `withCreatePostingDraftStorage(action, fallback)` | 38 | localStorage-Wrapper mit Error-Handling |
| `readCreatePostingDraft()` | 47 | Draft aus localStorage lesen |
| `writeCreatePostingDraft(draft)` | 56 | Draft in localStorage speichern |
| `clearCreatePostingDraft()` | 63 | Draft löschen |
| `clearCreatePostingDraftResetCookie()` | 69 | Reset-Cookie löschen |
| `consumeCreatePostingDraftResetFlag()` | 73 | Reset-Flag prüfen und löschen |
| `syncSelectedKeywordButtonStates()` | 80 | Keyword-Button-States synchronisieren |
| `captureCreatePostingDraft()` | 93 | Kompletten Form-State als Draft erfassen |
| `persistCreatePostingDraftNow()` | 111 | Draft sofort speichern |
| `scheduleCreatePostingDraftSave(delay)` | 116 | Draft-Save debounced (180ms) |
| `startCreatePostingFresh()` | 122 | Neu starten (Draft löschen + Reload) |
| `hasMeaningfulCreatePostingDraft(draft)` | 129 | Prüft ob Draft Inhalt hat |
| `restoreCreatePostingDraft()` | 144 | Draft wiederherstellen |
| `showDraftRestoreBannerIfNeeded()` | 178 | Zeigt Draft-Restore-Banner wenn Draft vorhanden |

### Maßeinheiten (Zeile 208–260)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getAvailableUnits()` | 208 | Unit-Liste (mit Fallback) |
| `findUnitByDe(unitDe)` | 216 | Unit per deutschem Namen finden |
| `getUnitLabel(unitObj, langKey)` | 224 | Lokalisiertes Unit-Label |
| `buildUnitOptionsHtml(selectedDe)` | 242 | HTML `<select>`-Optionen generieren |

### Zutat-Konfiguration (Zeile 261–327)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `dockIngredientConfigUnder(row)` | 261 | Config-Panel unter Zutat positionieren |
| `openIngredientConfigPopup(row)` | 278 | Zutaten-Mengen/Einheit-Editor öffnen |
| `closeIngredientConfigPopup()` | 291 | Config-Popup schließen |
| `applyIngredientConfigPopup()` | 298 | Config-Änderungen speichern |

### Probability Variable Editor (Zeile 329–652)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `renderProbVarIngredientArticleOptions()` | 329 | Artikel-Auswahl-Chips rendern |
| `openProbVarEditor(masterId, varKey, ...)` | 353 | Variablen-Editor-Modal öffnen. Speichert `probVarEditorCurrentVarKey` für Zutatenfilterung (liquid→nur isLiquid, fat→nur isFat, components→alle) |
| `closeProbVarEditor()` | 442 | Editor schließen |
| `applyProbVarEditor()` | 452 | Ausgewählten Wert anwenden |
| `openProbVarInlineEditor(masterId, varKey, ...)` | 486 | Inline-Editor in Card öffnen. Setzt `.editing` auf `.probability-template-wrap` (Glow-Effekt) |
| `doApply()` | 689 | **UPDATED (2026-03-27):** "Einsetzen"-Logik für Inline-Editor. **BUGFIX 1:** Re-fetched Editor-Element am Anfang (Zeile 693-698) um stale Referenzen zu vermeiden. **BUGFIX 2:** Formatiert `val` für ingredient-Variablen (Zeile 816) mit `formatSelectedIngredientList()` bevor `onApply` aufgerufen wird. Prüft ob Multi-Ingredients existieren UND neue Selektion vorhanden → fügt zur Liste hinzu |

### Probability Area - Objekt-basierte Architektur (VOLLSTÄNDIG UMGESTELLT 2026-03-27)

**Status: ✅ 100% IDENTISCH MIT SMART STEP CREATOR**

**BREAKING CHANGE (2026-03-27):** Probability Area wurde vollständig von DOM-basiert auf objekt-basiert umgestellt:

**Alte Architektur (❌ entfernt):**
```javascript
// DOM als Single Source of Truth
token.text(newVal);  // Wert direkt im DOM gespeichert
```

**Neue Architektur (✅ aktiv):**
```javascript
// Object als Single Source of Truth
window.probabilityStates = {
  'PREP_MIX_DRY_01': {
    masterId: 'PREP_MIX_DRY_01',
    templateRaw: '{{action}} {{ingredient}} {{removal}} {{pronoun}}',
    values: { ingredient: 'die Hälfte der Pasta', pronoun: 'sie' },
    _multiIngredients: { ingredient: [{name: 'Pasta', fraction: '1/2', article: 'der'}] }
  }
}

// DOM wird aus Object gerendert (wie Smart Step Creator!)
renderProbabilityTemplate('PREP_MIX_DRY_01');
```

**Neue Funktionen:**

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `extractTemplateRaw($container)` | ~4046 | Extrahiert Template-String aus DOM. Ersetzt Tokens mit `{{varName}}` Placeholders. Entfernt Reset-Buttons. Beispiel: `"{{action}} {{ingredient}}"` |
| `renderProbabilityTemplate(masterId)` | ~4070 | **Render-Funktion analog zu renderMasterText()!** Holt State aus `probabilityStates[masterId]`, ruft `helpers.renderTemplateWithConfig()` mit `tokenExtraClasses: 'js-probability-var'` auf, aktualisiert DOM. **KRITISCH:** Ohne `tokenExtraClasses` funktioniert Token-Click nicht! |
| Token Click Handler | ~4103 | Initialisiert State falls nicht vorhanden, holt `currentVal` aus `prob.values[varKey]`, öffnet Unified Overlay via `helpers.openUniversalVariableEditor()` |
| Template Click Handler | ~4172 | Initialisiert State-Object beim Template-Select, extrahiert `templateRaw` via `extractTemplateRaw()` |
| Reset Button Handler | ~4188 | Löscht `prob.values[varName]` und `prob._multiIngredients[varName]`, ruft `renderProbabilityTemplate()` auf |

**Kritischer CSS-Klassen-Fix (2026-03-27):**
```javascript
// PROBLEM: Nach Re-render hatten Tokens nicht mehr die Klasse .js-probability-var
// → Event-Handler funktionierte nicht mehr!

// LÖSUNG: renderTemplateWithConfig mit tokenExtraClasses aufrufen
renderTemplateWithConfig(prob.templateRaw, masterId, prob.values, {
  tokenExtraClasses: 'js-probability-var',  // ← OHNE DIESE KLASSE FUNKTIONIERT NICHTS!
  includeVarKey: true,
  enablePlusButtons: true,
  multiIngredientsData: prob._multiIngredients || {}
})
```

**LIVE Preview-Updates im Overlay-Header:**
```javascript
// VORHER (statisch): Preview nur einmal geholt
previewHtml = context.templatePreviewHtml || "";

// NACHHER (LIVE): Preview immer aktuell aus DOM
const card = document.querySelector(`.probability-template-wrap[data-master-id="${masterId}"] .probability-template-card`);
previewHtml = card.innerHTML;  // Zeigt aktuelle Werte!
```

**Vorteile der neuen Architektur:**
- ✅ **100% identische Logik** mit Smart Step Creator
- ✅ Single Source of Truth: Object statt DOM
- ✅ State bleibt erhalten bei Template-Wechseln
- ✅ Multi-Ingredient-Workflow funktioniert identisch
- ✅ Preview im Overlay zeigt immer aktuelle Werte
- ✅ Einfacheres Debugging (State in Object sichtbar)

**Migration-Info:**
- Alte DOM-basierte Logik vollständig ersetzt
- `window.ProbabilityMultiIngredients` → `probabilityStates[masterId]._multiIngredients`
- Alle Token-Updates via `updateContextValue()` → `renderProbabilityTemplate()`
- Auskommentierter alter Code kann gelöscht werden

### Probability Area Multi-Ingredient Support (NEU 2026-03-27)

**Event-Handler:**

| Event | Selektor | Zeile | Beschreibung |
|-------|----------|-------|-------------|
| click | `.js-probability-var` | 4363 | **NEU (2026-03-27):** Öffnet Editor für Probability-Variable. Ruft `e.stopPropagation()` auf um Template-Akzeptierung zu verhindern. Erstellt `onApply` Callback der Token-Text aktualisiert: `token.text(newVal \|\| varKey)` |
| click | `.js-probability-template` | 4395 | Template-Click akzeptiert ganzen Step (nur wenn nicht auf Token geklickt) |
| click | `.probability-template-wrap .ingredient-plus-btn` | 4412 | **Plus-Button im Template**: (1) Editor geschlossen → öffnet Editor direkt via `openProbVarInlineEditor()` (NICHT token.click() um Template-Akzeptierung zu vermeiden!). (2) Editor offen → fügt aktuell ausgewählte Zutat zur `window.ProbabilityMultiIngredients[masterId][varName]`-Liste hinzu und öffnet Editor neu |
| click | `.prob-inline-editor-host button[data-remove-multi-ingredient]` | 4506 | **Remove-Chip im Probability Editor**: Entfernt Zutat an Index aus `window.ProbabilityMultiIngredients[masterId][varName]`-Array, formatiert neue Liste mit `helpers.formatSelectedIngredientList()`, öffnet Editor neu |
| click | `button[data-pick-mode]` (in #universalEditorOverlay) | 845 | **Pick-Mode-Handler**: **BUGFIX 1:** Re-fetched Editor-Element statt stale Referenz. **BUGFIX 2:** `.off('.universal')` entfernt Smart Step Creator Handler um doppelte Events zu vermeiden (Zeile 839). Nutzt `helpers.handlePickModeClick(this, currentEditorEl, true)` |
| click | `button[data-duration-unit]` (in #universalEditorOverlay) | 849 | **Duration-Unit-Handler**: **BUGFIX:** Re-fetched Editor-Element statt stale Referenz. Nutzt `helpers.handleDurationUnitClick(this, currentEditorEl)` - shared Handler |

**Workflow identisch zum Step Creator:**
1. User klickt Token → Editor öffnet
2. User wählt Fraction + Artikel + Zutat → "Einsetzen"
3. Plus-Button [+] erscheint im Template
4. User klickt [+] → Editor öffnet wieder
5. Bereits hinzugefügte Zutaten sind ausgegraut (`.ingredient-chip-disabled`)
6. User fügt weitere Zutat hinzu → "die Hälfte der Pasta und ein Drittel des Mehls"
7. Chips mit X-Button am Boden des Editors zum Entfernen
8. User klickt Template-Card → `getMergedVars()` überträgt ALLE Multi-Ingredients korrekt

**Plus-Button Handler (Zeile 4199-4254) - Wichtige Details:**
```javascript
// Editor geschlossen? → Direkt öffnen (NICHT token.click()!)
if (!editorEl || $host.hasClass('d-none') || $host.html().trim() === '') {
    openProbVarInlineEditor(masterId, varName, currentVal, dummyOnApply, token);
    return;
}

// Editor offen → Zutat zur Liste hinzufügen
const existingList = (window.ProbabilityMultiIngredients[masterId] || {})[varName] || [];
normalized.forEach(item => {
    item.article = article;
    item.fraction = fraction;
    existingList.push(item);
});
window.ProbabilityMultiIngredients[masterId][varName] = existingList;

// Editor neu öffnen mit aktualisierter Liste
openProbVarInlineEditor(masterId, varName, composed, dummyOnApply, token);
```

**Remove-Chip Handler (Zeile 4319-4377) - Wichtige Details:**
```javascript
// Index aus data-Attribut lesen
const idx = parseInt(btn.dataset.removeMultiIngredient, 10);

// Item entfernen
const existingList = (window.ProbabilityMultiIngredients[masterId] || {})[varName] || [];
existingList.splice(idx, 1);
window.ProbabilityMultiIngredients[masterId][varName] = existingList;

// Neue Liste formatieren
const composed = helpers.formatSelectedIngredientList(existingList, currentLang || 'de');

// Editor neu öffnen
openProbVarInlineEditor(masterId, varName, composed, onApply, token);
```

### Zutaten-Laden & Grammatik (Zeile 654–764)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `loadIngredientArticleRules()` | 654 | Artikel-Regeln async laden |
| `loadIngredientTransforms()` | 662 | Transform-Definitionen async laden |
| `normalizeGenusKey(key)` | 671 | Genus normalisieren (m/f/n) |
| `applyArticleToName(name, genus)` | 680 | Artikel vor Zutat setzen |
| `getAllStepRows()` | 699 | Alle Step-Katalog-Zeilen holen |
| `getPhaseLabel(phase)` | 706 | HTML-Badge für Koch-Phase (1–4) |
| `getStepBoundIngredientIds(stepId)` | 720 | An Step gebundene Zutat-IDs |
| `getSelectedIngredientIds()` | 726 | IDs ausgewählter Zutaten |
| `stripLeadingArticle(text)` | 745 | Artikel-Prefix entfernen |
| `normalizeIngredientMatchValue(text)` | 755 | Text für Zutat-Matching normalisieren |
| `splitIngredientNames(text)` | 759 | Komma/Konjunktion-getrennte Namen aufteilen |

### Zutat-Derivation & Transforms (Zeile 767–1185)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getBaseSandboxIngredients()` | 767 | Basis-Zutaten (nicht abgeleitet). Liefert `namesByLang`, `genusByLang`, `groupId`, `isLiquid`, `isFat`, `isHard`, `isSoft` |
| `buildDerivedIngredientName(base, adjective)` | 810 | Abgeleitete Adjektiv-Form erstellen |
| `getStepVariableDisplayValue(step, varName)` | 829 | Variablenwert aus Step-Metadaten |
| `resolveCutTransformationType(step)` | 835 | Schnitt-Form aus Step-Variablen erkennen |
| `matchesWholeWord(name, term)` | ~926 | Wortgrenzen-Match per Regex. Verhindert false-positives z.B. "ei" in "Rindfleisch" |
| `matchSandboxItemsByStep(items, step)` | 849 | Zutaten zu Step-Variablen zuordnen |
| `buildSpecialTransformItems(step)` | 877 | Spezial-Transform-Outputs erstellen |
| `collectSelectedStepDerivationDescriptors()` | 893 | Alle Step-Transform-Descriptors sammeln |
| `deriveIngredientsForSteps(baseItems, ...)` | 937 | Abgeleitete Zutaten generieren. Nutzt `matchesWholeWord()` für Wortgrenzen-Matching. **Reihenfolge:** 1) auto_show-Transforms VOR descriptor-Loop (z.B. Ei → Eiklar/Eigelb), damit Sub-Zutaten für nachfolgende trigger_step-Transforms verfügbar sind (z.B. Eiklar → Eischnee). 2) Special transforms: Outputs werden nur EINMAL pro Transform generiert (nicht pro matchendem Target), alle matchenden Targets werden aber entfernt. 3) Adjective transforms. |
| `deriveSandboxIngredients()` | 998 | Wrapper für Zutat-Derivation |
| `getSelectedIngredientsForSandbox(langKey)` | 1005 | Ausgewählte Zutaten für Sandbox |
| `localizeIngredientValueForSandbox(value, langKey, ...)` | 1020 | Zutat-Wert lokalisieren |
| `findCatalogIngredientRowByNames(names)` | 1047 | Zutat-Zeile per Name finden |
| `setIngredientRowDisabled(row, disabled)` | 1063 | Zutat-Zeile deaktivieren/aktivieren |
| `buildDerivedIngredientRowHtml(item)` | 1067 | HTML für abgeleitete Zutat-Zeile |
| `applyDerivedIngredientRowVisuals()` | 1119 | Alle abgeleiteten Zutat-Rows aktualisieren. Auto-show-Items erhalten `.ingredient-optional-sub` CSS-Klasse (gestrichelte lila Umrandung, eingerückt, ↳ Prefix) |

### Theme-Management (Zeile 1186–1291)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getStoredProfileTheme()` | 1186 | Gespeichertes Theme aus localStorage |
| `normalizeCreatePostingTheme(theme)` | 1194 | Theme-Name validieren/normalisieren |
| `getVisualCreatePostingTheme(theme)` | 1203 | Visuelles Theme-Variante (light/dark) |
| `readStoredCreatePostingTheme()` | 1208 | Gespeichertes Theme lesen |
| `syncCreatePostingThemeToggleState()` | 1247 | UI-Buttons an Theme anpassen |
| `applyCurrentThemeAttributes()` | 1263 | Theme-CSS auf DOM anwenden |

**Window-Exports:**
```javascript
window.CreatePostingCurrentTheme      // Zeile 1226
window.CreatePostingThemeMode         // Zeile 1227
window.getCreatePostingTheme()        // Zeile 1235
window.getCreatePostingVisualTheme()  // Zeile 1239
window.getThemeMutedTextClass()       // Zeile 1243
window.setCreatePostingTheme(theme)   // Zeile 1284
```

### Grammatik & Variablen-Auflösung (Zeile 1382–1655)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `resolveGrammarForIngredient(ingredientName)` | 1382 | Genus/Artikel für Zutat bestimmen |
| `getPlaceholderKeysFromTemplate(template)` | 1423 | `{{variable}}`-Namen extrahieren |
| `getSelectedIngredientNames()` | 1434 | Namen ausgewählter Zutaten |
| `getSelectedIngredientNamesWithArticle()` | 1443 | Namen mit Artikeln |
| `getSelectedIngredientForCreator()` | 1460 | Zutat-Wert für Step-Creator |
| `getSelectedIngredientValueForInsert()` | 1477 | Wert für Template-Einsetzung |
| `prioritizeSelectedIngredient(ingredientName)` | 1491 | Zutat als primär setzen |
| `assignIngredientPlaceholderValue(tokenId, value)` | 1502 | Zutat an Token binden |
| `syncGrammarAssignmentsForSelectedIngredient()` | 1518 | Genus über alle Zuweisungen synchronisieren |
| `getEffectiveTemplateId()` | 1540 | Aktive Template-ID |
| `normalizePlaceholderKey(key)` | 1544 | Variable normalisieren |
| `getPlaceholderType(key)` | 1567 | Variablen-Typ bestimmen via `placeholderTypeMatchers`: ingredient (ingredient, ingredients, liquid, fat, components), duration, count, pronoun, temperature |
| `getJsonVariableOptions(varName)` | 1573 | Optionsliste aus JSON |
| `getFirstJsonVariableOption(varName)` | 1588 | Erste gültige Option |
| `getPlaceholderKeyByTokenId(tokenId)` | 1635 | Reverse-Lookup: tokenId → Variable |
| `applyTokenAssignmentsToVariables()` | 1640 | Alle Token-Zuweisungen anwenden |

### Spezial-Editoren (Zeile 1657–2530)

#### Duration (Zeile 1657)
| Funktion | Beschreibung |
|----------|-------------|
| `getDurationUnitLabel(unit)` | Unit-Label (min/hour/per_package) |
| `getDurationInsertText(value, unit)` | Duration formatieren |
| `refreshDurationUnitControls()` | Duration-UI aktualisieren |
| `openDurationEditorForToken(tokenId)` | Duration-Editor öffnen |
| `closeDurationEditor()` | Duration-Editor schließen |

#### Count (Zeile 1717)
| Funktion | Beschreibung |
|----------|-------------|
| `getCountInsertText(value)` | Count formatieren |
| `openCountEditorForToken(tokenId)` | Count-Editor öffnen |
| `closeCountEditor()` | Count-Editor schließen |

#### Temperature (Zeile 1738)
| Funktion | Beschreibung |
|----------|-------------|
| `getTemperatureInsertText(value, unit)` | Temperatur formatieren (°C/°F) |
| `openTemperatureEditorForToken(tokenId)` | Temperatur-Editor öffnen |
| `closeTemperatureEditor()` | Temperatur-Editor schließen |

#### Weitere Variablen-Editoren

| Variable | Open-Funktion | Zeile | Options-Funktion |
|----------|---------------|-------|------------------|
| Heat | `openHeatEditorForToken()` | 1780 | `getHeatOptions()` |
| Mode | `openModeEditorForToken()` | 1802 | `getModeOptions()` |
| Pronoun | `openPronounEditorForToken()` | 1823 | – |
| Equipment | `openEquipmentEditorForToken()` | 2030 | `getEquipmentOptions()` |
| State | `openStateEditorForToken()` | 2063 | `getStateOptions()` |
| Tool | `openToolEditorForToken()` | 2100 | `getToolOptions()` |
| Grind Size | `openGrindSizeEditorForToken()` | 2132 | `getGrindSizeOptions()` |
| Shape | `openShapeEditorForToken()` | 2166 | `getShapeOptions()` |
| Base | `openBaseEditorForToken()` | 2199 | `getBaseOptions()` |
| Item | `openItemEditorForToken()` | 2230 | `getItemOptions()` |
| Balance | `openBalanceEditorForToken()` | 2263 | `getBalanceOptions()` |
| Seasonings | `openSeasoningsEditorForToken()` | 2296 | `getSeasoningsOptions()` |

Jeder Editor folgt dem gleichen Pattern:
```
openXxxEditorForToken(tokenId)  → öffnet UI
closeXxxEditor()                → schließt UI
getXxxOptions()                 → liefert Optionsliste
renderInlineXxxOptions()        → rendert Inline-Buttons
```

### Artikel-Auswahl-System (Zeile 1847–2060)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `ensureEquipmentArticle(name)` | 1847 | Artikel zu Equipment hinzufügen |
| `getEditorArticleOptions()` | 1912 | Artikel-Optionen für Editor |
| `splitLeadingArticle(text)` | 1916 | Führenden Artikel abtrennen |
| `composeArticleAndNoun(article, noun)` | 1929 | Artikel + Nomen zusammensetzen |
| `getNounOptions(values, articleOptions)` | 1939 | Nomen-Optionen aus Werten |
| `renderArticleOptions(options)` | 1949 | Artikel-Chips rendern |
| `buildArticleChoiceSelectionState()` | 1978 | Auswahl-State für Artikel aufbauen |
| `renderArticleChoiceOptions(options)` | 2006 | Artikel-Choice-UI rendern |

### Template & Vorschau (Zeile 2530–2765)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `animatePreview()` | 2530 | Vorschau animieren |
| `updatePreviewText()` | 2536 | Master-Step-Vorschau aktualisieren |
| `clearSelectedIngredientChips()` | 2562 | Zutat-Chip-Auswahl leeren |
| `updateStoryProgress()` | 2567 | Fortschritts-Zähler aktualisieren |
| `showCreatorToast(msg)` | 2573 | Toast-Nachricht anzeigen |
| `animateTemplateToPreview()` | 2582 | Template → Vorschau animieren |
| `renderIngredientChips()` | 2589 | Zutat-Chips rendern |
| `renderTemplateCards()` | 2625 | Template-Karten rendern |
| `showMasterStepCreatorError(msg)` | 2636 | Fehler anzeigen |
| `refreshMasterTemplateBuilder()` | 2649 | Template-Builder komplett erneuern |
| `collectMasterVariables()` | 2666 | Alle Template-Variablen sammeln |
| `normalizeVariablesForPersist()` | 2672 | Variablen für Speicherung normalisieren |
| `resolveOptionalTemplateSegmentsForPersist()` | 2703 | Optionale Segmente für Speicherung |
| `renderTemplateForPersist(template, vars)` | 2724 | Template für Persistierung rendern |
| `buildRenderedPayloadFromTemplate()` | 2746 | Kompletten Step-Payload bauen |

### Server-Integration (Zeile 2753–2830)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getCurrentUserHashForRequests()` | 2753 | User-Hash für Requests |
| `createFallbackStepId()` | 2765 | Fallback Step-ID generieren |
| `addRenderedMasterStep()` | 2770 | Gerenderten Step zum Form hinzufügen |

### Zutat-Suche & Sichtbarkeit (Zeile 2833–2880)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `normalizeSearchText(text)` | 2833 | Text für Suche normalisieren |
| `syncIngredientSourceVisibility()` | 2845 | Zutaten nach Suche ein-/ausblenden |
| `isStepAlreadySelected(id)` | 2862 | Prüft ob Step schon ausgewählt |

### Sprach-Aktualisierung (Zeile 2879–3010)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getCommonUnitChipLabel(unit, langKey)` | 2879 | Unit-Chip-Label |
| `resolveSelectUnitValueByKey(key)` | 2884 | Unit per Key auflösen |
| `updateCommonUnitChipLabels()` | 2900 | Unit-Chip-Labels aktualisieren |
| `updateLanguageLabels()` | 2982 | Alle Sprach-Labels aktualisieren |

### Video/Media (Zeile 3011–3046)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `handleVideoUpload(event)` | 3011 | Video/Bild-Upload verarbeiten |
| `resetVideo()` | 3034 | Video-Upload zurücksetzen |

### Daten-Formatierung (Zeile 3047–3110)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `normalizeDecimalInputValue(input)` | 3047 | Dezimal normalisieren (,→.) |
| `sanitizeQuantityInputValue(input)` | 3052 | Nicht-numerisch entfernen |
| `escapeAttr(value)` | 3070 | HTML-Attribut escapen |
| `getLocalizedIngredientData(row)` | 3074 | Zutat-Daten lokalisiert (names, genus, groupId, isLiquid, isFat, isHard, isSoft) |
| `buildIngredientDataAttributes(data)` | 3084 | Data-Attribute aus lokalen Daten (inkl. data-group-id, data-is-liquid, data-is-fat, data-is-hard, data-is-soft) |
| `buildIngredientRowHtml(name, icon, ...)` | 3090 | Zutat-Zeile HTML generieren |

### Zutat-Verwaltung (Zeile 3112–3388)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `addIngredient(id, btn)` | 3112 | Zutat zur Auswahl hinzufügen |
| `indexOfIgnoreCase(text, search)` | 3151 | Case-insensitive Suche |
| `buildIngredientTokenTemplate(text, ingredientName)` | 3160 | Template mit Zutat-Token |
| `materializeStepTextFromTemplate(template, ingredientName)` | 3181 | Token durch echte Zutat ersetzen |
| `renderStepTextWithIngredientToken(template, name, id)` | 3189 | Step mit klickbarem Zutat-Token |
| `ensureStepLanguageTemplate(row, lang)` | 3213 | Template für Sprache sicherstellen |
| `startStepIngredientEdit(event, btn)` | 3231 | Zutat in Step bearbeiten starten |
| `applyChipToStep(row, ingredientRow)` | 3245 | Ausgewählte Zutat auf Step anwenden |
| `cancelStepIngredientEdit()` | 3287 | Zutat-Bearbeitung abbrechen |
| `removeIngredientRow(btn)` | 3295 | Zutat aus Auswahl entfernen (schließt offenes Config-Popup) |
| `removeConflictingSteps(id)` | 3304 | Konfligierende Steps entfernen |

### Step-Verwaltung (Zeile 3334–3496)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `slugifyStableStepKey(value)` | 3334 | Stabilen Key-Slug erstellen |
| `normalizeStableKeyArray(value)` | 3345 | Key-Array normalisieren |
| `sanitizeStableReferenceMetadata(stepData, options)` | 3353 | Step-Metadaten bereinigen |
| `addStep(id, btn, manualText, options)` | 3390 | **Step zur Auswahl hinzufügen** |
| `removeStep(btn)` | 3472 | Step entfernen |
| `removeStepsByMasterTemplateId(masterId)` | 3480 | Steps per Template-ID entfernen |

#### `addStep()` — Detail (Zeile 3390)

```
Parameter: id, btn, manualText, options
Options: {
  skipRender: boolean,
  ingredientName: string,
  masterTemplateId: string,
  stepData: { de, en, esp, prt, phase, equipment, masterTemplateId, stableReference },
  fractionData: { ingredientName, fractionKey, numerator, denominator } | null,
  enforceIngredientPhaseUniqueness: boolean
}

Ablauf:
1. Duplikat-Prüfung (isStepAlreadySelected)
2. Optionale Konflikt-Entfernung
3. StepData normalisieren (alle Sprachen)
4. StableReference bereinigen
5. Ingredient-Token-Templates erstellen
6. HTML generieren mit:
   - data-step-id
   - data-step-edited
   - data-master-template-id
   - data-step-reference-json
   - data-ingredient-name
   - data-ingredient-fractions  ← NEU (Fraction-Daten)
   - Hidden inputs für Form-Binding
7. updateStepIndices()
8. applyDerivedIngredientRowVisuals()
9. renderIngredientChips()
10. scheduleCreatePostingDraftSave()
```

### Keyword-Verwaltung (Zeile 3499–3530)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `toggleKeyword(id, btn)` | 3499 | Keyword-Auswahl umschalten |

### Window-Exports (Zeile 2963–2970)

```javascript
window.CreatePostingIngredientHelpers = {
    getSelectedIngredientsForSandbox,
    localizeIngredientValueForSandbox
};

window.MasterStepCreatorHelpers = {
    deriveIngredientsForSteps
};
```

---

## 21. JS-Datei: create-posting-publish-checklist.js

**Pfad:** `wwwroot/js/create-posting-publish-checklist.js` · **307 Zeilen**
**Pattern:** IIFE mit DOMContentLoaded Auto-Init

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `rafThrottle(fn)` | 4 | requestAnimationFrame-Throttle |
| `debounce(fn, delay)` | 16 | Debounce mit Timer |
| `injectStyles()` | 24 | CSS für Toolbar/Checklist injizieren |
| `hasMeaningfulChildren(container)` | 90 | Sichtbare Kinder prüfen |
| `hasMediaSelected(fileInput, imagePreview, videoPreview)` | 94 | Media validieren |
| `initChecklist()` | 110 | Haupt-Initialisierung |
| `buildChecklistState()` | 174 | Completion-Status je Section |
| `scrollToSection(key)` | 184 | Zu Section scrollen |
| `centerChecklistItem(item)` | 196 | Checklist-Item zentrieren |
| `setActiveSection(key)` | 208 | Aktive Section setzen |
| `updateActiveSection()` | 219 | Aktive Section per Scroll aktualisieren |
| `updateChecklist()` | 239 | UI-State + Button-Disabled aktualisieren |

**Getrackte Sections:**

| Key | Section-ID | Label |
|-----|-----------|-------|
| `basics` | `card-basics` | Basis |
| `media` | `card-media` | Media |
| `ingredients` | `card-ingredients` | Zutaten |
| `steps` | `card-steps` | Steps |
| `keywords` | `card-keywords` | Keywords |

**Publish-Bedingung:** Mindestens 4 von 5 Sections müssen "done" sein.

---

## 22. CSS-Datei: create-posting-step-filter.css

**Pfad:** `wwwroot/css/create-posting-step-filter.css` · **247 Zeilen** · **NEU (2026-04-02)**

**Zweck:** Styling für Smart Step Creator Filter UI (Suchfeld + Phase-Tabs)

**Haupt-Komponenten:**

| Klasse | Beschreibung |
|--------|-------------|
| `.step-filter-controls` | Container für Suchfeld + Tabs (flex column, padding, border-radius) |
| `.step-search-wrapper` | Wrapper für Suchfeld |
| `.step-search-input` | Suchfeld (mit Focus-State: border-color, box-shadow) |
| `.phase-tabs` | Flex-Container für Phase-Filter-Tabs |
| `.phase-tab` | Einzelner Phase-Tab (border-radius 999px, hover/active states) |
| `.phase-tab.active` | Aktiver Tab (gradient background, box-shadow) |

**Theme-Support:**

| Theme | data-theme | Beschreibung |
|-------|-----------|-------------|
| Dark (Default) | `data-theme="dark"` | Dunkler Hintergrund, weiße Schrift |
| Light | `data-theme="light"` | Heller Hintergrund, dunkle Schrift |
| Rosa | `data-theme="rosa"` | Rosa Gradient, text-shadow für Kontrast |
| Gold | `data-theme="gold"` | Gold Gradient mit Navy-Blue Inputs |
| Navy | `data-theme="navy"` | Navy Blue mit hellblauem Accent |

**Responsive Breakpoints:**
- `@media (max-width: 576px)` - Mobile: kleinere Tabs, kleineres Suchfeld
- `@media (max-width: 768px)` - Tablet: reduzierte Paddings

**CSS-Variablen (je Theme):**
- `--creator-bg` - Hintergrund-Gradient
- `--creator-border` - Border-Color
- `--creator-text` - Text-Color
- `--creator-accent` - Accent-Color für Active States

**Verwendung in:** `CreatePosting.cshtml` (geladen via `<link rel="stylesheet">`)

---

## 23. Window-API-Übersicht

### Globale Objekte

| Object | Quelle | Beschreibung |
|--------|--------|-------------|
| `window.CreatePostingUtils` | create-posting-utils.js | Kern-Utilities |
| `window.CreatePostingDataUrls` | create-posting-data-urls.js | URL-Registry (frozen) |
| `window.CreatePostingDataStore` | create-posting-data-store.js | Daten-Cache |
| `window.CreatePostingFeedback` | create-posting-feedback.js | Feedback/Toast |
| `window.CreatePostingPageData` | create-posting-page-data.js | Async-Loader |
| `window.CreatePostingTemplateBuilder` | create-posting-template-builder.js | Template-Builder (nur für #masterTemplateCards) |
| `window.CreatePostingStepFilterUI` | create-posting-step-filter-ui.js | **NEU:** Filter-UI für Smart Step Creator |
| `window.MasterStepRenderer` | master-step-renderer.js | Rendering-Engine |
| `window.MasterStepCreatorHelpers` | CreatePostingSmartStepCreator.js | Smart Step Creator + Filter-Logik |
| `window.RecipeStepSuggest` | recipe-step-suggest.js | Rezepttyp-Erkennung |
| `window.CreatePostingProbability` | create-posting-probability.js | Wahrscheinlichkeit |
| `window.IngredientManager` | ingredientmanager.js | Maßeinheiten |
| `window.MasterStepCreatorHelpers` | SmartStepCreator.js | **Step-Creator-Helpers** (siehe unten für vollständige Export-Liste) |
| `window.CreatePostingIngredientHelpers` | CreatePostingPage.js | Zutat-Helpers |

#### window.MasterStepCreatorHelpers - Vollständige Export-Liste (Zeile 2819–2837)

**Template & Rendering:**
- `renderTemplateWithConfig(templateRaw, stepId, values, config)` - Template mit erweiterten Optionen rendern
- `renderAssignedPlaceholderTemplate(templateRaw, stepId, values, config)` - Template mit Zuweisungen rendern
- `getVarDisplayName(varName)` - Mehrsprachiger Anzeigename für Variable

**Text-Verarbeitung:**
- `splitLeadingArticleByOptions(text, articleOptions)` - Artikel von Nomen trennen
- `composeArticleAndNoun(article, noun)` - Artikel + Nomen zusammensetzen
- `getNounOptionsFromValues(values, articleOptions)` - Nomen-Optionen ableiten
- `splitEditorPrefillValue(value, config)` - Wert für Editor-Prefill aufteilen

**Editor-System (Shared zwischen Step Creator + Probability Area):**
- `buildInlineEditorHtml(varName, currentVal, opts)` - **SHARED** HTML für Inline-Editor generieren (Wrapper um `_generateEditorHtml` mit `useClassBasedIds: true`)
- `applyEditorValue(editorEl)` - **SHARED** Editor-Wert auslesen und zusammensetzen
- `applyEditorExtras(editorEl)` - **SHARED** Zusätzliche Werte (z.B. Pronomen)
- `triggerPronounBeforeState(config)` - Pronomen-vor-State-Logik
- `handlePickModeClick(pickBtn, host, useClassBasedIds)` - **NEU (2026-03-27)** Shared Pick-Mode Handler
- `handleDurationUnitClick(btn, host)` - **NEU (2026-03-27)** Shared Duration-Unit Handler

**Multi-Ingredient Helpers:**
- `normalizeIngredientValues(values)` - **NEU (2026-03-27)** Normalisiert Zutat-Array zu `{name, fraction, article}` Format
- `formatSelectedIngredientList(names, langKey)` - **NEU (2026-03-27)** Formatiert Multi-Ingredient-Liste mit individuellen Artikeln/Fraktionen
- `isIngredientVariable(varName)` - **NEU (2026-03-27)** Prüft ob Variable eine Zutat ist

**Siehe Section 19 für vollständige Dokumentation aller exportierten Funktionen.**

### Globale State-Variablen

| Variable | Quelle | Beschreibung |
|----------|--------|-------------|
| `window.CreatePostingCurrentTheme` | CreatePostingPage.js:1226 | Aktuelles Theme |
| `window.CreatePostingThemeMode` | CreatePostingPage.js:1227 | Theme-Mode-Funktion |
| `window.ingredientTransforms` | CreatePostingPage.js:665 | Gecachte Transforms |
| `window.stepCatalog` | CreatePostingPage.js:701 | Gecachter Step-Katalog |
| `window.stepIngredientBindings` | CreatePostingPage.js | Step↔Zutat-Bindungen |
| `window.ProbabilityMultiIngredients` | **NEU** create-posting-probability.js:5 | **Multi-Ingredient Storage** für Probability Area. Struktur: `{ "masterId": { "varName": [{name, fraction, article}, ...] } }`. Persistent während Session |

### Globale Funktionen

| Funktion | Quelle | Beschreibung |
|----------|--------|-------------|
| `window.getCreatePostingTheme()` | CreatePostingPage.js:1235 | Theme-Name |
| `window.getCreatePostingVisualTheme()` | CreatePostingPage.js:1239 | Visuelles Theme |
| `window.getThemeMutedTextClass()` | CreatePostingPage.js:1243 | CSS-Klasse für gedämpften Text |
| `window.setCreatePostingTheme(theme)` | CreatePostingPage.js:1284 | Theme setzen + persistieren |
| `window.addStep(id, btn, text, options)` | CreatePostingPage.js:3390 | Step hinzufügen |
| `window.removeStep(btn)` | CreatePostingPage.js:3472 | Step entfernen |
| `window.createFallbackStepId()` | CreatePostingPage.js:2765 | Fallback-ID generieren |
| `window.updateStepIndices()` | CreatePostingPage.js | Step-Indizes aktualisieren |
| `window.updateStoryProgress()` | CreatePostingPage.js:2567 | Fortschritt aktualisieren |
| `window.moveStepRow(btn, direction)` | CreatePostingPage.js | Step-Reihenfolge ändern |
| `window.startStepIngredientEdit(e, btn)` | CreatePostingPage.js:3231 | Zutat-Edit starten |

---

## 23. Event-Listener-Katalog

### Theme & Navigation

| Event | Selektor | Zeile (Page.js) | Beschreibung |
|-------|----------|-----------------|-------------|
| click | `[data-create-posting-theme]` | ~3938 | Theme wechseln |

### Zutat-Suche

| Event | Selektor | Zeile | Beschreibung |
|-------|----------|-------|-------------|
| input | `#ingredientSearch` | ~3943 | Zutaten filtern |
| click | `#clearIngredientSearch` | ~3950 | Suche leeren |

### Draft-Management

| Event | Selektor | Zeile | Beschreibung |
|-------|----------|-------|-------------|
| click | `#restoreDraftBtn` | ~4012 | Draft wiederherstellen, Banner ausblenden |
| click | `#discardDraftBtn` | ~4015 | Draft löschen, Banner ausblenden |
| input/change | `#recipeForm input, textarea, select` | ~3957 | Auto-Save triggern |
| submit | `#recipeForm` | ~3960 | Form absenden |
| pagehide/beforeunload | `window` | ~3963 | Draft speichern bei Verlassen |

### Zutat-Konfiguration

| Event | Selektor | Zeile | Beschreibung |
|-------|----------|-------|-------------|
| input | `#ingredientConfigQty, .js-db-qty` | ~3973 | Menge ändern |
| click | `.js-common-unit-chip` | ~3980 | Schnell-Einheit wählen |
| click | `.js-config-unit-chip` | ~3993 | Config-Einheit wählen |
| click | `.ingredient-db-row` | ~4599 | Zutat aus Katalog wählen |
| click | `#btnCloseIngredientConfig` | ~4610 | Config schließen |
| click | `#btnApplyIngredientConfig` | ~4615 | Config anwenden |

### Probability/Rezepttyp

| Event | Selektor | Zeile | Beschreibung |
|-------|----------|-------|-------------|
| click | `.js-probability-type` | ~4013 | Rezepttyp auswählen |
| click | `.js-probability-template` | ~4037 | Template-Vorschlag annehmen |
| click | `.js-typical-ingredient-chip` | ~4037 | Typische Zutat hinzufügen |

### Template-Auswahl

| Event | Selektor | Zeile | Beschreibung |
|-------|----------|-------|-------------|
| click | `.ingredient-chip` (in `#currentStepIngredientButtons`) | ~4079 | Zutat-Chip auswählen |
| click | `.template-card` (in `#masterTemplateCards`) | ~4101 | Template-Karte auswählen |

### Step-Editor (SmartStepCreator wireEvents)

| Event | Selektor | Zeile (SmartStepCreator.js) | Beschreibung |
|-------|----------|----------------------------|-------------|
| click | `.template-card` | 1780 | Step-Karte auswählen → `onStepCardClick()` |
| click | `.template-var` | 1788 | Token klicken → `openInlineEditor()` |
| click | `.js-optional-var-add` | 1805 | Optional-Variable hinzufügen |
| click | `.placeholder-reset` | 1816 | Variable zurücksetzen |
| click | `.js-toggle-fallback-options` | 1828 | "Weitere anzeigen" toggle |
| click | `#btnAcceptStep` | 1840 | Step akzeptieren → `acceptActiveStep()` |
| click | `#BtnCloseVar, #BtnCloseVarTop` | 1846 | Editor schließen |
| click | `#BtnApplyVar` | ~2300 | Wert einsetzen → `applyCurrentEditorSelection()` |
| click | `[data-pick-mode="fraction"]` | ~2314 | **Fraction wählen** (alte Picker-Logik, deprecated) |
| click | `[data-pick-mode="article"]` | ~2380 | Artikel wählen (nicht für ingredient-vars) |
| click | `[data-pick-mode="pronoun"]` | 1922 | Pronomen wählen |
| click | `[data-pick-mode="value"]` | 1922 | Wert wählen |
| click | `[data-duration-unit]` | 1930 | Dauer-Einheit wählen |
| click | `#BtnPickDurationQuick` | 1947 | Dauer schnell einsetzen |
| click | `#BtnPickCountQuick` | 1969 | Count schnell einsetzen |
| click | `#BtnPickTempQuick` | ~2750 | Temperatur schnell einsetzen |
| change | `#LangSelect` | ~2770 | Sprache wechseln |

### Multi-Ingredient Plus-Button (NEU)

| Event | Selektor | Zeile (SmartStepCreator.js) | Beschreibung |
|-------|----------|----------------------------|-------------|
| click | `.ingredient-plus-btn` | ~2229 | **Plus-Button im Step**: (1) Wenn Editor geschlossen → öffnet Editor. (2) Wenn Editor offen → fügt aktuell ausgewählte Zutat (Artikel + Fraktion) zur `_multiIngredients`-Liste hinzu und rendert Editor neu. Erscheint wenn Ingredient-Variable einen Wert hat |
| click | `[data-remove-multi-ingredient]` | ~2284 | **Remove-Chip im Editor**: Entfernt Zutat an Index aus `_multiIngredients`-Array, aktualisiert Preview-Text, rendert Step und Editor neu |
| click | `[data-pick-mode="fraction"]` | ~2360 | **Fraktions-Chip**: Wählt Fraktion (Ganzes, 1/2, 1/3, 2/3, 1/4, 3/4) aus, setzt `dataset.selectedFraction` |

**Workflow-Logik:**
- **activeStep._multiIngredients**: Array `[{name, fraction, article}, ...]` speichert hinzugefügte Zutaten
- **dataset.selectedIngredientValues**: Aktuell ausgewählte Chips (für nächstes Hinzufügen)
- **dataset.selectedArticle**: Aktuell ausgewählter Artikel (z.B. "des", "der")
- **dataset.selectedFraction**: Aktuell ausgewählte Fraktion (z.B. "1/2")
- **Plus-Button Anzeige**: `isIngredient && val.trim().length > 0` (wenn Ingredient-Variable Wert hat)
- **Chip-Deaktivierung**: Chips in `_multiIngredients` bekommen `.ingredient-chip-disabled` Klasse
- **Removable Chips**: Am Boden des Editors werden bereits hinzugefügte Zutaten als Bootstrap badge chips mit X-Button angezeigt

### Probability Inline-Editor

| Event | Selektor | Zeile (Page.js) | Beschreibung |
|-------|----------|-----------------|-------------|
| click | `.js-prob-inline-close` | ~613 | Inline-Editor schließen |
| click | `#BtnCloseVarTop` | ~614 | Top-Close-Button |
| click | `.js-prob-inline-apply` | ~615 | Wert anwenden |
| click | `#BtnPickDurationQuick` | ~616 | Duration schnell |
| click | `#BtnPickTempQuick` | ~617 | Temp schnell |
| click | `#BtnPickCountQuick` | ~618 | Count schnell |
| click | `[data-pick-mode]` | ~619 | Pick-Mode-Handler |
| click | `[data-duration-unit]` | ~644 | Duration-Unit |

### Master-Vorschau Drag & Drop

| Event | Selektor | Zeile | Beschreibung |
|-------|----------|-------|-------------|
| click | `.placeholder-token` | ~4203 | Token klicken |
| dragstart | `.ingredient-chip` | ~4219 | Zutat-Chip ziehen starten |
| dragover | `.placeholder-token` | ~4227 | Über Token ziehen |
| dragleave | `.placeholder-token` | ~4235 | Token verlassen |
| drop | `.placeholder-token` | ~4239 | Auf Token fallen lassen |
| click | `.placeholder-reset` | ~4265 | Variable zurücksetzen |

---

## 24. DOM-Element-Verzeichnis

### Haupt-IDs

| ID | Element | Container | Beschreibung |
|----|---------|-----------|-------------|
| `recipeForm` | `<form>` | – | Haupt-Formular |
| `card-basics` | `<section>` | form | Basis-Infos (Titel, Kategorie) |
| `card-media` | `<section>` | form | Media-Upload |
| `card-ingredients` | `<section>` | form | Zutaten-Auswahl |
| `card-probability` | `<section>` | form | Rezepttyp-Erkennung |
| `card-steps` | `<section>` | Partial | Steps (Smart Step Creator) |
| `card-keywords` | `<section>` | form | Keywords |
| `ingredientSearch` | `<input>` | card-ingredients | Zutat-Suchfeld |
| `ingredientsCatalog` | `<div>` | card-ingredients | Zutat-Katalog (scrollbar) |
| `selectedIngredients` | `<div>` | card-ingredients | Ausgewählte Zutaten |
| `selectedIngredientsSuggestions` | `<div>` | card-ingredients | Vorschläge |
| `ingredientProbabilityBadges` | `<div>` | card-probability | Typ-Badges |
| `ingredientProbabilityTemplates` | `<div>` | card-probability | Template-Vorschläge |
| `sc2MasterPreviewCanvas` | `<div>` | card-steps | Vorschau-Canvas |
| `sc2MasterPreviewCard` | `<div>` | card-steps | Vorschau-Karte |
| `MasterText` | `<div>` | card-steps | Step-Text-Anzeige |
| `InlineVarEditorHost` | `<div>` | card-steps | Inline-Editor-Container |
| `insertContainer` | `<div>` | card-steps | Template-Bibliothek |
| `stepsChipStrip` | `<div>` | card-steps | Zutat-Chip-Streifen |
| `stepsIngredientButtons` | `<div>` | card-steps | Zutat-Chips für Step |
| `btnApplyStepIngredient` | `<button>` | card-steps | Zutat-Anwenden |
| `btnCancelStepIngredient` | `<button>` | card-steps | Zutat-Abbrechen |
| `selectedSteps` | `<div>` | card-steps | Akzeptierte Steps |
| `selectedKeywords` | `<div>` | card-keywords | Ausgewählte Keywords |
| `videoInput` | `<input>` | card-media | Datei-Input |
| `videoPreviewContainer` | `<div>` | card-media | Preview-Container |
| `videoPreview` | `<video>` | card-media | Video-Preview |
| `imagePreview` | `<img>` | card-media | Bild-Preview |
| `uploadLabel` | `<label>` | card-media | Drag & Drop Zone |
| `ingredientConfigOverlay` | `<div>` | form | Config-Popup-Overlay |
| `ingredientConfigDock` | `<div>` | form | Config-Panel |
| `probVarEditorDock` | `<div>` | form | Probability-Var-Editor |
| `creatorToast` | `<div>` | form | Toast-Bereich |
| `LangSelect` | `<select>` | topbar | Sprach-Dropdown |

### Dynamische Elemente (per JS erzeugt)

| Selektor | Erzeugt durch | Beschreibung |
|----------|---------------|-------------|
| `.ingredient-db-row` | Server (cshtml) | Zutat-Katalog-Zeile |
| `.ingredient-row` | `addIngredient()` | Ausgewählte Zutat-Zeile |
| `.step-row` | `addStep()` | Akzeptierter Step |
| `.template-card` | `renderStepButtons()` | Template-Auswahl-Karte |
| `.ingredient-chip` | `openInlineEditor()` | Zutat-Chip im Editor |
| `.fraction-pick` | `renderFractionPickerHtml()` | Fraction-Button |
| `.pill-like` | `renderPillButtons()` | Variablen-Auswahl-Pill |
| `.keyword-pill` | `toggleKeyword()` | Ausgewähltes Keyword |
| `.placeholder-token` | `renderTemplateTokens()` | Klickbarer Platzhalter im Text |
| `.placeholder-reset` | `renderTemplateTokens()` | Variable-Reset-Button |
| `#FractionPickerRow` | `renderFractionPickerHtml()` | Fraction-Picker-Container |

### Data-Attribute auf Step-Rows

| Attribut | Beschreibung |
|----------|-------------|
| `data-step-id` | Step-ID |
| `data-step-edited` | Ob manuell bearbeitet ("true"/"false") |
| `data-master-template-id` | Master-Template-ID |
| `data-step-reference-json` | JSON-Metadaten (stabile Referenz) |
| `data-ingredient-name` | Gebundener Zutat-Name |
| `data-ingredient-fractions` | Fraction-Daten (JSON: `{ingredientName, fractionKey, numerator, denominator}`) |

---

## 25. Datenfluss & User-Flow

### Rezept erstellen

```
User öffnet CreatePosting
    │
    ├─1. Basics ausfüllen (Titel, Kategorie, Personen, Zeit)
    │   └→ scheduleCreatePostingDraftSave()
    │
    ├─2. Media hochladen (Video/Bild)
    │   └→ handleVideoUpload() → Preview anzeigen
    │
    ├─3. Zutaten auswählen
    │   ├→ ingredientSearch → syncIngredientSourceVisibility()
    │   ├→ Click .ingredient-db-row → addIngredient()
    │   │   ├→ buildIngredientRowHtml() → #selectedIngredients
    │   │   ├→ applyDerivedIngredientRowVisuals()
    │   │   └→ refreshIngredientProbabilityHints()
    │   │       └→ RecipeStepSuggest.detectRecipeTypes()
    │   │           └→ #ingredientProbabilityBadges aktualisieren
    │   └→ scheduleCreatePostingDraftSave()
    │
    ├─4. Steps erstellen (Smart Step Creator)
    │   ├→ Template-Karte klicken → onStepCardClick()
    │   │   └→ renderMasterText() (Preview mit Platzhaltern)
    │   │
    │   ├→ Platzhalter klicken → openInlineEditor()
    │   │   ├→ Zutat-Chips anzeigen (getSelectedIngredientsFromPage)
    │   │   │   └→ inkl. Remainder-Chips (getRemainderChipsFromAcceptedSteps)
    │   │   ├→ Fraction-Picker anzeigen (wenn 1 Zutat ausgewählt)
    │   │   ├→ Variablen-Optionen anzeigen (getRankedVarOptionEntries)
    │   │   └→ Spezial-Editoren (duration/temp/count)
    │   │
    │   ├→ Wert einsetzen → applyCurrentEditorSelection()
    │   │   ├→ Fraction-aware: composeFractionText()
    │   │   └→ activeStep.values[varName] = composed
    │   │       └→ rerenderAfterValueSet() → renderMasterText()
    │   │
    │   └→ Step akzeptieren → acceptActiveStep()
    │       └→ window.addStep(id, null, text, {
    │           ingredientName, masterTemplateId,
    │           stepData, fractionData
    │       })
    │           ├→ HTML in #selectedSteps einfügen
    │           │   └→ data-ingredient-fractions Attribut
    │           ├→ updateStepIndices()
    │           ├→ applyDerivedIngredientRowVisuals()
    │           └→ renderIngredientChips()
    │
    ├─5. Keywords auswählen
    │   └→ toggleKeyword() → .keyword-pill in #selectedKeywords
    │
    └─6. Veröffentlichen
        └→ <form> submit → prepareBinding()
            └→ POST /WorldMiniApp/Home/UploadNewVideoAsync
                ├→ Rate-Limit prüfen
                ├→ Blob-Upload
                ├→ Rezept erstellen
                ├→ Steps extrahieren
                ├→ Smart Steps extrahieren
                ├→ Keywords verknüpfen
                └→ Redirect → Home/Index
```

### Fraction-Flow (Zutaten-Aufteilung)

```
Step 1: User wählt 1 Zutat-Chip ("Mehl")
    → Fraction-Picker erscheint (#FractionPickerRow)

Step 2: User tippt "die Hälfte (1/2)"
    → host.dataset.selectedFraction = "1/2"

Step 3: User klickt "Einsetzen"
    → applyCurrentEditorSelection()
    → composeFractionText({label:"die Hälfte"}, "Mehl", "des")
    → activeStep.values.ingredient = "die Hälfte des Mehl"
    → activeStep._fractionData = {
        ingredientName: "Mehl",
        fractionKey: "1/2",
        numerator: 1,
        denominator: 2
    }

Step 4: User akzeptiert Step
    → acceptActiveStep() → window.addStep(..., { fractionData })
    → Step-Row bekommt: data-ingredient-fractions='{"ingredientName":"Mehl",...}'

Step 5: Nächster Step
    → getSelectedIngredientsFromPage() aufgerufen
    → getRemainderChipsFromAcceptedSteps()
        → scannt data-ingredient-fractions auf Step-Rows
        → berechnet: 1 - 0.5 = 0.5 Rest
        → findet Fraction "1/2" → Label "die Hälfte"
    → Original "Mehl" Chip wird ersetzt durch:
        { name: "die Hälfte Mehl", isRemainder: true, originalName: "Mehl" }
    → Chip hat CSS-Klasse: .ingredient-chip.fraction-remainder (dashed border)
```

### Multi-Ingredient-Flow (NEU 2026-03-27)

**Mehrere Zutaten mit individuellen Fraktionen/Artikeln kombinieren:**

```
Step 1: User klickt Token {{ingredient}}
    → openInlineEditor() oder openProbVarInlineEditor()
    → Editor öffnet mit leerer _multiIngredients-Liste

Step 2: User wählt: Artikel "des" + Zutat "Zucker" + Fraktion "1/2"
    → host.dataset.selectedArticle = "des"
    → host.dataset.selectedIngredientValues = '[{"name":"Zucker"}]'
    → host.dataset.selectedFraction = "1/2"

Step 3: User klickt "Einsetzen"
    → applyCurrentEditorSelection() (Step Creator) oder doApply() (Probability Area)
    → composeFractionText() → "die Hälfte des Zuckers"
    → activeStep.values.ingredient = "die Hälfte des Zuckers"
    → Editor schließt

Step 4: Plus-Button [+] erscheint im Step/Template
    → renderTemplateTokens() erkennt: isIngredient && hasValue
    → HTML: `{{ingredient}} [+] [↻]`

Step 5: User klickt Plus-Button [+]
    WICHTIG: Unterschiedliches Verhalten je nach Editor-Status!

    5a) Editor geschlossen:
        → Öffnet Editor neu
        → Zeigt bisherige Zutat: "die Hälfte des Zuckers"

    5b) Editor offen:
        → Fügt aktuelle Selektion zur _multiIngredients-Liste hinzu
        → activeStep._multiIngredients = [{name: "Zucker", fraction: "1/2", article: "des"}]
        → Editor bleibt offen, wird neu gerendert

Step 6: Editor neu gerendert
    → Multi-Ingredients-Chips am Boden des Editors:
        [die Hälfte des Zuckers [X]]
    → "Zucker"-Chip bekommt .ingredient-chip-disabled (ausgegraut, line-through)
    → User wählt nun: Artikel "der" + Zutat "Butter" + Fraktion "1/3"

Step 7: User klickt Plus-Button [+] erneut
    → Fügt zweite Zutat hinzu
    → activeStep._multiIngredients = [
        {name: "Zucker", fraction: "1/2", article: "des"},
        {name: "Butter", fraction: "1/3", article: "der"}
      ]
    → formatSelectedIngredientList() formatiert:
        "die Hälfte des Zuckers und ein Drittel der Butter"
    → activeStep.values.ingredient = "die Hälfte des Zuckers und ein Drittel der Butter"
    → Step-Text wird aktualisiert

Step 8: User klickt X-Button auf "Zucker"-Chip
    → Remove-Handler entfernt Index 0 aus _multiIngredients
    → Liste wird neu formatiert: "ein Drittel der Butter"
    → Editor wird neu gerendert

Step 9: User klickt "Einsetzen"
    → Editor schließt
    → activeStep behält _multiIngredients-Array für spätere Bearbeitung

Step 10: Probability Area - Template akzeptieren
    → User klickt Template-Card
    → getMergedVars(masterId) wird aufgerufen
    → Liest window.ProbabilityMultiIngredients[masterId][varName]
    → Formatiert mit formatSelectedIngredientList()
    → ALLE Multi-Ingredients werden korrekt in akzeptierten Step übertragen
```

**Storage-Unterschiede:**
- **Step Creator:** `activeStep._multiIngredients` (Array direkt am Step-Objekt)
- **Probability Area:** `window.ProbabilityMultiIngredients[masterId][varName]` (Global, per Master-ID + Variable)

**Chip-Deaktivierung:**
```javascript
// Bereits hinzugefügte Zutaten werden ausgegraut
multiIngredients.forEach(item => {
    if (getIngredientName(item) === value) {
        disabledCls = " ingredient-chip-disabled";
    }
});
```

**CSS:**
- `.ingredient-chip-disabled` → opacity 0.4, grayscale(0.8), line-through, pointer-events none
- `.ingredient-plus-btn` → Runder grüner Button, 26×26px, hover: scale(1.1)

### Zutat-Chip-Filterung nach Variable-Typ

```
Wenn User einen Platzhalter anklickt (z.B. {{liquid}}, {{fat}}, {{seasonings}}):

1. openInlineEditor() oder buildInlineEditorHtml()
2. → getSelectedIngredientsFromPage()
   → alle Zutaten mit isLiquid, isFat, isHard, isSoft, groupId
3. → filterIngredientsByVarType(items, varName)
   → Mapping:
      {{liquid}}    → item.isLiquid === true
      {{fat}}       → item.isFat === true
      {{seasonings}}→ item.groupId === "5" (Gewürze)
      {{base}} / {{dough}} / {{thickener}} → item.groupId === "8" (Grundnahrungsmittel)
      {{ingredient}} / {{ingredients}} → kein Filter (alle)
   → Rückgabe: { filtered: [...passend], rest: [...nicht passend] }

4. Passende Chips: normal dargestellt
5. Restliche Chips: nach Trennlinie (.ingredient-chip-divider), gedimmt (.ingredient-chip-dimmed)
6. Wenn KEINE passende Zutat gefunden → Fallback: alle Zutaten normal anzeigen

Datenquelle (data-* Attribute auf .ingredient-db-row):
  data-is-liquid="true/false"   ← DB: is_liquid (bool)
  data-is-fat="true/false"      ← DB: is_fat (bool)
  data-is-hard="true/false"     ← DB: is_hard (bool)
  data-is-soft="true/false"     ← DB: is_soft (bool)
  data-group-id="1-9"           ← DB: GroupId (FK → Group)

Gruppen-IDs: 1=Fleisch, 2=Gemüse, 3=Milchprodukte, 4=Obst,
             5=Gewürze, 6=Fisch, 7=Sonstiges, 8=Grundnahrungsmittel,
             9=Nüsse/Samen/Hülsenfrüchte
```

---

## 26. Initialisierungs-Sequenz

```
1. Seite laden → Synchrone Scripts laden
   ├→ window.CreatePostingUtils       verfügbar
   ├→ window.CreatePostingDataUrls    verfügbar
   ├→ window.CreatePostingDataStore   verfügbar
   ├→ window.CreatePostingFeedback    verfügbar
   ├→ window.CreatePostingPageData    verfügbar
   └→ window.CreatePostingTemplateBuilder verfügbar

2. DOMContentLoaded → Async-Scripts starten
   ├→ CreatePostingSmartStepCreator.js: init()
   │   ├→ loadJson() → master_steps.json
   │   ├→ loadVariableCatalog() → master_step_variables.json
   │   ├→ renderStepButtons()
   │   ├→ renderMasterText()
   │   └→ wireEvents()
   │
   ├→ master-step-renderer.js: load()
   │   ├→ master_steps.json
   │   ├→ master_step_option_rules.json
   │   ├→ recipe_type_step_variables.json
   │   └→ recipe_step_mapping.json
   │
   ├→ recipe-step-suggest.js: loadMapping()
   │   ├→ recipe_step_mapping.json
   │   └→ recipe_category_scoring.json
   │
   ├→ ingredientmanager.js: refreshUnits()
   │
   └→ create-posting-publish-checklist.js: initChecklist()

3. jQuery Ready → CreatePostingPage.js
   ├→ Theme initialisieren (readStoredCreatePostingTheme)
   ├→ loadIngredientArticleRules()
   ├→ loadIngredientTransforms()
   ├→ Alle Event-Listener binden
   ├→ showDraftRestoreBannerIfNeeded()
   │   ├→ consumeCreatePostingDraftResetFlag()
   │   │   └→ Falls Flag: clearCreatePostingDraft()
   │   ├→ hasMeaningfulCreatePostingDraft()
   │   │   └→ Falls ja: Banner #draftRestoreBanner einblenden
   │   └→ User klickt "Wiederherstellen" → restoreCreatePostingDraft()
   │       oder "Verwerfen" → clearCreatePostingDraft()
   └→ updateLanguageLabels()
```

---

> **Externe Abhängigkeiten:** jQuery, Bootstrap 5, Bootstrap Icons
> **Unterstützte Sprachen:** DE, EN, ESP, PRT, ID, NL, SV, DA, NO, MS (10 Sprachen)
> **Tech-Stack:** ASP.NET Core 8.0 MVC · C# · JavaScript (Vanilla + jQuery) · CSS3

---

## 27. Changelog

### 2026-04-02 — Bugfixes: Multi-Ingredient-System & Artikel-Grammatik

#### Kritische Bugfixes

**1. CreatePostingPage.js — `localizeIngredientValueForSandbox()` (Zeile 1030-1059):**
- 🐛 **Problem:** Funktion entfernte Artikel selbst bei gleicher Sprache (DE→DE)
  - Beispiel: "die Tomaten, der Knoblauch" wurde zu "Tomaten und Knoblauch"
  - Betraf Step-Akzeptierung: Zutaten kamen OHNE Artikel im Rezept an
- ✅ **Fix:** Wenn Quell- und Zielsprache identisch sind, wird Originalwert zurückgegeben
  - Neue Prüfung: `if (resolveLangKey(sourceLang) === resolveLangKey(targetLang)) return value;`
  - Artikel bleiben erhalten: "die Tomaten" → "die Tomaten"
- **Betroffene Variablen:** `{{ingredient}}`, `{{ingredient2}}`, `{{ingredients}}`, `{{liquid}}`, `{{fat}}`
- **Impact:** Alle Steps die Zutaten verwenden, jetzt mit korrekten Artikeln

**2. CreatePostingSmartStepCreator.js — Overlay Close Handler (Zeile 2332-2349):**
- 🐛 **Problem:** Wenn Nutzer Zutaten auswählt aber Overlay OHNE "Einsetzen" schließt, gehen Werte verloren
  - Workflow: Overlay öffnen → Chips klicken → "Schließen" → Step akzeptieren → LEER
  - Grund: "Schließen"-Button speicherte nicht vor dem Schließen
- ✅ **Fix:** Auto-Save beim Overlay-Schließen implementiert
  - Liest aktuellen Editor-Wert via `applyEditorValue(currentEditorEl)`
  - Speichert automatisch via `updateContextValue()` wenn Wert vorhanden
  - Nur wenn `value !== null && value !== ''` (vermeidet leere Speicherungen)
- **Impact:** Nutzer können Overlay schließen ohne explizit "Einsetzen" zu klicken

**3. CreatePostingSmartStepCreator.js — Genus-zu-Artikel Konvertierung (Zeile 98-109, 2148-2159):**
- 🐛 **Problem:** Falsche Artikel bei Multi-Ingredient-Auswahl
  - Beispiel: "das Pasta und das Zucker" statt "die Pasta und der Zucker"
  - Beispiel 2: "n mehl" statt "das Mehl" (Genus-Buchstabe direkt als Artikel)
  - Grund: Genus (m/f/n) wurde nicht zu Artikel (der/die/das) konvertiert
- ✅ **Fix:** Neue Funktion `genusToArticle(genus, lang)` hinzugefügt
  - Konvertiert Genus → Artikel: m→der, f→die, n→das
  - Automatischer Artikel-Lookup beim Ingredient-Chip-Klick
  - `const autoArticle = genusToArticle(genusFromCatalog, currentLang)`
- **Impact:** Jede Zutat erhält automatisch ihren korrekten Artikel basierend auf Genus

**4. CreatePostingSmartStepCreator.js — Existing Multi-Ingredients nutzen (Zeile 3828-3844):**
- 🐛 **Problem:** Variable wird gelöscht wenn "Step akzeptieren" im Overlay geklickt wird
  - Workflow: Overlay öffnen → Zutaten auswählen → "Step akzeptieren" → Variable leer
  - Grund: selectedValues war leer (bereits in multiIngredients gespeichert), null wurde überschrieben
- ✅ **Fix:** Existierende multiIngredients verwenden wenn selectedValues leer ist
  - Prüft `activeStep?._multiIngredients?.[varName]` bevor null zurückgegeben wird
  - Verhindert null-Überschreibung in `saveCurrentEditorValueBeforeAccept()`
- **Impact:** Zutaten bleiben erhalten beim Akzeptieren von Steps im Overlay

**5. ingredient_transforms.json — Genus-basierte Adjektiv-Deklination:**
- 🐛 **Problem:** Grammatikfehler bei transformierten Zutaten
  - Beispiel: "geschnittene Salat" statt "geschnittener Salat" (maskulin)
  - Grund: Statische Adjektiv-Strings ohne Genus-Varianten
- ✅ **Fix:** 6 Transform-Patterns mit m/f/n-Varianten erweitert
  - `piece`: "geschnittener (m) / geschnittene (f) / geschnittenes (n) {{noun}}"`
  - `slice`: "geschnittener (m) / geschnittene (f) / geschnittenes (n) {{noun}}"`
  - `strip`: "gestreifter (m) / gestreifte (f) / gestreiftes (n) {{noun}}"`
  - `wedge`: "gevierteilter (m) / gevierteilte (f) / gevierteiltes (n) {{noun}}"`
  - `ring`: "geringter (m) / geringte (f) / geringtes (n) {{noun}}"`
  - `julienne`: "juliennierter (m) / juliennierte (f) / julienniertes (n) {{noun}}"`
- **Impact:** Grammatikalisch korrekte Adjektive basierend auf Genus der Zutat

**6. CreatePostingSmartStepCreator.js — Duplikat-Prävention (Zeile 2593-2612):**
- 🐛 **Problem:** Doppelte Zutaten mit verschiedenen Fraktionen
  - Beispiel: "die Garnelen" UND "die Hälfte der Garnelen" gleichzeitig in Liste
  - Grund: Neue Zutaten wurden einfach zu existierenden hinzugefügt
- ✅ **Fix:** Filter entfernt existierende Zutaten mit gleichem Namen vor Kombination
  - `filteredExisting = filteredExisting.filter(existing => getIngredientName(existing) !== getIngredientName(newItem))`
  - Neueste Version ersetzt alte (Fraktion kann geändert werden)
- **Impact:** Keine Duplikate mehr, Fraktionen können überschrieben werden

**7. CreatePostingSmartStepCreator.js — Genus-basierter Genitiv (Zeile 116-148):**
- 🐛 **Problem:** Falscher Genitiv bei Fraktionen mit "der"-Artikel
  - Beispiel: "die Hälfte der Zitronensaft" statt "des Zitronensafts"
  - Grund: "der" kann maskulin-nominativ ODER feminin-genitiv sein
- ✅ **Fix:** Genus-Parameter in `composeFractionText()` hinzugefügt
  - Maskulin (m): "der" → "des" (Zitronensaft)
  - Feminin (f): "der" → "der" (Zitrone)
- **Impact:** Grammatikalisch korrekter Genitiv basierend auf Geschlecht

**8. CreatePostingSmartStepCreator.js — Multi-Section immer sichtbar (Zeile 1954-1959):**
- 🐛 **Problem:** Multi-Ingredient-Sektion erst sichtbar nach erster Zutat
  - UX: Nutzer wussten nicht, dass Multi-Auswahl möglich ist
- ✅ **Fix:** Sektion immer anzeigen mit Platzhalter-Text
  - "Noch keine Zutaten ausgewählt. Wähle unten mehrere Zutaten aus."
- **Impact:** Klarere UX, Multi-Modus von Anfang an erkennbar

**9. CreatePostingSmartStepCreator.js — Auto-Artikel bei Chip-Klick (Zeile 2148-2159):**
- 🐛 **Problem:** Multi-Ingredients bekamen alle denselben Artikel (globalArticle)
- ✅ **Fix:** Artikel automatisch aus Katalog-Genus konvertieren beim Chip-Klick
  - `const genusFromCatalog = catalogItem?.genusByLang?.[currentLang] || ''`
  - `const autoArticle = genusToArticle(genusFromCatalog, currentLang)`
  - Jede Zutat bekommt ihren eigenen genusspezifischen Artikel
- **Impact:** Korrekte Artikel für jede Zutat, auch bei Multi-Auswahl (die Pasta, der Zucker, das Mehl)

**10. CreatePostingSmartStepCreator.js — Editor-Reload nach erstem Einsetzen (Zeile 2654-2675):**
- 🐛 **Problem:** Nach erster Zutat keine Badges und Chips bleiben markiert
  - Workflow: "Garnelen" klicken → "Einsetzen" → Chip bleibt aktiv, kein Badge zum Entfernen
  - Erst nach ZWEITER Zutat erscheinen Badges für beide
  - Grund: UI wurde nicht aktualisiert nach erster Speicherung
- ✅ **Fix:** `switchEditorVariable()` nach erstem Ingredient-Add aufrufen
  - Lädt Editor neu mit aktuellem State
  - Badges erscheinen, Chips werden deselektiert
  - Overlay bleibt offen für weitere Zutaten
- **Impact:** Konsistente UX ab erster Zutat, keine manuelle Deselektierung nötig

**11. CreatePostingPage.js — Optionale Variablen leer lassen (Zeile 2604-2614):**
- 🐛 **Problem:** Optionale Variablen im Probability-Bereich falsch gerendert
  - Beispiel: "Wasche die Garnelen unter kaltem Wasserremoval..." (ohne Leerzeichen)
  - `[, entferne {{removal}}]` wurde zu "removal" ohne ", entferne"
  - Grund: `removal` bekam Key-Name als Default statt leer zu bleiben
- ✅ **Fix:** Optionale Variablen bleiben leer wenn kein Wert vorhanden
  - `const isOptional = template?.optional_variables.includes(key)`
  - `if (isOptional) vars[key] = '';`
  - Optional-Section wird korrekt als Plus-Button gerendert
- **Impact:** Probability-Bereich zeigt Plus-Button für optionale Sections (wie Step Creator)

#### Verhaltensänderungen

**Smart Step Creator — Overlay Workflow:**
- **ALT:**
  1. Zutat auswählen
  2. "Einsetzen" klicken (ERFORDERLICH!)
  3. Overlay schließen
  4. Step akzeptieren
- **NEU:**
  1. Zutat auswählen (Artikel wird automatisch aus Katalog geholt)
  2. Optional: Weitere Zutaten hinzufügen (Badges erscheinen sofort)
  3. Overlay schließen (Auto-Save!) ODER "Step akzeptieren" im Overlay
- **"Einsetzen"-Button:** Bleibt verfügbar für explizites Speichern + Overlay-offen-halten

**Multi-Ingredient-Auswahl — UX-Verbesserungen:**
- **ALT:** Erste Zutat → kein Badge, Chip bleibt markiert → manuelle Deselektierung nötig
- **NEU:** Erste Zutat → Badge erscheint sofort, Chip wird automatisch deselektiert
- Multi-Ingredient-Sektion immer sichtbar (auch ohne ausgewählte Zutaten)
- Platzhalter-Text: "Noch keine Zutaten ausgewählt. Wähle unten mehrere Zutaten aus."

**Step-Akzeptierung — Artikel-Grammatik:**
- **ALT:** "Schneide Tomaten in große Würfel" (ohne Artikel)
- **NEU:** "Schneide die Tomaten in große Würfel" (mit Artikel)
- **Multi-Ingredients:** "das Pasta und das Zucker" → "die Pasta und der Zucker" (genusspezifisch)
- Betrifft: Alle Sprachen (DE/EN/ESP/PRT/ID/NL/SV/DA/NO/MS)

**Transformierte Zutaten — Adjektiv-Deklination:**
- **ALT:** "geschnittene Salat" (falsche Grammatik)
- **NEU:** "geschnittener Salat" (maskulin), "geschnittene Zwiebel" (feminin)
- Betrifft: piece, slice, strip, wedge, ring, julienne

**Fraktionen — Genitiv-Kasus:**
- **ALT:** "die Hälfte der Zitronensaft" (falscher Genitiv)
- **NEU:** "die Hälfte des Zitronensafts" (maskulin-genitiv korrekt)
- Genus-basierte Genitiv-Konversion: "der" (m) → "des", "der" (f) → "der"

**Duplikat-Prävention:**
- Zutaten mit gleichem Namen werden ersetzt (nicht doppelt gelistet)
- Beispiel: "die Garnelen" → "die Hälfte der Garnelen" (alte Version wird entfernt)

#### Betroffene Funktionen

**CreatePostingPage.js:**
- `localizeIngredientValueForSandbox()` (Zeile 1030-1059) — Early-Return bei gleicher Sprache
- `buildVariablesForTemplate()` (Zeile 2604-2614) — Optionale Variablen leer lassen statt Key-Name

**CreatePostingSmartStepCreator.js:**
- `genusToArticle()` (Zeile 98-109) — **NEU:** Genus→Artikel Konvertierung (m→der, f→die, n→das)
- `bindUnifiedOverlayEventHandlers()` (Zeile 2332-2349) — Close-Handler mit Auto-Save
- `applyEditorValue()` (Zeile 3918-3931) — GlobalArticle-Anwendung (User-Auswahl hat Priorität)
- `saveCurrentEditorValueBeforeAccept()` (Zeile 3411-3420) — Null-Überschreibung verhindert
- `switchEditorVariable()` (Zeile 1954-1959) — Multi-Section immer sichtbar
- `applyUnifiedEditorValue()` (Zeile 2593-2612, 2654-2675) — Duplikat-Filter & Editor-Reload
- `composeFractionText()` (Zeile 116-148) — Genus-basierter Genitiv
- Chip-Click-Handler (Zeile 2148-2159) — Auto-Artikel via genusToArticle()

**ingredient_transforms.json:**
- 6 Transform-Patterns erweitert mit m/f/n-Varianten:
  - `piece`, `slice`, `strip`, `wedge`, `ring`, `julienne`
- Betrifft alle 10 Sprachen (DE primär, EN/ESP/PRT/ID/NL/SV/DA/NO/MS teilweise)

### 2026-03-27 — Ingredient-Filter & Variable-Updates

#### JavaScript-Änderungen

**CreatePostingSmartStepCreator.js & CreatePostingPage.js:**
- ✅ **Neue Ingredient-Filter hinzugefügt:**
  - `{{liquid}}` → filtert nur Zutaten mit `is_liquid = true`
  - `{{fat}}` → filtert nur Zutaten mit `is_fat = true`
  - `{{hard}}` → filtert nur Zutaten mit `is_hard = true`
  - `{{soft}}` → filtert nur Zutaten mit `is_soft = true`

- ✅ **Step-spezifischer Filter:**
  - `{{ingredient}}` bei `PREP_TENDERIZE_01` (Klopfen/Plattieren) → nur `is_hard = true`
  - Betrifft: Fleisch, Schnitzel, etc. die geklopft werden können

- ✅ **Fallback entfernt:**
  - Zeigt nun leere Liste bei 0 Filter-Treffern (statt alle Zutaten anzuzeigen)
  - Verhindert irrelevante Zutaten-Vorschläge

**CreatePostingPage.js — Pronomen+State UI:**
- ✅ **Kombiniertes Pop-up für Pronomen & State:**
  - Neue Funktion: Zeigt Pronomen UND State in einem Pop-up (wie Artikel+Ingredient)
  - **Layout:** OBEN = Pronomen-Chips | UNTEN = State-Chips
  - Event-Handler: `.js-prob-pronoun-chip` und `.js-prob-state-chip`
  - Logik in `openProbVarEditor()` erweitert (Zeile ~416-458)

#### JSON-Daten-Änderungen

**master_steps.json:**
- ✅ **PREP_TENDERIZE_01** (Klopfen/Plattieren):
  - Variable geändert: `{{base}}` → `{{ingredient}}`
  - Betrifft alle 10 Sprachen (DE, EN, ESP, PRT, ID, NL, SV, DA, NO, MS)

- ✅ **PREP_WRAP_01** (Einwickeln/Einrollen):
  - Variable geändert: `{{filling}}` → `{{basis}}`
  - Betrifft alle 10 Sprachen

- ✅ **COOK_CARAMELIZE_01** (Karamellisieren):
  - Variable **entfernt:** `{{heat}}`
  - Template vereinfacht: "bei {heat}er Hitze" → entfernt
  - Betrifft alle 10 Sprachen

- ✅ **COOK_FLAMBE_01** (Flambieren):
  - Variable geändert: `{{spirit}}` → `{{liquid}}`
  - Betrifft alle 10 Sprachen

- ✅ **PREP_KNEAD_DOUGH_01** (Teig kneten):
  - Variablen-Reihenfolge korrigiert: `pronoun` nun VOR `state` (grammatikalisch korrekt)

**master_step_variables.json:**
- ✅ **Tool-Variable erweitert:**
  - Neue Option: `fleischklopfer` (🔨 Fleischklopfer)
  - Labels in 10 Sprachen:
    - DE: Fleischklopfer | EN: meat tenderizer | ESP: mazo de carne
    - PRT: batedor de carne | ID: pemukul daging | NL: vleeshamer
    - SV: köttklubba | DA: kødbanker | NO: kjøttbanker | MS: pengetuk daging
  - Tags: `tool:tenderizer`, `technique:tenderize`, `pound`, `klopfen`

- ✅ **Liquid-Variable NEU erstellt (Hybrid-Modus):**
  - 6 Optionen mit Icons in 10 Sprachen:
    - 💧 Wasser (water, agua, água, air, vatten, vand, vann)
    - 🍲 Brühe (broth, caldo, kaldu, bouillon, buljong, sup pekat)
    - 🥛 Milch (milk, leche, leite, susu, mjölk, mælk, melk)
    - 🥛 Sahne (cream, nata, creme, krim, grädde, fløde, fløte)
    - 🍷 Wein (wine, vino, vinho, anggur, vin, wain)
    - 🫒 Öl (oil, aceite, óleo, minyak, olje, olie)
  - Hybrid-Logik: Zeigt ausgewählte Zutaten mit `is_liquid=true` PLUS diese 6 Preset-Optionen

#### HTML-Struktur-Änderungen

**CreatePosting.cshtml:**
- ✅ **Neuer Pop-up-Bereich hinzugefügt:**
  ```html
  <div id="probVarPronounStateArea" class="d-none">
      <div class="small text-white-50 mb-2">Pronomen</div>
      <div id="probVarPronounChips" class="d-flex flex-wrap gap-2 mb-3"></div>
      <div class="small text-white-50 mb-2">Zustand</div>
      <div id="probVarStateChips" class="d-flex flex-wrap gap-2"></div>
  </div>
  ```
  - Ermöglicht getrennte Anzeige von Pronomen (oben) und State (unten)
  - Ähnlich dem Artikel+Ingredient-Layout

#### Betroffene Funktionen

**CreatePostingSmartStepCreator.js:**
- `filterIngredientsByVarType()` — Filter erweitert (hard, soft)
- Fallback-Logik entfernt (Zeile ~1088-1089)

**CreatePostingPage.js:**
- `openProbVarEditor()` — Pronomen+State-Erkennung & UI-Rendering
- `getBaseSandboxIngredients()` — Liest `data-is-fat`, `data-is-hard`, `data-is-soft` korrekt
- `getSelectedIngredientsForSandbox()` — Mappt Boolean-Flags (`isFat`, `isHard`, `isSoft`)
- Event-Handler hinzugefügt: `.js-prob-pronoun-chip`, `.js-prob-state-chip`

#### Datenfluss: Ingredient-Flags

```
DB: IngredientsAndNutrients.is_fat (boolean)
  ↓
Controller: ToSelectIngredientsAndNutrients (EF Core Include)
  ↓
View: data-is-fat="true/false" (HTML data-attribute)
  ↓
JS: getBaseSandboxIngredients() → isFat: boolean
  ↓
Filter: ings.filter(i => i.isFat) bei {{fat}}-Variable
```

#### Migration & Datenbank

**Wichtig:** Sicherstellen, dass `IngredientsAndNutrients`-Tabelle korrekte Flags hat:
```sql
-- Beispiel: Fett-Zutaten markieren
UPDATE IngredientsAndNutrients
SET is_fat = 1
WHERE Name_DE IN ('Butter', 'Öl', 'Olivenöl', 'Schmalz', 'Margarine');

-- Beispiel: Harte Zutaten markieren (zum Klopfen)
UPDATE IngredientsAndNutrients
SET is_hard = 1
WHERE Name_DE IN ('Hähnchenbrust', 'Schweineschnitzel', 'Kalbsschnitzel');
```

#### UI/UX-Verbesserungen

1. **Konsistente Filter-Logik:** Alle Ingredient-Variablen filtern nun korrekt
2. **Klarere Zutat-Auswahl:** Keine irrelevanten Optionen mehr bei spezifischen Variablen
3. **Verbessertes Pop-up-Layout:** Pronomen+State visuell getrennt (wie Artikel+Ingredient)
4. **Erweiterte Tool-Auswahl:** Fleischklopfer für Klopf-Steps verfügbar
5. **Hybrid-Liquid-Variable:** Kombination aus ausgewählten Zutaten + Preset-Optionen

---

## Änderungen 2026-03-28: Vereinheitlichtes Editor-System

### Überblick
Die Variable-Editoren für Pronomen und State wurden vereinheitlicht. Beide Bereiche (Wahrscheinlichkeits-Templates und Smart Step Creator) nutzen jetzt dasselbe Editor-System.

### Hauptänderungen

#### 1. Vereinheitlichtes Editor-System
**Vorher:**
- Wahrscheinlichkeitsbereich: Separater kombinierter Editor mit Auto-Close beim Klick
- Smart Step Creator: `openInlineEditor()` mit "Einsetzen"-Button

**Jetzt:**
- **Beide Bereiche** nutzen `buildInlineEditorHtml()` + einheitliche Event-Handler
- **Konsistentes Verhalten:** Immer "Einsetzen"-Button, kein Auto-Close
- **Vereinfachte Codebasis:** Kein doppelter Editor-Code mehr

**Betroffene Dateien:**
- `CreatePostingPage.js` (Zeile ~588-704): Separater kombinierter Editor entfernt
- `CreatePostingSmartStepCreator.js`: Logik für Pronomen/State-Speicherung erweitert

#### 2. Alle Pronomen direkt sichtbar
**Vorher:** Nur 3 Pronomen sichtbar, Rest in "Weitere anzeigen"
**Jetzt:** Alle Pronomen (er, sie, es, ihn) direkt sichtbar

**Code:**
```javascript
// CreatePostingSmartStepCreator.js - getVarOptions()
const isPronoun = (varName || '').toLowerCase() === 'pronoun';
const limit = isPronoun ? entries.length : VISIBLE_RANKED_OPTIONS;
```

#### 3. Intelligente Pronomen/State-Speicherung

**Für {{pronoun}} Token:**
```javascript
// Fall 1: Template hat BEIDE Tokens ({{pronoun}} UND {{state}})
if (hasSepState) {
    activeStep.values["pronoun"] = pronoun;  // "sie"
    activeStep.values["state"] = value;       // "geschmeidig"
}
// Fall 2: Template hat NUR {{pronoun}}
else {
    activeStep.values["pronoun"] = combined; // "sie geschmeidig"
}
```

**Für {{state}} Token:**
```javascript
// Immer separate Speicherung
activeStep.values["pronoun"] = pronoun;  // "sie" (falls {{pronoun}} existiert)
activeStep.values["state"] = composed;    // "sie geschmeidig" oder nur "geschmeidig"
```

**Logik in:** `CreatePostingSmartStepCreator.js` - `applyCurrentEditorSelection()` (Zeile ~1647-1677)

#### 4. Kombinierter Editor - HTML-Struktur

**Template-Struktur:**
```html
<div id="InlineVarEditorHost">
  <!-- Pronomen-Sektion (OBEN) -->
  <div class="small text-muted mt-2 mb-1">Pronomen</div>
  <div class="d-flex flex-wrap gap-2 mb-2" id="PronounBtnRow">
    <button data-pick-mode="pronoun" data-pick-value="er">er</button>
    <button data-pick-mode="pronoun" data-pick-value="sie">sie</button>
    <button data-pick-mode="pronoun" data-pick-value="es">es</button>
    <button data-pick-mode="pronoun" data-pick-value="ihn">ihn</button>
  </div>

  <!-- State-Sektion (UNTEN) -->
  <div class="small text-muted mb-1">Zustand</div>
  <div class="d-flex flex-wrap gap-2" id="ValueBtnRow">
    <button data-pick-mode="value" data-pick-value="geschmeidig">geschmeidig</button>
    <button data-pick-mode="value" data-pick-value="verdoppelt">verdoppelt</button>
    <button data-pick-mode="value" data-pick-value="fluffig">fluffig</button>
  </div>

  <!-- Fallback-Sektion (Weitere anzeigen) -->
  <div class="mt-2">
    <button class="js-toggle-fallback-options">Weitere anzeigen ▼</button>
    <div class="js-fallback-options-wrap">
      <!-- Weitere State-Optionen -->
    </div>
  </div>

  <!-- Einsetzen-Button -->
  <button id="BtnPickValueQuick">Einsetzen</button>
</div>
```

**Generiert durch:** `openInlineEditor()` in `CreatePostingSmartStepCreator.js` (Zeile ~1304-1380)

#### 5. Event-Handler-System

**Button-Klicks:**
```javascript
// Pronomen-Button geklickt
if (mode === 'pronoun') {
    host.dataset.selectedPronoun = val;  // "sie"
}

// State-Button geklickt
if (mode === 'value') {
    host.dataset.selectedValue = val;    // "geschmeidig"
}

// Einsetzen-Button geklickt
applyCurrentEditorSelection();
```

**Handler-Registrierung:**
- **Smart Step Creator:** Document-Level Event Delegation (Zeile ~1895-2020)
- **Wahrscheinlichkeitsbereich:** Host-Level Event Binding (CreatePostingPage.js, Zeile ~645-697)

### Datenfluss

```
User klickt {{pronoun}} Token
  ↓
openInlineEditor(varName='pronoun', tokenId)
  ↓
Erkennt isPronounOrState=true
  ↓
Lädt Pronomen-Optionen: getVarOptions('pronoun') → alle 4 Pronomen
Lädt State-Optionen: getVarOptions('state') → gefilterte States
  ↓
Rendert kombiniertes HTML mit beiden Sektionen
  ↓
User wählt "sie" (Pronomen-Button)
  → host.dataset.selectedPronoun = "sie"
  ↓
User wählt "geschmeidig" (State-Button)
  → host.dataset.selectedValue = "geschmeidig"
  ↓
User klickt "Einsetzen"
  ↓
applyCurrentEditorSelection()
  ↓
Prüft: hasSepState = /{{state}}/i.test(template)
  ↓
Fall 1 (hasSepState=true):
  activeStep.values["pronoun"] = "sie"
  activeStep.values["state"] = "geschmeidig"
  ↓
Fall 2 (hasSepState=false):
  activeStep.values["pronoun"] = "sie geschmeidig"
  ↓
rerenderAfterValueSet()
  → Template wird neu gerendert mit aktualisierten Werten
```

### Entfernte Features

**Separater kombinierter Editor (CreatePostingPage.js):**
- ❌ Auto-Close beim Pronomen-Klick (varKey='pronoun')
- ❌ Auto-Close beim State-Klick (beide ausgewählt)
- ❌ Separate Event-Handler (`.js-prob-combined-pronoun`, `.js-prob-combined-state`)
- ❌ Doppelte HTML-Generierung

**Sequentielle Bearbeitung:**
- ❌ Automatisches Öffnen des State-Editors nach Pronomen-Auswahl
- ❌ Code in `create-posting-probability.js` (Zeile 229-239, 250-257)
- ❌ Code in `CreatePostingPage.js` (Zeile ~5099-5117)
- ❌ Code in `CreatePostingSmartStepCreator.js` (Zeile ~1634-1654)

### Verbesserungen

1. **Konsistenz:** Gleiche UX in beiden Bereichen (Wahrscheinlichkeit & Smart Step Creator)
2. **Vollständigkeit:** Alle Pronomen direkt sichtbar, kein "Weitere anzeigen" nötig
3. **Intelligenz:** Automatische Erkennung ob separate Tokens existieren
4. **Wartbarkeit:** Ein Editor-System statt zwei parallele Implementierungen
5. **Flexibilität:** Funktioniert mit Templates die {{pronoun}}, {{state}} oder beide haben

### Betroffene Funktionen

**CreatePostingSmartStepCreator.js:**
- `getVarOptions()` - Zeile 511-517: Pronomen-Limit entfernt
- `openInlineEditor()` - Zeile 1304-1380: Kombinierter Editor für isPronounOrState
- `buildInlineEditorHtml()` - Zeile 2138-2216: Gleiche Logik wie openInlineEditor
- `applyCurrentEditorSelection()` - Zeile 1529-1677: Intelligente Pronomen/State-Speicherung
- `renderFallbackSection()` - Zeile 1379, 2216: Nutzt 'state' für isPronounOrState

**CreatePostingPage.js:**
- `openProbVarInlineEditor()` - Zeile 542-698: Separater Editor entfernt, nutzt buildInlineEditorHtml
- `doApply()` - Zeile 618-643: Pronomen-Extras-Handling für beide Variablen

**create-posting-probability.js:**
- `bindInlineEvents()` - Zeile 219-275: Sequentielle Logik deaktiviert (auskommentiert)

### Test-Szenarien

✅ **Template nur mit {{pronoun}}:**
- Auswahl: "sie" + "geschmeidig" → Ergebnis: {{pronoun}} = "sie geschmeidig"

✅ **Template mit {{pronoun}} UND {{state}}:**
- Auswahl: "sie" + "geschmeidig" → Ergebnis: {{pronoun}} = "sie", {{state}} = "geschmeidig"

✅ **Template mit {{state}} (kein {{pronoun}}):**
- Auswahl: "sie" + "geschmeidig" → Ergebnis: {{state}} = "sie geschmeidig"

✅ **Nur Pronomen ausgewählt:**
- {{pronoun}} Editor: Ergebnis: {{pronoun}} = "sie", {{state}} = leer
- {{state}} Editor: Ergebnis: {{pronoun}} = "sie", {{state}} = leer

✅ **Nur State ausgewählt:**
- Ergebnis: {{state}} = "geschmeidig", {{pronoun}} = leer

---

## Änderungen 2026-03-28: PREP_WASH_01 erweitert mit {{removal}} Variable

### Überblick
Der PREP_WASH_01 Step wurde erweitert um flexibles Entfernen von Schale, Gräten, Hülle etc. beim Waschen von Zutaten.

### Hauptänderungen

#### 1. Neue Variable {{removal}} erstellt

**8 Optionen in allen 10 Sprachen:**

| Key | DE | EN | ESP | PRT | ID | NL | SV | DA | NO | MS |
|-----|----|----|-----|-----|----|----|----|----|----|----|
| **schale** | die Schale | the peel | la cáscara | a casca | kulitnya | de schil | skalet | skallen | skallet | kulitnya |
| **graeten** | die Gräten | the bones | las espinas | as espinhas | durinya | de graten | benen | benene | beinene | tulangnya |
| **huelle** | die Hülle | the hull | la vaina | a vagem | kulitnya | de schil | skalet | bælgen | belgen | kulitnya |
| **stiele** | die Stiele | the stems | los tallos | os talos | batangnya | de stelen | stjälkarna | stilkene | stilkene | tangkainya |
| **kerne** | die Kerne | the seeds | las semillas | as sementes | bijinya | de pitten | kärnorna | kernerne | kjernene | bijinya |
| **faeden** | die Fäden | the strings | las hebras | os fios | seratnya | de draden | trådarna | trådene | trådene | seratnya |
| **sand** | den Sand | the sand | la arena | a areia | pasirnya | het zand | sanden | sandet | sanden | pasirnya |
| **innereien** | die Innereien | the innards | las vísceras | as vísceras | isi perutnya | de ingewanden | inälvorna | indvoldene | innvollene | isi perutnya |

**Tags:** `variable:removal`, jeweiliger Value-Tag, `prep`, `wash`, `clean`, plus spezifische Tags wie `fish`, `seafood`, `beans`, `vegetable`, `mussels`

**Datei:** `master_step_variables.json` (Zeile ~6147-6246)

#### 2. {{action}} Variable erweitert

**Neu hinzugefügte Wasch-Aktionen:**

| Key | DE | EN | ESP | PRT | ID | NL | SV | DA | NO | MS |
|-----|----|----|-----|-----|----|----|----|----|----|----|
| **wasche** | wasche | wash | lava | lave | cuci | was | tvätta | vask | vask | basuh |
| **spuele** | spüle | rinse | enjuaga | enxágue | bilas | spoel | skölj | skyl | skyll | bilas |
| **reinige** | reinige | clean | limpia | limpe | bersihkan | reinig | rengör | rens | rengjør | bersihkan |

**Tags:** `variable:action`, jeweiliger Value-Tag, `prep`, `wash`, `clean`

**Bereits vorhanden:**
- **putze** (clean) - Tags: `variable:action`, `value:putze`
- **tupfe** (pat) - Tags: `variable:action`, `value:tupfe`, `prep`, `dry`, `wash`

**Datei:** `master_step_variables.json` (Zeile ~900-975)

#### 3. PREP_WASH_01 Template aktualisiert

**Vorher:**
```json
"de": "Wasche {{ingredient}} gründlich und {{action}} {{pronoun}} trocken."
```

**Jetzt:**
```json
"de": "{{action}} {{ingredient}} gründlich unter kaltem Wasser[, entferne {{removal}}] und tupfe {{pronoun}} trocken."
```

**Änderungen:**
- ✅ **{{action}}** Variable am Anfang (flexibel: wasche/spüle/reinige/putze)
- ✅ **"unter kaltem Wasser"** hinzugefügt (Best Practice)
- ✅ **[, entferne {{removal}}]** als optionales Segment
- ✅ **"tupfe"** fest am Ende (spezifischer als {{action}})
- ✅ Description erweitert: "Waschen, optionales Entfernen (Schale/Gräten/etc.) und Trocknen von Zutaten"

**Variables-Konfiguration:**
```json
"variables": ["action", "ingredient", "removal", "pronoun"],
"required_variables": ["action", "ingredient", "pronoun"],
"optional_variables": ["removal"]
```

**Datei:** `master_steps.json` (Zeile ~1274-1302)

#### 4. Alle 10 Sprachen aktualisiert

| Sprache | Template |
|---------|----------|
| **DE** | {{action}} {{ingredient}} gründlich unter kaltem Wasser[, entferne {{removal}}] und tupfe {{pronoun}} trocken. |
| **EN** | {{action}} {{ingredient}} thoroughly under cold water[, remove {{removal}}] and pat {{pronoun}} dry. |
| **ESP** | {{action}} {{ingredient}} a fondo bajo agua fría[, quita {{removal}}] y seca bien. |
| **PRT** | {{action}} {{ingredient}} bem sob água fria[, remova {{removal}}] e seque bem. |
| **ID** | {{action}} {{ingredient}} hingga bersih di bawah air dingin[, buang {{removal}}] lalu tepuk hingga kering. |
| **NL** | {{action}} {{ingredient}} grondig onder koud water[, verwijder {{removal}}] en dep {{pronoun}} droog. |
| **SV** | {{action}} {{ingredient}} noggrant under kallt vatten[, ta bort {{removal}}] och torka {{pronoun}} torr. |
| **DA** | {{action}} {{ingredient}} grundigt under koldt vand[, fjern {{removal}}] og dup {{pronoun}} tør. |
| **NO** | {{action}} {{ingredient}} grundig under kaldt vann[, fjern {{removal}}] og tork {{pronoun}} tørr. |
| **MS** | {{action}} {{ingredient}} hingga bersih di bawah air sejuk[, buang {{removal}}] dan lap sehingga kering. |

### Beispiel-Outputs

**Szenario 1: Kartoffeln schälen**
```
Input:
- action: "Wasche"
- ingredient: "die Kartoffeln"
- removal: "die Schale"
- pronoun: "sie"

Output (DE): "Wasche die Kartoffeln gründlich unter kaltem Wasser, entferne die Schale und tupfe sie trocken."
Output (EN): "Wash the potatoes thoroughly under cold water, remove the peel and pat them dry."
```

**Szenario 2: Fisch entgräten**
```
Input:
- action: "Spüle"
- ingredient: "den Fisch"
- removal: "die Gräten"
- pronoun: "ihn"

Output (DE): "Spüle den Fisch gründlich unter kaltem Wasser, entferne die Gräten und tupfe ihn trocken."
Output (EN): "Rinse the fish thoroughly under cold water, remove the bones and pat it dry."
```

**Szenario 3: Salat ohne Entfernung**
```
Input:
- action: "Wasche"
- ingredient: "den Salat"
- removal: null (leer)
- pronoun: "ihn"

Output (DE): "Wasche den Salat gründlich unter kaltem Wasser und tupfe ihn trocken."
Output (EN): "Wash the lettuce thoroughly under cold water and pat it dry."
```

**Szenario 4: Bohnen mit Fäden**
```
Input:
- action: "Reinige"
- ingredient: "die Bohnen"
- removal: "die Fäden"
- pronoun: "sie"

Output (DE): "Reinige die Bohnen gründlich unter kaltem Wasser, entferne die Fäden und tupfe sie trocken."
Output (EN): "Clean the beans thoroughly under cold water, remove the strings and pat them dry."
```

### Anwendungsfälle

**{{removal}} Optionen nach Zutat:**

| Zutat | Empfohlene {{removal}} Option |
|-------|------------------------------|
| Kartoffeln, Karotten | **schale** (die Schale) |
| Fisch (Filet) | **graeten** (die Gräten) |
| Erbsen, Bohnen | **huelle** (die Hülle) oder **faeden** (die Fäden) |
| Spinat, Mangold | **stiele** (die Stiele) |
| Paprika, Tomaten | **kerne** (die Kerne) |
| Muscheln | **sand** (den Sand) |
| Ganzer Fisch | **innereien** (die Innereien) |

### Technische Details

**Optionales Segment:**
```
[, entferne {{removal}}]
```
- Wird nur angezeigt wenn `{{removal}}` gesetzt ist
- Komma wird automatisch mit ausgegeben
- Funktioniert durch Template-Rendering-Logik in `resolveOptionalTemplateSegments()` (CreatePostingSmartStepCreator.js)

**Variable-Reihenfolge:**
```json
"variables": ["action", "ingredient", "removal", "pronoun"]
```
- Reihenfolge bestimmt Display-Reihenfolge im Smart Step Creator
- `action` zuerst → User wählt erst die Wasch-Aktion
- `ingredient` zweitens → dann die Zutat
- `removal` drittens → optional: was entfernen
- `pronoun` letztes → Pronomen für "tupfe {{pronoun}}"

### Verbesserungen

1. **Flexibilität:** User kann Wasch-Aktion wählen (wasche/spüle/reinige)
2. **Präzision:** "unter kaltem Wasser" statt nur "gründlich"
3. **Optionalität:** {{removal}} nur wenn nötig
4. **Konsistenz:** "tupfe" statt generisches {{action}} am Ende
5. **Vollständigkeit:** 8 häufige Entfernungs-Szenarien abgedeckt
6. **Mehrsprachigkeit:** Alle Optionen in 10 Sprachen verfügbar

### Betroffene Dateien

1. **master_step_variables.json**
   - {{action}} erweitert: +3 Optionen (wasche, spüle, reinige)
   - {{removal}} neu: +8 Optionen (schale, graeten, huelle, stiele, kerne, faeden, sand, innereien)

2. **master_steps.json**
   - PREP_WASH_01: Templates in 10 Sprachen aktualisiert
   - Variables: +1 (removal)
   - Optional_variables: +1 (removal)
   - Description erweitert

---

## Änderungen 2026-03-28: Entfernung des PREP_SEPARATE_01 Steps

### Übersicht

Der Step **PREP_SEPARATE_01** ("Trenne {{ingredient}} vorsichtig von {{ingredient2}}") wurde vollständig entfernt.

**Grund:** Fragliche Nützlichkeit - das Trennen von Zutaten (z.B. Eigelb von Eiweiß) wird selten benötigt und kann durch andere Steps oder manuelle Beschreibung abgebildet werden.

---

### Entfernter Step

**master_steps.json - PREP_SEPARATE_01**

```json
{
  "master_id": "PREP_SEPARATE_01",
  "phase": 1,
  "sub_group": "ingredient_prep",
  "action": "separate",
  "equipment": 0,
  "description": "Trennen von Zutaten (z.B. Eier in Eigelb und Eiweiß)",
  "templates": {
    "de": "Trenne {{ingredient}} vorsichtig von {{ingredient2}}.",
    "en": "Carefully separate {{ingredient}} from {{ingredient2}}.",
    "esp": "Separa {{ingredient}} de {{ingredient2}} con cuidado.",
    "prt": "Separe {{ingredient}} de {{ingredient2}} com cuidado.",
    "id": "Pisahkan {{ingredient}} dari {{ingredient2}} dengan hati-hati.",
    "nl": "Scheid {{ingredient}} voorzichtig van {{ingredient2}}.",
    "sv": "Separera {{ingredient}} försiktigt från {{ingredient2}}.",
    "da": "Adskil {{ingredient}} forsigtigt fra {{ingredient2}}.",
    "no": "Skill {{ingredient}} forsiktig fra {{ingredient2}}.",
    "ms": "Asingkan {{ingredient}} daripada {{ingredient2}} dengan berhati-hati."
  },
  "variables": ["ingredient", "ingredient2"],
  "required_variables": ["ingredient", "ingredient2"],
  "selection_tags": ["phase:prep", "action:separate", "technique:separate", "trennen", "separate", ...]
}
```

**Gelöscht:** Zeilen 2149-2198 (50 Zeilen)

---

### Betroffene Dateien

| Datei | Änderungen |
|-------|------------|
| **master_steps.json** | PREP_SEPARATE_01 Step entfernt (50 Zeilen) |
| **CreatePosting-Dokumentation.md** | Dokumentation aktualisiert |

---

### Alternative

Falls das Trennen von Zutaten beschrieben werden soll, kann der User:
- **Manuelle Beschreibung** verwenden (freier Text im Step Creator)
- **PREP_CUT_01** verwenden mit passendem Text
- **Bestehende Steps kombinieren** (z.B. "Knacke Ei" + "Entferne Eigelb")

---

## Änderungen 2026-03-27: Duration-Editor CSS-Klassen-Bug behoben

### Überblick

Ein kritischer Bug verhinderte das Öffnen von Special Editoren (duration/temp/count) im Probability Area. Der Editor konnte nicht gefunden werden, weil die CSS-Klasse `.prob-inline-editor` fehlte.

### Symptome:

```javascript
[Probability Var Click] Opening editor for: {masterId: 'PREP_KNEAD_01', varKey: 'duration', ...}
[Probability Area] Editor element not found in overlay!
```

**Ursache:** In `_generateEditorHtml()` fehlte bei Special Editoren die bedingte `.prob-inline-editor` Klasse.

### Root Cause Analysis:

**Regular Editor (Zeile 1891):**
```javascript
const html = `<div class="duration-editor mt-2${useClassBasedIds ? ' prob-inline-editor' : ''}" ...>
```
✅ Fügt `.prob-inline-editor` hinzu wenn `useClassBasedIds: true`

**Special Editor (Zeile 1760 - VORHER):**
```javascript
html: `<div class="duration-editor mt-2" data-editor-for="${escapeHtml(varName)}">
```
❌ `.prob-inline-editor` fehlt komplett

**Special Editor (Zeile 1760 - NACHHER):**
```javascript
html: `<div class="duration-editor mt-2${useClassBasedIds ? ' prob-inline-editor' : ''}" data-editor-for="${escapeHtml(varName)}">
```
✅ Konsistent mit Regular Editor

### Fix:

**CreatePostingSmartStepCreator.js (Zeile 1760):**

Hinzugefügt: `${useClassBasedIds ? ' prob-inline-editor' : ''}` zur CSS-Klasse des Special Editor Wrappers.

**Warum war das wichtig?**

- `useClassBasedIds: true` wird von der Probability Area verwendet (CreatePostingPage.js, Zeile 611)
- Der Probability Area Code sucht nach `.prob-inline-editor` (CreatePostingPage.js, Zeile 667, 704)
- Ohne diese Klasse konnte der Editor nicht gefunden werden → "Editor element not found in overlay!"

### Betroffene Dateien:

| Datei | Zeilen | Änderungen |
|-------|--------|------------|
| **CreatePostingSmartStepCreator.js** | 1760 | `.prob-inline-editor` Klasse hinzugefügt bei `useClassBasedIds: true` |
| **CreatePosting-Dokumentation.md** | 1255 | Bugfix #9 in Liste hinzugefügt |

### Teste:

1. Öffne Probability Area
2. Klicke auf ein `{{duration}}` Token
3. Editor sollte sich öffnen mit neuer 3-Row-Struktur (Inputs → Chips → Actions)
4. Console sollte keine Fehler zeigen

---

## Änderungen 2026-03-27: Duration-Editor Layout optimiert

### Überblick

Das Duration-Editor-Overlay wurde umstrukturiert für eine klarere visuelle Hierarchie und bessere Benutzerführung.

### Vorher (alte Struktur):
```
Row 1: [Input Von] [–] [Input Bis] [Einsetzen] [Schließen]  ← Inputs und Actions gemischt
Row 2: [Minute] [Stunde]                                      ← Unit-Chips separat
Row 3: [Pro Packung]                                          ← Per-Package separat
```

### Nachher (neue Struktur):
```
Row 1: [Input Von] [–] [Input Bis]                           ← Nur Inputs
Row 2: [Minute] [Stunde] [Pro Packung]                       ← Alle Unit-Chips zusammen
Row 3: [Einsetzen] [Schließen]                               ← Action-Buttons separat
```

### Vorteile:
- **Klarere visuelle Trennung** zwischen Eingabe, Auswahl und Aktionen
- **Alle Unit-Chips zusammen** für einfacheren Vergleich und Auswahl
- **Action-Buttons am Ende** folgen dem natürlichen Lese- und Interaktionsfluss

### Betroffene Dateien:

| Datei | Zeilen | Änderungen |
|-------|--------|------------|
| **CreatePostingSmartStepCreator.js** | 2223-2248 | `renderSpecialEditor()` - Duration-Editor HTML-Struktur umstrukturiert: Input-Row, Chips-Row (inkl. Pro Packung), Action-Row. Alle Action-Rows erhalten `.js-editor-action-row` Klasse (duration/temp/count Zeile 2245/2267/2277, regular editor Zeile 1930) |
| **CreatePostingPage.js** | 617 | Selector-Fix: `.js-editor-action-row` statt `.d-flex.gap-2.align-items-center` (verhindert, dass Input-Row als Button-Row extrahiert wird) |
| **CreatePosting-Dokumentation.md** | - | Dokumentation dieser Änderung hinzugefügt |

### Technische Details:

**CreatePostingSmartStepCreator.js (Zeile 2223-2248):**

```javascript
// Row 1: Nur Input-Felder
<div class="d-flex gap-2 align-items-center">
  <input type="number" id="DurationValueInput" ... placeholder="Von" />
  <span class="text-muted small">–</span>
  <input type="number" id="DurationValueToInput" ... placeholder="Bis" />
</div>

// Row 2: Alle Unit-Chips zusammen (Minute/Stunde/Pro Packung)
<div class="d-flex flex-wrap gap-2 mt-2">
  ${unitBtns}  // Minute, Stunde
  ${perPackage ? '<button ... js-duration-per-package ...>' : ''}
</div>

// Row 3: Action-Buttons (mit .js-editor-action-row Marker)
<div class="d-flex gap-2 align-items-center mt-2 js-editor-action-row">
  <button id="BtnPickDurationQuick">Einsetzen</button>
  <button id="BtnCloseVarTop">Schließen</button>
</div>
```

**Änderungen:**
- Per-Package-Button ist jetzt inline mit den anderen Unit-Chips statt in separatem Block
- `.js-editor-action-row` Klasse hinzugefügt zur Action-Button-Row (auch bei temp/count/regular editors)
- **Probability Area Fix:** Zeile 617 in CreatePostingPage.js nutzt jetzt `.js-editor-action-row` Selector statt `.d-flex.gap-2.align-items-center` (verhindert, dass Input-Row als Button-Row extrahiert wird)

---

## 2026-03-28: Ingredient-Transformationen nach Step-Accept aktualisieren

### Problem:
Nach dem Akzeptieren eines Steps wurden Ingredient-Transformationen nicht angewendet. Die Ei-Aufteilung (Ei → Eiklar/Eigelb) funktionierte beim Hinzufügen von Eiern, aber wenn ein Step akzeptiert wurde, wurde `applyDerivedIngredientRowVisuals()` nicht aufgerufen.

### Lösung:
In `acceptActiveStep()` wird jetzt `refreshMasterTemplateBuilder()` aufgerufen, was wiederum `applyDerivedIngredientRowVisuals()` ausführt.

**CreatePostingSmartStepCreator.js (Zeile 3376-3381):**
```javascript
if (typeof window.updateStepIndices === "function") {
    window.updateStepIndices();
} else if (typeof window.updateStoryProgress === "function") {
    window.updateStoryProgress();
}

// ✅ NEU (2026-03-28): Transformationen nach Step-Accept anwenden
if (typeof window.refreshMasterTemplateBuilder === "function") {
    window.refreshMasterTemplateBuilder();
}
```

### Verhalten:
- Nach jedem Step-Accept werden Ingredient-Transformationen neu berechnet
- Ei-Aufteilung (Ei → Eiklar/Eigelb) wird korrekt aktualisiert
- Chips und Ingredient-List bleiben synchron

### Betroffene Dateien:

| Datei | Zeilen | Änderungen |
|-------|--------|------------|
| **CreatePostingSmartStepCreator.js** | 3381-3384 | `acceptActiveStep()` - Ruft `refreshMasterTemplateBuilder()` nach Step-Accept auf, um Transformationen anzuwenden |
| **CreatePosting-Dokumentation.md** | - | Dokumentation aktualisiert |

---

> **Letzte Aktualisierung:** 2026-03-28
> **Geänderte Dateien:** 1 (CreatePostingSmartStepCreator.js)
