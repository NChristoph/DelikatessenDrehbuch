(function (window) {
    'use strict';

    function setMasterTemplateError(message) {
        const box = window.jQuery('#masterTemplateCards');
        if (!box.length) return;
        box.html(`<div class="small text-warning">${message}</div>`);
    }

    function renderTemplateCards(deps) {
        const box = window.jQuery('#masterTemplateCards');
        box.empty();

        if (!window.MasterStepRenderer || typeof window.MasterStepRenderer.getAllTemplates !== 'function') {
            box.append(`<div class="small ${window.getThemeMutedTextClass()}">Templates werden geladen ...</div>`);
            return;
        }

        const loadError = typeof window.MasterStepRenderer.getLastLoadError === 'function'
            ? window.MasterStepRenderer.getLastLoadError()
            : '';
        if (loadError) {
            setMasterTemplateError(`Template-Fehler: ${loadError}`);
            return;
        }

        var allTemplates = window.MasterStepRenderer.getAllTemplates();
        if (!allTemplates.length) {
            setMasterTemplateError('Keine Templates gefunden. Prüfe /data/master_steps.json.');
            deps.creatorState.selectedTemplateId = '';
            deps.updatePreviewText();
            return;
        }

        // Collect ingredient data for tag filtering + affinity scoring
        var ingredientGroupIds = [];
        var ingredientTags = new Set();
        if (window.CreatePostingIngredientHelpers && typeof window.CreatePostingIngredientHelpers.getSelectedIngredientsForSandbox === 'function') {
            var ings = window.CreatePostingIngredientHelpers.getSelectedIngredientsForSandbox(deps.currentLang());
            var seen = {};
            ings.forEach(function (item) {
                var gid = (item && item.groupId || '').toString();
                if (gid && !seen[gid]) { seen[gid] = true; ingredientGroupIds.push(gid); }
            });
            if (window.MasterStepRenderer.collectIngredientTags) {
                ingredientTags = window.MasterStepRenderer.collectIngredientTags(ings);
            }
        }

        // Filter by ingredient tags
        var templates = allTemplates;
        if (ingredientTags.size && window.MasterStepRenderer.shouldShowStep) {
            templates = allTemplates.filter(function(step) {
                return window.MasterStepRenderer.shouldShowStep(step.master_id, ingredientTags);
            });
        }

        // Sort by affinity score (desc) within same phase
        templates = [...templates].sort(function(a, b) {
            var pa = a.phase ?? 0, pb = b.phase ?? 0;
            if (pa !== pb) return pa - pb;
            var sa = window.MasterStepRenderer.getStepGroupScore ? window.MasterStepRenderer.getStepGroupScore(a.master_id, ingredientGroupIds) : 0;
            var sb = window.MasterStepRenderer.getStepGroupScore ? window.MasterStepRenderer.getStepGroupScore(b.master_id, ingredientGroupIds) : 0;
            if (sa !== sb) return sb - sa;
            return (a.master_id || '').localeCompare(b.master_id || '');
        });

        if (!deps.creatorState.selectedTemplateId || !templates.some(x => x.master_id === deps.creatorState.selectedTemplateId)) {
            deps.creatorState.selectedTemplateId = templates.length ? templates[0].master_id : '';
        }

        templates.forEach((step, index) => {
            const active = step.master_id === deps.creatorState.selectedTemplateId ? 'active' : '';
            const icon = step.categoryIcon || (index % 3 === 0 ? '&#128293;' : index % 3 === 1 ? '&#128298;' : '&#129532;');
            const vars = deps.buildVariablesForTemplate(step.master_id);
            const snippet = window.MasterStepRenderer.render(step.master_id, vars, deps.currentLang()) || step.master_id;
            const title = (step.description || '').toString().trim() || `Template ${index + 1}`;

            // Affinity-Score Badge
            const affScore = window.MasterStepRenderer.getStepGroupScore
                ? window.MasterStepRenderer.getStepGroupScore(step.master_id, ingredientGroupIds)
                : 0;
            const scoreBadge = affScore >= 3 ? '<span class="step-affinity-badge high" title="Sehr relevant">\u2605\u2605\u2605</span>'
                : affScore === 2 ? '<span class="step-affinity-badge medium" title="Relevant">\u2605\u2605</span>'
                : affScore === 1 ? '<span class="step-affinity-badge low" title="M\u00f6glich">\u2605</span>'
                : '';

            box.append(`<button type="button" class="template-card ${active}" data-theme="${window.getCreatePostingTheme()}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title} ${scoreBadge}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
        });

        deps.updatePreviewText();
    }

    function refreshMasterTemplateBuilder(deps) {
        try {
            deps.applyCurrentThemeAttributes();
            deps.applyDerivedIngredientRowVisuals();
            deps.renderIngredientChips();
            renderTemplateCards(deps);
            deps.applyCurrentThemeAttributes();
            deps.updateStoryProgress();
            deps.renderAcceptedRecipeTextCard();
        } catch (error) {
            deps.showMasterStepCreatorError(error, 'refreshMasterTemplateBuilder');
        }
    }

    window.CreatePostingTemplateBuilder = {
        setMasterTemplateError: setMasterTemplateError,
        renderTemplateCards: renderTemplateCards,
        refreshMasterTemplateBuilder: refreshMasterTemplateBuilder
    };
})(window);
