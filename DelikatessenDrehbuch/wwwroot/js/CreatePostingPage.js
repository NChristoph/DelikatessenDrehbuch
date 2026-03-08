// BEGIN LEGACY COMPAT BUNDLE (inlined to keep CreatePosting self-contained)

(function (window) {
    'use strict';

    let data = null;
    let loadingPromise = null;
    let lastLoadError = '';

    function load() {
        if (loadingPromise) return loadingPromise;

        const configuredUrl = window.MasterStepRendererConfig && window.MasterStepRendererConfig.dataUrl;
        const dataUrl = configuredUrl || '/data/master_steps.json';

        loadingPromise = fetch(dataUrl)
            .then(function (r) {
                if (!r.ok) {
                    throw new Error('master_steps.json konnte nicht geladen werden (' + r.status + ' ' + r.statusText + ') von ' + dataUrl);
                }
                return r.json();
            })
            .then(function (json) {
                if (!json || !Array.isArray(json.master_steps)) {
                    throw new Error('master_steps.json hat ein ungÃ¼ltiges Format.');
                }
                data = json;
                lastLoadError = '';
                return json;
            })
            .catch(function (error) {
                data = null;
                lastLoadError = error && error.message ? error.message : 'Unbekannter Fehler beim Laden von master_steps.json';
                console.error('MasterStepRenderer.load fehlgeschlagen:', error);
                return null;
            });

        return loadingPromise;
    }

    function getLastLoadError() {
        return lastLoadError;
    }

    function getMasterSteps() {
        return data && Array.isArray(data.master_steps) ? data.master_steps : [];
    }

    function findTemplate(masterId) {
        return getMasterSteps().find(function (x) { return x && x.master_id === masterId; }) || null;
    }

    function getAllTemplates() {
        return getMasterSteps().filter(function (step) { return !!step; });
    }

    function renderText(template, vars) {
        if (!template) return '';
        return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
            const value = vars && vars[key] != null ? String(vars[key]).trim() : '';
            return value || key;
        });
    }

    function resolveGenderFromPronoun(value) {
        const v = (value || '').toString().trim().toLowerCase();
        if (!v) return 'f';

        if (['sie', 'her', 'la', 'ela', 'a'].includes(v)) return 'f';
        if (['ihn', 'him', 'lo', 'ele', 'o'].includes(v)) return 'm';
        if (['es', 'it'].includes(v)) return 'n';

        return 'f';
    }

    function stripKnownArticle(value, lang) {
        const text = (value || '').toString().trim();
        if (!text) return '';

        const articleByLang = {
            de: ['der', 'die', 'das', 'den', 'dem', 'des', 'ein', 'eine', 'einer', 'einem'],
            en: ['the', 'a', 'an'],
            esp: ['el', 'la', 'los', 'las', 'un', 'una'],
            prt: ['o', 'a', 'os', 'as', 'um', 'uma']
        };

        const l = (lang || 'de').toString().toLowerCase();
        const lower = text.toLowerCase();
        const articles = articleByLang[l] || [];

        for (let i = 0; i < articles.length; i++) {
            const article = articles[i];
            if (lower === article) return '';
            if (lower.startsWith(article + ' ')) {
                return text.slice(article.length + 1).trim();
            }
        }

        return text;
    }

    function resolveGenderFromIngredient(value, lang) {
        const base = stripKnownArticle(value, lang).toLowerCase();
        if (!base) return null;

        const known = {
            m: ['basilikum', 'zucker', 'knoblauch', 'reis', 'ingwer', 'lauch', 'sellerie'],
            f: ['zwiebel', 'paprika', 'karotte', 'tomate', 'kartoffel', 'sauce', 'brÃ¼he'],
            n: ['salz', 'Ã¶l', 'wasser', 'ei', 'mehl', 'fleisch', 'hÃ¤hnchen']
        };

        if (known.m.includes(base)) return 'm';
        if (known.f.includes(base)) return 'f';
        if (known.n.includes(base)) return 'n';

        if (base.endsWith('chen') || base.endsWith('lein') || base.endsWith('ment')) return 'n';
        if (base.endsWith('ung') || base.endsWith('keit') || base.endsWith('heit') || base.endsWith('ion')) return 'f';
        return null;
    }

    function getLocalizedGrammar(lang, gender) {
        const l = (lang || 'de').toString().toLowerCase();
        const g = (gender || 'f').toString().toLowerCase();

        if (l === 'en') {
            return { article: 'the', pronoun: g === 'm' ? 'him' : g === 'n' ? 'it' : 'her' };
        }

        if (l === 'esp') {
            return { article: g === 'm' ? 'el' : 'la', pronoun: g === 'm' ? 'lo' : 'la' };
        }

        if (l === 'prt') {
            return { article: g === 'm' ? 'o' : 'a', pronoun: g === 'm' ? 'o' : 'a' };
        }

        return {
            article: g === 'm' ? 'den' : g === 'n' ? 'das' : 'die',
            pronoun: g === 'm' ? 'ihn' : g === 'n' ? 'es' : 'sie'
        };
    }

    function startsWithKnownArticle(value, lang) {
        const text = (value || '').toString().trim().toLowerCase();
        if (!text) return false;

        const articleByLang = {
            de: ['der', 'die', 'das', 'den', 'dem', 'des', 'ein', 'eine', 'einer', 'einem'],
            en: ['the', 'a', 'an'],
            esp: ['el', 'la', 'los', 'las', 'un', 'una'],
            prt: ['o', 'a', 'os', 'as', 'um', 'uma']
        };

        const articles = articleByLang[(lang || 'de').toString().toLowerCase()] || [];
        return articles.some(function (article) {
            return text === article || text.startsWith(article + ' ');
        });
    }

    function localizeVariables(variables, lang) {
        const result = Object.assign({}, variables || {});
        const pronounGender = resolveGenderFromPronoun(result.pronoun);
        const ingredientGender = resolveGenderFromIngredient(result.ingredient, lang);
        const gender = ingredientGender || pronounGender;
        const grammar = getLocalizedGrammar(lang, gender);

        if (!result.pronoun || ['sie', 'her', 'la', 'ela', 'a', 'ihn', 'him', 'lo', 'ele', 'o', 'es', 'it'].includes(String(result.pronoun).toLowerCase())) {
            result.pronoun = grammar.pronoun;
        }

        const ingredient = (result.ingredient || '').toString().trim();
        if (ingredient && !startsWithKnownArticle(ingredient, lang)) {
            result.ingredient = grammar.article + ' ' + ingredient;
        }

        return result;
    }

    function render(masterId, variables, lang) {
        const step = findTemplate(masterId);
        if (!step || !step.templates) return '';

        const key = (lang || 'de').toLowerCase();
        const tpl = step.templates[key] || step.templates.de || step.templates.en || '';
        const localizedVariables = localizeVariables(variables || {}, key);
        return renderText(tpl, localizedVariables);
    }

    function renderAll(masterId, variables) {
        const step = findTemplate(masterId);
        if (!step || !step.templates) {
            return { de: '', en: '', esp: '', prt: '' };
        }

        return {
            de: render(masterId, variables || {}, 'de'),
            en: render(masterId, variables || {}, 'en'),
            esp: render(masterId, variables || {}, 'esp'),
            prt: render(masterId, variables || {}, 'prt')
        };
    }

    function getVariablePresets(variableName, lang) {
        const normalizedLang = (lang || 'de').toLowerCase();
        const options = data && data.variable_options;
        const localized = options && options[variableName];

        if (localized && typeof localized === 'object') {
            const entries = localized[normalizedLang] || localized.de || localized.en;
            if (Array.isArray(entries)) {
                return entries;
            }
        }

        const presets = {
            shape: ['WÃ¼rfel', 'Scheiben', 'Streifen', 'Spalten', 'grobe StÃ¼cke'],
            grind_size: ['fein', 'mittel', 'grob', 'ca. 1 cm groÃŸe', 'ca. 0,5 mm groÃŸe', 'about 3/8-inch', 'about 0.02-inch'],
            item: ['den Teig', 'die Masse', 'die Mischung', 'das Gericht', 'die Suppe', 'die SoÃŸe', 'den Auflauf', 'the dough', 'the batter', 'the mixture', 'the dish', 'the soup', 'the sauce', 'the casserole', 'la masa', 'la mezcla', 'la preparaciÃ³n', 'el plato', 'la sopa', 'la salsa', 'el gratÃ©n', 'a massa', 'a mistura', 'a preparaÃ§Ã£o', 'o prato', 'a sopa', 'o molho', 'a caÃ§arola'],
            tool: ['Messer', 'SparschÃ¤ler', 'Reibe', 'Schneebesen', 'Spatel', 'HolzlÃ¶ffel', 'Suppenkelle', 'Messbecher', 'Nudelholz', 'Teigschaber'],
            duration: ['5 Minuten', '10 Minuten', '15 Minuten', '30 Minuten'],
            temperature: ['160Â°C', '180Â°C', '200Â°C', '220Â°C', '350Â°F', '400Â°F'],
            temp: ['160Â°C', '180Â°C', '200Â°C', '220Â°C', '350Â°F', '400Â°F'],
            liquid: ['Wasser', 'GemÃ¼sebrÃ¼he', 'Milch', 'Kokosmilch'],
            equipment: ['Pfanne', 'Topf', 'Backofen', 'BrÃ¤ter', 'Kochfeld', 'Mixer', 'Grill', 'Dampfgarer', 'SchÃ¼ssel', 'Sieb', 'KÃ¼chenmaschine', 'Zange', 'Schneidebrett'],
            state: ['goldbraun', 'weich', 'glasig', 'gar', 'knusprig', 'cremig', 'bissfest', 'eingedickt', 'sprudelnd'],
            garnish: ['frischen KrÃ¤utern', 'Sesam', 'Parmesan', 'NÃ¼ssen'],
            base: ['den Eischnee', 'die Masse', 'den Teig', 'die Creme', 'die Sauce', 'die Glasur', 'die FÃ¼llung', 'die Marinade', 'die Emulsion', 'den Schaum'],
            seasonings: ['Salz', 'Pfeffer', 'Salz und Pfeffer', 'KrÃ¤uter', 'GewÃ¼rze', 'salt', 'pepper', 'salt and pepper', 'herbs', 'spices'],
            balance: ['die SÃ¤ure', 'die SÃ¼ÃŸe', 'die SchÃ¤rfe', 'the acidity', 'the sweetness', 'the spiciness']
        };

        return presets[variableName] || [];
    }

    function getSmartDefaults(masterId, context) {
        context = context || {};
        const ingredientName = context.ingredientName || 'die Zutat';
        const recipeCategory = (context.recipeCategory || '').toString().toLowerCase();

        const defaults = {
            ingredient: ingredientName,
            ingredients: ingredientName,
            pronoun: 'sie',
            tool: 'Messer',
            shape: 'WÃ¼rfel',
            grind_size: 'fein',
            marinade: 'Ã–l, Salz und GewÃ¼rzen',
            duration: '10 Minuten',
            liquid: 'Wasser',
            equipment: 'Pfanne',
            quantity: 'etwas',
            temperature: '180Â°C',
            temp: '180Â°C',
            heat: 'mittlerer',
            spice_mix: 'Salz, Pfeffer und GewÃ¼rzen',
            sauce: 'Sauce',
            target_consistency: 'cremig',
            garnish: 'frischen KrÃ¤utern',
            serving_style: 'auf Tellern',
            side: recipeCategory.includes('salat') ? 'frischem Brot' : 'Beilage'
        };

        const template = findTemplate(masterId);
        if (!template || !Array.isArray(template.variables)) return defaults;

        const scoped = {};
        template.variables.forEach(function (key) {
            scoped[key] = defaults[key] || '';
        });
        return scoped;
    }

    function suggestForIngredient(ingredientName) {
        const name = (ingredientName || '').toString().toLowerCase().trim();
        const all = getAllTemplates();
        if (!all.length) return [];

        const hasAny = function (terms) { return terms.some(function (x) { return name.includes(x); }); };

        const pantryTerms = ['Ã¶l', 'oil', 'salz', 'pfeffer', 'gewÃ¼rz', 'spice', 'zucker', 'sugar', 'essig', 'vinegar'];
        const proteinTerms = ['huhn', 'hÃ¤hn', 'chicken', 'rind', 'beef', 'schwein', 'pork', 'lamm', 'fisch', 'lachs', 'tofu'];
        const carbTerms = ['reis', 'rice', 'pasta', 'nudel', 'kartoff', 'potato', 'quinoa', 'couscous'];
        const vegTerms = ['tomat', 'zwiebel', 'karotte', 'paprika', 'brokkoli', 'zucchini', 'aubergine', 'gemÃ¼se', 'salat'];

        let preferredActions = ['wash', 'cut', 'boil', 'steam', 'fry', 'season', 'serve'];

        if (hasAny(pantryTerms)) {
            preferredActions = ['add', 'mix', 'stir', 'season', 'serve'];
        } else if (hasAny(proteinTerms)) {
            preferredActions = ['cut', 'marinate', 'fry', 'roast', 'grill', 'rest', 'season', 'serve'];
        } else if (hasAny(carbTerms)) {
            preferredActions = ['boil', 'stir', 'strain', 'steam', 'season', 'serve'];
        } else if (hasAny(vegTerms)) {
            preferredActions = ['wash', 'peel', 'cut', 'boil', 'steam', 'fry', 'roast', 'season', 'serve'];
        }

        const actionRank = function (action) {
            const idx = preferredActions.findIndex(function (x) { return x === action; });
            return idx >= 0 ? idx : 999;
        };

        return all.sort(function (a, b) {
            const aHasIngredient = Array.isArray(a.variables) && a.variables.includes('ingredient');
            const bHasIngredient = Array.isArray(b.variables) && b.variables.includes('ingredient');
            if (aHasIngredient !== bHasIngredient) return aHasIngredient ? -1 : 1;

            const ar = actionRank((a.action || '').toString());
            const br = actionRank((b.action || '').toString());
            if (ar !== br) return ar - br;

            const pa = parseInt(a.phase || 0, 10);
            const pb = parseInt(b.phase || 0, 10);
            if (pa !== pb) return pa - pb;

            return (a.master_id || '').localeCompare(b.master_id || '');
        });
    }

    window.MasterStepRenderer = {
        load: load,
        getAllTemplates: getAllTemplates,
        render: render,
        renderAll: renderAll,
        getSmartDefaults: getSmartDefaults,
        suggestForIngredient: suggestForIngredient,
        getVariablePresets: getVariablePresets,
        getLastLoadError: getLastLoadError,
        findTemplate: findTemplate
    };
})(window);


/**
 * Recipe Step Suggestion Engine
 * Uses recipe_step_mapping.json to suggest preparation steps based on selected ingredients.
 *
 * Features:
 * - Recipe type detection with score (e.g. "82% Lasagne")
 * - Ingredient-specific step filtering (only steps matching selected ingredients)
 * - Phase-sorted step sequences
 * - Step text validation against selected ingredient names (avoids "Gouda streuen" when Cheddar selected)
 */
(function (window) {
    'use strict';

    let mappingData = null;
    let categoryScoringData = null;
    let masterStepsData = null;
    let mappingLoaded = false;
    let loadingPromise = null;

    /** Load mapping JSON files (cached after first load) */
    function loadMapping() {
        if (loadingPromise) return loadingPromise;

        loadingPromise = Promise.all([
            fetch('/data/recipe_step_mapping.json').then(r => r.json()),
            fetch('/data/recipe_category_scoring.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null),
            fetch('/data/master_steps.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null)
        ]).then(([stepData, categoryData, masterData]) => {
            mappingData = stepData;
            categoryScoringData = categoryData;
            masterStepsData = masterData;
            mappingLoaded = true;
            return { stepData, categoryData, masterData };
        });

        return loadingPromise;
    }

    /**
     * Detect which recipe types match the selected ingredients.
     * Returns sorted array: [{ type, name, score, matchedSignatureCount, totalSignature }]
     * Score = percentage of signature ingredients matched (weighted by specificity)
     */
    function detectRecipeTypes(selectedIngredientIds) {
        // If category scoring data is available, use it exclusively.
        // Old recipe_step_mapping template IDs can differ from current
        // ingredients_and_nutrients IDs and produce misleading matches
        // (e.g. unrelated categories with low percentages).
        if (categoryScoringData && Array.isArray(categoryScoringData.categories)) {
            return detectRecipeTypesFromCategoryScoring(selectedIngredientIds);
        }

        if (!mappingData || !mappingData.recipe_type_templates) return [];
        const selectedSet = new Set(selectedIngredientIds.map(id => parseInt(id, 10)));

        const results = [];
        for (const [typeName, template] of Object.entries(mappingData.recipe_type_templates)) {
            const sigs = template.signature_ingredients || [];
            if (!sigs.length) continue;

            let weightedMatched = 0;
            let weightedTotal = 0;
            let matchedCount = 0;

            sigs.forEach(sig => {
                const weight = Math.min(sig.specificity || 1, 10);
                weightedTotal += weight;
                if (selectedSet.has(sig.id)) {
                    weightedMatched += weight;
                    matchedCount++;
                }
            });

            if (matchedCount === 0) continue;

            const score = Math.round((weightedMatched / weightedTotal) * 100);
            if (score < 10) continue;

            results.push({
                type: typeName,
                name: typeName.charAt(0).toUpperCase() + typeName.slice(1).replace('_', ' '),
                score: score,
                matchedSignatureCount: matchedCount,
                totalSignature: sigs.length
            });
        }

        results.sort((a, b) => b.score - a.score);
        return results;
    }

    /**
     * Preferred recipe-type detection using recipe_category_scoring.json (new ingredients_and_nutrients IDs).
     *
     * Scoring v3 (hybrid formula):
     * 1. base        = weightedMatched / weightedTotal
     * 2. fp_penalty  = Abzug fÃ¼r ausgewÃ¤hlte Zutaten, die NICHT in dieser Kategorie sind
     *                  adjusted = base Ã— (1 - falsePositiveRatio Ã— 0.5)
     * 3. required    = Falls required_ingredient_ids definiert:
     *                  - Alle matched  â†’ Ã—1.5 (capped 1.0)
     *                  - Keine matched â†’ Ã—0.2
     *                  - Teils matched â†’ Ã—(1 + ratioÃ—0.5), capped 1.0
     * 4. min_match   = Mindestanzahl Treffer (aus JSON oder Fallback: â‰¥5 Zutaten â†’ min 2)
     */
    function detectRecipeTypesFromCategoryScoring(selectedIngredientIds) {
        if (!categoryScoringData || !Array.isArray(categoryScoringData.categories)) return [];

        const selectedSet = new Set(selectedIngredientIds.map(id => parseInt(id, 10)).filter(id => !isNaN(id)));
        const selectedCount = selectedSet.size;
        const results = [];

        categoryScoringData.categories.forEach(cat => {
            const weights = Array.isArray(cat.ingredient_weights) ? cat.ingredient_weights : [];
            if (!weights.length) return;

            // Build category ingredient ID set for false-positive detection
            const categoryIngredientIds = new Set(weights.map(w => parseInt(w.ingredient_id, 10)));

            let weightedTotal = 0;
            let weightedMatched = 0;
            let matchedCount = 0;

            weights.forEach(item => {
                const ingredientId = parseInt(item.ingredient_id, 10);
                const weight = Number(item.weight || 0);
                if (isNaN(ingredientId) || weight <= 0) return;

                weightedTotal += weight;
                if (selectedSet.has(ingredientId)) {
                    weightedMatched += weight;
                    matchedCount += 1;
                }
            });

            if (matchedCount === 0 || weightedTotal <= 0) return;

            // Minimum match check
            const minMatch = (typeof cat.min_match === 'number')
                ? cat.min_match
                : (weights.length >= 5 ? 2 : 1);
            if (matchedCount < minMatch) return;

            // 1. Base score
            let score = weightedMatched / weightedTotal;

            // 2. False-positive penalty:
            //    ingredients selected that don't belong to this category reduce confidence
            const notInCategory = Array.from(selectedSet).filter(id => !categoryIngredientIds.has(id)).length;
            const falsePositiveRatio = selectedCount > 0 ? notInCategory / selectedCount : 0;
            score = score * (1 - falsePositiveRatio * 0.5);

            // 3. Required ingredient boost / penalty
            const requiredIds = Array.isArray(cat.required_ingredient_ids) ? cat.required_ingredient_ids : [];
            if (requiredIds.length > 0) {
                const requiredMatched = requiredIds.filter(id => selectedSet.has(parseInt(id, 10))).length;
                if (requiredMatched === 0) {
                    score = score * 0.2;
                } else if (requiredMatched === requiredIds.length) {
                    score = Math.min(score * 1.5, 1.0);
                } else {
                    const requiredRatio = requiredMatched / requiredIds.length;
                    score = Math.min(score * (1 + requiredRatio * 0.5), 1.0);
                }
            }

            const finalScore = Math.round(score * 100);
            if (finalScore < 10) return;

            const localizedName = cat?.display_name?.de || cat?.display_name?.en || cat.id || 'Unbekannt';

            results.push({
                type: cat.id,
                name: localizedName,
                score: finalScore,
                matchedSignatureCount: matchedCount,
                totalSignature: weights.length,
                typicalIngredientIds: Array.isArray(cat.typical_ingredients) ? cat.typical_ingredients : []
            });
        });

        results.sort((a, b) => b.score - a.score);
        return results;
    }

    /**
     * Returns the full category object for a given typeId (from recipe_category_scoring.json).
     * Useful for retrieving typical_ingredients, required_ingredient_ids, etc.
     * @param {string} typeId
     * @returns {Object|null}
     */
    function getCategoryById(typeId) {
        if (!categoryScoringData || !Array.isArray(categoryScoringData.categories)) return null;
        return categoryScoringData.categories.find(cat => cat.id === typeId) || null;
    }

    /**
     * Get suggested step IDs for the selected ingredients.
     * Only returns steps where the step text is relevant to the actual selected ingredients.
     *
     * @param {number[]} selectedIngredientIds - IDs of selected ingredients
     * @param {Object} stepCatalogById - Map/object of stepId -> step data (from the page's existing catalog)
     * @param {Object} options
     * @param {string[]} [options.selectedIngredientNames] - Names of selected ingredients for text validation
     * @param {number} [options.maxSteps=20] - Maximum steps to return
     * @returns {{ steps: Array, recipeTypes: Array, topType: Object|null }}
     */
    function suggestStepsForIngredients(selectedIngredientIds, stepCatalogById, options) {
        options = options || {};
        const maxSteps = options.maxSteps || 20;
        const selectedNames = (options.selectedIngredientNames || []).map(n => n.toLowerCase().trim());
        const selectedSet = new Set(selectedIngredientIds.map(id => id.toString()));

        if (!mappingData) return { steps: [], recipeTypes: [], topType: null };

        const ingredientToSteps = mappingData.ingredient_to_steps || {};
        const recipeTypes = detectRecipeTypes(selectedIngredientIds);
        const topType = recipeTypes.length ? recipeTypes[0] : null;

        // Collect candidate step IDs with scores
        const stepScores = new Map(); // stepId -> { score, phase, triggeredBy[] }

        // 1) Direct ingredient->step mapping
        selectedIngredientIds.forEach(ingId => {
            const stepIds = ingredientToSteps[ingId.toString()] || [];
            stepIds.forEach(sid => {
                const sidStr = sid.toString();
                if (!stepScores.has(sidStr)) {
                    stepScores.set(sidStr, { score: 0, phase: 0, triggeredBy: [] });
                }
                const entry = stepScores.get(sidStr);
                entry.score += 2;
                entry.triggeredBy.push(parseInt(ingId, 10));
            });
        });

        // 2) Bonus from recipe type templates
        if (topType && topType.score >= 25) {
            const template = mappingData.recipe_type_templates[topType.type];
            if (template && template.suggested_step_sequence) {
                template.suggested_step_sequence.forEach(ss => {
                    const sidStr = ss.step_id.toString();
                    // Only add if the triggering ingredient is actually selected
                    if (!selectedSet.has(ss.triggered_by.toString())) return;
                    if (!stepScores.has(sidStr)) {
                        stepScores.set(sidStr, { score: 0, phase: ss.phase, triggeredBy: [] });
                    }
                    const entry = stepScores.get(sidStr);
                    entry.score += (topType.score / 50); // bonus proportional to type match
                });
            }
        }

        // 3) Filter: step must exist in the page's step catalog
        // 4) Filter: validate step text against selected ingredient names
        //    Avoid suggesting "geriebenen Gouda" when only "Cheddar" is selected
        const conflictIngredients = buildConflictMap();
        const results = [];

        stepScores.forEach((data, sidStr) => {
            const catalogStep = getStepFromCatalog(sidStr, stepCatalogById);
            if (!catalogStep) return;

            const stepText = getStepText(catalogStep);
            if (!stepText) return;

            // Check for conflicting ingredient names in step text
            if (hasConflictingIngredient(stepText, selectedNames, conflictIngredients)) {
                return;
            }

            const phase = parseInt(catalogStep.phase || catalogStep.Phase || 0, 10);
            results.push({
                stepId: parseInt(sidStr, 10),
                score: data.score,
                phase: phase,
                triggeredBy: data.triggeredBy,
                text: stepText
            });
        });

        // Sort: phase order first, then score desc
        const phaseOrder = { 1: 0, 2: 1, 3: 2, 4: 3, 0: 4 };
        results.sort((a, b) => {
            const pa = phaseOrder[a.phase] ?? 4;
            const pb = phaseOrder[b.phase] ?? 4;
            if (pa !== pb) return pa - pb;
            return b.score - a.score;
        });

        // Deduplicate by phase: max 4 per phase
        const phaseCounts = {};
        const filtered = results.filter(r => {
            phaseCounts[r.phase] = (phaseCounts[r.phase] || 0) + 1;
            return phaseCounts[r.phase] <= 4;
        });

        return {
            steps: filtered.slice(0, maxSteps),
            recipeTypes: recipeTypes,
            topType: topType
        };
    }

    /**
     * Build a map of ingredient names that are "alternatives" to each other.
     * E.g., Gouda/Cheddar/Emmentaler are cheese alternatives.
     * If a step mentions "Gouda" but only "Cheddar" is selected, skip that step.
     */
    function buildConflictMap() {
        // Groups of alternative ingredients (lowercase)
        // If step text contains one from a group but the selected ingredient is a different one from the same group -> conflict
        return [
            ['gouda', 'cheddar', 'emmentaler', 'gruyÃ¨re', 'edamer', 'bergkÃ¤se', 'raclette'],
            ['mozzarella', 'burrata'],
            ['parmesan', 'pecorino', 'grana padano'],
            ['feta', 'ziegenkÃ¤se', 'schafskÃ¤se', 'halloumi'],
            ['lachs', 'forelle', 'kabeljau', 'thunfisch', 'zander', 'dorade', 'seelachs', 'pangasius'],
            ['hÃ¤hnchen', 'pute', 'truthahn', 'ente', 'gans'],
            ['rindfleisch', 'schweinefleisch', 'lammfleisch', 'kalbfleisch', 'wildfleisch'],
            ['spaghetti', 'penne', 'rigatoni', 'tagliatelle', 'fusilli', 'farfalle', 'linguine', 'fettuccine', 'makkaroni'],
            ['basmati', 'jasminreis', 'risotto-reis', 'langkornreis', 'wildreis'],
            ['kokosmilch', 'pflanzenmilch', 'hafermilch', 'sojamilch', 'mandelmilch'],
        ];
    }

    /**
     * Check if step text mentions an ingredient that conflicts with the selection.
     * Example: step says "Gouda", but selected ingredients include "Cheddar" not "Gouda" -> conflict
     */
    function hasConflictingIngredient(stepText, selectedNames, conflictGroups) {
        const textLower = stepText.toLowerCase();

        for (const group of conflictGroups) {
            // Find which items from this group appear in the step text
            const mentionedInStep = group.filter(item => textLower.includes(item));
            if (!mentionedInStep.length) continue;

            // Find which items from this group are in the selected ingredients
            const selectedFromGroup = group.filter(item =>
                selectedNames.some(name => name.includes(item) || item.includes(name))
            );

            // Conflict: step mentions items from this group, but none of those items are selected
            // AND at least one other item from this group IS selected
            if (selectedFromGroup.length > 0) {
                const mentionedAndSelected = mentionedInStep.filter(m =>
                    selectedFromGroup.some(s => s.includes(m) || m.includes(s))
                );
                if (mentionedAndSelected.length === 0) {
                    return true; // step mentions "Gouda" but only "Cheddar" is selected
                }
            }
        }

        return false;
    }

    /** Get step from catalog (handles both Map and plain object, and different key formats) */
    function getStepFromCatalog(stepId, catalog) {
        const sid = stepId.toString();
        if (catalog instanceof Map) {
            return catalog.get(sid) || catalog.get(parseInt(sid, 10));
        }
        return catalog[sid] || catalog[parseInt(sid, 10)];
    }

    /** Get step display text (handles different property name conventions) */
    function getStepText(step) {
        return step.step_DE || step.de || step.Step_DE || step.StepDe || '';
    }

    /**
     * Render recipe type score badges as HTML
     * @param {Array} recipeTypes - from detectRecipeTypes
     * @param {number} [maxShow=3]
     * @returns {string} HTML string
     */
    function renderRecipeTypeBadges(recipeTypes, maxShow) {
        maxShow = maxShow || 3;
        if (!recipeTypes || !recipeTypes.length) {
            return '<span class="badge bg-secondary">Kein Rezepttyp erkannt</span>';
        }

        return recipeTypes.slice(0, maxShow).map((rt, i) => {
            let colorClass;
            if (rt.score >= 60) colorClass = 'bg-success';
            else if (rt.score >= 35) colorClass = 'bg-primary';
            else colorClass = 'bg-secondary';

            const icon = i === 0 ? '<i class="bi bi-star-fill me-1"></i>' : '';
            return `<span class="badge ${colorClass} me-1">${icon}${rt.score}% ${rt.name}</span>`;
        }).join('');
    }

    function renderMasterTemplate(template, vars) {
        if (!template) return '';
        return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
            const raw = vars[key];
            if (raw === null || raw === undefined || raw === '') {
                return key;
            }
            return raw;
        });
    }

    function getMasterTemplateDefaults(ingredientName) {
        return {
            ingredient: ingredientName || 'Zutat',
            ingredients: ingredientName || 'die Zutaten',
            pronoun: 'sie',
            tool: 'Messer',
            shape: 'mundgerechte StÃ¼cke',
            grind_size: 'fein',
            marinade: 'Ã–l, Salz und GewÃ¼rzen',
            duration: '10 Minuten',
            liquid: 'Wasser',
            quantity: 'etwas',
            temperature: 'mittlerer Hitze',
            heat: 'mittlerer',
            spice_mix: 'Salz, Pfeffer und GewÃ¼rzen',
            sauce: 'Sauce',
            target_consistency: 'cremig',
            garnish: 'frischen KrÃ¤utern',
            serving_style: 'auf Tellern',
            side: 'Beilage'
        };
    }

    /**
       * Erstellt eine vollstÃ¤ndige Vorschau aller Master-Steps fÃ¼r die gewÃ¤hlten Zutaten.
       * Sortiert nach Phasen (Vorbereitung -> Kochen -> Finishing).
       */
    function getMasterStepPreview(selectedIngredients, options) {
        options = options || {};
        const lang = (options.lang || 'de').toLowerCase();

        // Wir nehmen an, selectedIngredients ist ein Array von Strings (Namen) 
        // oder Objekten {name: "..."}. Wir vereinheitlichen das hier:
        const ingredientNames = selectedIngredients.map(ing =>
            typeof ing === 'string' ? ing : (ing.name || ing.ingredient_name)
        );

        if (!masterStepsData || !Array.isArray(masterStepsData.master_steps)) return [];

        const rendered = [];
        const seen = new Set();

        // 1. Jede gewÃ¤hlte Zutat durchgehen
        ingredientNames.forEach(name => {

            // 2. JEDEN Master-Step aus der JSON prÃ¼fen
            masterStepsData.master_steps.forEach(masterStep => {

                // Nur Steps nehmen, die eine einzelne Zutat verarbeiten kÃ¶nnen
                if (masterStep.variables.includes('ingredient')) {

                    const key = `${masterStep.master_id}::${name}`;
                    if (!seen.has(key)) {
                        seen.add(key);

                        // Template fÃ¼r die Sprache wÃ¤hlen
                        const templates = masterStep.templates || {};
                        const rawTemplate = templates[lang] || templates.de || "";

                        // Platzhalter fÃ¼llen (Nutzt deine Default-Logik fÃ¼r Pronomen etc.)
                        const vars = getMasterTemplateDefaults(name);
                        const text = renderMasterTemplate(rawTemplate, vars);

                        if (text) {
                            rendered.push({
                                masterId: masterStep.master_id,
                                phase: parseInt(masterStep.phase || 0, 10),
                                equipment: masterStep.equipment,
                                action: masterStep.action,
                                ingredient: name,
                                text: text
                            });
                        }
                    }
                }
            });
        });

        // 3. Nach Phase sortieren (Phase 1: Vorbereitung kommt zuerst)
        return rendered.sort((a, b) => a.phase - b.phase);
    }

    // Export
    window.RecipeStepSuggest = {
        loadMapping: loadMapping,
        detectRecipeTypes: detectRecipeTypes,
        suggestStepsForIngredients: suggestStepsForIngredients,
        renderRecipeTypeBadges: renderRecipeTypeBadges,
        getMasterStepPreview: getMasterStepPreview,
        getCategoryById: getCategoryById,
        hasMasterSteps: function () { return !!(masterStepsData && Array.isArray(masterStepsData.master_steps) && masterStepsData.master_steps.length); },
        isLoaded: function () { return mappingLoaded; }
    };

})(window);


