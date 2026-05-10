# Master Steps + Variables JSON Split - Implementation Summary

## Overview

The master_steps.json (230KB) and master_step_variables.json (187KB) have been **merged and split** into 10 language-specific files (~195KB each) to improve frontend load performance.

**Key Innovation:** Variable options now use **language-independent IDs** that are identical across all languages, making database storage and cross-language comparison trivial.

## Changes Made

### 1. Created Unified Language-Specific JSON Files

**Script:** `split_master_steps.py`

**Generated Files (Each contains Steps + Variable Options):**
- `master_steps.de.json` (German) - 195.5 KB
- `master_steps.en.json` (English) - 194.0 KB
- `master_steps.esp.json` (Spanish) - 194.8 KB
- `master_steps.prt.json` (Portuguese) - 194.5 KB
- `master_steps.id.json` (Indonesian) - 194.7 KB
- `master_steps.nl.json` (Dutch) - 194.3 KB
- `master_steps.sv.json` (Swedish) - 194.1 KB
- `master_steps.da.json` (Danish) - 193.8 KB
- `master_steps.no.json` (Norwegian) - 193.7 KB
- `master_steps.ms.json` (Malay) - 194.8 KB

**Content per file:**
- 87 Master Steps with language-specific templates
- 45 Variable types
- 324 Variable options with language-independent IDs

### 2. Structure Changes

#### Steps (Template Format)

**Old format:** Each step had a `templates` object with all 10 languages
```json
{
  "master_id": "PREP_CUT_01",
  "templates": {
    "de": "Schneide {{ingredient}} {{shape}}.",
    "en": "Cut {{ingredient}} {{shape}}.",
    "esp": "Corta {{ingredient}} {{shape}}.",
    ...
  }
}
```

**New format:** Each language file has only a single `template` field
```json
{
  "master_id": "PREP_CUT_01",
  "template": "Schneide {{ingredient}} {{shape}}."
}
```

#### Variable Options (NEW - Integrated from master_step_variables.json)

**Key Feature:** IDs are **identical across all languages**, only labels change.

**German (master_steps.de.json):**
```json
{
  "variable_options": {
    "shape": [
      { "id": "shape_w_rfel_1", "key": "w_rfel", "label": "Würfel" },
      { "id": "shape_scheiben_2", "key": "scheiben", "label": "Scheiben" },
      { "id": "shape_streifen_3", "key": "streifen", "label": "Streifen" }
    ],
    "heat": [
      { "id": "heat_niedriger_1", "key": "niedriger", "label": "niedriger" },
      { "id": "heat_mittlerer_2", "key": "mittlerer", "label": "mittlerer" },
      { "id": "heat_hoher_3", "key": "hoher", "label": "hoher" }
    ]
  }
}
```

**English (master_steps.en.json):**
```json
{
  "variable_options": {
    "shape": [
      { "id": "shape_w_rfel_1", "key": "w_rfel", "label": "cubes" },
      { "id": "shape_scheiben_2", "key": "scheiben", "label": "slices" },
      { "id": "shape_streifen_3", "key": "streifen", "label": "strips" }
    ],
    "heat": [
      { "id": "heat_niedriger_1", "key": "niedriger", "label": "low" },
      { "id": "heat_mittlerer_2", "key": "mittlerer", "label": "medium" },
      { "id": "heat_hoher_3", "key": "hoher", "label": "high" }
    ]
  }
}
```

**Spanish (master_steps.esp.json):**
```json
{
  "variable_options": {
    "shape": [
      { "id": "shape_w_rfel_1", "key": "w_rfel", "label": "dados" },
      { "id": "shape_scheiben_2", "key": "scheiben", "label": "rodajas" },
      { "id": "shape_streifen_3", "key": "streifen", "label": "tiras" }
    ],
    "heat": [
      { "id": "heat_niedriger_1", "key": "niedriger", "label": "baja" },
      { "id": "heat_mittlerer_2", "key": "mittlerer", "label": "media" },
      { "id": "heat_hoher_3", "key": "hoher", "label": "alta" }
    ]
  }
}
```

### 3. Language-Independent ID System

**ID Format:** `{variableName}_{sanitizedKey}_{index}`

**Examples:**
- `shape_w_rfel_1` - Same in DE/EN/ESP/PRT/etc., only label differs
- `heat_hoher_3` - Same ID everywhere: "hoher" (DE), "high" (EN), "alta" (ESP)
- `action_braten_5` - Universal reference across all languages

**Benefits:**
- ✅ **Database Storage:** Store `shape_w_rfel_1` once, display correct label per language
- ✅ **Cross-Language Comparison:** Compare recipes by IDs regardless of language
- ✅ **API Consistency:** Return IDs in API, let frontend handle localization
- ✅ **Easy Migration:** Change labels without touching stored IDs

### 4. Updated Frontend JavaScript Files

#### `create-posting-utils.js`
- Modified `getDataUrl()` to support language-specific URLs for masterSteps
- Updated `fetchJson()` to pass options to getDataUrl
- Now constructs URL as `/data/master_steps.{lang}.json`

#### `create-posting-data-store.js`
- Modified `load()` function to include language in cache key for masterSteps
- Cache key format: `masterSteps.de`, `masterSteps.en`, etc.
- Passes language option through to fetchJson

