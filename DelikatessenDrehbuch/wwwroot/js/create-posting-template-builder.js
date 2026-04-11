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

        const templates = window.MasterStepRenderer.getAllTemplates();
        if (!templates.length) {
            setMasterTemplateError('Keine Templates gefunden. Prüfe /data/master_steps.json.');
            deps.creatorState.selectedTemplateId = '';
            deps.updatePreviewText();
            return;
        }

        if (!deps.creatorState.selectedTemplateId || !templates.some(x => x.master_id === deps.creatorState.selectedTemplateId)) {
            deps.creatorState.selectedTemplateId = templates[0].master_id;
        }

        // Collect ingredient groupIds for affinity scoring
        var ingredientGroupIds = [];
        if (window.CreatePostingIngredientHelpers && typeof window.CreatePostingIngredientHelpers.getSelectedIngredientsForSandbox === 'function') {
            var ings = window.CreatePostingIngredientHelpers.getSelectedIngredientsForSandbox(deps.currentLang());
            var seen = {};
            ings.forEach(function (item) {
                var gid = (item && item.groupId || '').toString();
                if (gid && !seen[gid]) { seen[gid] = true; ingredientGroupIds.push(gid); }
            });
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
