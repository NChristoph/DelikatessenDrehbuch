/**
 * smart-step-logic.js
 *
 * Business Logic für Smart Step Creator
 * Verwaltet akzeptierte Steps, Filterung, aktuellen Step
 */

(function (window) {
    'use strict';

    // ============================
    // STATE
    // ============================
    let acceptedSteps = [];        // Liste der akzeptierten Steps
    let currentStep = null;        // Aktuell bearbeiteter Step
    let currentLang = 'de';

    // Filter
    let phaseFilter = 'all';       // 'all' | 'prep' | 'cook' | 'finish'
    let searchQuery = '';

    // Callbacks
    let onChange = null;           // Wird aufgerufen bei Änderungen

    // ============================
    // HILFSFUNKTIONEN
    // ============================

    function generateId() {
        return `step_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    function triggerChange() {
        if (typeof onChange === 'function') {
            onChange({
                acceptedSteps: getAcceptedSteps(),
                currentStep: getCurrentStep()
            });
        }
    }

    // ============================
    // AKTUELLER STEP
    // ============================

    /**
     * Setzt einen Step als aktuell zur Bearbeitung
     * @param {string} masterId
     * @returns {object|null}
     */
    function setCurrentStep(masterId) {
        const masterStep = window.SmartStepData.findById(masterId);
        if (!masterStep) {
            console.warn('[SmartStepLogic] Step nicht gefunden:', masterId);
            return null;
        }

        // Hole Smart Defaults via MasterStepRenderer
        let defaults = {};
        if (window.MasterStepRenderer && typeof window.MasterStepRenderer.getSmartDefaults === 'function') {
            defaults = window.MasterStepRenderer.getSmartDefaults(masterId, {}) || {};
        }

        currentStep = {
            id: generateId(),
            master_id: masterId,
            title: masterStep.title || {},
            variables: Array.isArray(masterStep.variables) ? [...masterStep.variables] : [],
            values: { ...defaults },
            phase: masterStep.phase || 2,
            action: masterStep.action || ''
        };

        console.log('[SmartStepLogic] Aktueller Step gesetzt:', masterId);
        triggerChange();
        return currentStep;
    }

    /**
     * Gibt aktuellen Step zurück
     */
    function getCurrentStep() {
        return currentStep ? { ...currentStep } : null;
    }

    /**
     * Aktualisiert Variable im aktuellen Step
     * @param {string} varName
     * @param {*} value
     */
    function updateCurrentStepVariable(varName, value) {
        if (!currentStep) return false;
        currentStep.values[varName] = value;
        triggerChange();
        return true;
    }

    /**
     * Löscht aktuellen Step
     */
    function clearCurrentStep() {
        currentStep = null;
        triggerChange();
    }

    // ============================
    // AKZEPTIERTE STEPS
    // ============================

    /**
     * Fügt aktuellen Step zur Liste hinzu
     * @returns {boolean}
     */
    function acceptCurrentStep() {
        if (!currentStep) return false;

        acceptedSteps.push({ ...currentStep });
        console.log('[SmartStepLogic] Step akzeptiert:', currentStep.master_id);

        clearCurrentStep();
        return true;
    }

    /**
     * Entfernt Step aus Liste
     * @param {string} stepId
     */
    function removeAcceptedStep(stepId) {
        const index = acceptedSteps.findIndex(s => s.id === stepId);
        if (index === -1) return false;

        acceptedSteps.splice(index, 1);
        console.log('[SmartStepLogic] Step entfernt:', stepId);
        triggerChange();
        return true;
    }

    /**
     * Verschiebt Step an neue Position
     * @param {string} stepId
     * @param {number} newIndex
     */
    function moveAcceptedStep(stepId, newIndex) {
        const oldIndex = acceptedSteps.findIndex(s => s.id === stepId);
        if (oldIndex === -1) return false;

        const [step] = acceptedSteps.splice(oldIndex, 1);
        acceptedSteps.splice(newIndex, 0, step);

        console.log('[SmartStepLogic] Step verschoben:', stepId, oldIndex, '→', newIndex);
        triggerChange();
        return true;
    }

    /**
     * Gibt akzeptierte Steps zurück
     */
    function getAcceptedSteps() {
        return acceptedSteps.map(s => ({ ...s }));
    }

    /**
     * Löscht alle akzeptierten Steps
     */
    function clearAcceptedSteps() {
        acceptedSteps = [];
        triggerChange();
    }

    // ============================
    // FILTERUNG
    // ============================

    /**
     * Setzt Phase-Filter
     * @param {string} phase - 'all' | 'prep' | 'cook' | 'finish'
     */
    function setPhaseFilter(phase) {
        phaseFilter = phase || 'all';
        triggerChange();
    }

    /**
     * Setzt Such-Query
     * @param {string} query
     */
    function setSearchQuery(query) {
        searchQuery = (query || '').trim().toLowerCase();
        triggerChange();
    }

    /**
     * Gibt gefilterte Steps zurück
     * @returns {object} { prep: [], cook: [], finish: [] }
     */
    function getFilteredSteps() {
        let grouped = window.SmartStepData.getGrouped();

        // Phase-Filter
        if (phaseFilter !== 'all') {
            const phaseMap = { prep: 'prep', cook: 'cook', finish: 'finish' };
            const selectedPhase = phaseMap[phaseFilter];

            grouped = {
                prep: selectedPhase === 'prep' ? grouped.prep : [],
                cook: selectedPhase === 'cook' ? grouped.cook : [],
                finish: selectedPhase === 'finish' ? grouped.finish : []
            };
        }

        // Such-Filter
        if (searchQuery) {
            const filterBySearch = (steps) => steps.filter(step => {
                if (!step) return false;

                // Durchsuche Titel
                if (step.title && step.title[currentLang]) {
                    if (step.title[currentLang].toLowerCase().includes(searchQuery)) {
                        return true;
                    }
                }

                // Durchsuche Action
                if (step.action && step.action.toLowerCase().includes(searchQuery)) {
                    return true;
                }

                // Durchsuche master_id
                if (step.master_id && step.master_id.toLowerCase().includes(searchQuery)) {
                    return true;
                }

                return false;
            });

            grouped.prep = filterBySearch(grouped.prep);
            grouped.cook = filterBySearch(grouped.cook);
            grouped.finish = filterBySearch(grouped.finish);
        }

        return grouped;
    }

    /**
     * Berechnet Stern-Score für Step
     * @param {string} masterId
     * @param {Array} ingredientGroupIds
     * @returns {number} 0-3
     */
    function getStepScore(masterId, ingredientGroupIds = []) {
        if (!window.MasterStepRenderer || typeof window.MasterStepRenderer.getStepGroupScore !== 'function') {
            return 0;
        }
        return window.MasterStepRenderer.getStepGroupScore(masterId, ingredientGroupIds);
    }

    // ============================
    // SERIALISIERUNG
    // ============================

    /**
     * Exportiert akzeptierte Steps als JSON für Server
     * @returns {string}
     */
    function serialize() {
        const data = acceptedSteps.map(step => ({
            master_id: step.master_id,
            values: step.values
        }));
        return JSON.stringify(data);
    }

    /**
     * Lädt Steps aus JSON
     * @param {string} jsonString
     */
    function deserialize(jsonString) {
        try {
            const data = JSON.parse(jsonString);
            if (!Array.isArray(data)) return false;

            clearAcceptedSteps();

            data.forEach(item => {
                if (!item || !item.master_id) return;

                const masterStep = window.SmartStepData.findById(item.master_id);
                if (!masterStep) return;

                acceptedSteps.push({
                    id: generateId(),
                    master_id: item.master_id,
                    title: masterStep.title || {},
                    variables: masterStep.variables || [],
                    values: item.values || {},
                    phase: masterStep.phase || 2,
                    action: masterStep.action || ''
                });
            });

            console.log('[SmartStepLogic] Steps geladen:', acceptedSteps.length);
            triggerChange();
            return true;

        } catch (error) {
            console.error('[SmartStepLogic] Fehler beim Deserialisieren:', error);
            return false;
        }
    }

    // ============================
    // KONFIGURATION
    // ============================

    /**
     * Setzt Sprache
     * @param {string} lang
     */
    function setLanguage(lang) {
        currentLang = lang || 'de';
    }

    /**
     * Registriert Change-Callback
     * @param {Function} callback
     */
    function onStateChange(callback) {
        onChange = callback;
    }

    // ============================
    // PUBLIC API
    // ============================
    window.SmartStepLogic = {
        // Aktueller Step
        setCurrentStep,
        getCurrentStep,
        updateCurrentStepVariable,
        clearCurrentStep,

        // Akzeptierte Steps
        acceptCurrentStep,
        removeAcceptedStep,
        moveAcceptedStep,
        getAcceptedSteps,
        clearAcceptedSteps,

        // Filterung
        setPhaseFilter,
        setSearchQuery,
        getFilteredSteps,
        getStepScore,

        // Serialisierung
        serialize,
        deserialize,

        // Konfiguration
        setLanguage,
        onStateChange,

        // Getter
        getPhaseFilter: () => phaseFilter,
        getSearchQuery: () => searchQuery
    };

})(window);