(function (window, $) {
    'use strict';

    const UI_TEXT = {
        selectIngredients: 'WÃ¤hle Zutaten aus, um Wahrscheinlichkeiten zu sehen.',
        moduleMissing: 'Analyse-Modul nicht verfÃ¼gbar.',
        noTrend: 'Noch keine klare Tendenz erkannt.',
        analysisUnavailable: 'Wahrscheinlichkeitsanalyse aktuell nicht verfÃ¼gbar.',
        templatesUnavailable: 'Template-VorschlÃ¤ge sind aktuell nicht verfÃ¼gbar.',
        selectIngredientsFirst: 'Bitte zuerst Zutaten auswÃ¤hlen.',
        noTemplates: 'Keine passenden Templates gefunden.',
        suggestedIngredients: 'Vorgeschlagene Zutaten',
        suggestedIngredientsHint: 'Tippen zum HinzufÃ¼gen'
    };

    function escapeHtml(value) {
        return $('<div>').text(value ?? '').html();
    }

    function setStatus(container, text) {
        container.text(text || UI_TEXT.analysisUnavailable);
    }

    function getPreferredMasterTemplateIdsForType(typeId, presets) {
        const key = (typeId || '').toString().trim().toLowerCase();
        const types = presets && presets.types ? presets.types : {};
        const fromExact = Array.isArray(types[key]) ? types[key] : [];
        if (fromExact.length) return fromExact;

        if (key === 'lasagna' && Array.isArray(types.lasagne)) {
            return types.lasagne;
        }
        return [];
    }

    function buildInlineTemplateText(template, lang, vars) {
        const templateText = (template?.templates?.[lang] || template?.templates?.de || '').toString();
        if (!templateText) return '';

        return templateText.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
            const varKey = String(key || '').trim();
            const safeKey = escapeHtml(varKey);
            const nextValue = vars && vars[varKey] != null ? String(vars[varKey]).trim() : '';
            const safeValue = escapeHtml(nextValue || varKey);
            return `<span class="probability-var-inline-wrap"><span class="js-probability-var probability-var-inline-token" data-var-key="${safeKey}" role="button" tabindex="0">${safeValue}</span></span>`;
        });
    }

    function buildTemplateCardHtml(masterId, displayText, template, vars, lang) {
        const safeId = escapeHtml(masterId || '');
        const safeText = escapeHtml(displayText || masterId || '');
        const inlineText = buildInlineTemplateText(template, lang, vars);

        return `<div class="probability-template-wrap" data-master-id="${safeId}">
  <div class="probability-template-card w-100 text-start">
    <div class="probability-template-select js-probability-template" data-master-id="${safeId}" role="button" tabindex="0">
      <span class="probability-template-text">${inlineText || safeText}</span>
    </div>
  </div>
</div>`;
    }

    function renderProbabilityTypeButtons(recipeTypes) {
        return (recipeTypes || []).slice(0, 5).map((recipeType, idx) => {
            const colorClass = recipeType.score >= 60 ? 'btn-success' : (recipeType.score >= 35 ? 'btn-primary' : 'btn-secondary');
            const icon = idx === 0 ? '<i class="bi bi-star-fill me-1"></i>' : '';
            return `<button type="button" class="btn btn-sm ${colorClass} probability-type-btn js-probability-type" data-type="${escapeHtml(recipeType.type || '')}" data-name="${escapeHtml(recipeType.name || '')}" data-score="${recipeType.score}">${icon}${recipeType.score}% ${escapeHtml(recipeType.name || '')}</button>`;
        }).join('');
    }

    /**
     * Builds HTML for the "suggested ingredients" strip beneath the template cards.
     * Calls deps.getTypicalIngredientSuggestions(typeId) to retrieve unselected typical ingredients.
     * Each chip has data-ingredient-id so the view can handle the click (highlight/add).
     */
    function buildIngredientSuggestionHtml(typeId, deps, options) {
        options = options || {};
        const withHeader = options.withHeader !== false;
        if (!deps.getTypicalIngredientSuggestions) return '';
        const suggestions = deps.getTypicalIngredientSuggestions(typeId) || [];
        if (!suggestions.length) return '';

        const chips = suggestions.map(ing => {
            const safeId = escapeHtml(String(ing.id || ''));
            const safeName = escapeHtml(ing.name || String(ing.id));
            return `<button type="button" class="btn btn-sm btn-outline-warning js-typical-ingredient-chip" data-ingredient-id="${safeId}" title="${UI_TEXT.suggestedIngredientsHint}">${safeName}</button>`;
        }).join('');

        if (!withHeader) {
            return `<div class="d-flex flex-wrap gap-2">${chips}</div>`;
        }

        return `<div class="mt-3 pt-2" style="border-top:1px solid rgba(255,255,255,.12)">
  <div class="small text-white-50 mb-2">${escapeHtml(UI_TEXT.suggestedIngredients)} <span class="opacity-50">&middot; ${escapeHtml(UI_TEXT.suggestedIngredientsHint)}</span></div>
  <div class="d-flex flex-wrap gap-2">${chips}</div>
</div>`;
    }

    function create(deps) {
        const state = {
            presets: null,
            presetsPromise: null,
            varsByTemplate: {},
            inlineOverrides: {}
        };

        const getLang = () => (deps.getCurrentLang ? deps.getCurrentLang() : 'de');

        async function loadPresets() {
            if (state.presets) return state.presets;
            if (state.presetsPromise) return state.presetsPromise;

            const defaultPresets = { types: {} };
            state.presetsPromise = fetch('/data/probability_template_presets.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null)
                .then(data => {
                    state.presets = data && typeof data === 'object' ? data : defaultPresets;
                    return state.presets;
                })
                .finally(() => {
                    state.presetsPromise = null;
                });

            return state.presetsPromise;
        }

        function buildPreferredCards(preferredTemplateIds, varsByTemplate) {
            const cards = [];
            (preferredTemplateIds || []).forEach(masterId => {
                const template = deps.findTemplate(masterId);
                if (!template) return;
                const vars = deps.buildVariablesForTemplate(masterId);
                varsByTemplate[masterId] = vars;
                const snippet = deps.renderTemplate(masterId, vars, getLang()) || masterId;
                cards.push(buildTemplateCardHtml(masterId, snippet, template, vars, getLang()));
            });
            return cards;
        }

        function buildFallbackCards(ingredientNames, varsByTemplate) {
            const preview = deps.getMasterStepPreview(ingredientNames, { lang: getLang() }) || [];
            if (!preview.length) return [];

            const cards = [];
            const seen = new Set();
            preview.forEach(item => {
                const masterId = (item.masterId || '').toString();
                if (!masterId || seen.has(masterId)) return;
                seen.add(masterId);
                const template = deps.findTemplate(masterId);
                const vars = deps.buildVariablesForTemplate(masterId);
                cards.push(buildTemplateCardHtml(masterId, item.text || masterId, template, vars, getLang()));
                varsByTemplate[masterId] = vars;
            });
            return cards;
        }

        function getMergedVars(masterId) {
            const base = { ...(state.varsByTemplate[masterId] || deps.buildVariablesForTemplate(masterId) || {}) };
            const overrides = state.inlineOverrides[masterId] || {};
            return { ...base, ...overrides };
        }
        function rerenderInlineText(masterId, wrap) {
            const merged = getMergedVars(masterId);
            const template = deps.findTemplate(masterId);
            const nextHtml = buildInlineTemplateText(template, getLang(), merged);
            const nextText = deps.renderTemplate(masterId, merged, getLang()) || masterId;
            wrap.find('.probability-template-text').html(nextHtml || escapeHtml(nextText));
        }
        function bindInlineEvents() {

            $(document).off('click.probabilityInlineVar').on('click.probabilityInlineVar', '.js-probability-var', function (event) {
                event.preventDefault();
                event.stopPropagation();
                const chip = $(this);
                const wrap = chip.closest('.probability-template-wrap');
                const masterId = (wrap.data('master-id') || '').toString();
                const varKey = (chip.data('var-key') || '').toString();
                if (!masterId || !varKey) return;

                const currentVars = getMergedVars(masterId);
                const currentVal = (currentVars[varKey] || '').toString();

                if (deps.openProbVarEditor) {
                    deps.openProbVarEditor(masterId, varKey, currentVal, function (newVal) {
                        if (newVal == null) return;
                        if (!state.inlineOverrides[masterId]) state.inlineOverrides[masterId] = {};
                        state.inlineOverrides[masterId][varKey] = newVal;
                        rerenderInlineText(masterId, wrap);
                    }, chip);
                } else {
                    const nextVal = window.prompt(`Wert fÃ¼r ${varKey}:`, currentVal);
                    if (nextVal == null) return;
                    if (!state.inlineOverrides[masterId]) state.inlineOverrides[masterId] = {};
                    state.inlineOverrides[masterId][varKey] = nextVal;
                    rerenderInlineText(masterId, wrap);
                }
            });

        }

        async function refresh() {
            const container = $('#ingredientProbabilityBadges');
            const templatesBox = $('#ingredientProbabilityTemplates');
            const selectedIngredientSuggestionBox = $('#selectedIngredientsSuggestions');
            if (!container.length) return;

            const selectedIngredientIds = deps.getSelectedIngredientIds();
            templatesBox.addClass('d-none').empty();
            selectedIngredientSuggestionBox.empty();

            if (!selectedIngredientIds.length) {
                setStatus(container, UI_TEXT.selectIngredients);
                return;
            }

            if (!window.RecipeStepSuggest || !deps.loadMapping || !deps.detectRecipeTypes) {
                setStatus(container, UI_TEXT.moduleMissing);
                return;
            }

            try {
                await deps.loadMapping();
                const recipeTypes = deps.detectRecipeTypes(selectedIngredientIds) || [];
                if (!recipeTypes.length) {
                    setStatus(container, UI_TEXT.noTrend);
                    return;
                }

                container.html(renderProbabilityTypeButtons(recipeTypes));
            } catch (err) {
                setStatus(container, UI_TEXT.analysisUnavailable);
            }
        }

        async function showSuggestions(typeId, typeName, score) {
            const box = $('#ingredientProbabilityTemplates');
            const selectedIngredientSuggestionBox = $('#selectedIngredientsSuggestions');
            if (!box.length) return;

            if (!window.RecipeStepSuggest || !window.MasterStepRenderer) {
                box.removeClass('d-none').html(`<div class="small text-white-50">${escapeHtml(UI_TEXT.templatesUnavailable)}</div>`);
                selectedIngredientSuggestionBox.empty();
                return;
            }

            const ingredients = deps.getSelectedIngredientNames();
            if (!ingredients.length) {
                box.removeClass('d-none').html(`<div class="small text-white-50">${escapeHtml(UI_TEXT.selectIngredientsFirst)}</div>`);
                selectedIngredientSuggestionBox.empty();
                return;
            }

            await loadPresets();
            const preferredTemplateIds = getPreferredMasterTemplateIdsForType(typeId, state.presets);
            const varsByTemplate = {};
            let cards = buildPreferredCards(preferredTemplateIds, varsByTemplate);

            if (!cards.length) {
                cards = buildFallbackCards(ingredients, varsByTemplate);
                if (!cards.length) {
                    box.removeClass('d-none').html(`<div class="small text-white-50">${escapeHtml(UI_TEXT.noTemplates)}</div>`);
                    selectedIngredientSuggestionBox.empty();
                    return;
                }
            }

            state.varsByTemplate = varsByTemplate;
            state.inlineOverrides = {};
            box.removeClass('d-none').html(cards.join(''));
            selectedIngredientSuggestionBox.empty();
            bindInlineEvents();
        }

        return {
            refresh,
            showSuggestions
        };
    }

    window.CreatePostingProbability = {
        create
    };
})(window, window.jQuery);


