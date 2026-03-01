(function (window, $) {
    'use strict';

    const UI_TEXT = {
        selectIngredients: 'Wähle Zutaten aus, um Wahrscheinlichkeiten zu sehen.',
        moduleMissing: 'Analyse-Modul nicht verfügbar.',
        noTrend: 'Noch keine klare Tendenz erkannt.',
        analysisUnavailable: 'Wahrscheinlichkeitsanalyse aktuell nicht verfügbar.',
        templatesUnavailable: 'Template-Vorschläge sind aktuell nicht verfügbar.',
        selectIngredientsFirst: 'Bitte zuerst Zutaten auswählen.',
        noTemplates: 'Keine passenden Templates gefunden.',
        suggestedIngredients: 'Vorgeschlagene Zutaten',
        suggestedIngredientsHint: 'Tippen zum Hinzufügen'
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

    function buildTemplateCardHtml(masterId, displayText) {
        const safeId = escapeHtml(masterId || '');
        const safeText = escapeHtml(displayText || masterId || '');
        return `<button type="button" class="probability-template-card w-100 text-start js-probability-template" data-master-id="${safeId}">${safeText}</button>`;
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
    function buildIngredientSuggestionHtml(typeId, deps) {
        if (!deps.getTypicalIngredientSuggestions) return '';
        const suggestions = deps.getTypicalIngredientSuggestions(typeId) || [];
        if (!suggestions.length) return '';

        const chips = suggestions.map(ing => {
            const safeId = escapeHtml(String(ing.id || ''));
            const safeName = escapeHtml(ing.name || String(ing.id));
            return `<button type="button" class="btn btn-sm btn-outline-warning js-typical-ingredient-chip" data-ingredient-id="${safeId}" title="${UI_TEXT.suggestedIngredientsHint}">${safeName}</button>`;
        }).join('');

        return `<div class="mt-3 pt-2" style="border-top:1px solid rgba(255,255,255,.12)">
  <div class="small text-white-50 mb-2">${escapeHtml(UI_TEXT.suggestedIngredients)} <span class="opacity-50">&middot; ${escapeHtml(UI_TEXT.suggestedIngredientsHint)}</span></div>
  <div class="d-flex flex-wrap gap-2">${chips}</div>
</div>`;
    }

    function create(deps) {
        const state = {
            presets: null,
            presetsPromise: null
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
                const snippet = deps.renderTemplate(masterId, vars, getLang()) || template.description || masterId;
                cards.push(buildTemplateCardHtml(masterId, snippet));
            });
            return cards;
        }

        function buildFallbackCards(ingredientNames) {
            const preview = deps.getMasterStepPreview(ingredientNames, { lang: getLang() }) || [];
            if (!preview.length) return [];

            const cards = [];
            const seen = new Set();
            preview.forEach(item => {
                const masterId = (item.masterId || '').toString();
                if (!masterId || seen.has(masterId)) return;
                seen.add(masterId);
                cards.push(buildTemplateCardHtml(masterId, item.text || masterId));
            });
            return cards;
        }

        async function refresh() {
            const container = $('#ingredientProbabilityBadges');
            const templatesBox = $('#ingredientProbabilityTemplates');
            if (!container.length) return;

            const selectedIngredientIds = deps.getSelectedIngredientIds();
            templatesBox.addClass('d-none').empty();

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
            if (!box.length) return;

            if (!window.RecipeStepSuggest || !window.MasterStepRenderer) {
                box.removeClass('d-none').html(`<div class="small text-white-50">${escapeHtml(UI_TEXT.templatesUnavailable)}</div>`);
                return;
            }

            const ingredients = deps.getSelectedIngredientNames();
            if (!ingredients.length) {
                box.removeClass('d-none').html(`<div class="small text-white-50">${escapeHtml(UI_TEXT.selectIngredientsFirst)}</div>`);
                return;
            }

            await loadPresets();
            const preferredTemplateIds = getPreferredMasterTemplateIdsForType(typeId, state.presets);
            const varsByTemplate = {};
            let cards = buildPreferredCards(preferredTemplateIds, varsByTemplate);

            if (!cards.length) {
                cards = buildFallbackCards(ingredients);
                if (!cards.length) {
                    box.removeClass('d-none').html(`<div class="small text-white-50">${escapeHtml(UI_TEXT.noTemplates)}</div>`);
                    return;
                }
            }

            const head = `<div class="small text-white-50 mb-2">${escapeHtml(typeName || typeId || 'Typ')} (${score || 0}%) · Template-Auswahl</div>`;
            const ingredientSuggestions = buildIngredientSuggestionHtml(typeId, deps);
            box.removeClass('d-none').html(head + cards.join('') + ingredientSuggestions);
            document.getElementById('masterPreviewCanvas')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
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
