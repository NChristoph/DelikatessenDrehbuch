/**
 * smart-step-ui.js
 *
 * UI-Layer für Smart Step Creator
 * Rendering, Events, Modal, Drag & Drop
 */

(function (window) {
    'use strict';

    // ============================
    // KONFIGURATION
    // ============================
    const SELECTORS = {
        container: '#sc2MasterTemplateCards',
        currentStep: '#MasterText',
        acceptedSteps: '#selectedSteps',
        phaseFilter: '.js-phase-filter',
        searchInput: '.js-step-search',
        addButton: '.js-add-current-step',
        modal: '#variableEditorModal'
    };

    const COLORS = {
        tokenGreen: '#90EE90',  // Grün für Variablen-Tokens
        tokenHover: '#7CCD7C'   // Dunkleres Grün beim Hover
    };

    let currentLang = 'de';
    let escapeHtml = window.CreatePostingUtils?.escapeHtml || ((str) => String(str || '').replace(/[&<>"']/g, m => ({'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'}[m])));

    // ============================
    // PHASE-LABELS
    // ============================
    const PHASE_LABELS = {
        prep: { de: 'Vorbereitung', en: 'Preparation', esp: 'Preparación', prt: 'Preparação' },
        cook: { de: 'Kochen', en: 'Cooking', esp: 'Cocinar', prt: 'Cozinhar' },
        finish: { de: 'Fertigstellung', en: 'Finishing', esp: 'Acabado', prt: 'Finalização' }
    };

    function getPhaseLabel(phase) {
        const labels = PHASE_LABELS[phase];
        return labels ? (labels[currentLang] || labels.de) : phase;
    }

    // ============================
    // STERN-RENDERING
    // ============================
    function renderStars(score) {
        const count = Math.min(3, Math.max(0, parseInt(score, 10)));
        if (count === 0) return '';
        return '<span class="step-stars">' + '⭐'.repeat(count) + '</span>';
    }

    // ============================
    // STEP-LISTE RENDERN
    // ============================

    /**
     * Rendert Step-Card
     */
    function renderStepCard(step, score = 0) {
        const title = step.title?.[currentLang] || step.title?.de || step.master_id;
        const stars = renderStars(score);

        return `
            <div class="step-card" data-master-id="${escapeHtml(step.master_id)}">
                <div class="step-card-header">
                    <strong>${escapeHtml(title)}</strong>
                    ${stars}
                </div>
                <div class="step-card-action">${escapeHtml(step.action || '')}</div>
            </div>
        `;
    }

    /**
     * Rendert Phase-Gruppe
     */
    function renderPhaseGroup(phaseName, steps, ingredientGroupIds = []) {
        if (!steps || steps.length === 0) return '';

        const label = getPhaseLabel(phaseName);
        const cardsHtml = steps.map(step => {
            const score = window.SmartStepLogic.getStepScore(step.master_id, ingredientGroupIds);
            return renderStepCard(step, score);
        }).join('');

        return `
            <div class="step-phase-group">
                <h4 class="step-phase-title">${escapeHtml(label)}</h4>
                <div class="step-cards-grid">
                    ${cardsHtml}
                </div>
            </div>
        `;
    }

    /**
     * Rendert komplette Step-Liste
     */
    function renderStepList(ingredientGroupIds = []) {
        const container = document.querySelector(SELECTORS.container);
        if (!container) return;

        const filtered = window.SmartStepLogic.getFilteredSteps();
        const html = [
            renderPhaseGroup('prep', filtered.prep, ingredientGroupIds),
            renderPhaseGroup('cook', filtered.cook, ingredientGroupIds),
            renderPhaseGroup('finish', filtered.finish, ingredientGroupIds)
        ].join('');

        container.innerHTML = html || '<p class="text-muted">Keine Steps gefunden.</p>';
    }

    // ============================
    // AKTUELLER STEP RENDERN
    // ============================

    /**
     * Rendert Template mit GRÜNEN klickbaren Tokens (OHNE Reset-Button!)
     */
    function renderTemplate(masterId, values) {
        const masterStep = window.SmartStepData.findById(masterId);
        if (!masterStep || !masterStep.templates) return '';

        const template = masterStep.templates[currentLang] || masterStep.templates.de || '';
        let tokenIndex = 0;

        // Ersetze {{variable}} durch grüne klickbare Tokens
        return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, (match, varName) => {
            const value = values[varName] || '';
            const display = value || varName;
            const tokenId = `token_${varName}_${tokenIndex++}`;

            return `<span class="token-variable"
                           data-var="${escapeHtml(varName)}"
                           data-token-id="${escapeHtml(tokenId)}"
                           style="background-color: ${COLORS.tokenGreen}; padding: 2px 6px; border-radius: 4px; cursor: pointer; font-weight: 500;"
                           onmouseover="this.style.backgroundColor='${COLORS.tokenHover}'"
                           onmouseout="this.style.backgroundColor='${COLORS.tokenGreen}'">${escapeHtml(display)}</span>`;
        });
    }

    /**
     * Rendert aktuellen Step
     */
    function renderCurrentStep() {
        const container = document.querySelector(SELECTORS.currentStep);
        if (!container) return;

        const current = window.SmartStepLogic.getCurrentStep();
        if (!current) {
            container.innerHTML = '<p class="text-muted">Wähle einen Step aus der Liste aus.</p>';
            return;
        }

        const templateHtml = renderTemplate(current.master_id, current.values);
        const title = current.title?.[currentLang] || current.title?.de || current.master_id;

        container.innerHTML = `
            <div class="current-step-editor">
                <h5>${escapeHtml(title)}</h5>
                <div class="step-template">${templateHtml}</div>
                <button type="button" class="btn btn-primary mt-3 ${SELECTORS.addButton.slice(1)}">
                    + Hinzufügen
                </button>
            </div>
        `;

        attachCurrentStepEvents();
    }

    // ============================
    // AKZEPTIERTE STEPS RENDERN
    // ============================

    /**
     * Rendert einen akzeptierten Step
     */
    function renderAcceptedStep(step, index) {
        const text = window.MasterStepRenderer?.render(step.master_id, step.values, currentLang) || '';

        return `
            <div class="accepted-step" data-step-id="${escapeHtml(step.id)}" draggable="true">
                <span class="step-number">${index + 1}.</span>
                <span class="step-text">${escapeHtml(text)}</span>
                <button type="button" class="btn-remove-step" data-step-id="${escapeHtml(step.id)}">×</button>
            </div>
        `;
    }

    /**
     * Rendert Liste akzeptierter Steps
     */
    function renderAcceptedSteps() {
        const container = document.querySelector(SELECTORS.acceptedSteps);
        if (!container) return;

        const steps = window.SmartStepLogic.getAcceptedSteps();
        if (steps.length === 0) {
            container.innerHTML = '<p class="text-muted">Noch keine Steps hinzugefügt.</p>';
            return;
        }

        container.innerHTML = steps.map((step, index) => renderAcceptedStep(step, index)).join('');
        attachAcceptedStepsEvents();
    }

    // ============================
    // EVENTS - STEP-LISTE
    // ============================

    function attachStepListEvents() {
        const container = document.querySelector(SELECTORS.container);
        if (!container) return;

        // Delegiertes Event für Step-Cards
        container.addEventListener('click', (e) => {
            const card = e.target.closest('.step-card');
            if (!card) return;

            const masterId = card.dataset.masterId;
            if (masterId) {
                window.SmartStepLogic.setCurrentStep(masterId);
            }
        });
    }

    // ============================
    // EVENTS - AKTUELLER STEP
    // ============================

    function attachCurrentStepEvents() {
        const container = document.querySelector(SELECTORS.currentStep);
        if (!container) return;

        // Token-Klick → Modal öffnen
        container.querySelectorAll('.token-variable').forEach(token => {
            token.addEventListener('click', () => {
                const varName = token.dataset.var;
                openVariableModal(varName);
            });
        });

        // Add-Button
        const addBtn = container.querySelector(SELECTORS.addButton);
        if (addBtn) {
            addBtn.addEventListener('click', () => {
                if (window.SmartStepLogic.acceptCurrentStep()) {
                    renderCurrentStep();
                    renderAcceptedSteps();
                }
            });
        }
    }

    // ============================
    // EVENTS - AKZEPTIERTE STEPS
    // ============================

    function attachAcceptedStepsEvents() {
        const container = document.querySelector(SELECTORS.acceptedSteps);
        if (!container) return;

        // Remove-Button
        container.querySelectorAll('.btn-remove-step').forEach(btn => {
            btn.addEventListener('click', () => {
                const stepId = btn.dataset.stepId;
                if (window.SmartStepLogic.removeAcceptedStep(stepId)) {
                    renderAcceptedSteps();
                }
            });
        });

        // Drag & Drop
        attachDragAndDrop(container);
    }

    // ============================
    // DRAG & DROP
    // ============================

    let draggedElement = null;

    function attachDragAndDrop(container) {
        const steps = container.querySelectorAll('.accepted-step');

        steps.forEach(step => {
            step.addEventListener('dragstart', (e) => {
                draggedElement = step;
                step.style.opacity = '0.5';
            });

            step.addEventListener('dragend', () => {
                step.style.opacity = '1';
                draggedElement = null;
            });

            step.addEventListener('dragover', (e) => {
                e.preventDefault();
            });

            step.addEventListener('drop', (e) => {
                e.preventDefault();
                if (!draggedElement || draggedElement === step) return;

                const allSteps = Array.from(container.querySelectorAll('.accepted-step'));
                const draggedIndex = allSteps.indexOf(draggedElement);
                const targetIndex = allSteps.indexOf(step);

                const draggedId = draggedElement.dataset.stepId;
                window.SmartStepLogic.moveAcceptedStep(draggedId, targetIndex);
            });
        });
    }

    // ============================
    // MODAL FÜR VARIABLEN
    // ============================

    function openVariableModal(varName) {
        console.log('[SmartStepUI] Öffne Modal für Variable:', varName);

        const current = window.SmartStepLogic.getCurrentStep();
        if (!current) {
            console.warn('[SmartStepUI] Kein aktueller Step gesetzt');
            return;
        }

        // Nutze bestehendes Modal-System (aus CreatePostingSmartStepCreator.js)
        if (typeof window.CreatePostingSmartStepCreator?.openUniversalVariableEditor !== 'function') {
            console.error('[SmartStepUI] openUniversalVariableEditor nicht verfügbar');
            return;
        }

        const currentValue = current.values[varName] || '';

        window.CreatePostingSmartStepCreator.openUniversalVariableEditor({
            varName: varName,
            currentVal: currentValue,
            masterId: current.master_id,
            context: {
                type: 'step',
                stepId: current.id,
                masterId: current.master_id
            },
            onApply: (newValue) => {
                // Update Variable im Logic-Layer
                window.SmartStepLogic.updateCurrentStepVariable(varName, newValue);
                renderCurrentStep();
            },
            onClose: () => {
                console.log('[SmartStepUI] Modal geschlossen');
            }
        });
    }

    // ============================
    // FILTER-EVENTS
    // ============================

    function attachFilterEvents() {
        // Phase-Filter
        document.querySelectorAll(SELECTORS.phaseFilter).forEach(btn => {
            btn.addEventListener('click', () => {
                const phase = btn.dataset.phase || 'all';
                window.SmartStepLogic.setPhaseFilter(phase);
            });
        });

        // Such-Input
        const searchInput = document.querySelector(SELECTORS.searchInput);
        if (searchInput) {
            let timeout;
            searchInput.addEventListener('input', (e) => {
                clearTimeout(timeout);
                timeout = setTimeout(() => {
                    window.SmartStepLogic.setSearchQuery(e.target.value);
                }, 300);
            });
        }
    }

    // ============================
    // STATE-CHANGE HANDLER
    // ============================

    function onStateChange(state) {
        renderCurrentStep();
        renderAcceptedSteps();
        renderStepList(); // Re-render bei Filter-Änderung
    }

    // ============================
    // INITIALISIERUNG
    // ============================

    async function init(options = {}) {
        console.log('[SmartStepUI] Initialisiere...');

        currentLang = options.lang || 'de';

        // Daten laden
        const loaded = await window.SmartStepData.load();
        if (!loaded) {
            console.error('[SmartStepUI] Konnte Daten nicht laden');
            return false;
        }

        // State-Change-Listener
        window.SmartStepLogic.setLanguage(currentLang);
        window.SmartStepLogic.onStateChange(onStateChange);

        // Initial-Render
        renderStepList(options.ingredientGroupIds || []);
        renderCurrentStep();
        renderAcceptedSteps();

        // Events
        attachStepListEvents();
        attachFilterEvents();

        console.log('[SmartStepUI] ✓ Bereit');
        return true;
    }

    /**
     * Aktualisiert Ingredient-Context für Stern-Scoring
     */
    function updateIngredientContext(ingredientGroupIds) {
        renderStepList(ingredientGroupIds);
    }

    /**
     * Lädt Steps aus JSON (z.B. beim Edit)
     */
    function loadSteps(jsonString) {
        if (window.SmartStepLogic.deserialize(jsonString)) {
            renderAcceptedSteps();
            return true;
        }
        return false;
    }

    /**
     * Exportiert Steps als JSON
     */
    function exportSteps() {
        return window.SmartStepLogic.serialize();
    }

    // ============================
    // PUBLIC API
    // ============================
    window.SmartStepUI = {
        init,
        updateIngredientContext,
        loadSteps,
        exportSteps,
        renderStepList,
        renderCurrentStep,
        renderAcceptedSteps
    };

})(window);