(() => {
    const fallbackUnits = [
        { de: "Stk.", en: "pcs.", esp: "uds.", prt: "un." },
        { de: "EL.", en: "tbsp.", esp: "cda.", prt: "colh. sopa" },
        { de: "TL.", en: "tsp.", esp: "cdta.", prt: "colh. cha" },
        { de: "Pkg.", en: "pack", esp: "paq.", prt: "pac." },
        { de: "Prise", en: "pinch", esp: "pizca", prt: "pitada" },
        { de: "g.", en: "g.", esp: "g.", prt: "g." },
        { de: "Kg", en: "kg", esp: "kg", prt: "kg" },
        { de: "Bund", en: "bunch", esp: "manojo", prt: "molho" },
        { de: "Dose", en: "can", esp: "lata", prt: "lata" },
        { de: "etwas", en: "some", esp: "algo", prt: "um pouco" },
        { de: "spritzer", en: "dash", esp: "chorrito", prt: "golpe" },
        { de: "Becher", en: "cup", esp: "taza", prt: "copo" },
        { de: "Scheiben", en: "slices", esp: "rebanadas", prt: "fatias" },
        { de: "Blatt", en: "leaf", esp: "hoja", prt: "folha" },
        { de: "Blätter", en: "leaves", esp: "hojas", prt: "folhas" },
        { de: "Handvoll", en: "handful", esp: "puñado", prt: "punhado" },
        { de: "cm", en: "cm", esp: "cm", prt: "cm" },
        { de: "Zweig", en: "sprig", esp: "rama", prt: "ramo" },
        { de: "ml", en: "ml", esp: "ml", prt: "ml" },
        { de: "Gekocht", en: "cooked", esp: "cocido", prt: "cozido" },
        { de: "Portionen", en: "portions", esp: "porciones", prt: "porcoes" },
        { de: "l", en: "l", esp: "l", prt: "l" },
        { de: "Glas", en: "glass", esp: "vaso", prt: "copo" },
        { de: "kcal", en: "kcal", esp: "kcal", prt: "kcal" },
        { de: "mg", en: "mg", esp: "mg", prt: "mg" },
        { de: "µg", en: "ug", esp: "ug", prt: "ug" }
    ];

    let cachedUnits = null;

    function resolveLangKey(value) {
        const normalized = (value || "de").toString().toLowerCase();
        if (normalized === "es") return "esp";
        if (normalized === "pt") return "prt";
        if (normalized === "se") return "sv";
        if (normalized === "dk") return "da";
        return normalized;
    }

    function escapeHtml(value) {
        return (value ?? "")
            .toString()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function normalizeUnit(unit) {
        const de = (unit?.de || "").toString().trim();
        if (!de) return null;

        return {
            de,
            en: (unit?.en || de).toString(),
            esp: (unit?.esp || unit?.es || de).toString(),
            prt: (unit?.prt || unit?.pt || de).toString(),
            id: (unit?.id || de).toString(),
            ms: (unit?.ms || de).toString(),
            nl: (unit?.nl || de).toString(),
            sv: (unit?.sv || de).toString(),
            da: (unit?.da || de).toString(),
            no: (unit?.no || de).toString()
        };
    }

    function buildUnitMap() {
        const map = new Map();

        fallbackUnits.forEach((unit) => {
            const normalized = normalizeUnit(unit);
            if (!normalized) return;
            map.set(normalized.de.toLowerCase(), normalized);
        });

        document.querySelectorAll(".js-db-unit option").forEach((opt) => {
            const value = (opt.value || "").toString().trim();
            const label = (opt.textContent || value).toString().trim();
            if (!value) return;

            const key = value.toLowerCase();
            const current = map.get(key);
            if (current) {
                current.en = current.en || label;
                current.esp = current.esp || label;
                current.prt = current.prt || label;
                return;
            }

            map.set(key, normalizeUnit({ de: value, en: label, esp: label, prt: label }));
        });

        return map;
    }

    function collectUnitsFromDom() {
        return Array.from(buildUnitMap().values()).filter(Boolean);
    }

    function getUnits() {
        if (!cachedUnits || !cachedUnits.length) {
            cachedUnits = collectUnitsFromDom();
        }
        return cachedUnits;
    }

    function refreshUnits() {
        cachedUnits = collectUnitsFromDom();
        return cachedUnits;
    }

    function findUnitByDe(unitDe) {
        const needle = (unitDe || "").toString().trim().toLowerCase();
        if (!needle) return null;
        return getUnits().find((unit) => (unit?.de || "").toString().trim().toLowerCase() === needle) || null;
    }

    function getDefaultUnitDe() {
        return getUnits()[0]?.de || "g.";
    }

    function getUnitLabel(unitObj, langKey) {
        if (!unitObj || typeof unitObj !== "object") return "";
        const lang = resolveLangKey(langKey || "de");
        return (unitObj[lang] || unitObj.de || "").toString();
    }

    function buildUnitOptionsHtml(selectedDe, langKey) {
        const selectedValue = (selectedDe || "").toString().trim();
        const selectedKey = selectedValue.toLowerCase();
        const units = refreshUnits();

        const hasSelected = selectedKey && units.some((unit) => (unit?.de || "").toString().trim().toLowerCase() === selectedKey);
        if (selectedValue && !hasSelected) {
            units.push(normalizeUnit({ de: selectedValue }));
        }

        return units.map((unit) => {
            const de = (unit?.de || "").toString();
            const label = getUnitLabel(unit, langKey) || de;
            const isSelected = de.toLowerCase() === selectedKey;
            return `<option value="${escapeHtml(de)}" ${isSelected ? "selected" : ""}>${escapeHtml(label)}</option>`;
        }).join("");
    }

    window.IngredientManager = {
        getUnits,
        refreshUnits,
        findUnitByDe,
        getDefaultUnitDe,
        getUnitLabel,
        buildUnitOptionsHtml
    };

    document.addEventListener("DOMContentLoaded", () => {
        refreshUnits();
    });
})();


// END LEGACY COMPAT BUNDLE

// Extracted from CreatePosting.cshtml inline scripts (Smart Creator + page behavior)
        window.onerror = function(msg, url, line, col, error) {
            alert('GLOBAL JS ERROR: ' + msg + '\nZeile: ' + line + ' Spalte: ' + col + '\nDatei: ' + (url || '') + '\nStack: ' + (error && error.stack ? error.stack.substring(0, 300) : ''));
            return false;
        };


       

        //  const variablseTypes = ['duration', 'temperature', 'count', 'article', 'equipment', 
        //                           'tool', 'base', 'item', 'balance', 'seasoning','ingredient','ingredients'];

        
        


         function getCookieValue(name) {
             const cookie = document.cookie.split('; ').find(row => row.startsWith(`${name}=`));
             return cookie ? decodeURIComponent(cookie.split('=')[1]) : null;
         }

        function resolveLangKey(value) {
            const normalized = (value || 'de').toLowerCase();
            if (normalized === 'es') return 'esp';
            if (normalized === 'pt') return 'prt';
            if (normalized === 'se') return 'sv';
            if (normalized === 'dk') return 'da';
            return normalized;
        }

        let currentLang = resolveLangKey(getCookieValue('deli-lang'));
        let tempStepIdCounter = -1;
        let ingredientArticleRules = null;
        let activeIngredientConfigRow = null;

        const fallbackUnitsLocal = [
            { de: 'Stk.', en: 'pcs.', esp: 'uds.', prt: 'un.' },
            { de: 'EL.', en: 'tbsp.', esp: 'cda.', prt: 'colh. sopa' },
            { de: 'TL.', en: 'tsp.', esp: 'cdta.', prt: 'colh. cha' },
            { de: 'Pkg.', en: 'pack', esp: 'paq.', prt: 'pac.' },
            { de: 'Prise', en: 'pinch', esp: 'pizca', prt: 'pitada' },
            { de: 'g.', en: 'g.', esp: 'g.', prt: 'g.' },
            { de: 'Kg', en: 'kg', esp: 'kg', prt: 'kg' },
            { de: 'Bund', en: 'bunch', esp: 'manojo', prt: 'molho' },
            { de: 'Dose', en: 'can', esp: 'lata', prt: 'lata' },
            { de: 'etwas', en: 'some', esp: 'algo', prt: 'um pouco' },
            { de: 'spritzer', en: 'dash', esp: 'chorrito', prt: 'golpe' },
            { de: 'Becher', en: 'cup', esp: 'taza', prt: 'copo' },
            { de: 'Scheiben', en: 'slices', esp: 'rebanadas', prt: 'fatias' },
            { de: 'Blatt', en: 'leaf', esp: 'hoja', prt: 'folha' },
            { de: 'Blätter', en: 'leaves', esp: 'hojas', prt: 'folhas' },
            { de: 'Handvoll', en: 'handful', esp: 'puñado', prt: 'punhado' },
            { de: 'cm', en: 'cm', esp: 'cm', prt: 'cm' },
            { de: 'Zweig', en: 'sprig', esp: 'rama', prt: 'ramo' },
            { de: 'ml', en: 'ml', esp: 'ml', prt: 'ml' },
            { de: 'Gekocht', en: 'cooked', esp: 'cocido', prt: 'cozido' },
            { de: 'Portionen', en: 'portions', esp: 'porciones', prt: 'porcoes' },
            { de: 'l', en: 'l', esp: 'l', prt: 'l' },
            { de: 'Glas', en: 'glass', esp: 'vaso', prt: 'copo' },
            { de: 'kcal', en: 'kcal', esp: 'kcal', prt: 'kcal' },
            { de: 'mg', en: 'mg', esp: 'mg', prt: 'mg' },
            { de: 'µg', en: 'ug', esp: 'ug', prt: 'ug' }
        ];

        function getAvailableUnits() {
            if (window.IngredientManager && typeof window.IngredientManager.getUnits === 'function') {
                const units = window.IngredientManager.getUnits();
                if (Array.isArray(units) && units.length) return units;
            }
            return fallbackUnitsLocal;
        }

        function findUnitByDe(unitDe) {
            const normalized = (unitDe || '').toString().trim().toLowerCase();
            if (!normalized) return null;

            const units = getAvailableUnits();
            return units.find(u => (u?.de || '').toString().trim().toLowerCase() === normalized) || null;
        }

        function getUnitLabel(unitObj, langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            if (!unitObj || typeof unitObj !== 'object') return '';
            const map = {
                de: unitObj.de,
                en: unitObj.en,
                esp: unitObj.esp,
                prt: unitObj.prt,
                id: unitObj.id,
                ms: unitObj.ms,
                nl: unitObj.nl,
                sv: unitObj.sv,
                da: unitObj.da,
                no: unitObj.no
            };
            return (map[lang] || unitObj.de || '').toString();
        }

        function buildUnitOptionsHtml(selectedDe) {
            if (window.IngredientManager && typeof window.IngredientManager.buildUnitOptionsHtml === 'function') {
                return window.IngredientManager.buildUnitOptionsHtml(selectedDe, currentLang);
            }

            const selected = (selectedDe || '').toString().trim().toLowerCase();
            const units = [...getAvailableUnits()];
            if (selected && !units.some(u => (u?.de || '').toString().trim().toLowerCase() === selected)) {
                units.push({ de: selectedDe, en: selectedDe, esp: selectedDe, prt: selectedDe });
            }

            return units.map(u => {
                const de = (u?.de || '').toString();
                const label = getUnitLabel(u, currentLang) || de;
                const isSelected = de.toLowerCase() === selected;
                return `<option value="${$("<div>").text(de).html()}" ${isSelected ? 'selected' : ''}>${$("<div>").text(label).html()}</option>`;
            }).join('');
        }

        function dockIngredientConfigUnder(anchorElement) {
            const overlay = $('#ingredientConfigOverlay');
            const dock = $('#ingredientConfigDock');
            if (!overlay.length || !dock.length) return;
            dock.removeClass('ing-active');
            if (anchorElement && anchorElement.length) {
                anchorElement.after(dock);
            }
            overlay.addClass('ing-active').attr('aria-hidden', 'false');
            void dock[0].offsetWidth; // force reflow for animation
            dock.addClass('ing-active');
            $('#ingredientConfigQty').focus();
        }

        function openIngredientConfigPopup(row) {
            const target = $(row);
            if (!target.length) return;
            activeIngredientConfigRow = target;
            const name = (target.find('.display-name-selected').first().text() || '').trim();
            const qty = (target.find('.ingredient-qty-hidden').val() || '0').toString();
            const unitDe = (target.find('.ingredient-unit-hidden').val() || '').toString();
            $('#ingredientConfigTitle').text(name || 'Zutat');
            $('#ingredientConfigQty').val(qty);
            $('#ingredientConfigUnit').html(buildUnitOptionsHtml(unitDe));
            dockIngredientConfigUnder(target);
        }

        function closeIngredientConfigPopup() {
            activeIngredientConfigRow = null;
            $('#ingredientConfigDock').removeClass('ing-active');
            $('#ingredientConfigOverlay').removeClass('ing-active').attr('aria-hidden', 'true');
            $('#ingredientConfigDockParking').append($('#ingredientConfigDock'));
        }

        function applyIngredientConfigPopup() {
            const qty = normalizeDecimalInputValue($('#ingredientConfigQty').val()) || '0';
            const unitDe = ($('#ingredientConfigUnit').val() || '').toString();
            const unitObj = findUnitByDe(unitDe);
            const unitLabel = getUnitLabel(unitObj, currentLang) || unitDe;

            if (activeIngredientConfigRow && activeIngredientConfigRow.length) {
                activeIngredientConfigRow.find('.ingredient-qty-hidden').val(qty);
                activeIngredientConfigRow.find('.ingredient-unit-hidden').val(unitDe);
                activeIngredientConfigRow.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
                const ingredientId = (activeIngredientConfigRow.find('input[name$="IngredientsAndNutrients.Id"]').val() || '').toString();
                if (ingredientId) {
                    const sourceRow = $('.ingredient-db-row').filter(function () {
                        return ($(this).data('ingredient-id') || '').toString() === ingredientId;
                    }).first();
                    if (sourceRow.length) {
                        sourceRow.attr('data-selected-qty', qty);
                        sourceRow.attr('data-selected-unit', unitDe);
                    }
                }
                closeIngredientConfigPopup();
                return;
            }
        }

        /* ===== Probability Variable Editor ===== */
        let probVarEditorCallback = null;
        let probVarEditorAnchor = null;
        let probVarIngredientArticleValue = '';

        function renderProbVarIngredientArticleOptions(selectedArticle) {
            const wrap = $('#probVarIngredientArticleChips');
            if (!wrap.length) return;

            const options = getEditorArticleOptions(currentLang) || [];
            const current = (selectedArticle || '').toString().trim().toLowerCase();
            const noArticleLabelByLang = { de: 'ohne', en: 'none', esp: 'sin', prt: 'sem', nl: 'zonder' };
            const noArticleLabel = noArticleLabelByLang[resolveLangKey(currentLang)] || 'none';

            const noArticleActive = !current ? ' active' : '';
            const noArticleBtnClass = noArticleActive ? "btn-light text-dark" : "btn-outline-light";
            const noArticle = `<button type="button" class="btn btn-sm ${noArticleBtnClass} inline-article-opt js-prob-ingredient-article-chip${noArticleActive}" data-value="">${$('<div>').text(noArticleLabel).html()}</button>`;
            const chips = options.map(article => {
                const articleText = (article || '').toString();
                const isActive = current && current === articleText.toLowerCase();
                const active = isActive ? ' active' : '';
                const safe = $('<div>').text(articleText).html();
                const btnClass = isActive ? "btn-light text-dark" : "btn-outline-light";
                return `<button type="button" class="btn btn-sm ${btnClass} inline-article-opt js-prob-ingredient-article-chip${active}" data-value="${safe}">${safe}</button>`;
            }).join('');

            wrap.html(noArticle + chips);
        }

        function openProbVarEditor(masterId, varKey, currentVal, onApply, anchorElement) {
            probVarEditorCallback = onApply;
            probVarEditorAnchor = anchorElement ? $(anchorElement).closest('.probability-template-wrap') : null;
            const cleanKey = (varKey || '').toLowerCase().replace(/_/g, ' ');
            $('#probVarEditorTitle').text(cleanKey || 'Wert');
            $('#probVarIngredientArea, #probVarDurationArea, #probVarCountArea, #probVarTempArea, #probVarOptionsArea, #probVarTextArea').addClass('d-none');
            $('#probVarIngredientArticleChips').empty();
            $('#probVarApplyRow').addClass('d-none');

            const varType = typeof getPlaceholderType === 'function' ? getPlaceholderType(varKey) : '';

            if (varType === 'ingredient') {
                probVarIngredientArticleValue = '';
                $('#probVarIngredientArticleChips').empty();

                const selectedIds = (creatorState.selectedIngredientIds || []).map(x => x.toString());
                const ings = typeof getSelectedIngredientsForSandbox === 'function' ? getSelectedIngredientsForSandbox() : [];
                const chips = (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.buildIngredientChipsHtml === 'function')
                    ? window.MasterStepCreatorHelpers.buildIngredientChipsHtml(ings, selectedIds)
                    : '';
                $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewählt</span>');
                $('#probVarIngredientArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else if (varType === 'duration') {
                const num = parseInt((currentVal || '').split(' ')[0], 10) || 5;
                const isHour = (currentVal || '').toLowerCase().includes('stund') || (currentVal || '').toLowerCase().includes('hour');
                $('#probVarDurationVal').val(num);
                $('#probVarDurationUnit').val(isHour ? 'hour' : 'minute');
                $('#probVarDurationArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else if (varType === 'count') {
                $('#probVarCountVal').val(parseInt(currentVal, 10) || 1);
                $('#probVarCountArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else if (varType === 'temperature') {
                const tempNum = parseInt(currentVal, 10) || 180;
                const isFahr = (currentVal || '').includes('°F') || (currentVal || '').includes('F');
                $('#probVarTempVal').val(tempNum);
                $('#probVarTempUnit').val(isFahr ? 'fahrenheit' : 'celsius');
                $('#probVarTempArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else {
                // Options or text fallback — try to get presets from MasterStepRenderer
                let options = [];
                if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                    options = MasterStepRenderer.getVariablePresets(varKey, currentLang || 'de') || [];
                }
                if (!options.length && varType === 'heat' && typeof getHeatOptions === 'function') options = getHeatOptions();
                if (!options.length && varType === 'mode') options = (modeOptionsByLang[resolveLangKey(currentLang)] || modeOptionsByLang.de || []);
                if (!options.length && varType === 'tool' && typeof getToolOptions === 'function') options = getToolOptions();

                if (options.length) {
                    const chips = options.map(opt => {
                        const safe = $('<div>').text(opt).html();
                        const active = opt === currentVal ? ' active' : '';
                        return `<button type="button" class="btn btn-sm prob-var-chip js-prob-option-chip${active}" data-value="${safe}">${safe}</button>`;
                    }).join('');
                    $('#probVarOptionChips').html(chips);
                    $('#probVarOptionsArea').removeClass('d-none');
                } else {
                    $('#probVarTextVal').val(currentVal || '');
                    $('#probVarTextArea').removeClass('d-none');
                    $('#probVarApplyRow').removeClass('d-none');
                }
            }

            const dock = $('#probVarEditorDock');
            const overlay = $('#probVarEditorOverlay');
            dock.removeClass('prob-inline-mode');

            if (probVarEditorAnchor && probVarEditorAnchor.length) {
                overlay.removeClass('ing-active').attr('aria-hidden', 'true');
                probVarEditorAnchor.append(dock);
                setTimeout(function () { dock.addClass('ing-active prob-inline-mode'); }, 10);
                return;
            }

            $('#probVarEditorDockParking').append(dock);
            overlay.addClass('ing-active').attr('aria-hidden', 'false');
            setTimeout(function () { dock.addClass('ing-active'); }, 10);
        }

        function closeProbVarEditor() {
            probVarEditorCallback = null;
            probVarEditorAnchor = null;
            probVarIngredientArticleValue = '';
            const dock = $('#probVarEditorDock');
            dock.removeClass('ing-active prob-inline-mode');
            $('#probVarEditorDockParking').append(dock);
            $('#probVarEditorOverlay').removeClass('ing-active').attr('aria-hidden', 'true');
        }

        function applyProbVarEditor() {
            let value = null;
            if (!$('#probVarIngredientArea').hasClass('d-none')) {
                value = getSelectedIngredientValueForInsert();
            } else if (!$('#probVarDurationArea').hasClass('d-none')) {
                const num = parseInt($('#probVarDurationVal').val(), 10) || 1;
                const unit = $('#probVarDurationUnit').val() || 'minute';
                const label = typeof getDurationUnitLabel === 'function' ? getDurationUnitLabel(unit, currentLang) : unit;
                value = `${num} ${label}`;
            } else if (!$('#probVarCountArea').hasClass('d-none')) {
                value = (parseInt($('#probVarCountVal').val(), 10) || 1).toString();
            } else if (!$('#probVarTempArea').hasClass('d-none')) {
                const num = $('#probVarTempVal').val() || '180';
                const unit = $('#probVarTempUnit').val() === 'fahrenheit' ? '°F' : '°C';
                value = `${num} ${unit}`;
            } else if (!$('#probVarTextArea').hasClass('d-none')) {
                value = ($('#probVarTextVal').val() || '').toString().trim();
            }
            if (value !== null && probVarEditorCallback) {
                probVarEditorCallback(value);
            }
            closeProbVarEditor();
        }

        /* ===== End Probability Variable Editor ===== */

        async function loadIngredientArticleRules() {
            try {
                const response = await fetch('/data/ingredient_article_rules.json', { cache: 'no-store' });
                if (!response.ok) return;
                ingredientArticleRules = await response.json();
            } catch (_) {
                ingredientArticleRules = null;
            }
        }

        function normalizeGenusKey(genusRaw = '') {
            const value = (genusRaw || '').toString().trim().toLowerCase();
            if (!value) return '';
            if (['m', 'masc', 'mask', 'masculine', 'masculin', 'der', 'el', 'o', 'de'].some(x => value === x || value.includes(x))) return 'masc';
            if (['f', 'fem', 'feminine', 'femin', 'die', 'la', 'a'].some(x => value === x || value.includes(x))) return 'fem';
            if (['n', 'neut', 'neuter', 'das', 'het', 'det'].some(x => value === x || value.includes(x))) return 'neut';
            return '';
        }

        function applyArticleToName(name, article, lang) {
            const base = (name || '').toString().trim();
            const art = (article || '').toString().trim();
            if (!base || !art) return base;

            const langRules = {
                de: ['der', 'die', 'das', 'den', 'dem', 'des'],
                en: ['the', 'a', 'an'],
                esp: ['el', 'la', 'los', 'las', 'un', 'una'],
                prt: ['o', 'a', 'os', 'as', 'um', 'uma'],
                nl: ['de', 'het', 'een']
            };

            const prefixList = langRules[resolveLangKey(lang || currentLang || 'de')] || [];
            const lower = base.toLowerCase();
            if (prefixList.some(x => lower.startsWith(`${x} `))) return base;
            return `${art} ${base}`;
        }

        function getAllStepRows() {
            return Array.isArray(stepCatalog) ? stepCatalog : [];
        }

        function getPhaseLabel(phase) {
            const map = {
                1: { text: 'Vorb.', cls: 'phase-badge-1' },
                2: { text: 'Kochen', cls: 'phase-badge-2' },
                3: { text: 'Würzen', cls: 'phase-badge-3' },
                4: { text: 'Finish', cls: 'phase-badge-4' }
            };
            const info = map[phase];
            if (!info) return '';
            return `<span class="phase-badge ${info.cls}">${info.text}</span>`;
        }


        // Hilfsfunktion: Gibt die Zutat-IDs zurück, an die ein Step gebunden ist
        function getStepBoundIngredientIds(stepId) {
            return (stepIngredientBindings[stepId] ?? stepIngredientBindings[stepId?.toString?.()] ?? []).map(x => parseInt(x, 10));
        }


        function getSelectedIngredientIds() {
            return $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                return parseInt($(this).val(), 10);
            }).get().filter(id => !isNaN(id));
        }

        function getSelectedIngredientsForSandbox() {
            const fromSelected = $('#selectedIngredients .ingredient-row').map(function () {
                const row = $(this);
                const id = row.find('input[name$="IngredientsAndNutrients.Id"]').val()?.toString() || '';
                const name = row.find('.display-name-selected').text()?.trim() || '';
                const genusByLang = {
                    de: (row.data('genus-de') || '').toString().trim(),
                    en: (row.data('genus-en') || '').toString().trim(),
                    esp: (row.data('genus-esp') || '').toString().trim(),
                    prt: (row.data('genus-prt') || '').toString().trim(),
                    id: (row.data('genus-id') || '').toString().trim(),
                    nl: (row.data('genus-nl') || '').toString().trim(),
                    sv: (row.data('genus-sv') || '').toString().trim(),
                    da: (row.data('genus-da') || '').toString().trim(),
                    no: (row.data('genus-no') || '').toString().trim(),
                    ms: (row.data('genus-ms') || '').toString().trim()
                };
                return {
                    id,
                    name,
                    genusDe: genusByLang.de || '',
                    genusLocalized: genusByLang[currentLang] || '',
                    genusByLang
                };
            }).get().filter(x => x.id || x.name);

            if (fromSelected.length) {
                return fromSelected.map(x => ({
                    id: x.id || '',
                    name: x.name || 'Zutat',
                    genusDe: x.genusDe || '',
                    genusLocalized: x.genusLocalized || '',
                    genusByLang: x.genusByLang || {}
                }));
            }
            return [];
        }

        const isDarkTheme = true;
        const creatorState = {
            selectedIngredientIds: [],
            selectedTemplateId: '',
            activeStepIngredientRow: null,
            computedPronoun: 'ihn',
            computedArticle: 'den',
            previewText: '',
            ingredientReplaceArmed: false,
            activePlaceholderTokenId: '',
            placeholderAssignments: {},
            dragIngredientName: '',
            toastMessage: '',
            toastVisible: false,
            advancedPronounOverride: '',
            advancedArticleOverride: '',
            activeDurationTokenId: '',
            durationValue: 10,
            durationUnit: 'minute',
            activeCountTokenId: '',
            countValue: 2,
            activeTemperatureTokenId: '',
            temperatureValue: 180,
            temperatureUnit: 'celsius',
            activeHeatTokenId: '',
            heatValue: 'mittlerer',
            activeModeTokenId: '',
            modeValue: 'Ober- und Unterhitze',
            activePronounTokenId: '',
            pronounValue: '',
            activeEquipmentTokenId: '',
            equipmentValue: 'die Pfanne',
            equipmentArticleValue: 'die',
            activeStateTokenId: '',
            stateValue: 'goldbraun',
            activeToolTokenId: '',
            toolValue: 'Messer',
            toolArticleValue: '',
            activeShapeTokenId: '',
            activeGrindSizeTokenId: '',
            activeBaseTokenId: '',
            grindSizeValue: 'feine',
            shapeValue: 'Würfel',
            activeItemTokenId: '',
            itemValue: 'den Teig',
            itemArticleValue: 'den',
            baseValue: 'den Teig',
            baseArticleValue: 'den',
            activeBalanceTokenId: '',
            balanceValue: 'die Säure',
            balanceArticleValue: 'die',
            activeSeasoningsTokenId: '',
            seasoningsValue: 'Salz und Pfeffer'
        };

        const upsertStepUrl = window.CreatePostingPageConfig?.upsertStepUrl || '/WorldMiniApp/Home/UpsertStep';

        function getIngredientEmoji(name) {
            const text = (name || '').toLowerCase();
            if (text.includes('basil')) return '🌿';
            if (text.includes('tomat')) return '🍅';
            if (text.includes('zwiebel')) return '🧅';
            if (text.includes('knoblauch')) return '🧄';
            if (text.includes('reis')) return '🍚';
            if (text.includes('salat')) return '🥗';
            return '🥣';
        }

        function resolveGrammarForIngredient(ingredientName, genusByLang = {}, langKey = 'de') {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const value = (ingredientName || '').toString().trim().toLowerCase();
            const genusRaw = (genusByLang?.[lang] || genusByLang?.de || '').toString().trim().toLowerCase();
            const genusKey = normalizeGenusKey(genusRaw);

            const fromRules = ingredientArticleRules && ingredientArticleRules[lang] && ingredientArticleRules[lang][genusKey]
                ? ingredientArticleRules[lang][genusKey]
                : null;

            if (fromRules && (fromRules.article != null || fromRules.pronoun != null)) {
                return {
                    pronoun: (fromRules.pronoun || '').toString(),
                    article: (fromRules.article || '').toString()
                };
            }

            const hasAny = (hints) => hints.some(x => genusRaw === x || genusRaw.includes(x));

            if (lang === 'de') {
                const known = {
                    m: ['basilikum', 'reis', 'zucker', 'knoblauch', 'ingwer', 'kohl'],
                    f: ['tomate', 'zwiebel', 'paprika', 'karotte', 'kartoffel', 'soße', 'sauce'],
                    n: ['salz', 'öl', 'wasser', 'ei', 'mehl', 'fleisch', 'brot']
                };
                if (hasAny(['f', 'fem', 'femin', 'die'])) return { pronoun: 'sie', article: 'die' };
                if (hasAny(['n', 'neu', 'neut', 'das'])) return { pronoun: 'es', article: 'das' };
                if (hasAny(['m', 'mas', 'mask', 'der'])) return { pronoun: 'ihn', article: 'den' };

                let gender = 'm';
                if (known.f.some(x => value.includes(x))) gender = 'f';
                else if (known.n.some(x => value.includes(x))) gender = 'n';
                if (gender === 'f') return { pronoun: 'sie', article: 'die' };
                if (gender === 'n') return { pronoun: 'es', article: 'das' };
                return { pronoun: 'ihn', article: 'den' };
            }

            return { pronoun: 'it', article: lang === 'en' ? 'the' : '' };
        }


        function getPlaceholderKeysFromTemplate(template) {
            const tpl = template?.templates?.de || template?.templates?.en || '';
            const keys = new Set();
            const regex = /\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g;
            let match;
            while ((match = regex.exec(tpl)) !== null) {
                keys.add(match[1]);
            }
            return Array.from(keys);
        }

        function getSelectedIngredientNames() {
            const ingredients = getSelectedIngredientsForSandbox();
            if (!ingredients.length) return [];
            const selectedIds = creatorState.selectedIngredientIds || [];
            if (!selectedIds.length) return [];
            const selected = ingredients.filter(x => selectedIds.includes(x.id));
            return selected.map(x => x.name);
        }

        function getSelectedIngredientNamesWithArticle(langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const ingredients = getSelectedIngredientsForSandbox();
            if (!ingredients.length) return [];

            const selectedIds = creatorState.selectedIngredientIds || [];
            if (!selectedIds.length) return [];

            return ingredients
                .filter(x => selectedIds.includes(x.id))
                .map(x => {
                    const grammar = resolveGrammarForIngredient(x.name, x.genusByLang || {}, lang);
                    return applyArticleToName(x.name, grammar.article, lang) || x.name;
                });
        }

        function getSelectedIngredientForCreator() {
            const ingredients = getSelectedIngredientsForSandbox();
            const selectedId = (creatorState.selectedIngredientIds || [])[0] || '';
            if (!selectedId) return null;

            const selected = ingredients.find(x => x.id === selectedId);
            if (!selected) return null;

            return {
                id: selected.id || '',
                name: selected.name || '',
                genusDe: selected.genusDe || '',
                genusLocalized: selected.genusLocalized || '',
                genusByLang: selected.genusByLang || {}
            };
        }

        function getSelectedIngredientValueForInsert() {
            const names = getSelectedIngredientNames();
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.resolveIngredientInsertValue === 'function') {
                return window.MasterStepCreatorHelpers.resolveIngredientInsertValue(names, currentLang, '');
            }
            const list = (names || []).map(x => (x || '').toString().trim()).filter(Boolean);
            if (!list.length) return '';
            if (list.length === 1) return list[0];
            if (list.length === 2) return `${list[0]} und ${list[1]}`;
            const head = list.slice(0, -1).join(', ');
            const tail = list[list.length - 1];
            return `${head}, und ${tail}`;
        }

        function syncGrammarAssignmentsForSelectedIngredient() {
            const selectedIngredient = getSelectedIngredientForCreator();
            const grammar = resolveGrammarForIngredient(selectedIngredient?.name || '', selectedIngredient?.genusByLang || {}, currentLang);
            const pronounValue = (creatorState.advancedPronounOverride || grammar.pronoun || '').toString().trim();
            const articleValue = (creatorState.advancedArticleOverride || grammar.article || '').toString().trim();

            creatorState.computedPronoun = pronounValue;
            creatorState.computedArticle = articleValue;

            Object.keys(creatorState.placeholderAssignments || {}).forEach(tokenId => {
                const key = (tokenId || '').toString().split('__')[0].trim().toLowerCase();
                if (key === 'pronoun') {
                    if (pronounValue) creatorState.placeholderAssignments[tokenId] = pronounValue;
                    else delete creatorState.placeholderAssignments[tokenId];
                }
                if (key === 'article') {
                    if (articleValue) creatorState.placeholderAssignments[tokenId] = articleValue;
                    else delete creatorState.placeholderAssignments[tokenId];
                }
            });
        }

        function getEffectiveTemplateId() {
            return creatorState.selectedTemplateId;
        }

        function normalizePlaceholderKey(key) {
            return (key || '').toString().trim().toLowerCase();
        }

        const placeholderTypeMatchers = [
            { type: 'ingredient', match: key => key === 'ingredient' || key === 'ingredients' || key === 'liquid' || key === 'fat' },
            { type: 'duration', match: key => key.includes('duration') },
            { type: 'count', match: key => key === 'count' || key === 'servings' || key.includes('count') },
            { type: 'pronoun', match: key => key === 'pronoun' },
            { type: 'temperature', match: key => key === 'temp' || key === 'temperature' },
            { type: 'heat', match: key => key === 'heat' || key.includes('heat') },
            { type: 'mode', match: key => key === 'mode' || key.includes('mode') },
            { type: 'equipment', match: key => key === 'equipment' },
            { type: 'state', match: key => key === 'state' || key.includes('state') || key.includes('consistency') },
            { type: 'tool', match: key => key === 'tool' },
            { type: 'grindSize', match: key => key === 'grind_size' || key === 'grindsize' || key.includes('grind') },
            { type: 'shape', match: key => key === 'shape' },
            { type: 'base', match: key => key === 'base' },
            { type: 'item', match: key => key === 'item' || key === 'dish' || key.includes('item') },
            { type: 'balance', match: key => key === 'balance' },
            { type: 'seasonings', match: key => key === 'seasonings' || key === 'spices' }
        ];

        function getPlaceholderType(key) {
            const normalizedKey = normalizePlaceholderKey(key);
            const matched = placeholderTypeMatchers.find(entry => entry.match(normalizedKey));
            return matched ? matched.type : '';
        }

        const placeholderEditorDispatch = {
            duration: { open: openDurationEditorForToken, toast: 'Zeit setzen' },
            count: { open: openCountEditorForToken, toast: 'Anzahl setzen' },
            pronoun: { open: openPronounEditorForToken, toast: 'Pronomen wählen', keepTokenActive: true },
            temperature: { open: openTemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat: { open: openHeatEditorForToken, toast: 'Hitze-Stufe wählen' },
            mode: { open: openModeEditorForToken, toast: 'Ofenmodus wählen' },
            equipment: { open: openEquipmentEditorForToken, toast: 'Tool / Gerät wählen' },
            state: { open: openStateEditorForToken, toast: 'Zustand wählen' },
            tool: { open: openToolEditorForToken, toast: 'Tool / Gerät wählen' },
            grindSize: { open: openGrindSizeEditorForToken, toast: 'Schnittgröße wählen' },
            shape: { open: openShapeEditorForToken, toast: 'Schnittform wählen' },
            base: { open: openBaseEditorForToken, toast: 'Basis wählen' },
            item: { open: openItemEditorForToken, toast: 'Item wählen' },
            balance: { open: openBalanceEditorForToken, toast: 'Balance wählen' },
            seasonings: { open: openSeasoningsEditorForToken, toast: 'Seasonings wählen' }
        };

        const supportedLanguages = ['de', 'en', 'esp', 'prt', 'id', 'nl', 'sv', 'da', 'no', 'ms'];
        const durationUnitLabels = {
            minute: { de: 'Minuten', en: 'minutes', esp: 'minutos', prt: 'minutos', id: 'menit', nl: 'minuten', sv: 'minuter', da: 'minutter', no: 'minutter', ms: 'minit' },
            hour: { de: 'Stunden', en: 'hours', esp: 'horas', prt: 'horas', id: 'jam', nl: 'uur', sv: 'timmar', da: 'timer', no: 'timer', ms: 'jam' }
        };
        const modeOptionsByLang = {
            de: ['Oberhitze', 'Unterhitze', 'Ober- und Unterhitze', 'Umluft'],
            en: ['top heat', 'bottom heat', 'top and bottom heat', 'convection'],
            esp: ['calor superior', 'calor inferior', 'calor superior e inferior', 'convección'],
            prt: ['calor superior', 'calor inferior', 'calor superior e inferior', 'convecção'],
            id: ['panas atas', 'panas bawah', 'panas atas dan bawah', 'konveksi'],
            nl: ['bovenwarmte', 'onderwarmte', 'boven- en onderwarmte', 'hetelucht'],
            sv: ['övervärme', 'undervärme', 'över- och undervärme', 'varmluft'],
            da: ['overvarme', 'undervarme', 'over- og undervarme', 'varmluft'],
            no: ['overvarme', 'undervarme', 'over- og undervarme', 'varmluft'],
            ms: ['haba atas', 'haba bawah', 'haba atas dan bawah', 'peredaran udara']
        };
        const closeEditorHandlers = [
            closeDurationEditor,
            closeCountEditor,
            closeTemperatureEditor,
            closeHeatEditor,
            closeModeEditor,
            closePronounEditor,
            closeEquipmentEditor,
            closeStateEditor,
            closeToolEditor,
            closeGrindSizeEditor,
            closeShapeEditor,
            closeBaseEditor,
            closeItemEditor,
            closeBalanceEditor,
            closeSeasoningsEditor
        ];

        function getPlaceholderKeyByTokenId(tokenId) {
            return ($(`#masterPreviewText .placeholder-token[data-placeholder-token-id="${tokenId}"]`).data('placeholder-key') || '').toString();
        }


        function applyTokenAssignmentsToVariables(vars) {
            const merged = { ...(vars || {}) };
            const assignments = creatorState.placeholderAssignments || {};

            Object.keys(assignments).forEach(function (tokenId) {
                const assignedValue = (assignments[tokenId] || '').toString().trim();
                if (!assignedValue) return;

                const key = (tokenId || '').toString().split('__')[0].trim();
                if (!key) return;

                merged[key] = assignedValue;
            });

            return merged;
        }

        function getDurationUnitLabel(unitKey, langKey) {
            const key = (unitKey || 'minute').toString().toLowerCase();
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();
            return durationUnitLabels[key]?.[lang] || durationUnitLabels[key]?.de || '';
        }

        function getDurationInsertText() {
            const value = parseInt(creatorState.durationValue, 10);
            const safeValue = Number.isFinite(value) && value > 0 ? value : 1;
            const unitLabel = getDurationUnitLabel(creatorState.durationUnit, currentLang);
            return `${safeValue} ${unitLabel}`.trim();
        }

        function refreshDurationUnitControls() {
            const minuteLabel = getDurationUnitLabel('minute', currentLang);
            const hourLabel = getDurationUnitLabel('hour', currentLang);
            const minuteOption = $('#durationUnitSelect option[value="minute"]');
            const hourOption = $('#durationUnitSelect option[value="hour"]');
            if (minuteOption.length) minuteOption.text(minuteLabel);
            if (hourOption.length) hourOption.text(hourLabel);
        }

        function openDurationEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeDurationTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s+/);
            if (parsed) creatorState.durationValue = parseInt(parsed[1], 10);
            $('#durationValueInput').val(creatorState.durationValue || 10);
            refreshDurationUnitControls();
            $('#durationUnitSelect').val(creatorState.durationUnit || 'minute');
            $('#durationEditor').removeClass('d-none');
        }

        function closeDurationEditor() {
            creatorState.activeDurationTokenId = '';
            $('#durationEditor').addClass('d-none');
        }

        function getCountInsertText() {
            const value = parseInt(creatorState.countValue, 10);
            const safeValue = Number.isFinite(value) && value > 0 ? value : 1;
            return `${safeValue}`;
        }

        function openCountEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeCountTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)/);
            if (parsed) creatorState.countValue = parseInt(parsed[1], 10);
            $('#countValueInput').val(creatorState.countValue || 2);
            $('#countEditor').removeClass('d-none');
        }

        function closeCountEditor() {
            creatorState.activeCountTokenId = '';
            $('#countEditor').addClass('d-none');
        }

        function getTemperatureInsertText() {
            const value = parseInt(creatorState.temperatureValue, 10);
            const safeValue = Number.isFinite(value) && value > 0 ? value : 180;
            const unit = creatorState.temperatureUnit === 'fahrenheit' ? '°F' : '°C';
            return `${safeValue}${unit}`;
        }

        function openTemperatureEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeTemperatureTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s*°?\s*(C|F)?/i);
            if (parsed) {
                creatorState.temperatureValue = parseInt(parsed[1], 10);
                if (parsed[2] && parsed[2].toUpperCase() === 'F') {
                    creatorState.temperatureUnit = 'fahrenheit';
                }
            }
            $('#temperatureValueInput').val(creatorState.temperatureValue || 180);
            $('#temperatureUnitSelect').val(creatorState.temperatureUnit || 'celsius');
            $('#temperatureEditor').removeClass('d-none');
        }

        function closeTemperatureEditor() {
            creatorState.activeTemperatureTokenId = '';
            $('#temperatureEditor').addClass('d-none');
        }

        function getHeatOptions() {
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('heat', currentLang || 'de');
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['niedriger', 'mittlerer', 'hoher'];
        }

        function renderInlineHeatOptions() {
            renderInlineChoiceButtons('#inlineHeatOptions', 'inline-heat-opt', getHeatOptions(), creatorState.heatValue || '');
        }

        function openHeatEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeHeatTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.heatValue || '').toString().trim().toLowerCase();
            const options = getHeatOptions();
            const selected = options.find(x => x.toLowerCase() === currentText) || options[1] || options[0] || 'mittlerer';
            creatorState.heatValue = selected;
            renderInlineHeatOptions();
            placeEditorLikeTemperature('#heatEditor');
            $('#heatEditor').removeClass('d-none');
        }

        function closeHeatEditor() {
            creatorState.activeHeatTokenId = '';
            $('#heatEditor').addClass('d-none');
        }

        function getModeOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            return modeOptionsByLang[lang] || modeOptionsByLang.de;
        }

        function openModeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeModeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.modeValue || '').toString().trim().toLowerCase();
            const options = getModeOptions();
            const selected = options.find(x => x.toLowerCase() === currentText) || options[2] || options[0] || 'Ober- und Unterhitze';
            creatorState.modeValue = selected;
            renderInlineModeOptions();
            placeEditorLikeTemperature('#modeEditor');
            $('#modeEditor').removeClass('d-none');
        }

        function closeModeEditor() {
            creatorState.activeModeTokenId = '';
            $('#modeEditor').addClass('d-none');
        }

        function renderInlineModeOptions() {
            renderInlineChoiceButtons('#inlineModeOptions', 'inline-mode-opt', getModeOptions(), creatorState.modeValue || '');
        }

        function openPronounEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activePronounTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.computedPronoun || '').toString().trim();
            creatorState.pronounValue = currentText;
            renderInlinePronounOptions();
            placeEditorLikeTemperature('#pronounEditor');
            $('#pronounEditor').removeClass('d-none');
        }

        function closePronounEditor() {
            creatorState.activePronounTokenId = '';
            $('#pronounEditor').addClass('d-none');
        }

        function renderInlinePronounOptions() {
            renderInlineChoiceButtons(
                '#inlinePronounOptions',
                'inline-pronoun-opt',
                getPronounSuggestionsForLang(currentLang),
                creatorState.pronounValue || creatorState.computedPronoun || ''
            );
        }

        function ensureEquipmentArticle(value, langKey) {
            const raw = (value || '').toString().trim();
            if (!raw) return '';

            const lang = resolveLangKey(langKey || currentLang || 'de');
            const articlePrefixesByLang = {
                de: ['der', 'die', 'das', 'den', 'dem', 'des'],
                en: ['the'],
                esp: ['el', 'la', 'los', 'las'],
                prt: ['o', 'a', 'os', 'as'],
                nl: ['de', 'het']
            };

            const lower = raw.toLowerCase();
            const existing = articlePrefixesByLang[lang] || [];
            if (existing.some(x => lower.startsWith(`${x} `))) return raw;

            const articleMapByLang = {
                de: {
                    'backofen': 'den',
                    'pfanne': 'die',
                    'bräter': 'den',
                    'topf': 'den',
                    'küchenmaschine': 'die',
                    'auflaufform': 'die'
                },
                en: {
                    'oven': 'the',
                    'pan': 'the',
                    'roaster': 'the',
                    'pot': 'the',
                    'food processor': 'the',
                    'baking dish': 'the'
                },
                esp: {
                    'horno': 'el',
                    'sartén': 'la',
                    'asador': 'el',
                    'olla': 'la',
                    'procesador de alimentos': 'el',
                    'fuente para horno': 'la'
                },
                prt: {
                    'forno': 'o',
                    'frigideira': 'a',
                    'assadeira': 'a',
                    'panela': 'a',
                    'processador de alimentos': 'o',
                    'travessa de forno': 'a'
                },
                nl: {
                    'oven': 'de',
                    'pan': 'de',
                    'braadslede': 'de',
                    'kookpot': 'de',
                    'keukenmachine': 'de',
                    'ovenschaal': 'de'
                }
            };

            const article = articleMapByLang[lang]?.[lower] || '';
            if (!article) return raw;
            return `${article} ${raw}`;
        }

        function getEditorArticleOptions(langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('articles', lang);
                if (Array.isArray(options) && options.length) {
                    return options.filter(x => (x || '').toString().trim() !== '');
                }
            }
            const map = {
                de: ['der', 'die', 'das', 'den'],
                en: ['the'],
                esp: ['el', 'la', 'los', 'las'],
                prt: ['o', 'a', 'os', 'as'],
                nl: ['de', 'het']
            };
            return map[lang] || [];
        }

        function splitLeadingArticle(value, langKey = currentLang) {
            const raw = (value || '').toString().trim();
            if (!raw) return { article: '', noun: '' };
            const options = getEditorArticleOptions(langKey);
            const lower = raw.toLowerCase();
            const found = options.find(x => lower.startsWith(`${x.toLowerCase()} `));
            if (!found) return { article: '', noun: raw };
            return { article: found, noun: raw.substring(found.length).trim() };
        }

        function composeArticleAndNoun(article, noun) {
            const art = (article || '').toString().trim();
            const n = (noun || '').toString().trim();
            if (!n) return '';
            return art ? `${art} ${n}` : n;
        }

        function getNounOptions(options, langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const result = (options || []).map(x => splitLeadingArticle(x, lang).noun).filter(Boolean);
            return Array.from(new Set(result));
        }

        function renderArticleOptions(wrapSelector, selectedArticle) {
            const wrap = $(wrapSelector);
            if (!wrap.length) return;
            const options = getEditorArticleOptions(currentLang);
            if (!options.length) {
                wrap.empty().addClass('d-none');
                return;
            }
            wrap.removeClass('d-none');
            const current = (selectedArticle || '').toString().trim().toLowerCase();
            const noArticleLabelByLang = {
                de: 'ohne',
                en: 'none',
                esp: 'sin',
                prt: 'sem',
                nl: 'zonder'
            };
            const noArticleLabel = noArticleLabelByLang[resolveLangKey(currentLang)] || 'none';
            const optionHtml = options.map(x => {
                const isActive = current === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-article-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            const noArticleIsActive = !current;
            const noArticleBtnClass = noArticleIsActive ? 'btn-light text-dark' : 'btn-outline-light';
            const noArticleHtml = `<button type="button" class="btn btn-sm ${noArticleBtnClass} inline-article-opt" data-value="">${$('<div>').text(noArticleLabel).html()}</button>`;
            wrap.html(noArticleHtml + optionHtml);
        }

        function getEquipmentOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            const fallbackByLang = {
                de: ['Pfanne', 'Topf', 'Backofen', 'Rührschüssel', 'Sieb', 'Mixer', 'Pürierstab', 'Küchenmaschine', 'Bräter', 'Wok', 'Grill', 'Dampfgarer', 'Auflaufform', 'Zange', 'Schneidebrett'],
                en: ['pan', 'pot', 'oven', 'mixing bowl', 'strainer', 'blender', 'immersion blender', 'food processor', 'roaster', 'wok', 'grill', 'steamer', 'baking dish', 'tongs', 'cutting board'],
                esp: ['sartén', 'olla', 'horno', 'bol para mezclar', 'colador', 'batidora', 'batidora de mano', 'procesador de alimentos', 'asador', 'wok', 'parrilla', 'vaporera', 'fuente para horno', 'pinzas', 'tabla de cortar'],
                prt: ['frigideira', 'panela', 'forno', 'tigela de mistura', 'coador', 'liquidificador', 'mixer de mão', 'processador de alimentos', 'assadeira', 'wok', 'grelha', 'cozedor a vapor', 'travessa de forno', 'pinça', 'tábua de corte'],
                id: ['wajan', 'panci', 'oven', 'mangkuk adonan', 'saringan', 'blender', 'blender tangan', 'food processor', 'loyang panggang', 'wok', 'pemanggang', 'kukusan', 'pinggan oven', 'penjepit', 'talenan'],
                nl: ['pan', 'kookpot', 'oven', 'mengkom', 'zeef', 'blender', 'staafmixer', 'keukenmachine', 'braadslede', 'wok', 'grill', 'stoomkoker', 'ovenschaal', 'tang', 'snijplank'],
                sv: ['stekpanna', 'gryta', 'ugn', 'blandningsskål', 'sil', 'mixer', 'stavmixer', 'matberedare', 'stekgryta', 'wok', 'grill', 'ångkokare', 'ugnsform', 'tång', 'skärbräda'],
                da: ['pande', 'gryde', 'ovn', 'røreskål', 'si', 'blender', 'stavblender', 'foodprocessor', 'bradepande', 'wok', 'grill', 'dampkoger', 'ovnfast fad', 'tang', 'skærebræt'],
                no: ['stekepanne', 'gryte', 'ovn', 'miksebolle', 'sil', 'blender', 'stavmikser', 'kjøkkenmaskin', 'stekeform', 'wok', 'grill', 'dampkoker', 'ildfast form', 'klype', 'skjærefjøl'],
                ms: ['kuali', 'periuk', 'ketuhar', 'mangkuk adunan', 'penapis', 'pengisar', 'pengisar tangan', 'pemproses makanan', 'dulang pembakar', 'wok', 'pemanggang', 'pengukus', 'bekas pembakar', 'penyepit', 'papan pemotong']
            };

            const rendererOptions = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('equipment', lang)
                : [];

            const fallback = fallbackByLang[lang] || fallbackByLang.de;
            const merged = [...(Array.isArray(rendererOptions) ? rendererOptions : []), ...fallback]
                .map(x => ensureEquipmentArticle((x || '').toString().trim(), lang))
                .filter(Boolean);

            return Array.from(new Set(merged));
        }

        function openEquipmentEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeEquipmentTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.equipmentValue || '').toString().trim();
            const options = getEquipmentOptions();
            const selected = options.find(x => x.toLowerCase() === (currentText || '').toLowerCase()) || options[0] || 'die Pfanne';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.equipmentArticleValue = parts.article;
            creatorState.equipmentValue = selected;
            renderInlineEquipmentOptions();
            placeEditorLikeTemperature('#equipmentEditor');
            $('#equipmentEditor').removeClass('d-none');
        }

        function closeEquipmentEditor() {
            creatorState.activeEquipmentTokenId = '';
            $('#equipmentEditor').addClass('d-none');
        }

        function renderInlineEquipmentOptions() {
            const wrap = $('#inlineEquipmentOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineEquipmentArticleOptions', creatorState.equipmentArticleValue);
            const options = getNounOptions(getEquipmentOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.equipmentValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-equipment-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getStateOptions() {
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('state', currentLang || 'de');
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['goldbraun', 'weich', 'glasig', 'gar', 'knusprig', 'cremig', 'bissfest', 'eingedickt', 'sprudelnd'];
        }

        function openStateEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeStateTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const options = getStateOptions();
            const selected = options.find(x => x.toLowerCase() === (currentText || '').toLowerCase()) || options[0] || 'goldbraun';
            creatorState.stateValue = selected;
            renderInlineStateOptions();
            placeEditorLikeTemperature('#stateEditor');
            $('#stateEditor').removeClass('d-none');
        }

        function closeStateEditor() {
            creatorState.activeStateTokenId = '';
            $('#stateEditor').addClass('d-none');
        }

        function renderInlineStateOptions() {
            const wrap = $('#inlineStateOptions');
            if (!wrap.length) return;
            const options = getStateOptions();
            const currentVal = (creatorState.stateValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-state-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getToolOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            const fallbackByLang = {
                de: ['Messer', 'Sparschäler', 'Reibe', 'Schneebesen', 'Spatel', 'Holzlöffel', 'Suppenkelle', 'Messbecher', 'Nudelholz', 'Teigschaber'],
                en: ['knife', 'peeler', 'grater', 'whisk', 'spatula', 'wooden spoon', 'ladle', 'measuring cup', 'rolling pin', 'dough scraper'],
                esp: ['cuchillo', 'pelador', 'rallador', 'batidor', 'espátula', 'cuchara de madera', 'cucharón', 'vaso medidor', 'rodillo', 'rasqueta de masa'],
                prt: ['faca', 'descascador', 'ralador', 'batedor', 'espátula', 'colher de pau', 'concha', 'copo medidor', 'rolo de massa', 'raspador de massa'],
                id: ['pisau', 'pengupas', 'parutan', 'pengocok', 'spatula', 'sendok kayu', 'sendok sayur', 'gelas ukur', 'rolling pin', 'scraper adonan'],
                nl: ['mes', 'dunschiller', 'rasp', 'garde', 'spatel', 'houten lepel', 'soeplepel', 'maatbeker', 'deegroller', 'deegschraper'],
                sv: ['kniv', 'potatisskalare', 'rivjärn', 'visp', 'stekspade', 'träslev', 'slev', 'måttkopp', 'kavel', 'degskrapa'],
                da: ['kniv', 'skræller', 'rivejern', 'piskeris', 'spatel', 'træske', 'suppeske', 'målebæger', 'kagerulle', 'dejskraber'],
                no: ['kniv', 'skreller', 'rivjern', 'visp', 'stekespade', 'tresleiv', 'øse', 'målebeger', 'kjevle', 'deigskrape'],
                ms: ['pisau', 'pengupas', 'parut', 'pemukul', 'spatula', 'sudu kayu', 'senduk', 'cawan penyukat', 'penggelek doh', 'pengikis doh']
            };

            const rendererOptions = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('tool', lang)
                : [];

            const fallback = fallbackByLang[lang] || fallbackByLang.de;
            const merged = [...(Array.isArray(rendererOptions) ? rendererOptions : []), ...fallback]
                .map(x => (x || '').toString().trim())
                .filter(Boolean);

            return Array.from(new Set(merged));
        }

        function openToolEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeToolTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.toolValue || '').toString().trim();
            const options = getToolOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'Messer';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.toolArticleValue = parts.article;
            creatorState.toolValue = composeArticleAndNoun(parts.article, parts.noun || selected);
            renderInlineToolOptions();
            $('#toolEditor').removeClass('d-none');
        }

        function closeToolEditor() {
            creatorState.activeToolTokenId = '';
            $('#toolEditor').addClass('d-none');
        }

        function renderInlineToolOptions() {
            const wrap = $('#inlineToolOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineToolArticleOptions', creatorState.toolArticleValue);
            const options = getNounOptions(getToolOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.toolValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-tool-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getGrindSizeOptions() {
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('grind_size', currentLang || 'de');
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            const fallback = {
                de: ['fein', 'feine', 'mittel', 'mittlere', 'grob', 'grobe', 'dünn', 'dünne', 'breit', 'breite', 'klein', 'kleine', 'groß', 'große', 'ca. 1 cm groß', 'ca. 1 cm große', 'ca. 0,5 mm groß', 'ca. 0,5 mm große'],
                en: ['fine', 'medium', 'coarse', 'thin', 'wide', 'small', 'large', 'about 3/8-inch', 'about 0.02-inch'],
                esp: ['finas', 'medianas', 'gruesas', 'delgadas', 'anchas', 'pequeñas', 'grandes'],
                prt: ['finas', 'médias', 'grossas', 'finas', 'largas', 'pequenas', 'grandes'],
                id: ['halus', 'sedang', 'kasar', 'tipis', 'tebal', 'kecil', 'besar'],
                nl: ['fijne', 'middelgrote', 'grove', 'dunne', 'brede', 'kleine', 'grote'],
                sv: ['fina', 'medelgrova', 'grova', 'tunna', 'breda', 'små', 'stora'],
                da: ['fine', 'mellemstore', 'grove', 'tynde', 'brede', 'små', 'store'],
                no: ['fine', 'middels', 'grove', 'tynne', 'brede', 'små', 'store'],
                ms: ['halus', 'sederhana', 'kasar', 'nipis', 'lebar', 'kecil', 'besar']
            };
            return fallback[(currentLang || 'de').toLowerCase()] || fallback.de;
        }

        function openGrindSizeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeGrindSizeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.grindSizeValue || '').toString().trim();
            const options = getGrindSizeOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'feine';
            creatorState.grindSizeValue = selected;
            renderInlineGrindSizeOptions();
            placeEditorLikeTemperature('#grindSizeEditor');
            $('#grindSizeEditor').removeClass('d-none');
        }

        function closeGrindSizeEditor() {
            creatorState.activeGrindSizeTokenId = '';
            $('#grindSizeEditor').addClass('d-none');
        }

        function renderInlineGrindSizeOptions() {
            const wrap = $('#inlineGrindSizeOptions');
            if (!wrap.length) return;
            const options = getGrindSizeOptions();
            const currentVal = (creatorState.grindSizeValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-grind-size-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getShapeOptions() {
            return ['Würfel', 'Scheiben', 'Streifen', 'Spalten', 'grobe Stücke', 'Ringe', 'Julienne', 'Stifte'];
        }

        function openShapeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeShapeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.shapeValue || '').toString().trim();
            creatorState.shapeValue = currentText || 'Würfel';
            renderInlineShapeOptions();
            placeEditorLikeTemperature('#shapeEditor');
            $('#shapeEditor').removeClass('d-none');
        }

        function closeShapeEditor() {
            creatorState.activeShapeTokenId = '';
            $('#shapeEditor').addClass('d-none');
        }
        function getBaseAndItemOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            const rendererBase = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('base', lang)
                : [];
            const rendererItem = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('item', lang)
                : [];

            const fallbackByLang = {
                de: ['den Teig', 'die Masse'],
                en: ['the dough', 'the mixture'],
                esp: ['la masa', 'la mezcla'],
                prt: ['a massa', 'a mistura'],
                id: ['adonan', 'campuran'],
                nl: ['het deeg', 'het mengsel'],
                sv: ['degen', 'blandningen'],
                da: ['dejen', 'blandingen'],
                no: ['deigen', 'blandingen'],
                ms: ['doh', 'campuran']
            };

            const merged = [
                ...(Array.isArray(rendererBase) ? rendererBase : []),
                ...(Array.isArray(rendererItem) ? rendererItem : []),
                ...((fallbackByLang[lang] || fallbackByLang.de))
            ].map(x => (x || '').toString().trim()).filter(Boolean);

            const seen = new Set();
            return merged.filter(x => {
                const key = x.toLowerCase();
                if (seen.has(key)) return false;
                seen.add(key);
                return true;
            });
        }

        function getBaseOptions() {
            return getBaseAndItemOptions();
        }

        function openBaseEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBaseTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.baseValue || '').toString().trim();
            const options = getBaseOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'den Teig';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.baseArticleValue = parts.article;
            creatorState.baseValue = selected;
            renderInlineBaseOptions();
            $('#baseEditor').removeClass('d-none');
        }

        function closeBaseEditor() {
            creatorState.activeBaseTokenId = '';
            $('#baseEditor').addClass('d-none');
        }

        function renderInlineBaseOptions() {
            const wrap = $('#inlineBaseOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineBaseArticleOptions', creatorState.baseArticleValue);
            const options = getNounOptions(getBaseOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.baseValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-base-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }
        function getItemOptions() {
            return getBaseAndItemOptions();
        }

        function openItemEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeItemTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.itemValue || '').toString().trim();
            const options = getItemOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'den Teig';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.itemArticleValue = parts.article;
            creatorState.itemValue = selected;
            renderInlineItemOptions();
            placeEditorLikeTemperature('#itemEditor');
            $('#itemEditor').removeClass('d-none');
        }

        function closeItemEditor() {
            creatorState.activeItemTokenId = '';
            $('#itemEditor').addClass('d-none');
        }

        function renderInlineItemOptions() {
            const wrap = $('#inlineItemOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineItemArticleOptions', creatorState.itemArticleValue);
            const options = getNounOptions(getItemOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.itemValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-item-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getBalanceOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('balance', lang);
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['die Säure', 'die Süße', 'die Schärfe'];
        }

        function openBalanceEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBalanceTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.balanceValue || '').toString().trim();
            const options = getBalanceOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'die Säure';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.balanceArticleValue = parts.article;
            creatorState.balanceValue = selected;
            renderInlineBalanceOptions();
            placeEditorLikeTemperature('#balanceEditor');
            $('#balanceEditor').removeClass('d-none');
        }

        function closeBalanceEditor() {
            creatorState.activeBalanceTokenId = '';
            $('#balanceEditor').addClass('d-none');
        }

        function renderInlineBalanceOptions() {
            const wrap = $('#inlineBalanceOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineBalanceArticleOptions', creatorState.balanceArticleValue);
            const options = getNounOptions(getBalanceOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.balanceValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-balance-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getSeasoningsOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('seasonings', lang);
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['Salz', 'Pfeffer', 'Salz und Pfeffer', 'Kräuter', 'Gewürze'];
        }

        function openSeasoningsEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeSeasoningsTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.seasoningsValue || '').toString().trim();
            const options = getSeasoningsOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'Salz';
            creatorState.seasoningsValue = selected;
            renderInlineSeasoningsOptions();
            placeEditorLikeTemperature('#seasoningsEditor');
            $('#seasoningsEditor').removeClass('d-none');
        }

        function closeSeasoningsEditor() {
            creatorState.activeSeasoningsTokenId = '';
            $('#seasoningsEditor').addClass('d-none');
        }

        function renderInlineSeasoningsOptions() {
            renderInlineChoiceButtons('#inlineSeasoningsOptions', 'inline-seasonings-opt', getSeasoningsOptions(), creatorState.seasoningsValue || '');
        }

        function renderInlineChoiceButtons(wrapSelector, optionClass, options, currentValue) {
            const wrap = $(wrapSelector);
            if (!wrap.length) return;
            const currentVal = (currentValue || '').toString().trim().toLowerCase();
            const html = (options || []).map(x => {
                const value = (x || '').toString();
                const isActive = currentVal === value.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} ${optionClass}" data-value="${$('<div>').text(value).html()}">${$('<div>').text(value).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function closeAllEditors() {
            closeEditorHandlers.forEach(function (closeEditor) {
                if (typeof closeEditor === 'function') {
                    closeEditor();
                }
            });
        }

        function placeEditorLikeTemperature(editorSelector) {
            const editor = $(editorSelector);
            const tempEditor = $('#temperatureEditor');
            if (!editor.length || !tempEditor.length) return;

            editor.removeClass('bottom-sheet-editor').addClass('duration-editor mt-2');
            tempEditor.after(editor);
        }

        function renderInlineShapeOptions() {
            renderInlineChoiceButtons('#inlineShapeOptions', 'inline-shape-opt', getShapeOptions(), creatorState.shapeValue || '');
        }

        function getPronounSuggestionsForLang(langKey) {
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();
            const map = {
                de: ['ihn', 'sie', 'es'],
                en: ['him', 'her', 'it', 'them'],
                esp: ['lo', 'la', 'ellos', 'ellas'],
                prt: ['o', 'a', 'eles', 'elas'],
                id: ['dia', 'mereka'],
                nl: ['hem', 'haar', 'het', 'hen'],
                sv: ['honom', 'henne', 'den', 'det'],
                da: ['ham', 'hende', 'den', 'det'],
                no: ['ham', 'henne', 'den', 'det'],
                ms: ['dia', 'mereka']
            };
            return map[lang] || map.de;
        }

        function getLocalizedFallbackForVariable(key, langKey) {
            const keyNorm = (key || '').toString().trim().toLowerCase();
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();

            if (keyNorm === 'base') {
                const map = {
                    de: 'den Teig',
                    en: 'the dough',
                    esp: 'la masa',
                    prt: 'a massa',
                    id: 'adonan',
                    nl: 'het deeg',
                    sv: 'degen',
                    da: 'dejen',
                    no: 'deigen',
                    ms: 'doh'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'dough') {
                const map = {
                    de: 'den Teig',
                    en: 'the dough',
                    esp: 'la masa',
                    prt: 'a massa',
                    id: 'adonan',
                    nl: 'het deeg',
                    sv: 'degen',
                    da: 'dejen',
                    no: 'deigen',
                    ms: 'doh'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step1') {
                const map = {
                    de: 'Mehl', en: 'flour', esp: 'harina', prt: 'farinha', id: 'tepung', nl: 'bloem', sv: 'mjöl', da: 'mel', no: 'mel', ms: 'tepung'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step2') {
                const map = {
                    de: 'Ei', en: 'egg', esp: 'huevo', prt: 'ovo', id: 'telur', nl: 'ei', sv: 'ägg', da: 'æg', no: 'egg', ms: 'telur'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step3') {
                const map = {
                    de: 'Brösel', en: 'breadcrumbs', esp: 'pan rallado', prt: 'farinha de rosca', id: 'tepung roti', nl: 'paneermeel', sv: 'ströbröd', da: 'rasp', no: 'brødsmuler', ms: 'serbuk roti'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'equipment') {
                return getEquipmentOptions()[0] || 'die Pfanne';
            }

            if (keyNorm === 'tool') {
                return getToolOptions()[0] || 'Messer';
            }

            if (keyNorm === 'grind_size' || keyNorm === 'grindsize') {
                return getGrindSizeOptions()[0] || 'feine';
            }

            if (keyNorm === 'item' || keyNorm === 'dish') {
                return getItemOptions()[0] || 'den Teig';
            }

            if (keyNorm === 'balance') {
                return getBalanceOptions()[0] || 'die Säure';
            }

            if (keyNorm === 'seasonings' || keyNorm === 'spices') {
                return getSeasoningsOptions()[2] || getSeasoningsOptions()[0] || 'Salz und Pfeffer';
            }

            return '';
        }

        function getSelectedIngredientValueForLanguage(langKey) {
            const names = getSelectedIngredientNames();
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.resolveIngredientInsertValue === 'function') {
                return window.MasterStepCreatorHelpers.resolveIngredientInsertValue(names, langKey, '');
            }
            const list = (names || []).map(x => (x || '').toString().trim()).filter(Boolean);
            if (!list.length) return '';
            if (list.length === 1) return list[0];
            if (list.length === 2) return `${list[0]} und ${list[1]}`;
            const head = list.slice(0, -1).join(', ');
            const tail = list[list.length - 1];
            return `${head}, und ${tail}`;
        }

        function resolveDefaultVariableValueForLanguage(key, langKey) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const placeholderType = getPlaceholderType(key);
            if (key === 'ingredient' || key === 'ingredients' || key === 'liquid') {
                const ingredientValue = getSelectedIngredientValueForLanguage(lang);
                return ingredientValue || getLocalizedFallbackForVariable('ingredient', lang) || key;
            }
            if (key === 'pronoun') {
                const selectedIngredient = getSelectedIngredientForCreator();
                const grammar = resolveGrammarForIngredient(selectedIngredient?.name || '', selectedIngredient?.genusByLang || {}, lang);
                return creatorState.advancedPronounOverride || creatorState.pronounValue || grammar.pronoun || getLocalizedFallbackForVariable('pronoun', lang) || 'it';
            }
            if (key === 'article') {
                const selectedIngredient = getSelectedIngredientForCreator();
                const grammar = resolveGrammarForIngredient(selectedIngredient?.name || '', selectedIngredient?.genusByLang || {}, lang);
                return creatorState.advancedArticleOverride || grammar.article || getLocalizedFallbackForVariable('article', lang) || '';
            }
            if (placeholderType === 'duration' || key.includes('duration')) {
                const unit = getDurationUnitLabel(creatorState.durationUnit, lang);
                return `${creatorState.durationValue || 10} ${unit}`;
            }
            if (placeholderType === 'count') return getCountInsertText();
            if (placeholderType === 'temperature') return getTemperatureInsertText();
            if (placeholderType === 'heat' || key.includes('heat')) return creatorState.heatValue || getLocalizedFallbackForVariable('heat', lang) || 'medium';
            if (placeholderType === 'mode') return creatorState.modeValue || (modeOptionsByLang[lang] || modeOptionsByLang.de || [])[0] || '';
            if (placeholderType === 'tool' || key.includes('tool')) return creatorState.toolValue || getToolOptions()[0] || getLocalizedFallbackForVariable('tool', lang) || 'knife';
            if (placeholderType === 'grindSize' || key.includes('grind')) return creatorState.grindSizeValue || getGrindSizeOptions()[0] || getLocalizedFallbackForVariable('grind_size', lang) || '';
            if (placeholderType === 'item') return creatorState.itemValue || getItemOptions()[0] || getLocalizedFallbackForVariable('item', lang) || '';
            if (placeholderType === 'balance') return creatorState.balanceValue || getBalanceOptions()[0] || getLocalizedFallbackForVariable('balance', lang) || '';
            if (placeholderType === 'seasonings') return creatorState.seasoningsValue || getSeasoningsOptions()[2] || getSeasoningsOptions()[0] || getLocalizedFallbackForVariable('seasonings', lang) || '';
            if (placeholderType === 'shape' || key.includes('shape')) return getLocalizedFallbackForVariable('shape', lang) || 'pieces';
            if (placeholderType === 'base') return creatorState.baseValue || getLocalizedFallbackForVariable('base', lang) || '';
            const localizedFallback = getLocalizedFallbackForVariable(key, lang);
            return localizedFallback || key;
        }

        function resolveDefaultVariableValue(key) {
            return resolveDefaultVariableValueForLanguage(key, currentLang);
        }

        function prefillTemplatePlaceholdersInText(text, langKey) {
            const source = (text || '').toString();
            if (!source.includes('{{')) return source;
            return source.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const value = resolveDefaultVariableValueForLanguage(String(key || '').trim(), langKey);
                return (value || String(key || '')).toString();
            }).replace(/\s{2,}/g, ' ').trim();
        }

        function prefillUneditedStepPlaceholders() {
            $('#selectedSteps .step-row').each(function () {
                const row = $(this);
                if (row.data('step-edited') === true || row.attr('data-step-edited') === 'true') {
                    return;
                }

                const mappings = [
                    { selector: '.step-hidden-de', lang: 'de' },
                    { selector: '.step-hidden-en', lang: 'en' },
                    { selector: '.step-hidden-esp', lang: 'esp' },
                    { selector: '.step-hidden-prt', lang: 'prt' }
                ];

                mappings.forEach(function (entry) {
                    const input = row.find(entry.selector);
                    if (!input.length) return;
                    input.val(prefillTemplatePlaceholdersInText(input.val(), entry.lang));
                });

                const currentInput = row.find(`.step-hidden-${currentLang}`);
                const fallbackInput = row.find('.step-hidden-de');
                const visibleText = (currentInput.val() || fallbackInput.val() || '').toString();
                row.find('.step-text-content').text(visibleText);
            });
        }

        function buildVariablesForTemplate(templateId) {
            const template = MasterStepRenderer.findTemplate(templateId);
            const selectedIngredient = getSelectedIngredientForCreator();
            const selectedNames = getSelectedIngredientNames();
            const ingredientName = selectedIngredient?.name || (selectedNames.length ? selectedNames.join(', ') : '');
            const grammar = resolveGrammarForIngredient(ingredientName, selectedIngredient?.genusByLang || {}, currentLang);
            const ingredientNameWithArticle = applyArticleToName(ingredientName, grammar.article, currentLang);

            creatorState.computedPronoun = creatorState.advancedPronounOverride || grammar.pronoun;
            creatorState.computedArticle = creatorState.advancedArticleOverride || grammar.article;

            const defaults = MasterStepRenderer.getSmartDefaults(templateId, {
                ingredientName: ingredientNameWithArticle || ingredientName,
                recipeCategory: $('#Recipe_Category').val()
            }) || {};

            const keys = new Set([...(template?.variables || []), ...getPlaceholderKeysFromTemplate(template)]);
            const vars = { ...defaults };

            keys.forEach(key => {
                if (key === 'ingredient' || key === 'ingredients' || key === 'liquid') {
                    vars[key] = key;
                    return;
                }

                if (vars[key] == null || String(vars[key]).trim() === '') {
                    vars[key] = resolveDefaultVariableValue(key);
                }
            });

            if (!vars.ingredient) vars.ingredient = 'ingredient';
            if (!vars.pronoun) vars.pronoun = creatorState.computedPronoun;
            if (!vars.article) vars.article = creatorState.computedArticle;
            return applyTokenAssignmentsToVariables(vars);
        }

        function animatePreview() {
            const card = $('#masterPreviewCard');
            card.addClass('preview-animate');
            setTimeout(() => card.removeClass('preview-animate'), 250);
        }

        function updatePreviewText() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) {
                creatorState.previewText = 'Wähle eine Zutat und ein Template.';
                $('#masterPreviewText').text(creatorState.previewText);
                return;
            }

            const template = MasterStepRenderer.findTemplate(templateId);
            const langKey = (currentLang || 'de').toLowerCase();
            const tpl = template?.templates?.[langKey] || template?.templates?.de || '';
            const vars = buildVariablesForTemplate(templateId);
            let placeholderOccurrence = 0;

            const previewHtml = (tpl || '').replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const k = String(key || '').trim();
                const tokenId = `${k}__${placeholderOccurrence++}`;
                const fallback = vars[k] != null ? String(vars[k]).trim() : k;
                const assigned = creatorState.placeholderAssignments[tokenId];
                const value = assigned || fallback || k;
                const safeValue = $('<div>').text(value).html();
                const safeFallback = $('<div>').text(fallback || k).html();
                const activeClass = creatorState.activePlaceholderTokenId === tokenId ? ' token-active' : '';
                return `<span class="placeholder-wrap" data-placeholder-token-id="${tokenId}">
                    <span class="token-highlight placeholder-token${activeClass}" draggable="false" data-placeholder-key="${k}" data-placeholder-token-id="${tokenId}">${safeValue}</span>
                    <button type="button" class="placeholder-reset" data-placeholder-token-id="${tokenId}" data-default-value="${safeFallback}" title="Zurücksetzen">↺</button>
                </span>`;
            });

            $('#masterPreviewText').html(previewHtml || 'Keine Vorschau verfügbar.');
            creatorState.previewText = $('#masterPreviewText').text().trim() || 'Keine Vorschau verfügbar.';

            if (!creatorState.ingredientReplaceArmed) { $('#masterPreviewCard').removeClass('token-replace-active'); }
            animatePreview();
            updateSc2PreviewText();
        }

        function clearSelectedIngredientChips() {
            creatorState.selectedIngredientIds = [];
            renderIngredientChips();
        }

        function updateStoryProgress() {
            const count = $('#selectedSteps .step-row').length;
            $('#storyStepCount').text(`Schritte: ${count}`);
            $('#sc2StoryStepCount').text(`Schritte: ${count}`);
        }

        function showCreatorToast(message) {
            creatorState.toastMessage = message;
            creatorState.toastVisible = true;
            const toast = $('#creatorToast');
            toast.text(message).addClass('show');
            setTimeout(() => {
                creatorState.toastVisible = false;
                toast.removeClass('show');
            }, 1200);
        }

        function animateTemplateToPreview(templateTitle) {
            const ghost = $('#templateFlyGhost');
            ghost.text(templateTitle || 'Template').removeClass('fly');
            void ghost[0].offsetWidth;
            ghost.addClass('fly');
        }

        function renderIngredientChips() {
            const wrap = $("#currentStepIngredientButtons");
            const stepsWrap = $('#stepsIngredientButtons');
            const ingredients = getSelectedIngredientsForSandbox();
            wrap.empty();
            stepsWrap.empty();

            if (!ingredients.length) {
                wrap.append('<div class="small text-white-50">Keine Zutaten vorhanden.</div>');
                stepsWrap.append('<div class="small text-white-50">Keine Zutaten vorhanden.</div>');
                creatorState.selectedIngredientIds = [];
                updatePreviewText();
                return;
            }

            creatorState.selectedIngredientIds = (creatorState.selectedIngredientIds || []).filter(id => ingredients.some(x => x.id === id));
            if (!creatorState.selectedIngredientIds.length) {
                creatorState.selectedIngredientIds = ingredients.map(x => x.id).filter(Boolean);
            }

            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const emoji = getIngredientEmoji(item.name);
                const displayName = (item.name || '').toString();
                const chipHtml = `<button type="button" class="ingredient-chip ${active}" draggable="true" data-id="${item.id}" data-name="${displayName}">${emoji} ${displayName}</button>`;
                wrap.append(chipHtml);
                stepsWrap.append(chipHtml);
            });

            updatePreviewText();
            renderSc2IngredientChips();
        }

        function renderTemplateCards() {
            const box = $('#masterTemplateCards');
            box.empty();

            if (!window.MasterStepRenderer || typeof MasterStepRenderer.getAllTemplates !== 'function') {
                box.append('<div class="small text-white-50">Templates werden geladen ...</div>');
                return;
            }

            const loadError = typeof MasterStepRenderer.getLastLoadError === 'function' ? MasterStepRenderer.getLastLoadError() : '';
            if (loadError) {
                setMasterTemplateError(`Template-Fehler: ${loadError}`);
                return;
            }

            const templates = MasterStepRenderer.getAllTemplates();
            if (!templates.length) {
                setMasterTemplateError('Keine Templates gefunden. Prüfe /data/master_steps.json.');
                creatorState.selectedTemplateId = '';
                updatePreviewText();
                return;
            }

            if (!creatorState.selectedTemplateId || !templates.some(x => x.master_id === creatorState.selectedTemplateId)) {
                creatorState.selectedTemplateId = templates[0].master_id;
            }

            templates.forEach((step, index) => {
                const active = step.master_id === creatorState.selectedTemplateId ? 'active' : '';
                const icon = step.categoryIcon || (index % 3 === 0 ? '✨' : index % 3 === 1 ? '🔥' : '🔪');
                const vars = buildVariablesForTemplate(step.master_id);
                const snippet = MasterStepRenderer.render(step.master_id, vars, currentLang) || step.master_id;
                const title = (step.description || '').toString().trim() || `Template ${index + 1}`;

                box.append(`<button type="button" class="template-card ${active}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
            });

            updatePreviewText();
            renderSc2TemplateCards();
        }

        function showMasterStepCreatorError(error, context) {
            const message = error && error.message ? error.message : (error || 'Unbekannter Fehler');
            const details = error && error.stack ? `\nStack: ${error.stack.substring(0, 500)}` : '';
            console.error(`Master-Step-Creator Fehler (${context})`, error);
            alert(`Master-Step-Creator Fehler (${context}): ${message}${details}`);
        }

        function setMasterTemplateError(message) {
            const box = $('#masterTemplateCards');
            if (!box.length) return;
            box.html(`<div class="small text-warning">${message}</div>`);
        }

        function refreshMasterTemplateBuilder() {
            try {
                const creator = $('.smart-step-creator');
                creator.attr('data-theme', isDarkTheme ? 'dark' : 'light');
                renderIngredientChips();
                renderTemplateCards();
                updateStoryProgress();
            } catch (error) {
                showMasterStepCreatorError(error, 'refreshMasterTemplateBuilder');
            }
        }

        function collectMasterVariables() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) return {};
            return buildVariablesForTemplate(templateId);
        }

        function normalizeVariablesForPersist(template, vars) {
            const normalized = { ...(vars || {}) };
            const keys = new Set([...(template?.variables || []), ...getPlaceholderKeysFromTemplate(template)]);
            const ingredientValue = (getSelectedIngredientValueForInsert() || '').toString().trim();

            keys.forEach(function (key) {
                const current = (normalized[key] || '').toString().trim();

                if (getPlaceholderType(key) === 'ingredient') {
                    normalized[key] = ingredientValue || current;
                    return;
                }

                if (!current || current.toLowerCase() === key.toLowerCase()) {
                    normalized[key] = '';
                }
            });

            return normalized;
        }

        function renderTemplateForPersist(template, lang, vars) {
            const langKey = (lang || 'de').toLowerCase();
            let tpl = template?.templates?.[langKey] || template?.templates?.de || template?.templates?.en || '';
            if (!tpl) return '';

            Object.keys(vars || {}).forEach(function (key) {
                const val = (vars[key] || '').toString().trim();
                if (val) return;
                const esc = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                tpl = tpl.replace(new RegExp(`\\([^)]*\\{\\{\\s*${esc}\\s*\\}\\}[^)]*\\)`, 'gi'), ' ');
                tpl = tpl.replace(new RegExp(`\\[[^\\]]*\\{\\{\\s*${esc}\\s*\\}\\}[^\\]]*\\]`, 'gi'), ' ');
            });

            let rendered = tpl.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const value = vars && vars[key] != null ? String(vars[key]).trim() : '';
                return value;
            });

            rendered = rendered
                .replace(/\(\s*\)/g, ' ')
                .replace(/\[\s*\]/g, ' ')
                .replace(/\s+([,.;:!?])/g, '$1')
                .replace(/\s{2,}/g, ' ')
                .trim();

            return rendered;
        }

        function buildRenderedPayloadFromTemplate(template, vars) {
            return supportedLanguages.reduce((payload, lang) => {
                payload[lang] = renderTemplateForPersist(template, lang, vars);
                return payload;
            }, {});
        }

        function getCurrentUserHashForRequests() {
            const fromCookie = getCookieValue('WorldMiniAppUserHash');
            if (fromCookie) return fromCookie;

            const fromHidden = ($('#hiddenUserTokenField').val() || '').toString().trim();
            if (fromHidden) return fromHidden;

            const fromQuery = new URLSearchParams(window.location.search).get('userHash');
            return (fromQuery || '').toString().trim();
        }


        function createFallbackStepId() {
            tempStepIdCounter -= 1;
            return tempStepIdCounter.toString();
        }

        async function addRenderedMasterStep() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) return;

            const template = MasterStepRenderer.findTemplate(templateId);
            if (!template) return;

            const rawVars = collectMasterVariables();
            const vars = normalizeVariablesForPersist(template, rawVars);
            const rendered = buildRenderedPayloadFromTemplate(template, vars);
            const ingredientName = (vars.ingredient || vars.ingredients || vars.liquid || vars.fat || '').toString().trim();
            const payload = {
                de: rendered.de,
                en: rendered.en,
                esp: rendered.esp,
                prt: rendered.prt,
                phase: parseInt(template.phase || 0, 10),
                equipment: parseInt(template.equipment || 0, 10)
            };

            try {
                const userHash = getCurrentUserHashForRequests();
                const url = userHash ? `${upsertStepUrl}?userHash=${encodeURIComponent(userHash)}` : upsertStepUrl;
                const response = await fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) {
                    const errorText = await response.text();
                    throw new Error(errorText || 'Upsert fehlgeschlagen');
                }

                const result = await response.json();
                const newId = result?.id;
                if (!newId) {
                    throw new Error('Keine Step-ID erhalten');
                }

                const alreadyExists = getAllStepRows().some(x => x && x.id?.toString() === newId.toString());
                if (!alreadyExists) {
                    getAllStepRows().push({
                        id: newId,
                        de: rendered.de,
                        en: rendered.en,
                        esp: rendered.esp,
                        prt: rendered.prt,
                        phase: payload.phase,
                        equipment: payload.equipment
                    });
                }

                addStep(newId.toString(), null, rendered[currentLang] || rendered.de || `Schritt ${newId}`, { skipRender: true, ingredientName });
                updateStepIndices();
            } catch (error) {
                console.warn('Master-Step Upsert nicht möglich, nutze lokalen Fallback-Step', error);

                const fallbackId = createFallbackStepId();
                getAllStepRows().push({
                    id: fallbackId,
                    de: rendered.de,
                    en: rendered.en,
                    esp: rendered.esp,
                    prt: rendered.prt,
                    phase: payload.phase,
                    equipment: payload.equipment
                });

                addStep(fallbackId, null, rendered[currentLang] || rendered.de || 'Neuer Schritt', {
                    skipRender: true,
                    ingredientName,
                    stepData: {
                        de: rendered.de,
                        en: rendered.en,
                        esp: rendered.esp,
                        prt: rendered.prt,
                        phase: payload.phase,
                        equipment: payload.equipment
                    }
                });
                updateStepIndices();
            }
        }

        function syncIngredientSourceVisibility() {
            const query = ($('#ingredientSearch').val() || '').toString().trim().toLowerCase();
            const selectedIds = $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                return $(this).val()?.toString();
            }).get();

            $('.ingredient-db-row').each(function () {
                const row = $(this);
                const id = row.data('ingredient-id')?.toString() || '';
                const text = ((row.data('name-' + currentLang) || row.data('name-de') || '') + '').toLowerCase();
                const isSelected = !!id && selectedIds.includes(id);
                const matchesSearch = !query || text.includes(query);
                row.toggle(!isSelected && matchesSearch);
            });
        }

        // Prüft ob ein Step bereits in der Auswahl ist
        function isStepAlreadySelected(stepId) {
            return $('#selectedSteps .step-row').filter(function () {
                return $(this).data('step-id')?.toString() === stepId.toString();
            }).length > 0;
        }












        function getCommonUnitChipLabel(chip, langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            return chip.data('label-' + lang) || chip.data('label-de') || chip.text();
        }

        function resolveSelectUnitValueByKey(selectEl, unitKey) {
            const options = Array.from(selectEl.options || []);
            if (!options.length) return '';

            const normalize = (v) => (v || '').toString().toLowerCase().replace(/\s+/g, '').replace(/\./g, '');
            const key = (unitKey || '').toString().toLowerCase();
            const aliases = {
                g: ['g', 'gr', 'gramm', 'gram'],
                ml: ['ml', 'milliliter', 'millilitre'],
                piece: ['stk', 'stk.', 'stueck', 'stück', 'piece', 'pieces', 'pcs', 'pcs.', 'unit', 'units', 'uds', 'un']
            };
            const accepted = aliases[key] || [key];
            const match = options.find(opt => accepted.includes(normalize(opt.value)) || accepted.includes(normalize(opt.text)));
            return match ? match.value : '';
        }

        function updateCommonUnitChipLabels() {
            $('.js-common-unit-chip').each(function () {
                const chip = $(this);
                chip.text(getCommonUnitChipLabel(chip, currentLang));
            });
        }

        const probabilityFeature = window.CreatePostingProbability?.create({
            getCurrentLang: () => currentLang,
            getSelectedIngredientIds: () => $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                const value = parseInt($(this).val(), 10);
                return Number.isNaN(value) ? null : value;
            }).get().filter(v => v !== null),
            getSelectedIngredientNames: () => getSelectedIngredientsForSandbox().map(x => x.name).filter(Boolean),
            findTemplate: (masterId) => MasterStepRenderer.findTemplate(masterId),
            renderTemplate: (masterId, vars, lang) => MasterStepRenderer.render(masterId, vars, lang),
            buildVariablesForTemplate,
            detectRecipeTypes: (ingredientIds) => window.RecipeStepSuggest.detectRecipeTypes(ingredientIds) || [],
            loadMapping: () => window.RecipeStepSuggest.loadMapping(),
            getMasterStepPreview: (ingredients, options) => window.RecipeStepSuggest.getMasterStepPreview(ingredients, options) || [],
            /**
             * Returns typical ingredients for a category that aren't yet selected,
             * augmented with their localized name from the catalog.
             */
            getTypicalIngredientSuggestions: (typeId) => {
                if (!window.RecipeStepSuggest || !window.RecipeStepSuggest.getCategoryById) return [];
                const cat = window.RecipeStepSuggest.getCategoryById(typeId);
                if (!cat || !Array.isArray(cat.typical_ingredients)) return [];

                const alreadySelected = new Set(
                    $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]')
                        .map(function () { return parseInt($(this).val(), 10); })
                        .get().filter(v => !isNaN(v))
                );

                const suggestions = [];
                cat.typical_ingredients.forEach(ingId => {
                    if (alreadySelected.has(ingId)) return;
                    const catalogRow = $(`.ingredient-db-row[data-ingredient-id="${ingId}"]`).first();
                    if (!catalogRow.length) return; // not in this creator's catalog
                    const name = catalogRow.data('name-' + (currentLang || 'de')) || catalogRow.data('name-de') || String(ingId);
                    suggestions.push({ id: ingId, name: name });
                });
                return suggestions;
            },
            openProbVarEditor: (masterId, varKey, currentVal, onApply, anchorElement) => openProbVarEditor(masterId, varKey, currentVal, onApply, anchorElement)
        });

        async function refreshIngredientProbabilityHints() {
            if (!probabilityFeature) return;
            await probabilityFeature.refresh();
        }

        async function showProbabilityTemplateSuggestions(typeId, typeName, score) {
            if (!probabilityFeature) return;
            await probabilityFeature.showSuggestions(typeId, typeName, score);
        }

        function updateLanguageLabels() {
            const langKey = resolveLangKey(currentLang);
            $('.label-lang').each(function () { $(this).text($(this).data(langKey)); });
            $('.lang-ph').each(function () { $(this).attr('placeholder', $(this).data('ph-' + langKey)); });
            $('.lang-opt').each(function () { $(this).text($(this).data('txt-' + langKey)); });
            $('.ingredient-db-row').each(function () { $(this).find('.display-name').text($(this).data('name-' + langKey)); });
            $('#selectedIngredients .ingredient-row').each(function () {
                const row = $(this);
                const n = row.data('name-' + langKey) || row.data('name-de') || row.find('.display-name-selected').text();
                row.find('.display-name-selected').text(n);
                const qty = (row.find('.ingredient-qty-hidden').val() || '').toString();
                const unitDe = (row.find('.ingredient-unit-hidden').val() || '').toString();
                const unitObj = findUnitByDe(unitDe);
                const unitLabel = getUnitLabel(unitObj, langKey) || unitDe;
                row.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
            });
            syncIngredientSourceVisibility();
            $('.keyword-btn').each(function () { $(this).text($(this).data('word-' + langKey)); });
            $('.keyword-pill').each(function () { $(this).find('.keyword-text').text($(this).data('word-' + langKey)); });
            refreshDurationUnitControls();
            updateCommonUnitChipLabels();
            refreshMasterTemplateBuilder();
            refreshIngredientProbabilityHints();
        }

        function handleVideoUpload(input) {
            if (input.files && input.files[0]) {
                const file = input.files[0];
                const fileUrl = URL.createObjectURL(file);
                const isVideo = file.type.startsWith('video/');

                if (isVideo) {
                    $('#videoPreview').attr('src', fileUrl).removeClass('d-none');
                    $('#imagePreview').addClass('d-none').attr('src', '');
                } else {
                    $('#imagePreview').attr('src', fileUrl).removeClass('d-none');
                    $('#videoPreview').addClass('d-none').attr('src', '');
                }

                $('#videoPreviewContainer').removeClass('d-none');
                $('#uploadLabel').addClass('d-none');
            }
        }

        function resetVideo() {
            $('#videoInput').val('');
            $('#videoPreviewContainer').addClass('d-none');
            $('#uploadLabel').removeClass('d-none');
            $('#videoPreview').attr('src', '');
            $('#imagePreview').attr('src', '').addClass('d-none');
        }

        function normalizeDecimalInputValue(value) {
            if (!value) return value;
            return value.toString().trim().replace(/\s/g, '').replace(',', '.');
        }

        function sanitizeQuantityInputValue(value) {
            const raw = (value || '').toString();
            let out = '';
            let hasSeparator = false;
            for (const ch of raw) {
                if (ch >= '0' && ch <= '9') {
                    out += ch;
                    continue;
                }
                if (!hasSeparator && (ch === '.' || ch === ',')) {
                    out += ch;
                    hasSeparator = true;
                }
            }
            return out;
        }


        function escapeAttr(value) {
            return $('<div>').text((value ?? '').toString()).html();
        }

        function getLocalizedIngredientData(row, fallbackName) {
            const names = {};
            const genus = {};
            supportedLanguages.forEach(lang => {
                names[lang] = (row.data('name-' + lang) || fallbackName || '').toString();
                genus[lang] = (row.data('genus-' + lang) || '').toString().trim();
            });
            return { names, genus };
        }

        function buildIngredientDataAttributes(localizedData) {
            return supportedLanguages.map(lang =>
                `data-name-${lang}="${escapeAttr(localizedData.names[lang])}" data-genus-${lang}="${escapeAttr(localizedData.genus[lang])}"`
            ).join(' ');
        }

        function buildIngredientRowHtml({ id, localizedData, selectedQuantity, selectedUnit, selectedUnitLabel, displayName }) {
            return `
            <div class="dynamic-item ingredient-row shadow-sm" onclick="openIngredientConfigPopup(this)" title="Zum Bearbeiten antippen" ${buildIngredientDataAttributes(localizedData)}>
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].IngredientsAndNutrients.Id" value="${id}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Quantity.Quantitys" class="ingredient-qty-hidden" value="${selectedQuantity}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Measure.Metriks_DE" class="ingredient-unit-hidden" value="${escapeAttr(selectedUnit)}" />

                <div class="ingredient-row-main">
                    <div class="fw-bold display-name-selected">${escapeAttr(displayName)}</div>
                </div>

                <div class="ingredient-row-right">
                    <div class="ingredient-row-meta">${escapeAttr(selectedQuantity)} ${escapeAttr(selectedUnitLabel)}</div>
                    <div class="ingredient-row-actions">
                        <button type="button" class="btn btn-sm text-danger p-0" onclick="event.stopPropagation(); removeIngredientRow(this)" title="Zutat entfernen" aria-label="Zutat entfernen">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </div>
                </div>
            </div>`;
        }

        function addIngredient(id, rowElement) {
            const existing = $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').filter(function () {
                return $(this).val()?.toString() === id.toString();
            }).length > 0;

            if (existing) return;

            const row = $(rowElement).closest('.ingredient-db-row');
            const displayName = row.data('name-' + currentLang) || row.data('name-de');
            const localizedData = getLocalizedIngredientData(row, displayName);
            const selectedQuantity = normalizeDecimalInputValue(row.attr('data-selected-qty')) || '0';
            const fallbackUnit = (window.IngredientManager && typeof window.IngredientManager.getDefaultUnitDe === 'function')
                ? window.IngredientManager.getDefaultUnitDe()
                : 'g.';
            const selectedUnit = (row.attr('data-selected-unit') || fallbackUnit).toString();
            const selectedUnitObj = findUnitByDe(selectedUnit);
            const selectedUnitLabel = getUnitLabel(selectedUnitObj, currentLang) || selectedUnit;

            const newIngredient = buildIngredientRowHtml({
                id,
                localizedData,
                selectedQuantity,
                selectedUnit,
                selectedUnitLabel,
                displayName
            });

            $('#selectedIngredients').append(newIngredient);

            refreshMasterTemplateBuilder();
            if (typeof syncIngredientSourceVisibility === "function") {
                syncIngredientSourceVisibility();
            }
            refreshIngredientProbabilityHints();
        }

        function startStepIngredientEdit(btn) {
            const row = $(btn).closest('.step-row');
            creatorState.activeStepIngredientRow = row;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').removeClass('d-none');
            $('#card-steps')[0]?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            showCreatorToast('Chip auswählen – dann Einsetzen tippen');
        }

        function applyChipToStep() {
            const row = creatorState.activeStepIngredientRow;
            if (!row) { cancelStepIngredientEdit(); return; }

            const selectedNames = getSelectedIngredientNames();
            if (!selectedNames.length) { showCreatorToast('Bitte zuerst eine Zutat auswählen'); return; }

            const newName = selectedNames.join(', ');
            const oldName = (row.data('ingredient-name') || '').toString().trim();

            if (oldName) {
                const oldRx = new RegExp(oldName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
                ['step-hidden-de', 'step-hidden-en', 'step-hidden-esp', 'step-hidden-prt'].forEach(cls => {
                    const input = row.find('.' + cls);
                    if (input.length) input.val((input.val() || '').replace(oldRx, newName));
                });
                const textSpan = row.find('.step-text-content');
                textSpan.text((textSpan.text() || '').replace(oldRx, newName));
            }

            row.attr('data-ingredient-name', newName);
            row.attr('data-step-edited', 'true').data('step-edited', true);
            creatorState.activeStepIngredientRow = null;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').addClass('d-none');
            showCreatorToast('Zutat im Schritt ersetzt');
        }

        function cancelStepIngredientEdit() {
            creatorState.activeStepIngredientRow = null;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').addClass('d-none');
        }

        function removeIngredientRow(btn) {
            $(btn).closest('.ingredient-row').remove();
            refreshMasterTemplateBuilder();
            syncIngredientSourceVisibility();
            refreshIngredientProbabilityHints();
        }

        // Entfernt alle Steps die für dieselben Zutaten+Phase gebunden sind wie der neue Step
        function removeConflictingSteps(newStepId) {
            const newStep = getAllStepRows().find(x => x && x.id?.toString() === newStepId.toString());
            if (!newStep) return;

            const newStepPhase = parseInt(newStep.phase ?? 0, 10);
            if (newStepPhase < 1) return;

            const newStepBoundIds = getStepBoundIngredientIds(newStepId).map(x => x.toString());
            if (!newStepBoundIds.length) return;

            $('#selectedSteps .step-row').each(function () {
                const row = $(this);
                const rowStepId = row.data('step-id')?.toString();
                if (!rowStepId || rowStepId === newStepId.toString()) return;

                const rowStep = getAllStepRows().find(x => x && x.id?.toString() === rowStepId);
                if (!rowStep) return;

                const rowPhase = parseInt(rowStep.phase ?? 0, 10);
                if (rowPhase !== newStepPhase) return;

                // Prüfe ob der bestehende Step mindestens eine gemeinsame Zutat hat
                const rowBoundIds = getStepBoundIngredientIds(rowStepId).map(x => x.toString());
                const hasOverlap = rowBoundIds.some(id => newStepBoundIds.includes(id));
                if (hasOverlap) {
                    row.remove();
                }
            });
        }

        function addStep(id, btn, manualText = null, options = {}) {
            if (isStepAlreadySelected(id)) return;

            const enforceUniqueness = options?.enforceIngredientPhaseUniqueness === true;
            if (enforceUniqueness) {
                removeConflictingSteps(id);
            }

            const sourceRow = getAllStepRows().find(x => x && x.id?.toString() === id.toString());
            const stepData = options?.stepData || sourceRow || {};

            const normalizedStepData = {
                de: (stepData.de || '').toString(),
                en: (stepData.en || '').toString(),
                esp: (stepData.esp || '').toString(),
                prt: (stepData.prt || '').toString(),
                phase: parseInt(stepData.phase ?? 0, 10) || 0,
                equipment: parseInt(stepData.equipment ?? 0, 10) || 0
            };

            let text = manualText;
            if (!text) {
                text = normalizedStepData[currentLang] || normalizedStepData.de || `Schritt ${id}`;
            }

            if (!normalizedStepData[currentLang]) {
                normalizedStepData[currentLang] = text;
            }

            if (!normalizedStepData.de) normalizedStepData.de = normalizedStepData.en || text;
            if (!normalizedStepData.en) normalizedStepData.en = normalizedStepData.de || text;

            const stepIdAsInt = parseInt(id, 10);
            const postedStepId = Number.isNaN(stepIdAsInt) || stepIdAsInt < 1 ? 0 : stepIdAsInt;
            const phaseBadge = getPhaseLabel(normalizedStepData.phase);
            const stepIngredientName = (options?.ingredientName || '').toString().trim();
            const chipBtnHtml = stepIngredientName
                ? `<button type="button" class="btn btn-sm btn-outline-light opacity-75" onclick="startStepIngredientEdit(this)" title="Zutat per Chip ändern">🥣</button>`
                : '';

            $('#selectedSteps').append(`<div class="dynamic-item d-flex align-items-center step-row" draggable="true" data-step-id="${id}" data-step-edited="false" data-ingredient-name="${$('<div>').text(stepIngredientName).html()}">
                <input type="hidden" name="RecipePreperationSteps[INDEX].PreperationStepId" value="${postedStepId}" />
                <input type="hidden" class="step-index-input" name="RecipePreperationSteps[INDEX].StepIndex" value="0" />
                <input type="hidden" class="step-hidden-de" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_DE" value="${$('<div>').text(normalizedStepData.de).html()}" />
                <input type="hidden" class="step-hidden-en" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_EN" value="${$('<div>').text(normalizedStepData.en).html()}" />
                <input type="hidden" class="step-hidden-esp" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_ESP" value="${$('<div>').text(normalizedStepData.esp).html()}" />
                <input type="hidden" class="step-hidden-prt" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_PRT" value="${$('<div>').text(normalizedStepData.prt).html()}" />
                <input type="hidden" class="step-hidden-phase" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Phase" value="${normalizedStepData.phase}" />
                <input type="hidden" class="step-hidden-equipment" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Equipment" value="${normalizedStepData.equipment}" />
                <div class="badge candy-purple rounded-pill me-3 step-badge">0</div>
                <div class="small flex-grow-1 display-step-selected">
                    ${phaseBadge}
                    <span class="step-text-content">${text}</span>
                    <div class="step-row-actions">
                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="moveStepRow(this, -1)"><i class="bi bi-arrow-up"></i></button>
                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="moveStepRow(this, 1)"><i class="bi bi-arrow-down"></i></button>
                        ${chipBtnHtml}
                        <button type="button" class="btn btn-sm text-danger opacity-50" onclick="removeStep(this)"><i class="bi bi-trash3"></i></button>
                    </div>
                </div>
            </div>`);

            updateStepIndices();

        }

        function removeStep(btn) {
            $(btn).closest('.step-row').remove();
            updateStepIndices();
        }

        function toggleKeyword(id, btn) {
            const container = $('#selectedKeywords');
            const existing = container.find(`.keyword-pill[data-keyword-id="${id}"]`);
            if (existing.length > 0) {
                existing.remove();
                $(btn).removeClass('btn-secondary').addClass('btn-outline-secondary');
                return;
            }

            const word = $(btn).data('word-' + currentLang) || $(btn).data('word-de');
            container.append(`<div class="keyword-pill badge rounded-pill bg-secondary text-white d-inline-flex align-items-center me-2 mb-2"
                data-keyword-id="${id}"
                data-word-de="${$(btn).data('word-de')}"
                data-word-en="${$(btn).data('word-en')}"
                data-word-esp="${$(btn).data('word-esp')}"
                data-word-prt="${$(btn).data('word-prt')}">
                <input type="hidden" name="SelectedKeywordIds" value="${id}" />
                <span class="me-2 keyword-text">${word}</span>
                <button type="button" class="btn btn-sm btn-light rounded-circle p-0" style="width:20px;height:20px;line-height:1;" onclick="removeKeyword('${id}')"><i class="bi bi-x"></i></button>
            </div>`);
            $(btn).removeClass('btn-outline-secondary').addClass('btn-secondary');
        }

        function removeKeyword(id) {
            $('#selectedKeywords').find(`.keyword-pill[data-keyword-id="${id}"]`).remove();
            $(`.keyword-btn[data-keyword-id="${id}"]`).removeClass('btn-secondary').addClass('btn-outline-secondary');
        }

        function updateStepIndices() {
            $('#selectedSteps .step-row').each(function (i) {
                $(this).find('.step-badge').text(i + 1);
                $(this).find('.step-index-input').val(i + 1);
            });
            updateStoryProgress();
        }

        function prepareBinding() {
            prefillUneditedStepPlaceholders();
            updateStepIndices();
            $('.ingredient-row').each(function (i) {
                $(this).find('input, select').each(function () {
                    if (this.name) this.name = this.name.replace(/\[.*?\]/, '[' + i + ']');
                });
            });
            $('.step-row').each(function (i) {
                $(this).find('input, select').each(function () {
                    if (this.name) this.name = this.name.replace(/\[.*?\]/, '[' + i + ']');
                });
            });
            $('input[name$=".Quantity.Quantitys"], .js-decimal-input').each(function () {
                this.value = normalizeDecimalInputValue(this.value);
            });
            return true;
        }

        function moveStepRow(btn, direction) {
            const row = $(btn).closest('.step-row');
            if (direction < 0) {
                const prev = row.prev('.step-row');
                if (prev.length) prev.before(row);
            } else {
                const next = row.next('.step-row');
                if (next.length) next.after(row);
            }
            updateStepIndices();
        }

        let draggedStepRow = null;


        // =========================================================
        // SC2 – Eingebetteter Smart Step Creator im Steps-Bereich
        // Shares creatorState with the main creator; separate DOM
        // =========================================================

        function updateSc2PreviewText() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) {
                $('#sc2MasterPreviewText').text('Wähle eine Zutat und ein Template.');
                return;
            }
            const template = MasterStepRenderer.findTemplate(templateId);
            const langKey = (currentLang || 'de').toLowerCase();
            const tpl = template?.templates?.[langKey] || template?.templates?.de || '';
            const vars = buildVariablesForTemplate(templateId);
            let placeholderOccurrence = 0;
            const previewHtml = (tpl || '').replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const k = String(key || '').trim();
                const tokenId = `${k}__${placeholderOccurrence++}`;
                const fallback = vars[k] != null ? String(vars[k]).trim() : k;
                const assigned = creatorState.placeholderAssignments[tokenId];
                const value = assigned || fallback || k;
                const safeValue = $('<div>').text(value).html();
                const safeFallback = $('<div>').text(fallback || k).html();
                const activeClass = creatorState.activePlaceholderTokenId === tokenId ? ' token-active' : '';
                return `<span class="placeholder-wrap" data-placeholder-token-id="${tokenId}">
                    <span class="token-highlight placeholder-token${activeClass}" draggable="false" data-placeholder-key="${k}" data-placeholder-token-id="${tokenId}">${safeValue}</span>
                    <button type="button" class="placeholder-reset" data-placeholder-token-id="${tokenId}" data-default-value="${safeFallback}" title="Zurücksetzen">↺</button>
                </span>`;
            });
            $('#sc2MasterPreviewText').html(previewHtml || 'Keine Vorschau verfügbar.');
            if (!creatorState.ingredientReplaceArmed) { $('#sc2MasterPreviewCard').removeClass('token-replace-active'); }
            const sc2Card = $('#sc2MasterPreviewCard');
            sc2Card.addClass('preview-animate');
            setTimeout(() => sc2Card.removeClass('preview-animate'), 250);
        }

        function renderSc2IngredientChips() {
            const wrap = $('#sc2MasterIngredientButtons');
            if (!wrap.length) return;
            const ingredients = getSelectedIngredientsForSandbox();
            wrap.empty();
            if (!ingredients.length) {
                wrap.append('<div class="small text-white-50">Wähle zuerst Zutaten aus.</div>');
                return;
            }
            creatorState.selectedIngredientIds = (creatorState.selectedIngredientIds || []).filter(id => ingredients.some(x => x.id === id));
            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const emoji = getIngredientEmoji(item.name);
                const displayName = (item.name || '').toString();
                wrap.append(`<button type="button" class="ingredient-chip ${active}" data-id="${item.id}" data-name="${displayName}">${emoji} ${displayName}</button>`);
            });
        }

        function renderSc2TemplateCards() {
            const box = $('#sc2MasterTemplateCards');
            if (!box.length) return;
            box.empty();
            if (!window.MasterStepRenderer || typeof MasterStepRenderer.getAllTemplates !== 'function') {
                box.append('<div class="small text-white-50">Templates werden geladen ...</div>');
                return;
            }
            const loadError = typeof MasterStepRenderer.getLastLoadError === 'function' ? MasterStepRenderer.getLastLoadError() : '';
            if (loadError) { box.html(`<div class="small text-warning">Template-Fehler: ${loadError}</div>`); return; }
            const templates = MasterStepRenderer.getAllTemplates();
            if (!templates.length) { box.html('<div class="small text-white-50">Keine Templates gefunden.</div>'); return; }
            templates.forEach((step, index) => {
                const active = step.master_id === creatorState.selectedTemplateId ? 'active' : '';
                const icon = step.categoryIcon || (index % 3 === 0 ? '✨' : index % 3 === 1 ? '🔥' : '🔪');
                const vars = buildVariablesForTemplate(step.master_id);
                const snippet = MasterStepRenderer.render(step.master_id, vars, currentLang) || step.master_id;
                const title = (step.description || '').toString().trim() || `Template ${index + 1}`;
                box.append(`<button type="button" class="template-card ${active}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
            });
        }

        function showSc2CreatorToast(message) {
            const toast = $('#sc2CreatorToast');
            toast.text(message).addClass('show');
            setTimeout(() => toast.removeClass('show'), 1200);
        }

        // SC2 close functions
        function closeSc2DurationEditor()    { $('#sc2DurationEditor').addClass('d-none'); }
        function closeSc2CountEditor()       { $('#sc2CountEditor').addClass('d-none'); }
        function closeSc2TemperatureEditor() { $('#sc2TemperatureEditor').addClass('d-none'); }
        function closeSc2PronounEditor()     { $('#sc2PronounEditor').addClass('d-none'); }
        function closeSc2HeatEditor()        { $('#sc2HeatEditor').addClass('d-none'); }
        function closeSc2ModeEditor()        { $('#sc2ModeEditor').addClass('d-none'); }
        function closeSc2ToolEditor()        { $('#sc2ToolEditor').addClass('d-none'); }
        function closeSc2GrindSizeEditor()   { $('#sc2GrindSizeEditor').addClass('d-none'); }
        function closeSc2ShapeEditor()       { $('#sc2ShapeEditor').addClass('d-none'); }
        function closeSc2BaseEditor()        { $('#sc2BaseEditor').addClass('d-none'); }
        function closeSc2ItemEditor()        { $('#sc2ItemEditor').addClass('d-none'); }
        function closeSc2BalanceEditor()     { $('#sc2BalanceEditor').addClass('d-none'); }
        function closeSc2SeasoningsEditor()  { $('#sc2SeasoningsEditor').addClass('d-none'); }
        function closeSc2EquipmentEditor()   { $('#sc2EquipmentEditor').addClass('d-none'); }
        function closeSc2StateEditor()       { $('#sc2StateEditor').addClass('d-none'); }

        function closeSc2AllEditors() {
            [closeSc2DurationEditor, closeSc2CountEditor, closeSc2TemperatureEditor,
             closeSc2PronounEditor, closeSc2HeatEditor, closeSc2ModeEditor,
             closeSc2ToolEditor, closeSc2GrindSizeEditor, closeSc2ShapeEditor,
             closeSc2BaseEditor, closeSc2ItemEditor, closeSc2BalanceEditor,
             closeSc2SeasoningsEditor, closeSc2EquipmentEditor, closeSc2StateEditor
            ].forEach(fn => fn());
        }

        // SC2 open functions
        function openSc2DurationEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeDurationTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s+/);
            if (parsed) creatorState.durationValue = parseInt(parsed[1], 10);
            $('#sc2DurationValueInput').val(creatorState.durationValue || 10);
            $('#sc2DurationUnitSelect option[value="minute"]').text(getDurationUnitLabel('minute', currentLang));
            $('#sc2DurationUnitSelect option[value="hour"]').text(getDurationUnitLabel('hour', currentLang));
            $('#sc2DurationUnitSelect').val(creatorState.durationUnit || 'minute');
            $('#sc2DurationEditor').removeClass('d-none');
        }
        function openSc2CountEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeCountTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)/);
            if (parsed) creatorState.countValue = parseInt(parsed[1], 10);
            $('#sc2CountValueInput').val(creatorState.countValue || 2);
            $('#sc2CountEditor').removeClass('d-none');
        }
        function openSc2TemperatureEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeTemperatureTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)/);
            if (parsed) {
                creatorState.temperatureValue = parseInt(parsed[1], 10);
                creatorState.temperatureUnit = currentText.includes('°F') ? 'fahrenheit' : 'celsius';
            }
            $('#sc2TemperatureValueInput').val(creatorState.temperatureValue || 180);
            $('#sc2TemperatureUnitSelect').val(creatorState.temperatureUnit || 'celsius');
            $('#sc2TemperatureEditor').removeClass('d-none');
        }
        function openSc2HeatEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeHeatTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineHeatOptions', 'inline-heat-opt', getHeatOptions(), creatorState.heatValue || '');
            $('#sc2HeatEditor').removeClass('d-none');
        }
        function openSc2ModeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeModeTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineModeOptions', 'inline-mode-opt', getModeOptions(), creatorState.modeValue || '');
            $('#sc2ModeEditor').removeClass('d-none');
        }
        function openSc2PronounEditorForToken(tokenId) {
            creatorState.activePronounTokenId = tokenId || '__global__';
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.computedPronoun || '').toString().trim();
            creatorState.pronounValue = currentText;
            renderInlineChoiceButtons('#sc2InlinePronounOptions', 'inline-pronoun-opt', getPronounSuggestionsForLang(currentLang), creatorState.pronounValue || creatorState.computedPronoun || '');
            $('#sc2PronounEditor').removeClass('d-none');
        }
        function openSc2EquipmentEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeEquipmentTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.equipmentValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.equipmentArticleValue = parts.article;
            creatorState.equipmentValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineEquipmentArticleOptions', creatorState.equipmentArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getEquipmentOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-equipment-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineEquipmentOptions').html(chips);
            $('#sc2EquipmentEditor').removeClass('d-none');
        }
        function openSc2StateEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeStateTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineStateOptions', 'inline-state-opt', getStateOptions(), creatorState.stateValue || '');
            $('#sc2StateEditor').removeClass('d-none');
        }
        function openSc2ToolEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeToolTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.toolValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.toolArticleValue = parts.article;
            creatorState.toolValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineToolArticleOptions', creatorState.toolArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getToolOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-tool-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineToolOptions').html(chips);
            $('#sc2ToolEditor').removeClass('d-none');
        }
        function openSc2GrindSizeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeGrindSizeTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineGrindSizeOptions', 'inline-grind-size-opt', getGrindSizeOptions(), creatorState.grindSizeValue || '');
            $('#sc2GrindSizeEditor').removeClass('d-none');
        }
        function openSc2ShapeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeShapeTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineShapeOptions', 'inline-shape-opt', getShapeOptions(), creatorState.shapeValue || '');
            $('#sc2ShapeEditor').removeClass('d-none');
        }
        function openSc2BaseEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBaseTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.baseValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.baseArticleValue = parts.article;
            creatorState.baseValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineBaseArticleOptions', creatorState.baseArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getBaseOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-base-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineBaseOptions').html(chips);
            $('#sc2BaseEditor').removeClass('d-none');
        }
        function openSc2ItemEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeItemTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.itemValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.itemArticleValue = parts.article;
            creatorState.itemValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineItemArticleOptions', creatorState.itemArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getItemOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-item-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineItemOptions').html(chips);
            $('#sc2ItemEditor').removeClass('d-none');
        }
        function openSc2BalanceEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBalanceTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.balanceValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.balanceArticleValue = parts.article;
            creatorState.balanceValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineBalanceArticleOptions', creatorState.balanceArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getBalanceOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-balance-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineBalanceOptions').html(chips);
            $('#sc2BalanceEditor').removeClass('d-none');
        }
        function openSc2SeasoningsEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeSeasoningsTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineSeasoningsOptions', 'inline-seasonings-opt', getSeasoningsOptions(), creatorState.seasoningsValue || '');
            $('#sc2SeasoningsEditor').removeClass('d-none');
        }

        const sc2EditorDispatch = {
            duration:    { open: openSc2DurationEditorForToken,    toast: 'Zeit setzen' },
            count:       { open: openSc2CountEditorForToken,       toast: 'Anzahl setzen' },
            pronoun:     { open: openSc2PronounEditorForToken,     toast: 'Pronomen wählen', keepTokenActive: true },
            temperature: { open: openSc2TemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat:        { open: openSc2HeatEditorForToken,        toast: 'Hitze-Stufe wählen' },
            mode:        { open: openSc2ModeEditorForToken,        toast: 'Ofenmodus wählen' },
            equipment:   { open: openSc2EquipmentEditorForToken,   toast: 'Tool / Gerät wählen' },
            state:       { open: openSc2StateEditorForToken,       toast: 'Zustand wählen' },
            tool:        { open: openSc2ToolEditorForToken,        toast: 'Tool / Gerät wählen' },
            grindSize:   { open: openSc2GrindSizeEditorForToken,   toast: 'Schnittgröße wählen' },
            shape:       { open: openSc2ShapeEditorForToken,       toast: 'Schnittform wählen' },
            base:        { open: openSc2BaseEditorForToken,        toast: 'Basis wählen' },
            item:        { open: openSc2ItemEditorForToken,        toast: 'Item wählen' },
            balance:     { open: openSc2BalanceEditorForToken,     toast: 'Balance wählen' },
            seasonings:  { open: openSc2SeasoningsEditorForToken,  toast: 'Seasonings wählen' }
        };

        function openSc2EditorForPlaceholderToken(key, tokenId) {
            try {
                if (!key || !tokenId) return;
                closeSc2AllEditors();
                creatorState.ingredientReplaceArmed = false;
                $('#sc2MasterPreviewCard').removeClass('token-replace-active');
                const placeholderType = getPlaceholderType(key);
                if (placeholderType === 'ingredient') {
                    creatorState.activePlaceholderTokenId = tokenId;
                    creatorState.ingredientReplaceArmed = true;
                    $('#sc2MasterPreviewCard').addClass('token-replace-active');
                    showSc2CreatorToast('Zutat auswählen');
                } else if (sc2EditorDispatch[placeholderType]) {
                    creatorState.activePlaceholderTokenId = sc2EditorDispatch[placeholderType].keepTokenActive ? tokenId : '';
                    sc2EditorDispatch[placeholderType].open(tokenId);
                    showSc2CreatorToast(sc2EditorDispatch[placeholderType].toast);
                } else {
                    creatorState.activePlaceholderTokenId = '';
                }
                renderSc2TemplateCards();
            } catch (err) {
                console.error('SC2 openSc2EditorForPlaceholderToken error:', err);
            }
        }

        $(document).ready(function () {
            const token = sessionStorage.getItem('UserToken') || localStorage.getItem('UserToken');
            if (token) $('#hiddenUserTokenField').val(token);

            $('#ingredientSearch').on('input', function () {
                syncIngredientSourceVisibility();
            });
            refreshIngredientProbabilityHints();

            $(document).on('input', '#ingredientConfigQty, .js-db-qty', function () {
                const sanitized = sanitizeQuantityInputValue(this.value);
                if (this.value !== sanitized) {
                    this.value = sanitized;
                }
            });

            $(document).on('click', '.js-common-unit-chip', function (e) {
                e.preventDefault();
                e.stopPropagation();
                const row = $(this).closest('.ingredient-db-row');
                const select = row.find('.js-db-unit').first();
                if (!select.length) return;

                const unitValue = resolveSelectUnitValueByKey(select[0], $(this).data('unit-key'));
                if (!unitValue) return;
                select.val(unitValue).trigger('change');
            });

            // Quick unit chips inside the ingredient config dock
            $(document).on('click', '.js-config-unit-chip', function (e) {
                e.preventDefault();
                e.stopPropagation();
                const select = $('#ingredientConfigUnit');
                if (!select.length) return;

                let unitValue = resolveSelectUnitValueByKey(select[0], $(this).data('unit-key'));
                if (!unitValue) {
                    const key = ($(this).data('unit-key') || '').toString().toLowerCase();
                    const fallbackMap = { g: 'g.', ml: 'ml', piece: 'Stk.' };
                    const fallbackValue = fallbackMap[key] || '';
                    if (fallbackValue && !select.find(`option[value="${fallbackValue}"]`).length) {
                        select.append(`<option value="${fallbackValue}">${fallbackValue}</option>`);
                    }
                    unitValue = fallbackValue;
                }
                if (!unitValue) return;
                select.val(unitValue);
            });

            $(document).on('click', '.js-probability-type', async function () {
                const typeId = ($(this).data('type') || '').toString();
                const typeName = ($(this).data('name') || '').toString();
                const score = parseInt($(this).data('score'), 10) || 0;
                await showProbabilityTemplateSuggestions(typeId, typeName, score);
            });

            $(document).on('click', '.js-probability-template', function () {
                const masterId = ($(this).data('master-id') || '').toString();
                if (!masterId) return;

                creatorState.selectedTemplateId = masterId;
                creatorState.activePlaceholderTokenId = '';
                creatorState.placeholderAssignments = {};
                closeAllEditors();
                renderTemplateCards();
                updatePreviewText();
                showCreatorToast('Template aus Wahrscheinlichkeits-Hinweis gewählt');
            });

            // Vorgeschlagene Zutat hinzufügen: findet die Zeile im Katalog und ruft addIngredient auf
            $(document).on('click', '.js-typical-ingredient-chip', function () {
                const ingId = ($(this).data('ingredient-id') || '').toString();
                if (!ingId) return;

                const catalogRow = $(`.ingredient-db-row[data-ingredient-id="${ingId}"]`).first();
                if (!catalogRow.length) {
                    showCreatorToast('Zutat nicht im Katalog gefunden');
                    return;
                }

                addIngredient(ingId, catalogRow[0]);
                showCreatorToast(`Zutat hinzugefügt`);

                // Chip deaktivieren nach dem Hinzufügen
                $(this).prop('disabled', true).addClass('opacity-50');

                // Kurz zum Katalog scrollen, damit der User die Zutat sehen kann
                catalogRow[0]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            });
            refreshDurationUnitControls();
            loadIngredientArticleRules().finally(() => {
            MasterStepRenderer.load().then((result) => {
                if (!result) {
                    const loadError = typeof MasterStepRenderer.getLastLoadError === 'function' ? MasterStepRenderer.getLastLoadError() : 'Template-Datei konnte nicht geladen werden.';
                    setMasterTemplateError(`Template-Fehler: ${loadError}`);
                    return;
                }
                refreshMasterTemplateBuilder();
            });
            });

            $("#currentStepIngredientButtons").on('click', '.ingredient-chip', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                const selected = creatorState.selectedIngredientIds || [];
                if (selected.includes(id)) {
                    creatorState.selectedIngredientIds = selected.filter(x => x !== id);
                } else {
                    creatorState.selectedIngredientIds = [...selected, id];
                }

                creatorState.advancedPronounOverride = '';
                creatorState.advancedArticleOverride = '';
                syncGrammarAssignmentsForSelectedIngredient();

                if (creatorState.activePlaceholderTokenId) {
                    const key = getPlaceholderKeyByTokenId(creatorState.activePlaceholderTokenId);
                    if (getPlaceholderType(key) === 'ingredient') {
                        const selectedValue = getSelectedIngredientValueForInsert();
                        if (selectedValue) creatorState.placeholderAssignments[creatorState.activePlaceholderTokenId] = selectedValue;
                        creatorState.ingredientReplaceArmed = false;
                        clearSelectedIngredientChips();
                        creatorState.activePlaceholderTokenId = '';
                        $("#masterPreviewCard").removeClass('token-replace-active');
                        showCreatorToast('Platzhalter ersetzt');
                    }
                }

                renderIngredientChips();
                renderTemplateCards();
            });

            $('#masterTemplateCards').on('click', '.template-card', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                creatorState.selectedTemplateId = id;
                creatorState.activePlaceholderTokenId = '';
                creatorState.placeholderAssignments = {};
                closeAllEditors();
                $('.template-card').removeClass('active');
                $(this).addClass('active template-tap');
                setTimeout(() => $(this).removeClass('template-tap'), 180);
                animateTemplateToPreview($(this).data('title'));
                updatePreviewText();
                showCreatorToast('Template gewählt');
            });

            function handleIngredientPlaceholderSelection(tokenId) {
                creatorState.activePlaceholderTokenId = tokenId;
                creatorState.ingredientReplaceArmed = true;
                $('#masterPreviewCard').addClass('token-replace-active');

                const hasExistingAssignment = !!(creatorState.placeholderAssignments[tokenId] || '').toString().trim();
                if (hasExistingAssignment) {
                    clearSelectedIngredientChips();
                    showCreatorToast('Neue Zutat auswählen zum Ersetzen');
                    return;
                }

                const selectedValue = getSelectedIngredientValueForInsert();
                if (selectedValue) {
                    creatorState.placeholderAssignments[tokenId] = selectedValue;
                    clearSelectedIngredientChips();
                    creatorState.activePlaceholderTokenId = '';
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');
                    showCreatorToast('Platzhalter ersetzt');
                    return;
                }

                showCreatorToast('Erst Zutaten auswählen');
            }

            function openEditorForPlaceholderToken(key, tokenId) {
                try {
                    if (!key || !tokenId) return;

                    closeAllEditors();
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');

                    const placeholderType = getPlaceholderType(key);

                    if (placeholderType === 'ingredient') {
                        handleIngredientPlaceholderSelection(tokenId);
                    } else if (placeholderEditorDispatch[placeholderType]) {
                        creatorState.activePlaceholderTokenId = placeholderEditorDispatch[placeholderType].keepTokenActive ? tokenId : '';
                        placeholderEditorDispatch[placeholderType].open(tokenId);
                        showCreatorToast(placeholderEditorDispatch[placeholderType].toast);
                    } else {
                        creatorState.activePlaceholderTokenId = '';
                    }

                    renderTemplateCards();
                } catch (err) {
                    alert('ERROR in openEditorForPlaceholderToken: ' + (err.message || err) + '\nStack: ' + (err.stack || ''));
                }
            }

            $('#masterPreviewText').on('click', '.placeholder-token, .placeholder-wrap', function (e) {
                try {
                if ($(e.target).closest('.placeholder-reset').length) return;

                const token = $(this).hasClass('placeholder-token') ? $(this) : $(this).find('.placeholder-token').first();
                const key = (token.data('placeholder-key') || '').toString();
                const tokenId = (token.data('placeholder-token-id') || '').toString();
                openEditorForPlaceholderToken(key, tokenId);
                } catch (err) {
                    alert('ERROR in token click: ' + (err.message || err));
                }
            });

            $("#currentStepIngredientButtons").on('dragstart', '.ingredient-chip', function (e) {
                const name = ($(this).data('name') || '').toString();
                if (!name) return;
                creatorState.dragIngredientName = name;
                e.originalEvent.dataTransfer.effectAllowed = 'copy';
                e.originalEvent.dataTransfer.setData('text/plain', name);
            });

            $('#masterPreviewText').on('dragover', '.placeholder-token', function (e) {
                const key = ($(this).data('placeholder-key') || '').toString();
                if (getPlaceholderType(key) !== 'ingredient') return;
                e.preventDefault();
                $(this).addClass('drag-over');
                e.originalEvent.dataTransfer.dropEffect = 'copy';
            });

            $('#masterPreviewText').on('dragleave', '.placeholder-token', function () {
                $(this).removeClass('drag-over');
            });

            $('#masterPreviewText').on('drop', '.placeholder-token', function (e) {
                e.preventDefault();
                $(this).removeClass('drag-over');
                const tokenId = ($(this).data('placeholder-token-id') || '').toString();
                const key = ($(this).data('placeholder-key') || '').toString();
                const dropped = e.originalEvent.dataTransfer.getData('text/plain') || creatorState.dragIngredientName || '';
                if (!tokenId || !dropped) return;
                if (getPlaceholderType(key) !== 'ingredient') {
                    showCreatorToast('Drag & Drop nur für ingredient/ingredients');
                    return;
                }
                creatorState.activePlaceholderTokenId = tokenId;
                const existingValue = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
                const dropValue = dropped.toString().trim();
                creatorState.placeholderAssignments[tokenId] = existingValue
                    ? (existingValue.toLowerCase().includes(dropValue.toLowerCase()) ? existingValue : `${existingValue}, ${dropValue}`)
                    : dropValue;
                creatorState.ingredientReplaceArmed = false;
                clearSelectedIngredientChips();
                creatorState.activePlaceholderTokenId = '';
                $('#masterPreviewCard').removeClass('token-replace-active');
                closeAllEditors();
                showCreatorToast('Zutat per Drag & Drop eingesetzt');
                renderTemplateCards();
            });

            $('#masterPreviewText').on('click', '.placeholder-reset', function (e) {
                e.stopPropagation();
                const tokenId = ($(this).data('placeholder-token-id') || '').toString();
                const defaultValue = ($(this).data('default-value') || '').toString();
                if (!tokenId) return;
                if (defaultValue) creatorState.placeholderAssignments[tokenId] = defaultValue;
                else delete creatorState.placeholderAssignments[tokenId];
                if (creatorState.activePlaceholderTokenId === tokenId) {
                    creatorState.activePlaceholderTokenId = '';
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');
                }
                closeAllEditors();
                showCreatorToast('Platzhalter zurückgesetzt');
                renderIngredientChips();
                renderTemplateCards();
            });

            $('#durationValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.durationValue = Number.isFinite(value) && value > 0 ? value : 1;
            });

            $('#durationUnitSelect').on('change', function () {
                creatorState.durationUnit = ($(this).val() || 'minute').toString();
            });

            $('#btnApplyDuration').on('click', function () {
                const tokenId = creatorState.activeDurationTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getDurationInsertText();
                closeDurationEditor();
                showCreatorToast('Zeit eingesetzt');
                renderTemplateCards();
            });

            $('#countValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.countValue = Number.isFinite(value) && value > 0 ? value : 1;
            });

            $('#btnApplyCount').on('click', function () {
                const tokenId = creatorState.activeCountTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getCountInsertText();
                closeCountEditor();
                showCreatorToast('Anzahl eingesetzt');
                renderTemplateCards();
            });

            $('#temperatureValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.temperatureValue = Number.isFinite(value) && value > 0 ? value : 180;
            });

            $('#temperatureUnitSelect').on('change', function () {
                creatorState.temperatureUnit = ($(this).val() || 'celsius').toString();
            });

            $('#btnApplyTemperature').on('click', function () {
                const tokenId = creatorState.activeTemperatureTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getTemperatureInsertText();
                closeTemperatureEditor();
                showCreatorToast('Temperatur eingesetzt');
                renderTemplateCards();
            });

            function bindSimpleEditorOptionHandlers(config) {
                const {
                    optionsContainer,
                    optionButtonClass,
                    stateKey,
                    render,
                    applyButton,
                    activeTokenKey,
                    closeEditor,
                    toast
                } = config;

                $(optionsContainer).on('click', optionButtonClass, function () {
                    const val = ($(this).data('value') || '').toString().trim();
                    creatorState[stateKey] = val;
                    render();
                });

                $(applyButton).on('click', function () {
                    const tokenId = creatorState[activeTokenKey];
                    const val = (creatorState[stateKey] || '').toString().trim();
                    if (!tokenId || !val) return;
                    creatorState[stateKey] = val;
                    creatorState.placeholderAssignments[tokenId] = val;
                    closeEditor();
                    showCreatorToast(toast);
                    renderTemplateCards();
                });
            }

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineHeatOptions',
                optionButtonClass: '.inline-heat-opt',
                stateKey: 'heatValue',
                render: renderInlineHeatOptions,
                applyButton: '#btnApplyHeat',
                activeTokenKey: 'activeHeatTokenId',
                closeEditor: closeHeatEditor,
                toast: 'Hitze-Stufe eingesetzt'
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineModeOptions',
                optionButtonClass: '.inline-mode-opt',
                stateKey: 'modeValue',
                render: renderInlineModeOptions,
                applyButton: '#btnApplyMode',
                activeTokenKey: 'activeModeTokenId',
                closeEditor: closeModeEditor,
                toast: 'Ofenmodus eingesetzt'
            });

            $('#btnOpenAdvancedSettings').on('click', function () {
                openPronounEditorForToken(creatorState.activePronounTokenId || '__global__');
            });

            $('#inlinePronounOptions').on('click', '.inline-pronoun-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.pronounValue = val;
                renderInlinePronounOptions();
            });

            $('#btnApplyPronoun').on('click', function () {
                const tokenId = creatorState.activePronounTokenId;
                const val = (creatorState.pronounValue || creatorState.computedPronoun || '').toString().trim();
                if (!val) return;
                creatorState.advancedPronounOverride = val;
                creatorState.computedPronoun = val;
                if (tokenId && tokenId !== '__global__') {
                    creatorState.placeholderAssignments[tokenId] = val;
                }
                closePronounEditor();
                showCreatorToast('Pronomen eingesetzt');
                renderTemplateCards();
            });

            $('#inlineEquipmentOptions').on('click', '.inline-equipment-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.equipmentValue = composeArticleAndNoun(creatorState.equipmentArticleValue, val);
                renderInlineEquipmentOptions();
            });

            $('#inlineEquipmentArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.equipmentArticleValue = val;
                const noun = splitLeadingArticle(creatorState.equipmentValue || '', currentLang).noun;
                creatorState.equipmentValue = composeArticleAndNoun(val, noun);
                renderInlineEquipmentOptions();
            });

            $('#inlineToolArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.toolArticleValue = val;
                const noun = splitLeadingArticle(creatorState.toolValue || '', currentLang).noun;
                creatorState.toolValue = composeArticleAndNoun(val, noun);
                renderInlineToolOptions();
            });

            $('#inlineBaseArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.baseArticleValue = val;
                const noun = splitLeadingArticle(creatorState.baseValue || '', currentLang).noun;
                creatorState.baseValue = composeArticleAndNoun(val, noun);
                renderInlineBaseOptions();
            });

            $('#inlineItemArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.itemArticleValue = val;
                const noun = splitLeadingArticle(creatorState.itemValue || '', currentLang).noun;
                creatorState.itemValue = composeArticleAndNoun(val, noun);
                renderInlineItemOptions();
            });

            $('#inlineBalanceArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.balanceArticleValue = val;
                const noun = splitLeadingArticle(creatorState.balanceValue || '', currentLang).noun;
                creatorState.balanceValue = composeArticleAndNoun(val, noun);
                renderInlineBalanceOptions();
            });

            $('#btnApplyEquipment').on('click', function () {
                const tokenId = creatorState.activeEquipmentTokenId;
                const val = (creatorState.equipmentValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.equipmentValue = val;
                creatorState.placeholderAssignments[tokenId] = val;
                closeEquipmentEditor();
                showCreatorToast('Tool / Gerät eingesetzt');
                renderTemplateCards();
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineStateOptions',
                optionButtonClass: '.inline-state-opt',
                stateKey: 'stateValue',
                render: renderInlineStateOptions,
                applyButton: '#btnApplyState',
                activeTokenKey: 'activeStateTokenId',
                closeEditor: closeStateEditor,
                toast: 'Zustand eingesetzt'
            });

            $('#inlineToolOptions').on('click', '.inline-tool-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.toolValue = composeArticleAndNoun(creatorState.toolArticleValue, val);
                renderInlineToolOptions();
            });

            $('#btnApplyTool').on('click', function () {
                const tokenId = creatorState.activeToolTokenId;
                const val = (creatorState.toolValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeToolEditor();
                showCreatorToast('Werkzeug eingesetzt');
                renderTemplateCards();
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineGrindSizeOptions',
                optionButtonClass: '.inline-grind-size-opt',
                stateKey: 'grindSizeValue',
                render: renderInlineGrindSizeOptions,
                applyButton: '#btnApplyGrindSize',
                activeTokenKey: 'activeGrindSizeTokenId',
                closeEditor: closeGrindSizeEditor,
                toast: 'Schnittgröße eingesetzt'
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineShapeOptions',
                optionButtonClass: '.inline-shape-opt',
                stateKey: 'shapeValue',
                render: renderInlineShapeOptions,
                applyButton: '#btnApplyShape',
                activeTokenKey: 'activeShapeTokenId',
                closeEditor: closeShapeEditor,
                toast: 'Schnittform eingesetzt'
            });

            $('#inlineBaseOptions').on('click', '.inline-base-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.baseValue = composeArticleAndNoun(creatorState.baseArticleValue, val);
                renderInlineBaseOptions();
            });

            $('#btnApplyBase').on('click', function () {
                const tokenId = creatorState.activeBaseTokenId;
                const val = (creatorState.baseValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.baseValue = val;
                creatorState.placeholderAssignments[tokenId] = val;
                closeBaseEditor();
                showCreatorToast('Basis eingesetzt');
                renderTemplateCards();
            });

            $('#inlineItemOptions').on('click', '.inline-item-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.itemValue = composeArticleAndNoun(creatorState.itemArticleValue, val);
                renderInlineItemOptions();
            });

            $('#btnApplyItem').on('click', function () {
                const tokenId = creatorState.activeItemTokenId;
                const val = (creatorState.itemValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeItemEditor();
                showCreatorToast('Item eingesetzt');
                renderTemplateCards();
            });

            $('#inlineBalanceOptions').on('click', '.inline-balance-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.balanceValue = composeArticleAndNoun(creatorState.balanceArticleValue, val);
                renderInlineBalanceOptions();
            });

            $('#btnApplyBalance').on('click', function () {
                const tokenId = creatorState.activeBalanceTokenId;
                const val = (creatorState.balanceValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeBalanceEditor();
                showCreatorToast('Balance eingesetzt');
                renderTemplateCards();
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineSeasoningsOptions',
                optionButtonClass: '.inline-seasonings-opt',
                stateKey: 'seasoningsValue',
                render: renderInlineSeasoningsOptions,
                applyButton: '#btnApplySeasonings',
                activeTokenKey: 'activeSeasoningsTokenId',
                closeEditor: closeSeasoningsEditor,
                toast: 'Seasonings eingesetzt'
            });

            $(document).on('click', '.ingredient-db-row', function (e) {
                if ($(e.target).closest('button, input, select, label').length) return;
                const details = $(this).find('.ingredient-db-details').first();
                if (!details.length) return;
                const shouldOpen = details.hasClass('d-none');
                $('.ingredient-db-details').addClass('d-none');
                if (shouldOpen) {
                    details.removeClass('d-none');
                }
            });

            $('#btnCloseIngredientConfig, #btnCancelIngredientConfig').on('click', function () {
                closeIngredientConfigPopup();
            });

            $('#btnApplyIngredientConfig').on('click', function () {
                applyIngredientConfigPopup();
            });

            $('#ingredientConfigOverlay').on('click', function (e) {
                if (e.target === this) {
                    closeIngredientConfigPopup();
                }
            });

            // === Probability Variable Editor handlers ===
            $('#btnCloseProbVarEditor, #btnCancelProbVar').on('click', function () {
                closeProbVarEditor();
            });

            $('#btnApplyProbVar').on('click', function () {
                applyProbVarEditor();
            });

            $('#probVarEditorOverlay').on('click', function (e) {
                if (e.target === this) closeProbVarEditor();
            });

            // Ingredient chip - toggle selection in editor (apply via Einsetzen)
            $(document).on('click', '.js-prob-ingredient-chip', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;

                const selected = (creatorState.selectedIngredientIds || []).map(x => x.toString());
                if (selected.includes(id)) {
                    creatorState.selectedIngredientIds = selected.filter(x => x !== id);
                } else {
                    creatorState.selectedIngredientIds = [...selected, id];
                }

                const ings = typeof getSelectedIngredientsForSandbox === 'function' ? getSelectedIngredientsForSandbox() : [];
                const chips = (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.buildIngredientChipsHtml === 'function')
                    ? window.MasterStepCreatorHelpers.buildIngredientChipsHtml(ings, creatorState.selectedIngredientIds)
                    : '';
                $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewählt</span>');
            });

            // Option chip → auto-apply and close
            $(document).on('click', '.js-prob-option-chip', function () {
                const value = ($(this).data('value') || $(this).text()).toString();
                if (probVarEditorCallback) probVarEditorCallback(value);
                closeProbVarEditor();
            });

            $('#btnAddRenderedStep').on('click', async function () {
                await addRenderedMasterStep();
                updateStoryProgress();
                showCreatorToast('Step akzeptiert');
            });

            $('#stepsIngredientButtons').on('click', '.ingredient-chip', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                const selected = creatorState.selectedIngredientIds || [];
                if (selected.includes(id)) {
                    creatorState.selectedIngredientIds = selected.filter(x => x !== id);
                } else {
                    creatorState.selectedIngredientIds = [...selected, id];
                }
                syncGrammarAssignmentsForSelectedIngredient();
                renderIngredientChips();
            });

            $('#btnApplyStepIngredient').on('click', function () { applyChipToStep(); });
            $('#btnCancelStepIngredient').on('click', function () { cancelStepIngredientEdit(); });

            $('#selectedIngredients').on('click', '.ingredient-row', function (e) {
                if ($(e.target).closest('button, input, select, label').length) return;
                openIngredientConfigPopup(this);
            });

            $('#selectedSteps').on('dragstart', '.step-row', function (e) {
                draggedStepRow = this;
                e.originalEvent.dataTransfer.effectAllowed = 'move';
                $(this).addClass('opacity-50');
            });

            $('#selectedSteps').on('dragend', '.step-row', function () {
                $(this).removeClass('opacity-50');
                draggedStepRow = null;
            });

            $('#selectedSteps').on('dragover', '.step-row', function (e) {
                e.preventDefault();
                if (!draggedStepRow || draggedStepRow === this) return;
                const rect = this.getBoundingClientRect();
                const offset = e.originalEvent.clientY - rect.top;
                if (offset > rect.height / 2) $(this).after(draggedStepRow);
                else $(this).before(draggedStepRow);
                updateStepIndices();
            });

            // SC2 Event handlers
            $('#sc2MasterTemplateCards').on('click', '.template-card', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                creatorState.selectedTemplateId = id;
                creatorState.activePlaceholderTokenId = '';
                creatorState.placeholderAssignments = {};
                closeSc2AllEditors();
                closeAllEditors();
                $('.template-card').removeClass('active');
                $(this).addClass('active template-tap');
                setTimeout(() => $(this).removeClass('template-tap'), 180);
                const ghost = $('#sc2TemplateFlyGhost');
                ghost.text($(this).data('title') || 'Template').removeClass('fly');
                void ghost[0]?.offsetWidth;
                ghost.addClass('fly');
                updatePreviewText();
                showSc2CreatorToast('Template gewählt');
            });

            $('#sc2MasterIngredientButtons').on('click', '.ingredient-chip', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                const selected = creatorState.selectedIngredientIds || [];
                if (selected.includes(id)) {
                    creatorState.selectedIngredientIds = selected.filter(x => x !== id);
                } else {
                    creatorState.selectedIngredientIds = [...selected, id];
                }
                syncGrammarAssignmentsForSelectedIngredient();
                renderIngredientChips();
            });

            $('#sc2MasterPreviewText').on('click', '.placeholder-token, .placeholder-wrap', function (e) {
                try {
                    if ($(e.target).closest('.placeholder-reset').length) return;
                    const token = $(this).hasClass('placeholder-token') ? $(this) : $(this).find('.placeholder-token').first();
                    const key = (token.data('placeholder-key') || '').toString();
                    const tokenId = (token.data('placeholder-token-id') || '').toString();
                    openSc2EditorForPlaceholderToken(key, tokenId);
                } catch (err) { console.error('SC2 token click error:', err); }
            });

            $('#sc2MasterPreviewText').on('click', '.placeholder-reset', function (e) {
                e.stopPropagation();
                const tokenId = ($(this).data('placeholder-token-id') || '').toString();
                const defaultValue = ($(this).data('default-value') || '').toString();
                if (!tokenId) return;
                if (defaultValue) creatorState.placeholderAssignments[tokenId] = defaultValue;
                else delete creatorState.placeholderAssignments[tokenId];
                if (creatorState.activePlaceholderTokenId === tokenId) {
                    creatorState.activePlaceholderTokenId = '';
                    creatorState.ingredientReplaceArmed = false;
                    $('#sc2MasterPreviewCard').removeClass('token-replace-active');
                }
                closeSc2AllEditors();
                showSc2CreatorToast('Platzhalter zurückgesetzt');
                renderIngredientChips();
                renderTemplateCards();
            });

            $('#sc2BtnAddRenderedStep').on('click', async function () {
                await addRenderedMasterStep();
                updateStoryProgress();
                showSc2CreatorToast('Step akzeptiert');
            });

            $('#sc2BtnOpenAdvancedSettings').on('click', function () {
                openSc2PronounEditorForToken(creatorState.activePronounTokenId || '__global__');
            });

            // SC2 Duration
            $('#sc2DurationValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.durationValue = Number.isFinite(value) && value > 0 ? value : 1;
            });
            $('#sc2DurationUnitSelect').on('change', function () {
                creatorState.durationUnit = ($(this).val() || 'minute').toString();
            });
            $('#sc2BtnApplyDuration').on('click', function () {
                const tokenId = creatorState.activeDurationTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getDurationInsertText();
                closeSc2DurationEditor();
                showSc2CreatorToast('Zeit eingesetzt');
                renderTemplateCards();
            });

            // SC2 Count
            $('#sc2CountValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.countValue = Number.isFinite(value) && value > 0 ? value : 1;
            });
            $('#sc2BtnApplyCount').on('click', function () {
                const tokenId = creatorState.activeCountTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getCountInsertText();
                closeSc2CountEditor();
                showSc2CreatorToast('Anzahl eingesetzt');
                renderTemplateCards();
            });

            // SC2 Temperature
            $('#sc2TemperatureValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.temperatureValue = Number.isFinite(value) && value > 0 ? value : 180;
            });
            $('#sc2TemperatureUnitSelect').on('change', function () {
                creatorState.temperatureUnit = ($(this).val() || 'celsius').toString();
            });
            $('#sc2BtnApplyTemperature').on('click', function () {
                const tokenId = creatorState.activeTemperatureTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getTemperatureInsertText();
                closeSc2TemperatureEditor();
                showSc2CreatorToast('Temperatur eingesetzt');
                renderTemplateCards();
            });

            // SC2 Pronoun
            $('#sc2InlinePronounOptions').on('click', '.inline-pronoun-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.pronounValue = val;
                renderInlineChoiceButtons('#sc2InlinePronounOptions', 'inline-pronoun-opt', getPronounSuggestionsForLang(currentLang), val);
            });
            $('#sc2BtnApplyPronoun').on('click', function () {
                const tokenId = creatorState.activePronounTokenId;
                const val = (creatorState.pronounValue || creatorState.computedPronoun || '').toString().trim();
                if (!val) return;
                creatorState.advancedPronounOverride = val;
                creatorState.computedPronoun = val;
                if (tokenId && tokenId !== '__global__') creatorState.placeholderAssignments[tokenId] = val;
                closeSc2PronounEditor();
                showSc2CreatorToast('Pronomen gesetzt');
                renderTemplateCards();
            });

            // SC2 Heat
            $('#sc2InlineHeatOptions').on('click', '.inline-heat-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.heatValue = val;
                renderInlineChoiceButtons('#sc2InlineHeatOptions', 'inline-heat-opt', getHeatOptions(), val);
            });
            $('#sc2BtnApplyHeat').on('click', function () {
                const tokenId = creatorState.activeHeatTokenId;
                const val = (creatorState.heatValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2HeatEditor();
                showSc2CreatorToast('Hitze-Stufe eingesetzt');
                renderTemplateCards();
            });

            // SC2 Mode
            $('#sc2InlineModeOptions').on('click', '.inline-mode-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.modeValue = val;
                renderInlineChoiceButtons('#sc2InlineModeOptions', 'inline-mode-opt', getModeOptions(), val);
            });
            $('#sc2BtnApplyMode').on('click', function () {
                const tokenId = creatorState.activeModeTokenId;
                const val = (creatorState.modeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2ModeEditor();
                showSc2CreatorToast('Ofenmodus eingesetzt');
                renderTemplateCards();
            });

            // SC2 Equipment
            $('#sc2InlineEquipmentArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.equipmentArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineEquipmentArticleOptions', creatorState.equipmentArticleValue);
            });
            $('#sc2InlineEquipmentOptions').on('click', '.inline-equipment-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.equipmentValue = val;
                $('#sc2InlineEquipmentOptions .inline-equipment-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyEquipment').on('click', function () {
                const tokenId = creatorState.activeEquipmentTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.equipmentArticleValue, creatorState.equipmentValue);
                closeSc2EquipmentEditor();
                showSc2CreatorToast('Tool eingesetzt');
                renderTemplateCards();
            });

            // SC2 State
            $('#sc2InlineStateOptions').on('click', '.inline-state-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.stateValue = val;
                renderInlineChoiceButtons('#sc2InlineStateOptions', 'inline-state-opt', getStateOptions(), val);
            });
            $('#sc2BtnApplyState').on('click', function () {
                const tokenId = creatorState.activeStateTokenId;
                const val = (creatorState.stateValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2StateEditor();
                showSc2CreatorToast('Zustand eingesetzt');
                renderTemplateCards();
            });

            // SC2 Tool
            $('#sc2InlineToolArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.toolArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineToolArticleOptions', creatorState.toolArticleValue);
            });
            $('#sc2InlineToolOptions').on('click', '.inline-tool-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.toolValue = val;
                $('#sc2InlineToolOptions .inline-tool-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyTool').on('click', function () {
                const tokenId = creatorState.activeToolTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.toolArticleValue, creatorState.toolValue);
                closeSc2ToolEditor();
                showSc2CreatorToast('Werkzeug eingesetzt');
                renderTemplateCards();
            });

            // SC2 GrindSize
            $('#sc2InlineGrindSizeOptions').on('click', '.inline-grind-size-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.grindSizeValue = val;
                renderInlineChoiceButtons('#sc2InlineGrindSizeOptions', 'inline-grind-size-opt', getGrindSizeOptions(), val);
            });
            $('#sc2BtnApplyGrindSize').on('click', function () {
                const tokenId = creatorState.activeGrindSizeTokenId;
                const val = (creatorState.grindSizeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2GrindSizeEditor();
                showSc2CreatorToast('Schnittgröße eingesetzt');
                renderTemplateCards();
            });

            // SC2 Shape
            $('#sc2InlineShapeOptions').on('click', '.inline-shape-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.shapeValue = val;
                renderInlineChoiceButtons('#sc2InlineShapeOptions', 'inline-shape-opt', getShapeOptions(), val);
            });
            $('#sc2BtnApplyShape').on('click', function () {
                const tokenId = creatorState.activeShapeTokenId;
                const val = (creatorState.shapeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2ShapeEditor();
                showSc2CreatorToast('Schnittform eingesetzt');
                renderTemplateCards();
            });

            // SC2 Base
            $('#sc2InlineBaseArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.baseArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineBaseArticleOptions', creatorState.baseArticleValue);
            });
            $('#sc2InlineBaseOptions').on('click', '.inline-base-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.baseValue = val;
                $('#sc2InlineBaseOptions .inline-base-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyBase').on('click', function () {
                const tokenId = creatorState.activeBaseTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.baseArticleValue, creatorState.baseValue);
                closeSc2BaseEditor();
                showSc2CreatorToast('Basis eingesetzt');
                renderTemplateCards();
            });

            // SC2 Item
            $('#sc2InlineItemArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.itemArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineItemArticleOptions', creatorState.itemArticleValue);
            });
            $('#sc2InlineItemOptions').on('click', '.inline-item-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.itemValue = val;
                $('#sc2InlineItemOptions .inline-item-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyItem').on('click', function () {
                const tokenId = creatorState.activeItemTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.itemArticleValue, creatorState.itemValue);
                closeSc2ItemEditor();
                showSc2CreatorToast('Item eingesetzt');
                renderTemplateCards();
            });

            // SC2 Balance
            $('#sc2InlineBalanceArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.balanceArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineBalanceArticleOptions', creatorState.balanceArticleValue);
            });
            $('#sc2InlineBalanceOptions').on('click', '.inline-balance-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.balanceValue = val;
                $('#sc2InlineBalanceOptions .inline-balance-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyBalance').on('click', function () {
                const tokenId = creatorState.activeBalanceTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.balanceArticleValue, creatorState.balanceValue);
                closeSc2BalanceEditor();
                showSc2CreatorToast('Balance eingesetzt');
                renderTemplateCards();
            });

            // SC2 Seasonings
            $('#sc2InlineSeasoningsOptions').on('click', '.inline-seasonings-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.seasoningsValue = val;
                renderInlineChoiceButtons('#sc2InlineSeasoningsOptions', 'inline-seasonings-opt', getSeasoningsOptions(), val);
            });
            $('#sc2BtnApplySeasonings').on('click', function () {
                const tokenId = creatorState.activeSeasoningsTokenId;
                const val = (creatorState.seasoningsValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2SeasoningsEditor();
                showSc2CreatorToast('Seasonings eingesetzt');
                renderTemplateCards();
            });
            // === Ende SC2 ===

            updateLanguageLabels();
            syncIngredientSourceVisibility();

            // Page fully ready — hide overlay, show content
            $('#datatableLoadingOverlay').addClass('d-none');
            $('.creator-topbar, .feed-shell').css('visibility', 'visible');
        });


