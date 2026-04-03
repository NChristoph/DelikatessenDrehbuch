(function (window, $) {
    'use strict';

    // Global storage for multi-ingredients in probability area
    // Structure: { "masterId": { "varName": [{name, fraction, article}, ...] } }
    window.ProbabilityMultiIngredients = window.ProbabilityMultiIngredients || {};

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

    var escapeHtml = window.CreatePostingUtils.escapeHtml;

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

    // overrideVars: only user-set values (not defaults); used to decide if optional segment is a pill
    function buildInlineTemplateText(masterId, template, lang, vars, overrideVars) {
        const templateText = (template?.templates?.[lang] || template?.templates?.de || '').toString();
        if (!templateText) return '';

        const helpers = window.MasterStepCreatorHelpers || {};
        if (typeof helpers.renderTemplateWithConfig !== 'function') {
            return '';
        }

        // Get multi-ingredients data for this master
        const multiIngredientsData = window.ProbabilityMultiIngredients[masterId] || {};

        console.log(`[buildInlineTemplateText] "${masterId}" - vars.removal:`, vars?.removal);
        console.log(`[buildInlineTemplateText] "${masterId}" - overrideVars:`, overrideVars);

        return helpers.renderTemplateWithConfig(templateText, (masterId || 'template').toString(), vars || {}, {
            optionalValues: overrideVars || {},
            tokenWrapClass: 'probability-var-inline-wrap',
            tokenExtraClasses: 'js-probability-var probability-var-inline-token',
            pillExtraClasses: 'js-probability-var',
            includeVarKey: true,
            optionalVarAttrName: 'data-var',
            pillTitlePrefix: 'Optional',
            enablePlusButtons: true,
            multiIngredientsData: multiIngredientsData
        });
    }
    function buildTemplateCardHtml(masterId, displayText, template, vars, lang) {
        const safeId = escapeHtml(masterId || '');
        const safeText = escapeHtml(displayText || masterId || '');

        // ✅ FIX (2026-04-02): Einheitlich wie rerenderInlineText - nutze overrides explizit
        const overrides = {};  // Initial render = keine User-Overrides
        const inlineText = buildInlineTemplateText(masterId, template, lang, vars, overrides);

        return `<div class="probability-template-wrap" data-master-id="${safeId}">
  <div class="probability-template-card preview-step-card w-100 text-start">
    <div class="prob-step-header">
      <span class="prob-step-label">Erkannter Step</span>
      <div class="probability-template-actions d-flex gap-2 align-items-center">
        <button type="button" class="btn btn-sm creator-cta-primary js-probability-accept" data-master-id="${safeId}">Akzeptieren</button>
        <button type="button" class="btn btn-sm btn-outline-light js-probability-dismiss" data-master-id="${safeId}">Löschen</button>
      </div>
    </div>
    <div class="probability-template-select js-probability-template" data-master-id="${safeId}" role="button" tabindex="0">
      <span class="probability-template-text preview-step-text">${inlineText || safeText}</span>
    </div>
    <div class="prob-inline-editor-host d-none"></div>
  </div>
</div>`;
    }

    function renderProbabilityTypeButtons(recipeTypes) {
        return (recipeTypes || []).slice(0, 5).map((recipeType, idx) => {
            const colorClass = recipeType.score >= 60 ? 'btn-success' : (recipeType.score >= 35 ? 'btn-primary' : 'btn-secondary');
            const icon = idx === 0 ? '<i class="bi bi-star-fill me-1"></i>' : '';
            // Use subtype for step-lookup if available, otherwise parent type
            const effectiveType = recipeType.subtype || recipeType.type || '';
            return `<button type="button" class="btn btn-sm ${colorClass} probability-type-btn js-probability-type" data-type="${escapeHtml(effectiveType)}" data-parent-type="${escapeHtml(recipeType.type || '')}" data-name="${escapeHtml(recipeType.name || '')}" data-score="${recipeType.score}">${icon}${recipeType.score}% ${escapeHtml(recipeType.name || '')}</button>`;
        }).join('');
    }

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
            currentTypeId: ''
        };

        const getLang = () => (deps.getCurrentLang ? deps.getCurrentLang() : 'de');

        async function loadPresets() {
            if (state.presets) return state.presets;
            if (state.presetsPromise) return state.presetsPromise;

            const defaultPresets = { types: {} };
            state.presetsPromise = window.CreatePostingDataStore.load('probabilityTemplatePresets')
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

        function buildPreferredCards(preferredTemplateIds) {
            const cards = [];
            (preferredTemplateIds || []).forEach(masterId => {
                const template = deps.findTemplate(masterId);
                if (!template) return;
                const vars = deps.buildVariablesForTemplate(masterId, state.currentTypeId);
                const snippet = deps.renderTemplate(masterId, vars, getLang()) || masterId;
                cards.push(buildTemplateCardHtml(masterId, snippet, template, vars, getLang()));
            });
            return cards;
        }

        function buildFallbackCards(ingredientNames) {
            const preview = deps.getMasterStepPreview(ingredientNames, { lang: getLang(), recipeType: state.currentTypeId }) || [];
            if (!preview.length) return [];

            const cards = [];
            const seen = new Set();
            preview.forEach(item => {
                const masterId = (item.masterId || '').toString();
                if (!masterId || seen.has(masterId)) return;
                seen.add(masterId);
                const template = deps.findTemplate(masterId);
                const vars = deps.buildVariablesForTemplate(masterId, state.currentTypeId);
                cards.push(buildTemplateCardHtml(masterId, item.text || masterId, template, vars, getLang()));
            });
            return cards;
        }

        function getMergedVars(masterId) {
            // ✅ VEREINFACHT (2026-04-03): Nur noch unified System (window.probabilityStates)
            // Alte state.varsByTemplate und state.inlineOverrides wurden entfernt

            // Werte aus unified System holen
            const merged = { ...(window.probabilityStates?.[masterId]?.values || {}) };

            // Multi-ingredients formatieren (falls vorhanden)
            const multiIngredients = window.ProbabilityMultiIngredients?.[masterId];
            if (multiIngredients) {
                const helpers = window.MasterStepCreatorHelpers;
                if (helpers?.formatSelectedIngredientList) {
                    Object.keys(multiIngredients).forEach(varName => {
                        const ingredientList = multiIngredients[varName];
                        if (ingredientList?.length > 0) {
                            const formatted = helpers.formatSelectedIngredientList(ingredientList, getLang() || 'de');
                            if (formatted) merged[varName] = formatted;
                        }
                    });
                }
            }

            return merged;
        }

        // ✅ (2026-04-03): Alte Event-Handler entfernt - werden jetzt vom unified System behandelt!
        // Variable-Klicks: Unified Handler in CreatePostingPage.js (Zeile 4220)
        // Accept/Dismiss: Unten neu definiert (ohne alte rerenderInlineText Logik)

        function bindAcceptDismissEvents() {
            $(document).off('click.probabilityAccept').on('click.probabilityAccept', '.js-probability-accept', async function (event) {
                event.preventDefault();
                event.stopPropagation();
                const button = $(this);
                const wrap = button.closest('.probability-template-wrap');
                const masterId = (button.data('master-id') || wrap.data('master-id') || '').toString();
                if (!masterId || !deps.onAcceptTemplateStep) return;
                await deps.onAcceptTemplateStep(masterId, getMergedVars(masterId));
            });

            $(document).off('click.probabilityDismiss').on('click.probabilityDismiss', '.js-probability-dismiss', function (event) {
                event.preventDefault();
                event.stopPropagation();
                const wrap = $(this).closest('.probability-template-wrap');
                if (!wrap.length) return;
                wrap.slideUp(140, function () { $(this).remove(); });
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
            } catch (_err) {
                setStatus(container, UI_TEXT.analysisUnavailable);
            }
        }

        async function showSuggestions(typeId) {
            state.currentTypeId = typeId || '';
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
            let cards = buildPreferredCards(preferredTemplateIds);

            if (!cards.length) {
                cards = buildFallbackCards(ingredients);
                if (!cards.length) {
                    box.removeClass('d-none').html(`<div class="small text-white-50">${escapeHtml(UI_TEXT.noTemplates)}</div>`);
                    selectedIngredientSuggestionBox.empty();
                    return;
                }
            }

            box.removeClass('d-none').html(cards.join(''));
            selectedIngredientSuggestionBox.empty();
            bindAcceptDismissEvents();
        }

        return {
            refresh,
            showSuggestions
        };
    }

    window.CreatePostingProbability = {
        create,
        buildInlineTemplateText
    };
})(window, window.jQuery);
