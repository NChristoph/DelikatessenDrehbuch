/**
 * smart-step-init.js
 *
 * Initialisiert den Smart Step Creator
 * Bindet alle Module zusammen und startet das System
 */

(function (window) {
    'use strict';

    const SELECTORS = {
        // Container
        stepList: '#sc2MasterTemplateCards',
        currentStep: '#MasterText',
        acceptedSteps: '#selectedSteps',

        // Filter
        searchInput: '.step-search-input',
        searchClear: '.step-search-clear',
        phaseTabs: '.phase-tab'
    };

    let currentLang = 'de';
    let ingredientGroupIds = [];

    // ============================
    // RENDERING
    // ============================

    function escapeHtml(str) {
        return String(str || '').replace(/[&<>"']/g, m => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[m]));
    }

    /**
     * Rendert Phase-Überschrift
     */
    function renderPhaseHeader(phase, label) {
        const icons = {
            prep: '🔪',
            cook: '🔥',
            finish: '✨'
        };
        const icon = icons[phase] || '';

        return `
            <div class="phase-header d-flex align-items-center gap-2 mb-2 mt-3">
                <span class="phase-icon">${icon}</span>
                <span class="phase-title">${escapeHtml(label)}</span>
            </div>
        `;
    }

    /**
     * Rendert eine Step-Card (moderne Avocado-Version)
     */
    function renderStepCard(step, score = 0) {
        const title = step.title?.[currentLang] || step.title?.de || step.master_id;
        const template = step.templates?.[currentLang] || step.templates?.de || '';

        // Snippet: Erste 80 Zeichen des Templates
        const snippet = template.substring(0, 80) + (template.length > 80 ? '...' : '');

        // Sterne-Anzeige
        const stars = '⭐'.repeat(Math.min(3, Math.max(0, score)));

        return `
            <button type="button"
                    class="template-card-modern"
                    data-master-id="${escapeHtml(step.master_id)}"
                    data-phase="${step.phase || 2}">
                <div class="template-card-icon">🍳</div>
                <div class="template-card-content">
                    <div class="template-card-title">${escapeHtml(title)}</div>
                    <div class="template-card-snippet">${escapeHtml(snippet)}</div>
                </div>
                <div class="template-card-meta">
                    ${stars ? `<div class="template-card-score">${stars}</div>` : ''}
                    <div class="template-card-plus">+</div>
                </div>
            </button>
        `;
    }

    /**
     * Rendert komplette Step-Liste gruppiert nach Phase
     */
    function renderStepList() {
        const container = document.querySelector(SELECTORS.stepList);
        if (!container) return;

        const filtered = window.SmartStepLogic.getFilteredSteps();

        const phaseLabels = {
            prep: { de: 'Vorbereitung', en: 'Preparation' },
            cook: { de: 'Kochen', en: 'Cooking' },
            finish: { de: 'Finishing', en: 'Finishing' }
        };

        let html = '';

        // PREP
        if (filtered.prep && filtered.prep.length > 0) {
            html += renderPhaseHeader('prep', phaseLabels.prep[currentLang] || phaseLabels.prep.de);
            filtered.prep.forEach(step => {
                const score = window.SmartStepLogic.getStepScore(step.master_id, ingredientGroupIds);
                html += renderStepCard(step, score);
            });
        }

        // COOK
        if (filtered.cook && filtered.cook.length > 0) {
            html += renderPhaseHeader('cook', phaseLabels.cook[currentLang] || phaseLabels.cook.de);
            filtered.cook.forEach(step => {
                const score = window.SmartStepLogic.getStepScore(step.master_id, ingredientGroupIds);
                html += renderStepCard(step, score);
            });
        }

        // FINISH
        if (filtered.finish && filtered.finish.length > 0) {
            html += renderPhaseHeader('finish', phaseLabels.finish[currentLang] || phaseLabels.finish.de);
            filtered.finish.forEach(step => {
                const score = window.SmartStepLogic.getStepScore(step.master_id, ingredientGroupIds);
                html += renderStepCard(step, score);
            });
        }

        if (!html) {
            html = '<p class="text-muted text-center mt-4">Keine Steps gefunden.</p>';
        }

        container.innerHTML = html;
        attachStepCardEvents();
    }

    /**
     * Rendert aktuellen Step
     */
    function renderCurrentStep() {
        const container = document.querySelector(SELECTORS.currentStep);
        if (!container) return;

        const current = window.SmartStepLogic.getCurrentStep();
        if (!current) {
            container.innerHTML = '<span class="text-muted">Template auswählen.</span>';
            return;
        }

        // Rendere Template mit grünen Tokens
        const template = current.templateRaw?.[currentLang] || current.templateRaw?.de || '';
        let tokenIndex = 0;

        const html = template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, (match, varName) => {
            const value = current.values[varName] || '';
            const display = value || varName;
            const tokenId = `token_${varName}_${tokenIndex++}`;

            return `<span class="token-highlight placeholder-token template-var token-green"
                           data-var="${escapeHtml(varName)}"
                           data-token-id="${escapeHtml(tokenId)}"
                           style="background: linear-gradient(135deg, #faf6ec, #f5efe1); color: #14391f; padding: 2px 8px; border-radius: 4px; border-bottom: 2px dotted #5fa052; cursor: pointer; font-weight: 600; display: inline-block; margin: 0 2px;">${escapeHtml(display)}</span>`;
        });

        container.innerHTML = html;
        attachCurrentStepEvents();
    }

    /**
     * Rendert akzeptierte Steps
     */
    function renderAcceptedSteps() {
        const container = document.querySelector(SELECTORS.acceptedSteps);
        if (!container) return;

        const steps = window.SmartStepLogic.getAcceptedSteps();
        if (steps.length === 0) {
            container.innerHTML = '<p class="text-muted text-center">Noch keine Steps hinzugefügt.</p>';
            return;
        }

        const html = steps.map((step, index) => {
            const text = window.MasterStepRenderer?.render(step.master_id, step.values, currentLang) || '';
            return `
                <div class="step-row mb-2" data-step-id="${escapeHtml(step.id)}" draggable="true">
                    <div class="d-flex align-items-start gap-2">
                        <div class="step-badge">${index + 1}.</div>
                        <div class="flex-grow-1">
                            <div class="display-step-selected">${escapeHtml(text)}</div>
                        </div>
                        <button type="button"
                                class="btn btn-sm btn-outline-danger btn-remove-step"
                                data-step-id="${escapeHtml(step.id)}">×</button>
                    </div>
                </div>
            `;
        }).join('');

        container.innerHTML = html;
        attachAcceptedStepsEvents();
    }

    // ============================
    // EVENTS
    // ============================

    /**
     * Step-Card Click → Setzt als aktuellen Step
     */
    function attachStepCardEvents() {
        const cards = document.querySelectorAll('.template-card-modern');
        cards.forEach(card => {
            card.addEventListener('click', () => {
                const masterId = card.dataset.masterId;
                if (masterId) {
                    window.SmartStepLogic.setCurrentStep(masterId);
                }
            });
        });
    }

    /**
     * Token Click → Öffnet Modal
     */
    function attachCurrentStepEvents() {
        const tokens = document.querySelectorAll('.token-green');
        tokens.forEach(token => {
            token.addEventListener('click', () => {
                const varName = token.dataset.var;
                if (varName) {
                    openVariableModal(varName);
                }
            });
        });
    }

    /**
     * Remove-Button & Drag & Drop für akzeptierte Steps
     */
    function attachAcceptedStepsEvents() {
        // Remove-Buttons
        const removeButtons = document.querySelectorAll('.btn-remove-step');
        removeButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                const stepId = btn.dataset.stepId;
                if (stepId) {
                    window.SmartStepLogic.removeAcceptedStep(stepId);
                }
            });
        });

        // Drag & Drop
        const stepRows = document.querySelectorAll('[data-step-id]');
        let draggedElement = null;

        stepRows.forEach(row => {
            row.addEventListener('dragstart', (e) => {
                draggedElement = row;
                row.style.opacity = '0.5';
            });

            row.addEventListener('dragend', () => {
                row.style.opacity = '1';
                draggedElement = null;
            });

            row.addEventListener('dragover', (e) => {
                e.preventDefault();
            });

            row.addEventListener('drop', (e) => {
                e.preventDefault();
                if (!draggedElement || draggedElement === row) return;

                const allRows = Array.from(document.querySelectorAll('[data-step-id]'));
                const targetIndex = allRows.indexOf(row);
                const draggedId = draggedElement.dataset.stepId;

                window.SmartStepLogic.moveAcceptedStep(draggedId, targetIndex);
            });
        });
    }

    /**
     * Filter-Events
     */
    function attachFilterEvents() {
        // Suche
        const searchInput = document.querySelector(SELECTORS.searchInput);
        const searchClear = document.querySelector(SELECTORS.searchClear);

        if (searchInput) {
            let timeout;
            searchInput.addEventListener('input', (e) => {
                const query = e.target.value;

                // Clear-Button anzeigen
                if (searchClear) {
                    searchClear.classList.toggle('d-none', !query);
                }

                clearTimeout(timeout);
                timeout = setTimeout(() => {
                    window.SmartStepLogic.setSearchQuery(query);
                }, 300);
            });
        }

        if (searchClear) {
            searchClear.addEventListener('click', () => {
                if (searchInput) {
                    searchInput.value = '';
                    searchClear.classList.add('d-none');
                }
                window.SmartStepLogic.setSearchQuery('');
            });
        }

        // Phase-Tabs
        const phaseTabs = document.querySelectorAll(SELECTORS.phaseTabs);
        phaseTabs.forEach(tab => {
            tab.addEventListener('click', () => {
                // Active-Klasse umschalten
                phaseTabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');

                const phase = tab.dataset.phase;

                // Phase-Filter setzen (1→prep, 2→cook, 3→finish)
                const phaseMap = {
                    'all': 'all',
                    '1': 'prep',
                    '2': 'cook',
                    '3': 'finish',
                    '4': 'finish', // Servieren → Finish
                    'best': 'all' // Beste → Alle (Score-Sortierung später)
                };

                window.SmartStepLogic.setPhaseFilter(phaseMap[phase] || 'all');
            });
        });
    }

    /**
     * Öffnet Modal für Variable
     */
    function openVariableModal(varName) {
        const current = window.SmartStepLogic.getCurrentStep();
        if (!current) return;

        // Nutze bestehendes Modal-System
        if (typeof window.CreatePostingSmartStepCreator?.openUniversalVariableEditor !== 'function') {
            console.error('[SmartStepInit] Modal-System nicht verfügbar');
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
                window.SmartStepLogic.updateCurrentStepVariable(varName, newValue);
            },
            onClose: () => {
                console.log('[SmartStepInit] Modal geschlossen');
            }
        });
    }

    // ============================
    // STATE CHANGE HANDLER
    // ============================

    function onStateChange(state) {
        renderCurrentStep();
        renderAcceptedSteps();
        renderStepList();
    }

    // ============================
    // INITIALISIERUNG
    // ============================

    async function init(options = {}) {
        console.log('[SmartStepInit] Initialisiere Smart Step Creator...');

        currentLang = options.lang || 'de';
        ingredientGroupIds = options.ingredientGroupIds || [];

        // 1. Daten laden
        const loaded = await window.SmartStepData.load();
        if (!loaded) {
            console.error('[SmartStepInit] Konnte Daten nicht laden');
            return false;
        }

        // 2. Logic konfigurieren
        window.SmartStepLogic.setLanguage(currentLang);
        window.SmartStepLogic.onStateChange(onStateChange);

        // 3. Initial-Render
        renderStepList();
        renderCurrentStep();
        renderAcceptedSteps();

        // 4. Events binden
        attachFilterEvents();

        console.log('[SmartStepInit] ✅ Smart Step Creator bereit');
        return true;
    }

    /**
     * Aktualisiert Ingredient-Context (für Stern-Scoring)
     */
    function updateIngredientContext(groupIds) {
        ingredientGroupIds = groupIds || [];
        renderStepList();
    }

    /**
     * Exportiert Steps als JSON
     */
    function exportSteps() {
        return window.SmartStepLogic.serialize();
    }

    /**
     * Lädt Steps aus JSON
     */
    function loadSteps(jsonString) {
        if (window.SmartStepLogic.deserialize(jsonString)) {
            renderAcceptedSteps();
            return true;
        }
        return false;
    }

    // ============================
    // PUBLIC API
    // ============================
    window.SmartStepInit = {
        init,
        updateIngredientContext,
        exportSteps,
        loadSteps,
        renderStepList,
        renderCurrentStep,
        renderAcceptedSteps
    };

    console.log('[SmartStepInit] Modul geladen');

})(window);
