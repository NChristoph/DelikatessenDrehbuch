# CreatePosting — Vollständige technische Dokumentation

> **WorldMiniApp** · Rezept-Erstellungs-Modul
> **Basispfad:** `DelikatessenDrehbuch/`
> **Stand:** 2026-03-23

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
15. [JS-Datei: master-step-renderer.js](#15-js-datei-master-step-rendererjs)
16. [JS-Datei: recipe-step-suggest.js](#16-js-datei-recipe-step-suggestjs)
17. [JS-Datei: create-posting-probability.js](#17-js-datei-create-posting-probabilityjs)
18. [JS-Datei: ingredientmanager.js](#18-js-datei-ingredientmanagerjs)
19. [JS-Datei: CreatePostingSmartStepCreator.js](#19-js-datei-createpostingsmartstepcreatjs)
20. [JS-Datei: CreatePostingPage.js](#20-js-datei-createpostingpagejs)
21. [JS-Datei: create-posting-publish-checklist.js](#21-js-datei-create-posting-publish-checklistjs)
22. [Window-API-Übersicht](#22-window-api-übersicht)
23. [Event-Listener-Katalog](#23-event-listener-katalog)
24. [DOM-Element-Verzeichnis](#24-dom-element-verzeichnis)
25. [Datenfluss & User-Flow](#25-datenfluss--user-flow)
26. [Initialisierungs-Sequenz](#26-initialisierungs-sequenz)

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

### JavaScript-Dateien (12 Dateien, ~9.236 Zeilen)

| # | Datei | Pfad (unter `wwwroot/js/`) | Zeilen | Zweck |
|---|-------|---------------------------|--------|-------|
| 1 | `create-posting-utils.js` | `wwwroot/js/` | 74 | Kern-Utilities: escapeHtml, resolveLangKey, fetchJson, toast |
| 2 | `create-posting-data-urls.js` | `wwwroot/js/` | 11 | URL-Konfiguration aller JSON-Endpunkte |
| 3 | `create-posting-data-store.js` | `wwwroot/js/` | 98 | Daten-Cache mit Deduplizierung & Validierung |
| 4 | `create-posting-feedback.js` | `wwwroot/js/` | 41 | Toast-Nachrichten & Fehler-Reporting |
| 5 | `create-posting-page-data.js` | `wwwroot/js/` | 17 | Async-Loader für Article-Rules & Transforms |
| 6 | `create-posting-template-builder.js` | `wwwroot/js/` | 115 | Template-Card-UI-Builder |
| 7 | `master-step-renderer.js` | `wwwroot/js/` | 478 | Template-Rendering-Engine mit Variablen-Substitution |
| 8 | `recipe-step-suggest.js` | `wwwroot/js/` | 513 | Rezepttyp-Erkennung & Step-Vorschläge |
| 9 | `create-posting-probability.js` | `wwwroot/js/` | 378 | Wahrscheinlichkeits-Analyse & Template-Vorschläge |
| 10 | `ingredientmanager.js` | `wwwroot/js/` | 145 | Maßeinheiten-Verwaltung (mehrsprachig, 25 Units) |
| 11 | `CreatePostingSmartStepCreator.js` | `wwwroot/js/` | 2.253 | Haupt-State-Machine des Step-Creators |
| 12 | `CreatePostingPage.js` | `wwwroot/js/` | 5.112 | Haupt-Page-Controller |
| 13 | `create-posting-publish-checklist.js` | `wwwroot/js/` | 307 | Fortschritts-Checkliste (5-Schritte-Tracker) |

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
| `.chip-icon` | (inline) | Icon-Container innerhalb des Chips |

#### Fraction-Picker (neu)

| Klasse | Zeile | Eigenschaft |
|--------|-------|-------------|
| `.fraction-picker-row` | 311 | Border-top 1px solid, padding-top 8px, margin-top 6px |
| `.fraction-pick` | 316 | Font-size 0.78rem, padding 4px 10px, border-radius 12px |
| `.fraction-pick.active` | 323 | Lila border/shadow/background |

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

**Pfad:** `wwwroot/js/create-posting-template-builder.js` · **115 Zeilen**
**Export:** `window.CreatePostingTemplateBuilder`

| Methode | Beschreibung |
|---------|-------------|
| `setMasterTemplateError(message)` | Fehlermeldung im Template-Container anzeigen |
| `renderTemplateCards(deps)` | Alle Master-Template-Karten rendern (mit aktuellem Theme) |
| `renderSc2TemplateCards(deps)` | Template-Karten für zweiten Smart-Creator rendern |
| `refreshMasterTemplateBuilder(deps)` | Komplettes Template-Builder-UI neu aufbauen |

**Abhängigkeiten:**
- `window.MasterStepRenderer.getAllTemplates()`
- `window.MasterStepRenderer.getLastLoadError()`
- `window.getThemeMutedTextClass()`
- `window.getCreatePostingTheme()`

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
**Export:** `window.CreatePostingProbability`

| Methode | Beschreibung |
|---------|-------------|
| `create(deps)` | Factory: erstellt Probability-Modul-Instanz |
| → `.refresh()` | Analyse mit aktuellen Zutaten neu berechnen |
| → `.showSuggestions()` | Template-Vorschläge nach erkanntem Rezepttyp zeigen |

**Schwellenwerte:**
- `>= 60%` → Erfolg (grün)
- `>= 35%` → Primär (blau)
- `< 35%` → Niedrig

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

#### Fraction-Helpers (NEU, Zeile 97–190)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `getFractionOptions()` | 97 | Bruch-Optionen aus JSON lesen, lokalisiert zurückgeben |
| `composeFractionText(fractionDef, ingredientName)` | 112 | z.B. `"die Hälfte Mehl"` erzeugen |
| `findMatchingFractionOption(num, den, opts)` | 117 | Passende Fraction-Option finden |
| `getRemainderChipsFromAcceptedSteps()` | 123 | Akzeptierte Steps scannen, Rest-Chips berechnen |
| `renderFractionPickerHtml(fractionOptions)` | 176 | Fraction-Picker UI rendern |

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
| `getArticleOptions()` | 515 | Artikel-Optionen |
| `getDurationUnits()` | 521 | Dauer-Einheiten |

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
| `getSelectedIngredientsFromPage()` | 1139 | ALLE ausgewählten Zutaten (inkl. Remainder) |
| `parseSelectedIngredientValues(rawValue, options)` | 1193 | Zutat-Werte parsen |
| `formatSelectedIngredientList(names, langKey)` | 1216 | Zutaten-Liste formatieren ("X und Y") |

#### Variablen-Typ-Prüfung

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `isIngredientVariable(varName)` | 1050 | Prüft: ingredient, ingredient2, ingredients, liquid, fat |
| `isGrindSizeVariable(varName)` | 1055 | Prüft: grind_size |
| `isNoArticleVariable(varName)` | 1060 | Prüft: ingredients, seasonings, components |
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
| `renderSpecialEditor(varName, currentVal)` | 1362 | Spezial-Editor (duration/temp/count) |

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

#### Exportierte Probability-Helpers (Zeile 2019–2180)

| Funktion | Zeile | Beschreibung |
|----------|-------|-------------|
| `buildInlineEditorHtml(varName, currentVal, opts)` | 2021 | Editor-HTML generieren (für Probability-View) |
| `applyEditorValue(editorEl)` | 2089 | Editor-Wert auslesen und zusammensetzen |
| `applyEditorExtras(editorEl)` | 2136 | Zusätzliche Werte (z.B. Pronomen) |
| `triggerPronounBeforeState(config)` | 2148 | Pronomen-vor-State-Logik |

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
| `openProbVarEditor(masterId, varKey, ...)` | 353 | Variablen-Editor-Modal öffnen |
| `closeProbVarEditor()` | 442 | Editor schließen |
| `applyProbVarEditor()` | 452 | Ausgewählten Wert anwenden |
| `openProbVarInlineEditor(masterId, varKey, ...)` | 486 | Inline-Editor in Card öffnen |

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
| `getBaseSandboxIngredients()` | 767 | Basis-Zutaten (nicht abgeleitet) |
| `buildDerivedIngredientName(base, adjective)` | 810 | Abgeleitete Adjektiv-Form erstellen |
| `getStepVariableDisplayValue(step, varName)` | 829 | Variablenwert aus Step-Metadaten |
| `resolveCutTransformationType(step)` | 835 | Schnitt-Form aus Step-Variablen erkennen |
| `matchSandboxItemsByStep(items, step)` | 849 | Zutaten zu Step-Variablen zuordnen |
| `buildSpecialTransformItems(step)` | 877 | Spezial-Transform-Outputs erstellen |
| `collectSelectedStepDerivationDescriptors()` | 893 | Alle Step-Transform-Descriptors sammeln |
| `deriveIngredientsForSteps(baseItems, ...)` | 911 | Abgeleitete Zutaten generieren |
| `deriveSandboxIngredients()` | 998 | Wrapper für Zutat-Derivation |
| `getSelectedIngredientsForSandbox(langKey)` | 1005 | Ausgewählte Zutaten für Sandbox |
| `localizeIngredientValueForSandbox(value, langKey, ...)` | 1020 | Zutat-Wert lokalisieren |
| `findCatalogIngredientRowByNames(names)` | 1047 | Zutat-Zeile per Name finden |
| `setIngredientRowDisabled(row, disabled)` | 1063 | Zutat-Zeile deaktivieren/aktivieren |
| `buildDerivedIngredientRowHtml(item)` | 1067 | HTML für abgeleitete Zutat-Zeile |
| `applyDerivedIngredientRowVisuals()` | 1119 | Alle abgeleiteten Zutat-Rows aktualisieren |

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
| `getPlaceholderType(key)` | 1567 | Variablen-Typ bestimmen |
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
| `getLocalizedIngredientData(row)` | 3074 | Zutat-Daten lokalisiert |
| `buildIngredientDataAttributes(data)` | 3084 | Data-Attribute aus lokalen Daten |
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
| `removeIngredientRow(btn)` | 3295 | Zutat aus Auswahl entfernen |
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

## 22. Window-API-Übersicht

### Globale Objekte

| Object | Quelle | Beschreibung |
|--------|--------|-------------|
| `window.CreatePostingUtils` | create-posting-utils.js | Kern-Utilities |
| `window.CreatePostingDataUrls` | create-posting-data-urls.js | URL-Registry (frozen) |
| `window.CreatePostingDataStore` | create-posting-data-store.js | Daten-Cache |
| `window.CreatePostingFeedback` | create-posting-feedback.js | Feedback/Toast |
| `window.CreatePostingPageData` | create-posting-page-data.js | Async-Loader |
| `window.CreatePostingTemplateBuilder` | create-posting-template-builder.js | Template-Builder |
| `window.MasterStepRenderer` | master-step-renderer.js | Rendering-Engine |
| `window.RecipeStepSuggest` | recipe-step-suggest.js | Rezepttyp-Erkennung |
| `window.CreatePostingProbability` | create-posting-probability.js | Wahrscheinlichkeit |
| `window.IngredientManager` | ingredientmanager.js | Maßeinheiten |
| `window.MasterStepCreatorHelpers` | SmartStepCreator.js | Step-Creator-Helpers |
| `window.CreatePostingIngredientHelpers` | CreatePostingPage.js | Zutat-Helpers |

### Globale State-Variablen

| Variable | Quelle | Beschreibung |
|----------|--------|-------------|
| `window.CreatePostingCurrentTheme` | CreatePostingPage.js:1226 | Aktuelles Theme |
| `window.CreatePostingThemeMode` | CreatePostingPage.js:1227 | Theme-Mode-Funktion |
| `window.ingredientTransforms` | CreatePostingPage.js:665 | Gecachte Transforms |
| `window.stepCatalog` | CreatePostingPage.js:701 | Gecachter Step-Katalog |
| `window.stepIngredientBindings` | CreatePostingPage.js | Step↔Zutat-Bindungen |

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
| click | `#clearCreatePostingDraftBtn` | ~3954 | Draft löschen |
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
| click | `#BtnApplyVar` | 1852 | Wert einsetzen → `applyCurrentEditorSelection()` |
| click | `[data-pick-mode="fraction"]` | 1866 | **Fraction wählen** |
| click | `[data-pick-mode="ingredient-value"]` | 1880 | Zutat-Chip togglen + Fraction-Picker Show/Hide |
| click | `[data-pick-mode="article"]` | 1922 | Artikel wählen |
| click | `[data-pick-mode="pronoun"]` | 1922 | Pronomen wählen |
| click | `[data-pick-mode="value"]` | 1922 | Wert wählen |
| click | `[data-duration-unit]` | 1930 | Dauer-Einheit wählen |
| click | `#BtnPickDurationQuick` | 1947 | Dauer schnell einsetzen |
| click | `#BtnPickCountQuick` | 1969 | Count schnell einsetzen |
| click | `#BtnPickTempQuick` | 1977 | Temperatur schnell einsetzen |
| change | `#LangSelect` | 1988 | Sprache wechseln |

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
    → composeFractionText({label:"die Hälfte"}, "Mehl")
    → activeStep.values.ingredient = "die Hälfte Mehl"
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
   ├→ consumeCreatePostingDraftResetFlag()
   │   └→ Falls Flag: clearCreatePostingDraft()
   ├→ hasMeaningfulCreatePostingDraft()
   │   └→ Falls ja: restoreCreatePostingDraft()
   └→ updateLanguageLabels()
```

---

> **Externe Abhängigkeiten:** jQuery, Bootstrap 5, Bootstrap Icons
> **Unterstützte Sprachen:** DE, EN, ESP, PRT, ID, NL, SV, DA, NO, MS (10 Sprachen)
> **Tech-Stack:** ASP.NET Core 8.0 MVC · C# · JavaScript (Vanilla + jQuery) · CSS3
