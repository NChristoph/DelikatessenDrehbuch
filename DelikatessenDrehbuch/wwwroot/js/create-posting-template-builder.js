(function (window) {
    'use strict';

    // ===== Filter State =====
    let currentSearchQuery = '';
    let currentPhaseFilter = 'all';

    function matchesSearch(step, query) {
        if (!query) return true;
        const q = query.toLowerCase();
        const searchText = [
            step.description || '',
            step.master_id || '',
            step.action || '',
            ...(step.selection_tags || [])
        ].join(' ').toLowerCase();
        return searchText.includes(q);
    }

    function filterTemplates(templates) {
        return templates.filter(step => {
            // Phase filter
            if (currentPhaseFilter !== 'all') {
                const phaseNum = parseInt(currentPhaseFilter, 10);
                if (step.phase !== phaseNum) {
                    return false;
                }
            }
            // Search filter
            if (!matchesSearch(step, currentSearchQuery)) {
                return false;
            }
            return true;
        });
    }

    function setSearchQuery(query) {
        currentSearchQuery = query || '';
    }

    function setPhaseFilter(phase) {
        currentPhaseFilter = phase || 'all';
    }

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

        let templates = window.MasterStepRenderer.getAllTemplates();

        // Apply filters
        const originalLength = templates.length;
        templates = filterTemplates(templates);

        if (!templates.length) {
            if (originalLength > 0) {
                box.html(`<div class="small ${window.getThemeMutedTextClass()}">Keine Steps für diesen Filter gefunden.</div>`);
            } else {
                setMasterTemplateError('Keine Templates gefunden. Prüfe /data/master_steps.json.');
            }
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

        let templates = window.MasterStepRenderer.getAllTemplates();

        // Apply filters
        const originalLength = templates.length;
        templates = filterTemplates(templates);

        if (!templates.length) {
            if (originalLength > 0) {
                box.html(`<div class="small ${window.getThemeMutedTextClass()}">Keine Steps für diesen Filter gefunden.</div>`);
            } else {
                box.html(`<div class="small ${window.getThemeMutedTextClass()}">Keine Templates gefunden.</div>`);
            }
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
        refreshMasterTemplateBuilder: refreshMasterTemplateBuilder,
        setSearchQuery: setSearchQuery,
        setPhaseFilter: setPhaseFilter
    };
})(window);
