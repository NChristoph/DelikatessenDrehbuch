/**
 * smart-step-data.js
 *
 * Daten-Layer für Smart Step Creator
 * Lädt und cached Master Steps aus JSON
 */

(function (window) {
    'use strict';

    // ============================
    // STATE
    // ============================
    let masterSteps = [];
    let isLoaded = false;
    let loadPromise = null;

    // ============================
    // LADEN
    // ============================

    /**
     * Lädt Master Steps via MasterStepRenderer
     * @returns {Promise<boolean>}
     */
    async function load() {
        if (isLoaded) return true;
        if (loadPromise) return loadPromise;

        loadPromise = (async () => {
            try {
                console.log('[SmartStepData] Lade Master Steps...');

                // Nutze MasterStepRenderer als Single Source of Truth
                if (!window.MasterStepRenderer || typeof window.MasterStepRenderer.load !== 'function') {
                    throw new Error('MasterStepRenderer nicht verfügbar');
                }

                await window.MasterStepRenderer.load();
                masterSteps = window.MasterStepRenderer.getAllTemplates() || [];
                isLoaded = true;

                console.log(`[SmartStepData] ✓ ${masterSteps.length} Steps geladen`);
                return true;

            } catch (error) {
                console.error('[SmartStepData] Fehler beim Laden:', error);
                return false;
            }
        })();

        return loadPromise;
    }

    // ============================
    // ZUGRIFF
    // ============================

    /**
     * Gibt alle Master Steps zurück
     * @returns {Array}
     */
    function getAll() {
        return [...masterSteps];
    }

    /**
     * Findet Step by master_id
     * @param {string} masterId
     * @returns {object|null}
     */
    function findById(masterId) {
        return masterSteps.find(s => s && s.master_id === masterId) || null;
    }

    /**
     * Gibt Steps nach Phase zurück
     * @param {number} phase - 1=PREP, 2=COOK, 3=FINISH
     * @returns {Array}
     */
    function getByPhase(phase) {
        return masterSteps.filter(s => parseInt(s.phase, 10) === phase);
    }

    /**
     * Gruppiert alle Steps nach Phase
     * @returns {object} { prep: [], cook: [], finish: [] }
     */
    function getGrouped() {
        return {
            prep: getByPhase(1),
            cook: getByPhase(2),
            finish: getByPhase(3)
        };
    }

    // ============================
    // PUBLIC API
    // ============================
    window.SmartStepData = {
        load,
        getAll,
        findById,
        getByPhase,
        getGrouped,
        isLoaded: () => isLoaded
    };

})(window);
