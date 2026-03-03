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

    function buildInlineTemplateText(template, lang, vars) {
        const templateText = (template?.templates?.[lang] || template?.templates?.de || '').toString();
        if (!templateText) return '';

        return templateText.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
            const varKey = String(key || '').trim();
            const safeKey = escapeHtml(varKey);
            const nextValue = vars && vars[varKey] != null ? String(vars[varKey]).trim() : '';
            const safeValue = escapeHtml(nextValue || varKey);
            return `<span class="probability-var-inline-wrap"><span class="js-probability-var probability-var-inline-token" data-var-key="${safeKey}" role="button" tabindex="0">${safeValue}</span><button type="button" class="probability-var-reset-btn js-probability-var-reset" data-var-key="${safeKey}" title="Variable zurücksetzen" aria-label="Variable zurücksetzen">↺</button></span>`;
        });
    }

    function buildTemplateCardHtml(masterId, displayText, template, vars, lang) {
        const safeId = escapeHtml(masterId || '');
        const safeText = escapeHtml(displayText || masterId || '');
        const inlineText = buildInlineTemplateText(template, lang, vars);

        return `<div class="probability-template-wrap" data-master-id="${safeId}">
  <div class="probability-template-card w-100 text-start">
    <div class="probability-template-kicker">Aktueller Step</div>
    <button type="button" class="probability-template-select js-probability-template" data-master-id="${safeId}">
      <span class="probability-template-text">${inlineText || safeText}</span>
    </button>
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
                    const nextVal = window.prompt(`Wert für ${varKey}:`, currentVal);
                    if (nextVal == null) return;
                    if (!state.inlineOverrides[masterId]) state.inlineOverrides[masterId] = {};
                    state.inlineOverrides[masterId][varKey] = nextVal;
                    rerenderInlineText(masterId, wrap);
                }
            });

            $(document).off('click.probabilityInlineVarReset').on('click.probabilityInlineVarReset', '.js-probability-var-reset', function (event) {
                event.preventDefault();
                event.stopPropagation();
                const btn = $(this);
                const wrap = btn.closest('.probability-template-wrap');
                const masterId = (wrap.data('master-id') || '').toString();
                const varKey = (btn.data('var-key') || '').toString();
                if (!masterId || !varKey) return;

                if (state.inlineOverrides[masterId]) {
                    delete state.inlineOverrides[masterId][varKey];
                    if (!Object.keys(state.inlineOverrides[masterId]).length) {
                        delete state.inlineOverrides[masterId];
                    }
                }
                rerenderInlineText(masterId, wrap);
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
