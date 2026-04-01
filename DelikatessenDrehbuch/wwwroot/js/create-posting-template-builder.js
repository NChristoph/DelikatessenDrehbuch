(function (window) {
    'use strict';

    // ✅ State für Filterung
    let currentSearchQuery = '';
    let currentPhaseFilter = 'all'; // 'all', 1, 2, 3, 4

    // ✅ Icon-Mapping basierend auf action/phase
    const ACTION_ICONS = {
        'cut': '🔪',
        'chop': '🔪',
        'dice': '🔪',
        'grate': '🧀',
        'peel': '🥕',
        'sear': '🔥',
        'fry': '🍳',
        'roast': '🔥',
        'braise': '🫕',
        'simmer': '🥘',
        'boil': '💧',
        'bake': '🔥',
        'grill': '🔥',
        'deglaze': '🍷',
        'mix': '🥄',
        'whisk': '🥄',
        'stir': '🥄',
        'fold': '🥄',
        'knead': '👐',
        'season': '🧂',
        'sauce': '🥫',
        'serve': '🍽️',
        'baste': '🥄',
        'grill_finish': '🔥',
        'form_dumplings': '🥟',
        'simmer_dumplings': '🥟',
        'cure': '🧂'
    };

    const PHASE_ICONS = {
        0: '📋', // Basis
        1: '🔪', // Vorbereitung
        2: '🔥', // Kochen
        3: '✨', // Finishing
        4: '🍽️'  // Servieren
    };

    function getStepIcon(step) {
        const action = (step.action || '').toLowerCase();
        return ACTION_ICONS[action] || PHASE_ICONS[step.phase] || '📝';
    }

    function getPhaseBadge(phase) {
        const labels = {
            0: 'Basis',
            1: 'Vorbereitung',
            2: 'Kochen',
            3: 'Finishing',
            4: 'Servieren'
        };
        const label = labels[phase] || '';
        return label ? `<span class="phase-badge phase-${phase}">${label}</span>` : '';
    }

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
            // Phase-Filter
            if (currentPhaseFilter !== 'all' && step.phase !== currentPhaseFilter) {
                return false;
            }
            // Such-Filter
            if (!matchesSearch(step, currentSearchQuery)) {
                return false;
            }
            return true;
        });
    }

    function setSearchQuery(query) {
        currentSearchQuery = (query || '').trim();
    }

    function setPhaseFilter(phase) {
        currentPhaseFilter = phase;
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
        if (!templates.length) {
            setMasterTemplateError('Keine Templates gefunden. Prüfe /data/master_steps.json.');
            deps.creatorState.selectedTemplateId = '';
            deps.updatePreviewText();
            return;
        }

        // ✅ Filterung anwenden
        templates = filterTemplates(templates);

        if (!templates.length) {
            box.html(`<div class="small ${window.getThemeMutedTextClass()}">Keine Steps gefunden für diesen Filter.</div>`);
            return;
        }

        if (!deps.creatorState.selectedTemplateId || !templates.some(x => x.master_id === deps.creatorState.selectedTemplateId)) {
            deps.creatorState.selectedTemplateId = templates[0].master_id;
        }

        templates.forEach((step, index) => {
            const active = step.master_id === deps.creatorState.selectedTemplateId ? 'active' : '';
            const icon = getStepIcon(step); // ✅ Neues Icon-System
            const vars = deps.buildVariablesForTemplate(step.master_id);
            const snippet = window.MasterStepRenderer.render(step.master_id, vars, deps.currentLang()) || step.master_id;
            const title = (step.description || '').toString().trim() || `Template ${index + 1}`;
            const phaseLabel = getPhaseBadge(step.phase); // ✅ Phase-Badge

            box.append(`<button type="button" class="template-card ${active}" data-theme="${window.getCreatePostingTheme()}" data-id="${step.master_id}" data-title="${title}" data-phase="${step.phase}">
                    <div class="template-title">${icon} ${title} ${phaseLabel}</div>
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
