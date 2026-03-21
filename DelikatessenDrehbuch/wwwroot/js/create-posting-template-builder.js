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

        templates.forEach((step, index) => {
            const active = step.master_id === deps.creatorState.selectedTemplateId ? 'active' : '';
            const icon = step.categoryIcon || (index % 3 === 0 ? '&#128293;' : index % 3 === 1 ? '&#128298;' : '&#129532;');
            const vars = deps.buildVariablesForTemplate(step.master_id);
            const snippet = window.MasterStepRenderer.render(step.master_id, vars, deps.currentLang()) || step.master_id;
            const title = (step.description || '').toString().trim() || `Template ${index + 1}`;

            box.append(`<button type="button" class="template-card ${active}" data-theme="${window.getCreatePostingTheme()}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
        });

        deps.updatePreviewText();
        deps.renderSc2TemplateCards();
    }

    function renderSc2TemplateCards(deps) {
        const box = window.jQuery('#sc2MasterTemplateCards');
        if (!box.length) return;

        box.empty();
        if (!window.MasterStepRenderer || typeof window.MasterStepRenderer.getAllTemplates !== 'function') {
            box.append(`<div class="small ${window.getThemeMutedTextClass()}">Templates werden geladen ...</div>`);
            return;
        }

        const loadError = typeof window.MasterStepRenderer.getLastLoadError === 'function'
            ? window.MasterStepRenderer.getLastLoadError()
            : '';
        if (loadError) {
            box.html(`<div class="small text-warning">Template-Fehler: ${loadError}</div>`);
            return;
        }

        const templates = window.MasterStepRenderer.getAllTemplates();
        if (!templates.length) {
            box.html(`<div class="small ${window.getThemeMutedTextClass()}">Keine Templates gefunden.</div>`);
            return;
        }

        templates.forEach((step, index) => {
            const active = step.master_id === deps.creatorState.selectedTemplateId ? 'active' : '';
            const icon = step.categoryIcon || (index % 3 === 0 ? '&#128293;' : index % 3 === 1 ? '&#128298;' : '&#129532;');
            const vars = deps.buildVariablesForTemplate(step.master_id);
            const snippet = window.MasterStepRenderer.render(step.master_id, vars, deps.currentLang()) || step.master_id;
            const title = (step.description || '').toString().trim() || `Template ${index + 1}`;

            box.append(`<button type="button" class="template-card ${active}" data-theme="${window.getCreatePostingTheme()}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
        });
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
        renderSc2TemplateCards: renderSc2TemplateCards,
        refreshMasterTemplateBuilder: refreshMasterTemplateBuilder
    };
})(window);
