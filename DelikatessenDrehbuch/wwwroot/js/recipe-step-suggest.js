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
    let mappingLoaded = false;
    let loadingPromise = null;

    /** Load mapping JSON files (cached after first load) */
    function loadMapping() {
        if (loadingPromise) return loadingPromise;

        loadingPromise = Promise.all([
            fetch('/data/recipe_step_mapping.json').then(r => r.json()),
            fetch('/data/recipe_category_scoring.json')
                .then(r => r.ok ? r.json() : null)
                .catch(() => null)
        ]).then(([stepData, categoryData]) => {
            mappingData = stepData;
            categoryScoringData = categoryData;
            mappingLoaded = true;
            return { stepData, categoryData };
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
     */
    function detectRecipeTypesFromCategoryScoring(selectedIngredientIds) {
        if (!categoryScoringData || !Array.isArray(categoryScoringData.categories)) return [];

        const selectedSet = new Set(selectedIngredientIds.map(id => parseInt(id, 10)).filter(id => !isNaN(id)));
        const results = [];

        categoryScoringData.categories.forEach(cat => {
            const weights = Array.isArray(cat.ingredient_weights) ? cat.ingredient_weights : [];
            if (!weights.length) return;

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

            const score = Math.round((weightedMatched / weightedTotal) * 100);
            const localizedName = cat?.display_name?.de || cat?.display_name?.en || cat.id || 'Unbekannt';

            results.push({
                type: cat.id,
                name: localizedName,
                score: score,
                matchedSignatureCount: matchedCount,
                totalSignature: weights.length
            });
        });

        results.sort((a, b) => b.score - a.score);
        return results;
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

    // Export
    window.RecipeStepSuggest = {
        loadMapping: loadMapping,
        detectRecipeTypes: detectRecipeTypes,
        suggestStepsForIngredients: suggestStepsForIngredients,
        renderRecipeTypeBadges: renderRecipeTypeBadges,
        isLoaded: function () { return mappingLoaded; }
    };

})(window);
