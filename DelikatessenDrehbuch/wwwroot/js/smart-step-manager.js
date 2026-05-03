/**
 * smart-step-manager.js
 *
 * Dediziertes Modul für das Verwalten von Smart Steps im CreatePosting-Bereich.
 *
 * Verantwortlichkeiten:
 * - Laden und Cachen der Master Steps aus JSON
 * - Verwalten der aktuellen Step-Liste (hinzufügen, löschen, neu ordnen)
 * - Step-Vorschläge basierend auf Zutaten und Kontext generieren
 * - Step-Rendering-Logik (JSON Template → HTML)
 * - Schnittstelle zu MasterStepRenderer für Template-Rendering
 *
 * Abhängigkeiten:
 * - window.MasterStepRenderer (für Template-Rendering und Smart Defaults)
 * - window.CreatePostingDataStore (für JSON-Laden)
 * - window.CreatePostingUtils (für escapeHtml, etc.)
 */

(function (window) {
    'use strict';

    // =============================
    // KONSTANTEN
    // =============================
    const DEFAULT_LANG = 'de';
    const MIN_SUGGESTION_SCORE = 10; // Minimaler Score für Step-Vorschläge

    // =============================
    // INTERNER STATE
    // =============================
    let masterSteps = [];                    // Alle verfügbaren Master Steps aus JSON
    let acceptedSteps = [];                  // Aktuell vom User akzeptierte Steps
    let suggestedSteps = [];                 // Vorgeschlagene Steps basierend auf Kontext
    let currentLang = DEFAULT_LANG;          // Aktuelle Sprache
    let ingredientTagSet = new Set();        // Gesammelte Ingredient-Tags für Filterung
    let recipeTypes = [];                    // Erkannte Rezepttypen für Kontext

    // Step-Filter-State
    let currentPhaseFilter = 'all';          // 'all' | 'prep' | 'cook' | 'finish'
    let currentSearchQuery = '';             // Suchbegriff für Steps

    // Callbacks für externe UI-Updates
    let onStepsChanged = null;               // Callback wenn acceptedSteps sich ändert
    let onSuggestionsChanged = null;         // Callback wenn suggestedSteps sich ändert

    // =============================
    // HILFSFUNKTIONEN
    // =============================

    /**
     * Sicheres HTML-Escaping
     */
    function escapeHtml(str) {
        return window.CreatePostingUtils && window.CreatePostingUtils.escapeHtml
            ? window.CreatePostingUtils.escapeHtml(str)
            : String(str || '').replace(/[&<>"']/g, m => ({
                '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
            }[m]));
    }

    /**
     * Generiert eindeutige IDs
     */
    function generateId(prefix = 'step') {
        return `${prefix}_${Math.random().toString(36).substr(2, 9)}_${Date.now()}`;
    }

    /**
     * Deep Clone eines Objekts
     */
    function deepClone(obj) {
        if (obj === null || typeof obj !== 'object') return obj;
        if (Array.isArray(obj)) return obj.map(deepClone);
        const cloned = {};
        Object.keys(obj).forEach(key => {
            cloned[key] = deepClone(obj[key]);
        });
        return cloned;
    }

    // =============================
    // JSON-DATEN LADEN
    // =============================

    /**
     * Lädt die Master Steps aus JSON via CreatePostingDataStore
     * @returns {Promise<boolean>} True wenn erfolgreich geladen
     */
    async function loadMasterSteps() {
        try {
            console.log('[SmartStepManager] Lade Master Steps...');

            // Nutze MasterStepRenderer's load() für konsistente Datenquelle
            if (window.MasterStepRenderer && typeof window.MasterStepRenderer.load === 'function') {
                await window.MasterStepRenderer.load();
                masterSteps = window.MasterStepRenderer.getAllTemplates() || [];
            } else if (window.CreatePostingDataStore && typeof window.CreatePostingDataStore.load === 'function') {
                const data = await window.CreatePostingDataStore.load('masterSteps');
                masterSteps = (data && Array.isArray(data.master_steps)) ? data.master_steps : [];
            } else {
                console.error('[SmartStepManager] Keine Datenquelle verfügbar (MasterStepRenderer oder CreatePostingDataStore)');
                return false;
            }

            console.log(`[SmartStepManager] ${masterSteps.length} Master Steps geladen`);
            return true;

        } catch (error) {
            console.error('[SmartStepManager] Fehler beim Laden der Master Steps:', error);
            return false;
        }
    }

    // =============================
    // STEP-SUCHE & FILTERUNG
    // =============================

    /**
     * Findet einen Master Step anhand seiner ID
     * @param {string} masterId - Die master_id des gesuchten Steps
     * @returns {object|null} Der gefundene Step oder null
     */
    function findMasterStep(masterId) {
        if (!masterId) return null;
        return masterSteps.find(step => step && step.master_id === masterId) || null;
    }

    /**
     * Filtert Master Steps nach Phase
     * @param {string} phase - 'all' | 'prep' | 'cook' | 'finish'
     * @returns {Array} Gefilterte Steps
     */
    function filterStepsByPhase(phase) {
        if (phase === 'all') return [...masterSteps];

        const phaseMap = {
            prep: 1,
            cook: 2,
            finish: 3
        };

        const targetPhase = phaseMap[phase];
        if (!targetPhase) return [...masterSteps];

        return masterSteps.filter(step => {
            const stepPhase = parseInt(step.phase, 10) || 0;
            return stepPhase === targetPhase;
        });
    }

    /**
     * Filtert Steps nach Suchbegriff (durchsucht Titel, Action, Tags)
     * @param {string} query - Suchbegriff
     * @param {Array} steps - Zu durchsuchende Steps (optional, default: masterSteps)
     * @returns {Array} Gefilterte Steps
     */
    function searchSteps(query, steps = null) {
        const source = steps || masterSteps;
        if (!query || !query.trim()) return [...source];

        const searchTerm = query.toLowerCase().trim();

        return source.filter(step => {
            if (!step) return false;

            // Durchsuche Titel (mehrsprachig)
            if (step.title) {
                const titleMatch = Object.values(step.title).some(title =>
                    (title || '').toLowerCase().includes(searchTerm)
                );
                if (titleMatch) return true;
            }

            // Durchsuche Action
            if (step.action && step.action.toLowerCase().includes(searchTerm)) {
                return true;
            }

            // Durchsuche Tags
            if (Array.isArray(step.tags)) {
                const tagMatch = step.tags.some(tag =>
                    (tag || '').toLowerCase().includes(searchTerm)
                );
                if (tagMatch) return true;
            }

            return false;
        });
    }

    /**
     * Filtert Steps basierend auf Ingredient-Tags (z.B. is_peelable, is_cuttable)
     * @param {Array} steps - Zu filternde Steps
     * @param {Set} tagSet - Set von verfügbaren Tags
     * @returns {Array} Gefilterte und sortierte Steps
     */
    function filterStepsByIngredientTags(steps, tagSet) {
        if (!tagSet || tagSet.size === 0) return steps;

        return steps.filter(step => {
            if (!step || !step.required_ingredient_tags) return true;

            const requiredTags = Array.isArray(step.required_ingredient_tags)
                ? step.required_ingredient_tags
                : [];

            if (requiredTags.length === 0) return true;

            // Step wird angezeigt, wenn mindestens ein required tag vorhanden ist
            return requiredTags.some(tag => tagSet.has(tag));
        });
    }

    /**
     * Kombinierte Filterung: Phase + Suche + Ingredient Tags
     * @returns {Array} Gefilterte Steps
     */
    function getFilteredMasterSteps() {
        let filtered = masterSteps;

        // 1. Phase-Filter
        if (currentPhaseFilter !== 'all') {
            filtered = filterStepsByPhase(currentPhaseFilter);
        }

        // 2. Suchbegriff-Filter
        if (currentSearchQuery) {
            filtered = searchSteps(currentSearchQuery, filtered);
        }

        // 3. Ingredient-Tag-Filter
        if (ingredientTagSet.size > 0) {
            filtered = filterStepsByIngredientTags(filtered, ingredientTagSet);
        }

        return filtered;
    }

    // =============================
    // STEP-VORSCHLÄGE (SUGGESTIONS)
    // =============================

    /**
     * Berechnet einen Score für einen Step basierend auf Kontext
     * @param {object} step - Der zu bewertende Step
     * @param {object} context - Kontext-Objekt mit ingredients, recipeTypes, etc.
     * @returns {number} Score (höher = besser geeignet)
     */
    function calculateStepScore(step, context) {
        if (!step) return 0;

        let score = 0;
        const ctx = context || {};
        const ingredients = ctx.ingredients || [];
        const types = ctx.recipeTypes || recipeTypes || [];

        // +++ Ingredient-Matching +++
        // Steps mit "ingredient" Variable bevorzugen wenn Zutaten vorhanden
        if (ingredients.length > 0 && Array.isArray(step.variables)) {
            if (step.variables.includes('ingredient') || step.variables.includes('ingredients')) {
                score += 20;
            }
        }

        // +++ Recipe-Type-Matching +++
        // Wenn Step-Tags mit Rezepttypen übereinstimmen
        if (types.length > 0 && Array.isArray(step.tags)) {
            const typeMatch = step.tags.some(tag => {
                const tagLower = (tag || '').toLowerCase();
                return types.some(type => {
                    const typeLower = (type || '').toLowerCase();
                    return tagLower.includes(typeLower) || typeLower.includes(tagLower);
                });
            });
            if (typeMatch) {
                score += 30;
            }
        }

        // +++ Group Affinity Score (aus step_group_affinities.json) +++
        if (window.MasterStepRenderer && typeof window.MasterStepRenderer.getStepGroupScore === 'function') {
            const groupIds = ingredients
                .map(ing => ing.groupId)
                .filter(Boolean);

            if (groupIds.length > 0) {
                const groupScore = window.MasterStepRenderer.getStepGroupScore(step.master_id, groupIds);
                score += groupScore * 15; // Gewichtung: Score 1-3 → +15 bis +45
            }
        }

        // +++ Ingredient-Tag-Matching +++
        if (ingredientTagSet.size > 0 && Array.isArray(step.required_ingredient_tags)) {
            const matchedTags = step.required_ingredient_tags.filter(tag => ingredientTagSet.has(tag));
            score += matchedTags.length * 10;
        }

        // +++ Phase-Bonus (frühe Phasen leicht bevorzugen) +++
        const phase = parseInt(step.phase, 10) || 2;
        if (phase === 1) score += 5;  // PREP leicht bevorzugen

        return score;
    }

    /**
     * Generiert Step-Vorschläge basierend auf aktuellem Kontext
     * @param {object} context - Kontext mit ingredients, recipeTypes, etc.
     * @returns {Array} Sortierte Liste von vorgeschlagenen Steps
     */
    function generateStepSuggestions(context = {}) {
        console.log('[SmartStepManager] Generiere Step-Vorschläge...', context);

        const ctx = context || {};
        const maxSuggestions = ctx.limit || 10;

        // Alle Master Steps bewerten
        const scored = masterSteps.map(step => ({
            step: step,
            score: calculateStepScore(step, ctx)
        }));

        // Nach Score sortieren (absteigend) und filtern
        const suggestions = scored
            .filter(item => item.score >= MIN_SUGGESTION_SCORE)
            .sort((a, b) => {
                // Primär: Score
                if (b.score !== a.score) return b.score - a.score;
                // Sekundär: Phase (PREP → COOK → FINISH)
                const phaseA = parseInt(a.step.phase, 10) || 2;
                const phaseB = parseInt(b.step.phase, 10) || 2;
                if (phaseA !== phaseB) return phaseA - phaseB;
                // Tertiär: Alphabetisch nach master_id
                return (a.step.master_id || '').localeCompare(b.step.master_id || '');
            })
            .slice(0, maxSuggestions)
            .map(item => item.step);

        suggestedSteps = suggestions;

        console.log(`[SmartStepManager] ${suggestions.length} Vorschläge generiert`);

        // Trigger Callback
        if (typeof onSuggestionsChanged === 'function') {
            onSuggestionsChanged(suggestedSteps);
        }

        return suggestions;
    }

    /**
     * Legacy-Wrapper für Ingredient-basierte Vorschläge
     * @param {string} ingredientName - Name der Zutat
     * @returns {Array} Vorgeschlagene Steps
     */
    function suggestStepsForIngredient(ingredientName) {
        const name = (ingredientName || '').toString().toLowerCase().trim();
        if (!name) return [];

        // Nutze MasterStepRenderer's eingebaute Logik wenn verfügbar
        if (window.MasterStepRenderer && typeof window.MasterStepRenderer.suggestForIngredient === 'function') {
            return window.MasterStepRenderer.suggestForIngredient(ingredientName);
        }

        // Fallback: Einfache Filterung nach häufigen Zutat-Terms
        return generateStepSuggestions({
            ingredients: [{ name: ingredientName }],
            limit: 15
        });
    }

    // =============================
    // AKZEPTIERTE STEPS VERWALTEN
    // =============================

    /**
     * Fügt einen Step zur Liste der akzeptierten Steps hinzu
     * @param {string} masterId - Die master_id des hinzuzufügenden Steps
     * @param {object} variables - (Optional) Vorbelegte Variablen
     * @param {number} position - (Optional) Position an der eingefügt werden soll (-1 = am Ende)
     * @returns {object|null} Der hinzugefügte Step oder null bei Fehler
     */
    function addAcceptedStep(masterId, variables = {}, position = -1) {
        const masterStep = findMasterStep(masterId);
        if (!masterStep) {
            console.error(`[SmartStepManager] Master Step nicht gefunden: ${masterId}`);
            return null;
        }

        // Erzeuge Smart Defaults via MasterStepRenderer
        let defaults = {};
        if (window.MasterStepRenderer && typeof window.MasterStepRenderer.getSmartDefaults === 'function') {
            const context = {
                recipeType: recipeTypes.length > 0 ? recipeTypes[0] : '',
                ingredientName: '', // Kann später aus variables extrahiert werden
            };
            defaults = window.MasterStepRenderer.getSmartDefaults(masterId, context) || {};
        }

        // Merge: defaults → variables (user-provided überschreibt defaults)
        const mergedVars = Object.assign({}, defaults, variables || {});

        const newStep = {
            id: generateId('accepted_step'),
            master_id: masterId,
            title: masterStep.title || {},
            templateRaw: masterStep.templates || {},
            action: masterStep.action || '',
            phase: masterStep.phase || 2,
            variables: Array.isArray(masterStep.variables) ? [...masterStep.variables] : [],
            values: mergedVars,
            createdAt: Date.now()
        };

        // Einfügen an gewünschter Position
        if (position >= 0 && position < acceptedSteps.length) {
            acceptedSteps.splice(position, 0, newStep);
        } else {
            acceptedSteps.push(newStep);
        }

        console.log(`[SmartStepManager] Step hinzugefügt: ${masterId} (ID: ${newStep.id})`);

        // Trigger Callback
        if (typeof onStepsChanged === 'function') {
            onStepsChanged(acceptedSteps);
        }

        return newStep;
    }

    /**
     * Entfernt einen Step aus der Liste der akzeptierten Steps
     * @param {string} stepId - Die eindeutige ID des zu entfernenden Steps
     * @returns {boolean} True wenn entfernt, false wenn nicht gefunden
     */
    function removeAcceptedStep(stepId) {
        const index = acceptedSteps.findIndex(step => step.id === stepId);
        if (index === -1) {
            console.warn(`[SmartStepManager] Step nicht gefunden: ${stepId}`);
            return false;
        }

        const removed = acceptedSteps.splice(index, 1)[0];
        console.log(`[SmartStepManager] Step entfernt: ${removed.master_id} (ID: ${stepId})`);

        // Trigger Callback
        if (typeof onStepsChanged === 'function') {
            onStepsChanged(acceptedSteps);
        }

        return true;
    }

    /**
     * Aktualisiert die Variablen eines akzeptierten Steps
     * @param {string} stepId - Die eindeutige ID des Steps
     * @param {object} newValues - Neue Variable-Werte (wird gemerged)
     * @returns {boolean} True wenn erfolgreich, false wenn Step nicht gefunden
     */
    function updateStepVariables(stepId, newValues) {
        const step = acceptedSteps.find(s => s.id === stepId);
        if (!step) {
            console.warn(`[SmartStepManager] Step nicht gefunden: ${stepId}`);
            return false;
        }

        // Merge neue Werte
        Object.assign(step.values, newValues || {});

        console.log(`[SmartStepManager] Step aktualisiert: ${step.master_id} (ID: ${stepId})`);

        // Trigger Callback
        if (typeof onStepsChanged === 'function') {
            onStepsChanged(acceptedSteps);
        }

        return true;
    }

    /**
     * Verschiebt einen Step an eine neue Position
     * @param {string} stepId - Die eindeutige ID des zu verschiebenden Steps
     * @param {number} newPosition - Neue Position (0-basiert)
     * @returns {boolean} True wenn erfolgreich
     */
    function moveAcceptedStep(stepId, newPosition) {
        const oldIndex = acceptedSteps.findIndex(step => step.id === stepId);
        if (oldIndex === -1) {
            console.warn(`[SmartStepManager] Step nicht gefunden: ${stepId}`);
            return false;
        }

        const [movedStep] = acceptedSteps.splice(oldIndex, 1);
        const targetIndex = Math.max(0, Math.min(newPosition, acceptedSteps.length));
        acceptedSteps.splice(targetIndex, 0, movedStep);

        console.log(`[SmartStepManager] Step verschoben: ${stepId} (${oldIndex} → ${targetIndex})`);

        // Trigger Callback
        if (typeof onStepsChanged === 'function') {
            onStepsChanged(acceptedSteps);
        }

        return true;
    }

    /**
     * Löscht alle akzeptierten Steps
     */
    function clearAcceptedSteps() {
        acceptedSteps = [];
        console.log('[SmartStepManager] Alle akzeptierten Steps gelöscht');

        // Trigger Callback
        if (typeof onStepsChanged === 'function') {
            onStepsChanged(acceptedSteps);
        }
    }

    /**
     * Gibt die aktuelle Liste der akzeptierten Steps zurück
     * @returns {Array} Deep Clone der akzeptierten Steps
     */
    function getAcceptedSteps() {
        return deepClone(acceptedSteps);
    }

    // =============================
    // STEP-RENDERING
    // =============================

    /**
     * Rendert einen Step zu einem vollständigen Template-String
     * @param {object} step - Der zu rendernde Step (accepted step)
     * @param {string} lang - Sprache (optional, default: currentLang)
     * @returns {string} Gerenderter Template-Text
     */
    function renderStepTemplate(step, lang = null) {
        if (!step || !step.master_id) return '';

        const targetLang = lang || currentLang || DEFAULT_LANG;

        // Nutze MasterStepRenderer für konsistentes Rendering
        if (window.MasterStepRenderer && typeof window.MasterStepRenderer.render === 'function') {
            return window.MasterStepRenderer.render(
                step.master_id,
                step.values || {},
                targetLang
            );
        }

        // Fallback: Einfaches Template-Replace
        const template = step.templateRaw && step.templateRaw[targetLang]
            ? step.templateRaw[targetLang]
            : '';

        return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, (match, varName) => {
            const value = step.values && step.values[varName];
            return value != null ? String(value) : varName;
        });
    }

    /**
     * Rendert alle akzeptierten Steps zu einem Array von Template-Strings
     * @param {string} lang - Sprache (optional)
     * @returns {Array<string>} Array von gerenderten Step-Texten
     */
    function renderAllAcceptedSteps(lang = null) {
        return acceptedSteps.map(step => renderStepTemplate(step, lang));
    }

    /**
     * Serialisiert alle akzeptierten Steps zu JSON (für Server-Submit)
     * @returns {string} JSON-String
     */
    function serializeAcceptedSteps() {
        const exportData = acceptedSteps.map(step => ({
            master_id: step.master_id,
            values: step.values || {}
        }));
        return JSON.stringify(exportData);
    }

    /**
     * Lädt Steps aus JSON (z.B. beim Wiederherstellen eines Drafts)
     * @param {string} jsonString - JSON-String mit Steps
     * @returns {boolean} True wenn erfolgreich geladen
     */
    function loadAcceptedStepsFromJson(jsonString) {
        try {
            const data = JSON.parse(jsonString);
            if (!Array.isArray(data)) {
                console.error('[SmartStepManager] Ungültiges JSON-Format (kein Array)');
                return false;
            }

            clearAcceptedSteps();

            data.forEach(item => {
                if (item && item.master_id) {
                    addAcceptedStep(item.master_id, item.values || {});
                }
            });

            console.log(`[SmartStepManager] ${acceptedSteps.length} Steps aus JSON geladen`);
            return true;

        } catch (error) {
            console.error('[SmartStepManager] Fehler beim Parsen von JSON:', error);
            return false;
        }
    }

    // =============================
    // KONTEXT & KONFIGURATION
    // =============================

    /**
     * Setzt die aktuelle Sprache
     * @param {string} lang - Sprach-Code (de, en, esp, prt, etc.)
     */
    function setLanguage(lang) {
        currentLang = lang || DEFAULT_LANG;
        console.log(`[SmartStepManager] Sprache gesetzt: ${currentLang}`);
    }

    /**
     * Setzt erkannte Rezepttypen für bessere Vorschläge
     * @param {Array<string>} types - Array von Rezepttypen (z.B. ['pasta_gericht', 'vegan'])
     */
    function setRecipeTypes(types) {
        recipeTypes = Array.isArray(types) ? types : [];
        console.log(`[SmartStepManager] Rezepttypen gesetzt:`, recipeTypes);
    }

    /**
     * Aktualisiert Ingredient-Tags für Filterung
     * @param {Array} ingredients - Array von Ingredient-Objekten mit is_*-Properties
     */
    function updateIngredientTags(ingredients) {
        // Nutze MasterStepRenderer's Logik
        if (window.MasterStepRenderer && typeof window.MasterStepRenderer.collectIngredientTags === 'function') {
            ingredientTagSet = window.MasterStepRenderer.collectIngredientTags(ingredients);
        } else {
            // Fallback: Manuell sammeln
            ingredientTagSet = new Set();
            const tagProps = ['peelable', 'cuttable', 'grateable', 'fryable', 'roastable',
                'grillable', 'steamable', 'boilable', 'searable', 'poachable',
                'smokable', 'flambeable', 'blendable'];

            (ingredients || []).forEach(ing => {
                tagProps.forEach(prop => {
                    const key = 'is' + prop.charAt(0).toUpperCase() + prop.slice(1);
                    if (ing[key]) {
                        ingredientTagSet.add(prop);
                    }
                });
            });
        }

        console.log(`[SmartStepManager] Ingredient-Tags aktualisiert:`, Array.from(ingredientTagSet));
    }

    /**
     * Setzt den Phase-Filter
     * @param {string} phase - 'all' | 'prep' | 'cook' | 'finish'
     */
    function setPhaseFilter(phase) {
        currentPhaseFilter = phase || 'all';
        console.log(`[SmartStepManager] Phase-Filter gesetzt: ${currentPhaseFilter}`);
    }

    /**
     * Setzt den Such-Query
     * @param {string} query - Suchbegriff
     */
    function setSearchQuery(query) {
        currentSearchQuery = (query || '').toString().trim();
        console.log(`[SmartStepManager] Such-Query gesetzt: "${currentSearchQuery}"`);
    }

    /**
     * Registriert einen Callback der aufgerufen wird wenn acceptedSteps sich ändert
     * @param {Function} callback - Callback-Funktion (erhält acceptedSteps als Parameter)
     */
    function onAcceptedStepsChanged(callback) {
        if (typeof callback === 'function') {
            onStepsChanged = callback;
        }
    }

    /**
     * Registriert einen Callback der aufgerufen wird wenn suggestedSteps sich ändert
     * @param {Function} callback - Callback-Funktion (erhält suggestedSteps als Parameter)
     */
    function onSuggestedStepsChanged(callback) {
        if (typeof callback === 'function') {
            onSuggestionsChanged = callback;
        }
    }

    // =============================
    // PUBLIC API
    // =============================

    window.SmartStepManager = {
        // Initialisierung
        loadMasterSteps: loadMasterSteps,

        // Step-Suche & Filterung
        findMasterStep: findMasterStep,
        filterStepsByPhase: filterStepsByPhase,
        searchSteps: searchSteps,
        getFilteredMasterSteps: getFilteredMasterSteps,

        // Step-Vorschläge
        generateStepSuggestions: generateStepSuggestions,
        suggestStepsForIngredient: suggestStepsForIngredient,
        getSuggestedSteps: () => [...suggestedSteps],

        // Akzeptierte Steps verwalten
        addAcceptedStep: addAcceptedStep,
        removeAcceptedStep: removeAcceptedStep,
        updateStepVariables: updateStepVariables,
        moveAcceptedStep: moveAcceptedStep,
        clearAcceptedSteps: clearAcceptedSteps,
        getAcceptedSteps: getAcceptedSteps,

        // Step-Rendering
        renderStepTemplate: renderStepTemplate,
        renderAllAcceptedSteps: renderAllAcceptedSteps,
        serializeAcceptedSteps: serializeAcceptedSteps,
        loadAcceptedStepsFromJson: loadAcceptedStepsFromJson,

        // Kontext & Konfiguration
        setLanguage: setLanguage,
        setRecipeTypes: setRecipeTypes,
        updateIngredientTags: updateIngredientTags,
        setPhaseFilter: setPhaseFilter,
        setSearchQuery: setSearchQuery,

        // Callbacks
        onAcceptedStepsChanged: onAcceptedStepsChanged,
        onSuggestedStepsChanged: onSuggestedStepsChanged,

        // Getter für internen State (readonly)
        getCurrentLang: () => currentLang,
        getRecipeTypes: () => [...recipeTypes],
        getIngredientTags: () => new Set(ingredientTagSet),
        getPhaseFilter: () => currentPhaseFilter,
        getSearchQuery: () => currentSearchQuery,
        getAllMasterSteps: () => [...masterSteps]
    };

    console.log('[SmartStepManager] Modul initialisiert');

})(window);
