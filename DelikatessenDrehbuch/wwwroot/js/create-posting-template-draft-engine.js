(function (window) {
    'use strict';

    const probabilityDrafts = {};
    let activeStepDraft = null;

    function ensureObject(value) {
        return value && typeof value === 'object' ? value : {};
    }

    function ensureProbabilityDraft(masterId, initializer) {
        const key = (masterId || '').toString().trim();
        if (!key) return null;

        const initial = typeof initializer === 'function' ? initializer() : initializer;
        const normalized = ensureObject(initial);

        if (!probabilityDrafts[key]) {
            probabilityDrafts[key] = {
                masterId: key,
                templateRaw: (normalized.templateRaw || '').toString(),
                values: ensureObject(normalized.values),
                _multiIngredients: ensureObject(normalized._multiIngredients),
                _explicitValues: ensureObject(normalized._explicitValues)
            };
        } else {
            if (!probabilityDrafts[key].templateRaw && normalized.templateRaw) {
                probabilityDrafts[key].templateRaw = (normalized.templateRaw || '').toString();
            }
            if (normalized.values && typeof normalized.values === 'object') {
                probabilityDrafts[key].values = Object.assign({}, normalized.values, ensureObject(probabilityDrafts[key].values));
            } else {
                probabilityDrafts[key].values = ensureObject(probabilityDrafts[key].values);
            }
            if (normalized._multiIngredients && typeof normalized._multiIngredients === 'object') {
                probabilityDrafts[key]._multiIngredients = Object.assign({}, normalized._multiIngredients, ensureObject(probabilityDrafts[key]._multiIngredients));
            } else {
                probabilityDrafts[key]._multiIngredients = ensureObject(probabilityDrafts[key]._multiIngredients);
            }
            if (normalized._explicitValues && typeof normalized._explicitValues === 'object') {
                probabilityDrafts[key]._explicitValues = Object.assign({}, normalized._explicitValues, ensureObject(probabilityDrafts[key]._explicitValues));
            } else {
                probabilityDrafts[key]._explicitValues = ensureObject(probabilityDrafts[key]._explicitValues);
            }
            probabilityDrafts[key].values = ensureObject(probabilityDrafts[key].values);
            probabilityDrafts[key]._multiIngredients = ensureObject(probabilityDrafts[key]._multiIngredients);
            probabilityDrafts[key]._explicitValues = ensureObject(probabilityDrafts[key]._explicitValues);
        }

        return probabilityDrafts[key];
    }

    function getProbabilityDraft(masterId) {
        const key = (masterId || '').toString().trim();
        return key ? (probabilityDrafts[key] || null) : null;
    }

    function getProbabilityStates() {
        return probabilityDrafts;
    }

    function setProbabilityValue(masterId, varName, value, extras) {
        const draft = ensureProbabilityDraft(masterId);
        if (!draft || !varName) return null;

        draft.values[varName] = value;
        draft._explicitValues = ensureObject(draft._explicitValues);
        draft._explicitValues[varName] = value;
        if (extras && extras.pronoun) {
            draft.values.pronoun = extras.pronoun;
            draft._explicitValues.pronoun = extras.pronoun;
        }
        return draft;
    }

    function resetProbabilityValue(masterId, varName) {
        const draft = ensureProbabilityDraft(masterId);
        if (!draft || !varName) return null;

        delete draft.values[varName];
        draft._explicitValues = ensureObject(draft._explicitValues);
        delete draft._explicitValues[varName];
        if (draft._multiIngredients) {
            draft._multiIngredients[varName] = [];
        }
        return draft;
    }

    function getProbabilityExplicitValues(masterId) {
        const draft = ensureProbabilityDraft(masterId);
        if (!draft) return {};
        draft._explicitValues = ensureObject(draft._explicitValues);
        return draft._explicitValues;
    }

    function getProbabilityMultiIngredients(masterId, varName) {
        const draft = ensureProbabilityDraft(masterId);
        if (!draft) return varName ? [] : {};
        if (!varName) return draft._multiIngredients;
        return draft._multiIngredients[varName] || [];
    }

    function setProbabilityMultiIngredients(masterId, varName, values) {
        const draft = ensureProbabilityDraft(masterId);
        if (!draft || !varName) return [];

        draft._multiIngredients[varName] = Array.isArray(values) ? values : [];
        return draft._multiIngredients[varName];
    }

    function setActiveStepDraft(stepDraft) {
        activeStepDraft = stepDraft || null;
        if (activeStepDraft) {
            activeStepDraft.values = ensureObject(activeStepDraft.values);
            activeStepDraft._multiIngredients = ensureObject(activeStepDraft._multiIngredients);
        }
        return activeStepDraft;
    }

    function getActiveStepDraft() {
        return activeStepDraft;
    }

    function clearActiveStepDraft() {
        activeStepDraft = null;
    }

    window.CreatePostingTemplateDrafts = Object.assign(window.CreatePostingTemplateDrafts || {}, {
        ensureProbabilityDraft,
        getProbabilityDraft,
        getProbabilityStates,
        setProbabilityValue,
        resetProbabilityValue,
        getProbabilityExplicitValues,
        getProbabilityMultiIngredients,
        setProbabilityMultiIngredients,
        setActiveStepDraft,
        getActiveStepDraft,
        clearActiveStepDraft
    });
})(window);