#### `CreatePostingSmartStepCreator.js`
- Updated `loadJson()` to pass current language when loading masterSteps:
  ```javascript
  const lang = currentLang || DEFAULT_LANG;
  return await window.CreatePostingDataStore.load("masterSteps", { language: lang });
  ```
- Added `getStepTemplate(step, lang)` helper function to support both old and new formats
- Updated `matchesSearch()` to use getStepTemplate helper
- Updated step rendering in `sgSteps.forEach()` to use getStepTemplate helper

#### `master-step-renderer.js`
- Updated `render()` function to support both template formats:
  ```javascript
  let tpl = '';
  if (step.template) {
      // New format: single template from language-specific JSON
      tpl = step.template;
  } else if (step.templates) {
      // Old format: templates object with multiple languages
      tpl = step.templates[key] || step.templates.de || step.templates.en || '';
  }
  ```
- Updated `renderAll()` to check for both formats

### 5. Backend (No Changes Required)

**RecipeAiTransformService.cs:**
- Continues using the original `master_steps.json` and `master_step_variables.json` files
- No changes needed - backend requires all languages for AI processing

## Benefits

### Performance
- **Before:** 416.5 KB (2 files: 230 KB + 187 KB)
- **After:** 195.5 KB (1 file per language)
- **Savings:** 53% reduction + only 1 HTTP request instead of 2

### Developer Experience
- **Unified Data:** Steps and variables in one file
- **Type Safety:** IDs can be used as constants/enums
- **Easy Debugging:** Search for `shape_w_rfel_1` across all languages

### Database Design
```sql
-- Store language-independent IDs
CREATE TABLE RecipeStepVariables (
    RecipeStepId INT,
    VariableName VARCHAR(50),
    OptionId VARCHAR(100),  -- e.g., 'shape_w_rfel_1'
    PRIMARY KEY (RecipeStepId, VariableName)
);

-- Display logic (pseudo-code)
SELECT OptionId FROM RecipeStepVariables WHERE RecipeStepId = 123;
-- Returns: 'shape_w_rfel_1'

-- Frontend loads master_steps.{userLang}.json
// Lookup: variable_options.shape.find(opt => opt.id === 'shape_w_rfel_1')
// Display: opt.label  (DE: "Würfel", EN: "cubes", ESP: "dados")
```

## Migration Notes

- Original files (`master_steps.json`, `master_step_variables.json`) remain unchanged
- JavaScript code is backward compatible with both formats
- No database changes required (IDs are new feature, not breaking change)
- Frontend automatically uses new unified files

## Running the Split Script

To regenerate the language-specific files after updating source files:

```bash
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\wwwroot\data
python split_master_steps.py
```

**What the script does:**
1. Reads `master_steps.json` (230 KB)
2. Reads `master_step_variables.json` (187 KB)
3. For each of 10 languages:
   - Extracts language-specific template from steps
   - Generates variable_options with language-independent IDs
   - Merges into single `master_steps.{lang}.json` file (~195 KB)

## Troubleshooting

**If templates don't appear:**
- Check browser console for 404 errors on `master_steps.{lang}.json`
- Verify language code is correct (e.g., "esp" not "es", "prt" not "pt")
- Clear browser cache and reload

**If wrong language appears:**
- Check `currentLang` variable in CreatePostingSmartStepCreator.js
- Verify language is being passed correctly in the `load()` call

**If variable options are missing:**
- Verify `variable_options` key exists in JSON
- Check that script ran successfully (should show "Adding variable_options for {lang}...")

**To force reload original format:**
- Set `language: undefined` in the load options
- This will fall back to loading `master_steps.json`

## Future Enhancements

### Potential Optimizations
1. **Remove Duplicate Metadata:** Move shared taxonomies to separate file
2. **Compress Selection Tags:** Many tags are duplicated across steps
3. **Lazy Load by Category:** Load only steps for current recipe category
4. **Pre-compress Files:** Serve gzipped versions (could reduce to ~60 KB)

### ID System Enhancements
1. **Numeric IDs:** Use shorter IDs like `1001` instead of `shape_w_rfel_1` (requires mapping table)
2. **Hierarchical IDs:** Use dot notation like `shape.cube.fine` for better organization
3. **Version IDs:** Add version suffix like `shape_w_rfel_1_v2` for breaking changes

## Statistics

**Generated Files (10 languages):**
```
master_steps.de.json   195.5 KB  (German)
master_steps.en.json   194.0 KB  (English)
master_steps.esp.json  194.8 KB  (Spanish)
master_steps.prt.json  194.5 KB  (Portuguese)
master_steps.id.json   194.7 KB  (Indonesian)
master_steps.nl.json   194.3 KB  (Dutch)
master_steps.sv.json   194.1 KB  (Swedish)
master_steps.da.json   193.8 KB  (Danish)
master_steps.no.json   193.7 KB  (Norwegian)
master_steps.ms.json   194.8 KB  (Malay)
```

**Content per file:**
- 87 Master Steps
- 45 Variable Types
- 324 Variable Options (with language-independent IDs)
- All metadata (phases, sub_groups, taxonomies)

**Total Reduction:**
- Old: 416.5 KB (2 files)
- New: 195.5 KB (1 file)
- **Savings: 53.0%** + 1 fewer HTTP request
