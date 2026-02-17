(function (window) {
    'use strict';

    let data = null;
    let loadingPromise = null;

    function load() {
        if (loadingPromise) return loadingPromise;

        loadingPromise = fetch('/data/master_steps.json')
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (json) {
                data = json;
                return json;
            })
            .catch(function () {
                data = null;
                return null;
            });

        return loadingPromise;
    }

    function getMasterSteps() {
        return data && Array.isArray(data.master_steps) ? data.master_steps : [];
    }

    function findTemplate(masterId) {
        return getMasterSteps().find(function (x) { return x && x.master_id === masterId; }) || null;
    }

    function renderText(template, vars) {
        if (!template) return '';
        return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
            const value = vars && vars[key] != null ? String(vars[key]).trim() : '';
            return value || key;
        });
    }

    function render(masterId, variables, lang) {
        const step = findTemplate(masterId);
        if (!step || !step.templates) return '';

        const key = (lang || 'de').toLowerCase();
        const tpl = step.templates[key] || step.templates.de || step.templates.en || '';
        return renderText(tpl, variables || {});
    }

    function renderAll(masterId, variables) {
        const step = findTemplate(masterId);
        if (!step || !step.templates) {
            return { de: '', en: '', esp: '', prt: '' };
        }

        return {
            de: renderText(step.templates.de || '', variables || {}),
            en: renderText(step.templates.en || '', variables || {}),
            esp: renderText(step.templates.esp || '', variables || {}),
            prt: renderText(step.templates.prt || '', variables || {})
        };
    }

    function getVariablePresets(variableName) {
        const presets = {
            shape: ['Würfel', 'Scheiben', 'Streifen', 'feine Würfel', 'grobe Stücke'],
            tool: ['Messer', 'Sparschäler', 'Reibe', 'Küchenmaschine'],
            duration: ['5 Minuten', '10 Minuten', '15 Minuten', '30 Minuten'],
            heat: ['niedriger Hitze', 'mittlerer Hitze', 'hoher Hitze'],
            temperature: ['160°C', '180°C', '200°C', '220°C'],
            liquid: ['Wasser', 'Gemüsebrühe', 'Milch', 'Kokosmilch'],
            garnish: ['frischen Kräutern', 'Sesam', 'Parmesan', 'Nüssen']
        };

        return presets[variableName] || [];
    }

    function getSmartDefaults(masterId, context) {
        context = context || {};
        const ingredientName = context.ingredientName || 'die Zutat';
        const recipeCategory = (context.recipeCategory || '').toString().toLowerCase();

        const defaults = {
            ingredient: ingredientName,
            pronoun: 'sie',
            tool: 'Messer',
            shape: 'Würfel',
            grind_size: 'fein',
            marinade: 'Öl, Salz und Gewürzen',
            duration: '10 Minuten',
            liquid: 'Wasser',
            quantity: 'etwas',
            temperature: recipeCategory.includes('dessert') ? '180°C' : 'mittlerer Hitze',
            heat: 'mittlerer Hitze',
            spice_mix: 'Salz, Pfeffer und Gewürzen',
            sauce: 'Sauce',
            target_consistency: 'cremig',
            garnish: 'frischen Kräutern',
            serving_style: 'auf Tellern',
            side: recipeCategory.includes('salat') ? 'frischem Brot' : 'Beilage'
        };

        const template = findTemplate(masterId);
        if (!template || !Array.isArray(template.variables)) return defaults;

        const scoped = {};
        template.variables.forEach(function (key) {
            scoped[key] = defaults[key] || '';
        });
        return scoped;
    }

    function suggestForIngredient(ingredientName) {
        const name = (ingredientName || '').toString().toLowerCase().trim();
        if (!name) return [];

        const prepActions = ['wash', 'peel', 'cut', 'grate', 'mince', 'mix'];
        const cookActions = ['fry', 'boil', 'bake', 'simmer', 'steam', 'season', 'serve'];

        return getMasterSteps().filter(function (step) {
            if (!step || !Array.isArray(step.variables) || !step.variables.includes('ingredient')) {
                return false;
            }

            if (name.includes('öl') || name.includes('oil') || name.includes('salz') || name.includes('pfeffer')) {
                return cookActions.includes(step.action);
            }

            return prepActions.includes(step.action) || cookActions.includes(step.action);
        });
    }

    window.MasterStepRenderer = {
        load: load,
        render: render,
        renderAll: renderAll,
        getSmartDefaults: getSmartDefaults,
        suggestForIngredient: suggestForIngredient,
        getVariablePresets: getVariablePresets,
        findTemplate: findTemplate
    };
})(window);
