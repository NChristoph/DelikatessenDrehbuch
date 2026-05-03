/**
 * variable-editor-modal.js
 *
 * Schlankes, effizientes Modal-System für Variable-Editing
 * Ersetzt das alte 800+ Zeilen Modal-System
 */

(function (window) {
    'use strict';

    // ============================
    // CONFIG & STATE
    // ============================
    let currentConfig = null;
    let selectedIngredients = []; // Multi-select für Ingredients
    let selectedArticle = '';
    let selectedFraction = { num: 0, den: 1 }; // Ganzes = 0/1

    const escapeHtml = window.CreatePostingUtils?.escapeHtml || ((str) => String(str || '').replace(/[&<>"']/g, m => ({'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'}[m])));

    // ============================
    // HAUPT-FUNKTIONEN
    // ============================

    /**
     * Öffnet das Variable-Editor Modal
     * @param {object} config - { varName, currentVal, masterId, context, onApply, onClose }
     */
    function open(config) {
        if (!config || !config.varName) {
            console.error('[VariableEditorModal] Missing varName');
            return;
        }

        currentConfig = config;

        // Reset state
        selectedIngredients = [];
        selectedArticle = '';
        selectedFraction = { num: 0, den: 1 };

        // Parse current value
        parseCurrentValue(config.currentVal, config.varName);

        // Create & show overlay
        const html = createOverlayHtml();

        // Remove existing
        const existing = document.getElementById('universalEditorOverlay');
        if (existing) existing.remove();

        // Insert
        document.body.insertAdjacentHTML('beforeend', html);
        document.body.style.overflow = 'hidden';

        // Bind events
        bindEvents();

        // Update preview
        updatePreview();
    }

    /**
     * Schließt das Modal
     */
    function close() {
        const overlay = document.getElementById('universalEditorOverlay');
        if (overlay) overlay.remove();

        document.body.style.overflow = '';

        if (currentConfig && typeof currentConfig.onClose === 'function') {
            currentConfig.onClose();
        }

        currentConfig = null;
    }

    /**
     * Wendet die Auswahl an
     */
    function apply() {
        if (!currentConfig) return;

        const value = buildValueString();

        if (currentConfig.onApply && typeof currentConfig.onApply === 'function') {
            currentConfig.onApply(value);
        }

        close();
    }

    // ============================
    // RENDERING
    // ============================

    /**
     * Erstellt das komplette Overlay HTML
     */
    function createOverlayHtml() {
        const varName = currentConfig.varName;
        const varType = getVariableType(varName);

        return `
            <div id="universalEditorOverlay" class="smart-step-creator" data-theme="navy"
                 style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 9999; display: flex; flex-direction: column;">

                <!-- Header: Preview -->
                <div class="creator-preview-canvas" style="flex-shrink: 0; padding: 16px; border-bottom: 1px solid rgba(20, 57, 31, 0.12); background: #faf6ec;">
                    <div style="max-width: 800px; margin: 0 auto;">
                        <div class="current-step-wrap">
                            <div class="current-step-header d-flex justify-content-between align-items-center">
                                <div class="preview-step-title mb-0">${escapeHtml(getVariableLabel(varName))}</div>
                            </div>
                            <div class="preview-step-text mt-2" id="ModalPreviewText" style="color: #14391f;">
                                <span class="text-muted">Auswahl treffen...</span>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Content: Editor -->
                <div id="universalEditorContent" style="flex: 1; overflow-y: auto; padding: 24px 16px; background: white;">
                    <div style="max-width: 800px; margin: 0 auto;">
                        ${renderEditor(varType)}
                    </div>
                </div>

                <!-- Footer: Buttons -->
                <div style="flex-shrink: 0; padding: 16px; border-top: 1px solid rgba(20, 57, 31, 0.12); background: #faf6ec;">
                    <div style="max-width: 800px; margin: 0 auto;">
                        <div class="d-flex gap-2">
                            <button type="button" class="btn btn-sm creator-cta-primary" id="BtnApplyVar">Einsetzen</button>
                            <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVar">Schließen</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Rendert den Editor basierend auf Variablen-Typ
     */
    function renderEditor(varType) {
        switch (varType) {
            case 'ingredient':
            case 'ingredients':
                return renderIngredientEditor();
            case 'duration':
                return renderDurationEditor();
            case 'temperature':
            case 'temp':
                return renderTemperatureEditor();
            default:
                return renderOptionsEditor();
        }
    }

    /**
     * Ingredient-Editor (mit Artikel, Fraction, Multi-Select)
     */
    function renderIngredientEditor() {
        const ingredients = getPageIngredients();
        const articles = ['ohne', 'der', 'die', 'das', 'den', 'dem', 'des', 'einen', 'einem', 'einer'];
        const fractions = [
            { key: '', label: 'Ganzes', num: 0, den: 1 },
            { key: '1/2', label: 'die Hälfte', num: 1, den: 2 },
            { key: '1/3', label: 'ein Drittel', num: 1, den: 3 },
            { key: '2/3', label: 'zwei Drittel', num: 2, den: 3 },
            { key: '1/4', label: 'ein Viertel', num: 1, den: 4 },
            { key: '3/4', label: 'drei Viertel', num: 3, den: 4 }
        ];

        return `
            <div class="mb-3">
                <div class="small text-muted mb-2"><strong>Ausgewählte Zutaten:</strong></div>
                <div id="SelectedIngredientsList" class="mb-3">
                    <span class="text-muted small">Noch keine Zutaten ausgewählt.</span>
                </div>
            </div>

            <div class="mb-3">
                <div class="small text-muted mb-2">Menge</div>
                <div class="d-flex flex-wrap gap-2">
                    ${fractions.map(f => `
                        <button type="button" class="btn btn-sm btn-outline-light fraction-pick ${f.num === 0 ? 'active' : ''}"
                                data-num="${f.num}" data-den="${f.den}">
                            ${f.key ? f.key + ' ' : ''}${escapeHtml(f.label)}
                        </button>
                    `).join('')}
                </div>
            </div>

            <div class="mb-3">
                <div class="small text-muted mb-2">Artikel</div>
                <div class="d-flex flex-wrap gap-2">
                    ${articles.map(art => `
                        <button type="button" class="btn btn-sm btn-outline-light article-pick"
                                data-article="${escapeHtml(art)}">
                            ${escapeHtml(art)}
                        </button>
                    `).join('')}
                </div>
            </div>

            <div class="mb-3">
                <div class="small text-muted mb-2">Zutat auswählen</div>
                <div class="d-flex flex-wrap gap-2">
                    ${ingredients.map(ing => `
                        <button type="button" class="btn btn-sm btn-outline-light ingredient-pick"
                                data-ingredient-id="${ing.id}"
                                data-ingredient-name="${escapeHtml(ing.name)}">
                            ${escapeHtml(ing.name)}
                        </button>
                    `).join('')}
                </div>
            </div>
        `;
    }

    /**
     * Duration-Editor (Zahl + Einheit)
     */
    function renderDurationEditor() {
        const units = [
            { key: 'minute', label: 'Minuten' },
            { key: 'hour', label: 'Stunden' },
            { key: 'short', label: 'kurz' },
            { key: 'per_package', label: 'laut Packungsanweisung' }
        ];

        return `
            <div class="mb-3">
                <label class="form-label small text-muted">Dauer eingeben</label>
                <input type="number" class="form-control" id="DurationInput" placeholder="z.B. 15" min="1">
            </div>
            <div class="mb-3">
                <div class="small text-muted mb-2">Einheit</div>
                <div class="d-flex flex-wrap gap-2">
                    ${units.map((unit, idx) => `
                        <button type="button" class="btn btn-sm btn-outline-light duration-unit-pick ${idx === 0 ? 'active' : ''}"
                                data-unit="${escapeHtml(unit.key)}">
                            ${escapeHtml(unit.label)}
                        </button>
                    `).join('')}
                </div>
            </div>
        `;
    }

    /**
     * Temperature-Editor
     */
    function renderTemperatureEditor() {
        const presets = ['160°C', '180°C', '200°C', '220°C', '240°C'];

        return `
            <div class="mb-3">
                <label class="form-label small text-muted">Temperatur eingeben</label>
                <input type="text" class="form-control" id="TempInput" placeholder="z.B. 180°C">
            </div>
            <div class="mb-3">
                <div class="small text-muted mb-2">Vorschläge</div>
                <div class="d-flex flex-wrap gap-2">
                    ${presets.map(temp => `
                        <button type="button" class="btn btn-sm btn-outline-light temp-preset"
                                data-value="${escapeHtml(temp)}">
                            ${escapeHtml(temp)}
                        </button>
                    `).join('')}
                </div>
            </div>
        `;
    }

    /**
     * Generischer Options-Editor (für alle anderen Variablen)
     */
    function renderOptionsEditor() {
        const options = getVariableOptions(currentConfig.varName);

        return `
            <div class="mb-3">
                <div class="small text-muted mb-2">Option auswählen</div>
                <div class="d-flex flex-wrap gap-2">
                    ${options.map(opt => `
                        <button type="button" class="btn btn-sm btn-outline-light option-pick"
                                data-value="${escapeHtml(opt)}">
                            ${escapeHtml(opt)}
                        </button>
                    `).join('')}
                </div>
            </div>
            <div class="mb-3">
                <label class="form-label small text-muted">Oder eigenen Text eingeben</label>
                <input type="text" class="form-control" id="CustomInput" placeholder="Eigener Wert...">
            </div>
        `;
    }

    // ============================
    // EVENT-HANDLING
    // ============================

    function bindEvents() {
        const overlay = document.getElementById('universalEditorOverlay');
        if (!overlay) return;

        // Close & Apply Buttons
        overlay.querySelector('#BtnCloseVar')?.addEventListener('click', close);
        overlay.querySelector('#BtnApplyVar')?.addEventListener('click', apply);

        // Ingredient-Editor Events
        overlay.querySelectorAll('.ingredient-pick').forEach(btn => {
            btn.addEventListener('click', () => {
                const id = btn.dataset.ingredientId;
                const name = btn.dataset.ingredientName;
                toggleIngredientSelection(id, name, btn);
            });
        });

        overlay.querySelectorAll('.article-pick').forEach(btn => {
            btn.addEventListener('click', () => {
                overlay.querySelectorAll('.article-pick').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                selectedArticle = btn.dataset.article;
                updatePreview();
            });
        });

        overlay.querySelectorAll('.fraction-pick').forEach(btn => {
            btn.addEventListener('click', () => {
                overlay.querySelectorAll('.fraction-pick').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                selectedFraction = {
                    num: parseInt(btn.dataset.num, 10),
                    den: parseInt(btn.dataset.den, 10)
                };
                updatePreview();
            });
        });

        // Duration-Editor Events
        const durationInput = overlay.querySelector('#DurationInput');
        if (durationInput) {
            durationInput.addEventListener('input', updatePreview);
        }

        overlay.querySelectorAll('.duration-unit-pick').forEach(btn => {
            btn.addEventListener('click', () => {
                overlay.querySelectorAll('.duration-unit-pick').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                updatePreview();
            });
        });

        // Temperature-Editor Events
        const tempInput = overlay.querySelector('#TempInput');
        if (tempInput) {
            tempInput.addEventListener('input', updatePreview);
        }

        overlay.querySelectorAll('.temp-preset').forEach(btn => {
            btn.addEventListener('click', () => {
                if (tempInput) tempInput.value = btn.dataset.value;
                updatePreview();
            });
        });

        // Options-Editor Events
        overlay.querySelectorAll('.option-pick').forEach(btn => {
            btn.addEventListener('click', () => {
                const customInput = overlay.querySelector('#CustomInput');
                if (customInput) customInput.value = btn.dataset.value;
                updatePreview();
            });
        });

        const customInput = overlay.querySelector('#CustomInput');
        if (customInput) {
            customInput.addEventListener('input', updatePreview);
        }
    }

    /**
     * Toggle Ingredient Multi-Select
     */
    function toggleIngredientSelection(id, name, btn) {
        const index = selectedIngredients.findIndex(ing => ing.id === id);

        if (index >= 0) {
            // Remove
            selectedIngredients.splice(index, 1);
            btn.classList.remove('active');
        } else {
            // Add
            selectedIngredients.push({ id, name });
            btn.classList.add('active');
        }

        updateSelectedIngredientsList();
        updatePreview();
    }

    /**
     * Update Selected Ingredients Display
     */
    function updateSelectedIngredientsList() {
        const container = document.getElementById('SelectedIngredientsList');
        if (!container) return;

        if (selectedIngredients.length === 0) {
            container.innerHTML = '<span class="text-muted small">Noch keine Zutaten ausgewählt.</span>';
            return;
        }

        container.innerHTML = selectedIngredients.map(ing => `
            <span class="badge bg-success me-1">${escapeHtml(ing.name)}</span>
        `).join('');
    }

    /**
     * Update Preview im Header
     */
    function updatePreview() {
        const preview = document.getElementById('ModalPreviewText');
        if (!preview) return;

        const value = buildValueString();

        if (value) {
            preview.innerHTML = `<span style="color: #14391f; font-weight: 600;">${escapeHtml(value)}</span>`;
        } else {
            preview.innerHTML = '<span class="text-muted">Auswahl treffen...</span>';
        }
    }

    // ============================
    // HELPERS
    // ============================

    /**
     * Baut den finalen Wert-String zusammen
     */
    function buildValueString() {
        const varType = getVariableType(currentConfig.varName);

        switch (varType) {
            case 'ingredient':
            case 'ingredients': {
                if (selectedIngredients.length === 0) return '';

                const names = selectedIngredients.map(ing => ing.name).join(', ');
                let result = names;

                // Add article
                if (selectedArticle && selectedArticle !== 'ohne') {
                    result = selectedArticle + ' ' + result;
                }

                // Add fraction
                if (selectedFraction.num > 0) {
                    const fractionLabel = getFractionLabel(selectedFraction.num, selectedFraction.den);
                    result = fractionLabel + ' ' + result;
                }

                return result;
            }

            case 'duration': {
                const input = document.getElementById('DurationInput');
                const unitBtn = document.querySelector('.duration-unit-pick.active');
                if (!input || !input.value) return '';

                const num = input.value;
                const unit = unitBtn?.dataset.unit || 'minute';
                const unitLabels = {
                    minute: 'Minuten',
                    hour: 'Stunden',
                    short: 'kurz',
                    per_package: 'laut Packungsanweisung'
                };

                return unit === 'per_package' ? unitLabels[unit] : `${num} ${unitLabels[unit] || ''}`;
            }

            case 'temperature':
            case 'temp': {
                const input = document.getElementById('TempInput');
                return input ? input.value : '';
            }

            default: {
                const customInput = document.getElementById('CustomInput');
                return customInput ? customInput.value : '';
            }
        }
    }

    /**
     * Parst den aktuellen Wert (beim Öffnen)
     */
    function parseCurrentValue(value, varName) {
        if (!value) return;

        const varType = getVariableType(varName);

        switch (varType) {
            case 'duration': {
                // Parse "15 Minuten" → input=15, unit=minute
                const match = value.match(/^(\d+)\s*(.*)$/);
                if (match) {
                    setTimeout(() => {
                        const input = document.getElementById('DurationInput');
                        if (input) input.value = match[1];
                    }, 100);
                }
                break;
            }

            case 'temperature':
            case 'temp': {
                setTimeout(() => {
                    const input = document.getElementById('TempInput');
                    if (input) input.value = value;
                }, 100);
                break;
            }

            default: {
                setTimeout(() => {
                    const input = document.getElementById('CustomInput');
                    if (input) input.value = value;
                }, 100);
                break;
            }
        }
    }

    /**
     * Gibt den Variablen-Typ zurück
     */
    function getVariableType(varName) {
        const name = (varName || '').toLowerCase();

        if (['ingredient', 'ingredients', 'ingredient2', 'liquid', 'fat', 'seasonings', 'marinade', 'thickener', 'components', 'extra', 'base', 'dough'].includes(name)) {
            return 'ingredient';
        }
        if (['duration', 'time'].includes(name)) {
            return 'duration';
        }
        if (['temp', 'temperature'].includes(name)) {
            return 'temperature';
        }

        return 'option';
    }

    /**
     * Gibt Label für Variable zurück
     */
    function getVariableLabel(varName) {
        const labels = {
            ingredient: 'Zutat auswählen',
            ingredients: 'Zutaten auswählen',
            duration: 'Dauer einstellen',
            temp: 'Temperatur einstellen',
            temperature: 'Temperatur einstellen',
            shape: 'Form auswählen',
            state: 'Zustand auswählen',
            equipment: 'Gerät auswählen',
            tool: 'Werkzeug auswählen'
        };

        return labels[varName.toLowerCase()] || varName;
    }

    /**
     * Gibt Optionen für Variable zurück
     */
    function getVariableOptions(varName) {
        // Nutze MasterStepRenderer wenn verfügbar
        if (window.MasterStepRenderer && typeof window.MasterStepRenderer.getVariablePresets === 'function') {
            const presets = window.MasterStepRenderer.getVariablePresets(varName, 'de');
            if (presets && presets.length) return presets;
        }

        // Fallback
        const fallbacks = {
            shape: ['Würfel', 'Scheiben', 'Streifen', 'Ringe', 'gehackt', 'fein gehackt'],
            state: ['weich', 'bissfest', 'zart', 'knusprig', 'goldbraun'],
            heat: ['niedrig', 'mittel', 'hoch', 'sehr hoch'],
            mode: ['Ober-/Unterhitze', 'Umluft', 'Grill', 'Heißluft']
        };

        return fallbacks[varName.toLowerCase()] || [];
    }

    /**
     * Gibt Zutaten von der Seite zurück
     */
    function getPageIngredients() {
        // Versuche vom CreatePostingPageData zu holen
        if (window.CreatePostingPageData && typeof window.CreatePostingPageData.getIngredients === 'function') {
            return window.CreatePostingPageData.getIngredients();
        }

        // Fallback: Leere Liste
        return [];
    }

    /**
     * Gibt Fraction-Label zurück
     */
    function getFractionLabel(num, den) {
        const labels = {
            '1/2': 'die Hälfte',
            '1/3': 'ein Drittel',
            '2/3': 'zwei Drittel',
            '1/4': 'ein Viertel',
            '3/4': 'drei Viertel'
        };
        return labels[`${num}/${den}`] || `${num}/${den}`;
    }

    // ============================
    // PUBLIC API
    // ============================
    window.VariableEditorModal = {
        open,
        close
    };

    // Legacy-Support: Alte Funktionen weiterleiten
    window.CreatePostingSmartStepCreator = window.CreatePostingSmartStepCreator || {};
    window.CreatePostingSmartStepCreator.openUniversalVariableEditor = open;
    window.CreatePostingSmartStepCreator.closeUnifiedOverlay = close;

    console.log('[VariableEditorModal] Schlankes Modal-System geladen');

})(window);
