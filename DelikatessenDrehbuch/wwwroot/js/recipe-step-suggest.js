/**
 * Recipe Step Suggestion Engine
 * Uses recipe_category_scoring.json for recipe type detection and
 * master_steps.json + probability_template_presets.json for step previews.
 *
 * Features:
 * - Recipe type detection with score (e.g. "82% Lasagne")
 * - Curated step sequences per recipe type
 * - Phase-sorted step sequences
 */
(function (window) {
    'use strict';

    let mappingData = null;
    let categoryScoringData = null;
    let masterStepsData = null;
    let templatePresetsData = null;
    let recipeTypeStepVarsData = null;
    let mappingLoaded = false;
    let loadingPromise = null;

    /** Load mapping JSON files (cached after first load) */
    function loadMapping() {
        if (loadingPromise) return loadingPromise;

        loadingPromise = Promise.all([
            fetch('/data/recipe_step_mapping.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null),
            fetch('/data/recipe_category_scoring.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null),
            fetch('/data/master_steps.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null),
            fetch('/data/probability_template_presets.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null),
            fetch('/data/recipe_type_step_variables.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null)
        ]).then(([stepData, categoryData, masterData, presetsData, stepVarsData]) => {
            mappingData = stepData;
            categoryScoringData = categoryData;
            masterStepsData = masterData;
            templatePresetsData = presetsData;
            recipeTypeStepVarsData = stepVarsData;
            mappingLoaded = true;
            console.log('[RecipeStepSuggest] Daten geladen:', {
                mapping: !!stepData,
                scoring: !!categoryData,
                masterSteps: !!masterData,
                presets: !!presetsData,
                stepVars: !!stepVarsData
            });
            return { stepData, categoryData, masterData };
        }).catch(err => {
            console.error('[RecipeStepSuggest] Fehler beim Laden:', err);
            mappingLoaded = false;
            return null;
        });

        return loadingPromise;
    }

    /**
     * Detect which recipe types match the selected ingredients.
     * Uses recipe_category_scoring.json (weighted scoring with false-positive penalty).
     * Returns sorted array: [{ type, name, score, matchedSignatureCount, totalSignature }]
     */
    function detectRecipeTypes(selectedIngredientIds) {
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
        const stepScores = new Map();

        // Direct ingredient->step mapping
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

        // Filter: step must exist in the page's step catalog
        // Filter: validate step text against selected ingredient names
        const conflictIngredients = buildConflictMap();
        const results = [];

        stepScores.forEach((data, sidStr) => {
            const catalogStep = getStepFromCatalog(sidStr, stepCatalogById);
            if (!catalogStep) return;

            const stepText = getStepText(catalogStep);
            if (!stepText) return;

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
     */
    function buildConflictMap() {
        return [
            ['gouda', 'cheddar', 'emmentaler', 'gruyère', 'edamer', 'bergkäse', 'raclette'],
            ['mozzarella', 'burrata'],
            ['parmesan', 'pecorino', 'grana padano'],
            ['feta', 'ziegenkäse', 'schafskäse', 'halloumi'],
            ['lachs', 'forelle', 'kabeljau', 'thunfisch', 'zander', 'dorade', 'seelachs', 'pangasius'],
            ['hähnchen', 'pute', 'truthahn', 'ente', 'gans'],
            ['rindfleisch', 'schweinefleisch', 'lammfleisch', 'kalbfleisch', 'wildfleisch'],
            ['spaghetti', 'penne', 'rigatoni', 'tagliatelle', 'fusilli', 'farfalle', 'linguine', 'fettuccine', 'makkaroni'],
            ['basmati', 'jasminreis', 'risotto-reis', 'langkornreis', 'wildreis'],
            ['kokosmilch', 'pflanzenmilch', 'hafermilch', 'sojamilch', 'mandelmilch'],
        ];
    }

    function hasConflictingIngredient(stepText, selectedNames, conflictGroups) {
        const textLower = stepText.toLowerCase();

        for (const group of conflictGroups) {
            const mentionedInStep = group.filter(item => textLower.includes(item));
            if (!mentionedInStep.length) continue;

            const selectedFromGroup = group.filter(item =>
                selectedNames.some(name => name.includes(item) || item.includes(name))
            );

            if (selectedFromGroup.length > 0) {
                const mentionedAndSelected = mentionedInStep.filter(m =>
                    selectedFromGroup.some(s => s.includes(m) || m.includes(s))
                );
                if (mentionedAndSelected.length === 0) {
                    return true;
                }
            }
        }

        return false;
    }

    function getStepFromCatalog(stepId, catalog) {
        const sid = stepId.toString();
        if (catalog instanceof Map) {
            return catalog.get(sid) || catalog.get(parseInt(sid, 10));
        }
        return catalog[sid] || catalog[parseInt(sid, 10)];
    }

    function getStepText(step) {
        return step.step_DE || step.de || step.Step_DE || step.StepDe || '';
    }

    /**
     * Render recipe type score badges as HTML
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

    /**
     * Erstellt eine vollständige Vorschau aller Master-Steps für die gewählten Zutaten.
     * Nutzt kuratierte Step-Sequenzen aus probability_template_presets.json
     * mit vorausgefüllten Variablen via MasterStepRenderer.getSmartDefaults().
     */
    function getMasterStepPreview(selectedIngredients, options) {
        options = options || {};
        const lang = (options.lang || 'de').toLowerCase();
        const recipeType = (options.recipeType || '').toLowerCase();

        const ingredientNames = selectedIngredients.map(ing =>
            typeof ing === 'string' ? ing : (ing.name || ing.ingredient_name)
        );

        if (!masterStepsData || !Array.isArray(masterStepsData.master_steps)) return [];

        // If recipe type detected, use curated step sequence with pre-filled variables
        if (recipeType && templatePresetsData && templatePresetsData.types && templatePresetsData.types[recipeType]) {
            return buildRecipeTypePreview(recipeType, ingredientNames, lang);
        }

        return [];
    }

    /**
     * Build preview using curated step sequence and pre-filled variables for a recipe type.
     */
    function buildRecipeTypePreview(recipeType, ingredientNames, lang) {
        const stepIds = templatePresetsData.types[recipeType];
        if (!stepIds || !Array.isArray(stepIds)) return [];

        const rendered = [];
        const firstIngredient = ingredientNames.length ? ingredientNames[0] : 'die Zutat';

        stepIds.forEach(function (masterId) {
            const masterStep = masterStepsData.master_steps.find(s => s && s.master_id === masterId);
            if (!masterStep) return;

            var vars = MasterStepRenderer.getSmartDefaults(masterId, {
                ingredientName: firstIngredient,
                recipeType: recipeType
            });

            var text = MasterStepRenderer.render(masterId, vars, lang);

            if (text) {
                rendered.push({
                    masterId: masterId,
                    phase: parseInt(masterStep.phase || 0, 10),
                    equipment: masterStep.equipment,
                    action: masterStep.action,
                    ingredient: vars.ingredient || firstIngredient,
                    text: text
                });
            }
        });

        return rendered;
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
