(function (window) {
    'use strict';

    let data = null;
    let loadingPromise = null;
    let lastLoadError = '';

    function load() {
        if (loadingPromise) return loadingPromise;

        const configuredUrl = window.MasterStepRendererConfig && window.MasterStepRendererConfig.dataUrl;
        const dataUrl = configuredUrl || '/data/master_steps.json';

        loadingPromise = fetch(dataUrl)
            .then(function (r) {
                if (!r.ok) {
                    throw new Error('master_steps.json konnte nicht geladen werden (' + r.status + ' ' + r.statusText + ') von ' + dataUrl);
                }
                return r.json();
            })
            .then(function (json) {
                if (!json || !Array.isArray(json.master_steps)) {
                    throw new Error('master_steps.json hat ein ungültiges Format.');
                }
                data = json;
                lastLoadError = '';
                return json;
            })
            .catch(function (error) {
                data = null;
                lastLoadError = error && error.message ? error.message : 'Unbekannter Fehler beim Laden von master_steps.json';
                console.error('MasterStepRenderer.load fehlgeschlagen:', error);
                return null;
            });

        return loadingPromise;
    }

    function getLastLoadError() {
        return lastLoadError;
    }

    function getMasterSteps() {
        return data && Array.isArray(data.master_steps) ? data.master_steps : [];
    }

    function findTemplate(masterId) {
        return getMasterSteps().find(function (x) { return x && x.master_id === masterId; }) || null;
    }

    function getAllTemplates() {
        return getMasterSteps().filter(function (step) { return !!step; });
    }

    function renderText(template, vars) {
        if (!template) return '';
        return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
            const value = vars && vars[key] != null ? String(vars[key]).trim() : '';
            return value || key;
        });
    }

    function resolveGenderFromPronoun(value) {
        const v = (value || '').toString().trim().toLowerCase();
        if (!v) return 'f';

        if (['sie', 'her', 'la', 'ela', 'a'].includes(v)) return 'f';
        if (['ihn', 'him', 'lo', 'ele', 'o'].includes(v)) return 'm';
        if (['es', 'it'].includes(v)) return 'n';

        return 'f';
    }

    function stripKnownArticle(value, lang) {
        const text = (value || '').toString().trim();
        if (!text) return '';

        const articleByLang = {
            de: ['der', 'die', 'das', 'den', 'dem', 'des', 'ein', 'eine', 'einer', 'einem'],
            en: ['the', 'a', 'an'],
            esp: ['el', 'la', 'los', 'las', 'un', 'una'],
            prt: ['o', 'a', 'os', 'as', 'um', 'uma']
        };

        const l = (lang || 'de').toString().toLowerCase();
        const lower = text.toLowerCase();
        const articles = articleByLang[l] || [];

        for (let i = 0; i < articles.length; i++) {
            const article = articles[i];
            if (lower === article) return '';
            if (lower.startsWith(article + ' ')) {
                return text.slice(article.length + 1).trim();
            }
        }

        return text;
    }

    function resolveGenderFromIngredient(value, lang) {
        const base = stripKnownArticle(value, lang).toLowerCase();
        if (!base) return null;

        const known = {
            m: ['basilikum', 'zucker', 'knoblauch', 'reis', 'ingwer', 'lauch', 'sellerie'],
            f: ['zwiebel', 'paprika', 'karotte', 'tomate', 'kartoffel', 'sauce', 'brühe'],
            n: ['salz', 'öl', 'wasser', 'ei', 'mehl', 'fleisch', 'hähnchen']
        };

        if (known.m.includes(base)) return 'm';
        if (known.f.includes(base)) return 'f';
        if (known.n.includes(base)) return 'n';

        if (base.endsWith('chen') || base.endsWith('lein') || base.endsWith('ment')) return 'n';
        if (base.endsWith('ung') || base.endsWith('keit') || base.endsWith('heit') || base.endsWith('ion')) return 'f';
        return null;
    }

    function getLocalizedGrammar(lang, gender) {
        const l = (lang || 'de').toString().toLowerCase();
        const g = (gender || 'f').toString().toLowerCase();

        if (l === 'en') {
            return { article: 'the', pronoun: g === 'm' ? 'him' : g === 'n' ? 'it' : 'her' };
        }

        if (l === 'esp') {
            return { article: g === 'm' ? 'el' : 'la', pronoun: g === 'm' ? 'lo' : 'la' };
        }

        if (l === 'prt') {
            return { article: g === 'm' ? 'o' : 'a', pronoun: g === 'm' ? 'o' : 'a' };
        }

        return {
            article: g === 'm' ? 'den' : g === 'n' ? 'das' : 'die',
            pronoun: g === 'm' ? 'ihn' : g === 'n' ? 'es' : 'sie'
        };
    }

    function startsWithKnownArticle(value, lang) {
        const text = (value || '').toString().trim().toLowerCase();
        if (!text) return false;

        const articleByLang = {
            de: ['der', 'die', 'das', 'den', 'dem', 'des', 'ein', 'eine', 'einer', 'einem'],
            en: ['the', 'a', 'an'],
            esp: ['el', 'la', 'los', 'las', 'un', 'una'],
            prt: ['o', 'a', 'os', 'as', 'um', 'uma']
        };

        const articles = articleByLang[(lang || 'de').toString().toLowerCase()] || [];
        return articles.some(function (article) {
            return text === article || text.startsWith(article + ' ');
        });
    }

    function localizeVariables(variables, lang) {
        const result = Object.assign({}, variables || {});
        const pronounGender = resolveGenderFromPronoun(result.pronoun);
        const ingredientGender = resolveGenderFromIngredient(result.ingredient, lang);
        const gender = ingredientGender || pronounGender;
        const grammar = getLocalizedGrammar(lang, gender);

        if (!result.pronoun || ['sie', 'her', 'la', 'ela', 'a', 'ihn', 'him', 'lo', 'ele', 'o', 'es', 'it'].includes(String(result.pronoun).toLowerCase())) {
            result.pronoun = grammar.pronoun;
        }

        const ingredient = (result.ingredient || '').toString().trim();
        if (ingredient && !startsWithKnownArticle(ingredient, lang)) {
            result.ingredient = grammar.article + ' ' + ingredient;
        }

        return result;
    }

    function render(masterId, variables, lang) {
        const step = findTemplate(masterId);
        if (!step || !step.templates) return '';

        const key = (lang || 'de').toLowerCase();
        const tpl = step.templates[key] || step.templates.de || step.templates.en || '';
        const localizedVariables = localizeVariables(variables || {}, key);
        return renderText(tpl, localizedVariables);
    }

    function renderAll(masterId, variables) {
        const step = findTemplate(masterId);
        if (!step || !step.templates) {
            return { de: '', en: '', esp: '', prt: '' };
        }

        return {
            de: render(masterId, variables || {}, 'de'),
            en: render(masterId, variables || {}, 'en'),
            esp: render(masterId, variables || {}, 'esp'),
            prt: render(masterId, variables || {}, 'prt')
        };
    }

    function getVariablePresets(variableName, lang) {
        const normalizedLang = (lang || 'de').toLowerCase();
        const options = data && data.variable_options;
        const localized = options && options[variableName];

        if (localized && typeof localized === 'object') {
            const entries = localized[normalizedLang] || localized.de || localized.en;
            if (Array.isArray(entries)) {
                return entries;
            }
        }

        const presets = {
            shape: ['Würfel', 'Scheiben', 'Streifen', 'feine Würfel', 'grobe Stücke'],
            tool: ['Messer', 'Sparschäler', 'Reibe', 'Küchenmaschine'],
            duration: ['5 Minuten', '10 Minuten', '15 Minuten', '30 Minuten'],
            temperature: ['160°C', '180°C', '200°C', '220°C', '350°F', '400°F'],
            temp: ['160°C', '180°C', '200°C', '220°C', '350°F', '400°F'],
            liquid: ['Wasser', 'Gemüsebrühe', 'Milch', 'Kokosmilch'],
            equipment: ['Pfanne', 'Topf', 'Backofen', 'Bräter', 'Kochfeld', 'Mixer', 'Grill', 'Dampfgarer', 'Schüssel', 'Sieb'],
            state: ['goldbraun', 'weich', 'glasig', 'gar', 'knusprig', 'cremig', 'bissfest', 'eingedickt', 'sprudelnd'],
            garnish: ['frischen Kräutern', 'Sesam', 'Parmesan', 'Nüssen'],
            base: ['Eischnee', 'Masse', 'Teig', 'Creme', 'Sauce', 'Glasur', 'Füllung', 'Marinade', 'Emulsion', 'Schaum']
        };

        return presets[variableName] || [];
    }

    function getSmartDefaults(masterId, context) {
        context = context || {};
        const ingredientName = context.ingredientName || 'die Zutat';
        const recipeCategory = (context.recipeCategory || '').toString().toLowerCase();

        const defaults = {
            ingredient: ingredientName,
            ingredients: ingredientName,
            pronoun: 'sie',
            tool: 'Messer',
            shape: 'Würfel',
            grind_size: 'fein',
            marinade: 'Öl, Salz und Gewürzen',
            duration: '10 Minuten',
            liquid: 'Wasser',
            equipment: 'Pfanne',
            quantity: 'etwas',
            temperature: '180°C',
            temp: '180°C',
            heat: 'mittlerer',
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
        const all = getAllTemplates();
        if (!all.length) return [];

        const hasAny = function (terms) { return terms.some(function (x) { return name.includes(x); }); };

        const pantryTerms = ['öl', 'oil', 'salz', 'pfeffer', 'gewürz', 'spice', 'zucker', 'sugar', 'essig', 'vinegar'];
        const proteinTerms = ['huhn', 'hähn', 'chicken', 'rind', 'beef', 'schwein', 'pork', 'lamm', 'fisch', 'lachs', 'tofu'];
        const carbTerms = ['reis', 'rice', 'pasta', 'nudel', 'kartoff', 'potato', 'quinoa', 'couscous'];
        const vegTerms = ['tomat', 'zwiebel', 'karotte', 'paprika', 'brokkoli', 'zucchini', 'aubergine', 'gemüse', 'salat'];

        let preferredActions = ['wash', 'cut', 'boil', 'steam', 'fry', 'season', 'serve'];

        if (hasAny(pantryTerms)) {
            preferredActions = ['add', 'mix', 'stir', 'season', 'serve'];
        } else if (hasAny(proteinTerms)) {
            preferredActions = ['cut', 'marinate', 'fry', 'roast', 'grill', 'rest', 'season', 'serve'];
        } else if (hasAny(carbTerms)) {
            preferredActions = ['boil', 'stir', 'strain', 'steam', 'season', 'serve'];
        } else if (hasAny(vegTerms)) {
            preferredActions = ['wash', 'peel', 'cut', 'boil', 'steam', 'fry', 'roast', 'season', 'serve'];
        }

        const actionRank = function (action) {
            const idx = preferredActions.findIndex(function (x) { return x === action; });
            return idx >= 0 ? idx : 999;
        };

        return all.sort(function (a, b) {
            const aHasIngredient = Array.isArray(a.variables) && a.variables.includes('ingredient');
            const bHasIngredient = Array.isArray(b.variables) && b.variables.includes('ingredient');
            if (aHasIngredient !== bHasIngredient) return aHasIngredient ? -1 : 1;

            const ar = actionRank((a.action || '').toString());
            const br = actionRank((b.action || '').toString());
            if (ar !== br) return ar - br;

            const pa = parseInt(a.phase || 0, 10);
            const pb = parseInt(b.phase || 0, 10);
            if (pa !== pb) return pa - pb;

            return (a.master_id || '').localeCompare(b.master_id || '');
        });
    }

    window.MasterStepRenderer = {
        load: load,
        getAllTemplates: getAllTemplates,
        render: render,
        renderAll: renderAll,
        getSmartDefaults: getSmartDefaults,
        suggestForIngredient: suggestForIngredient,
        getVariablePresets: getVariablePresets,
        getLastLoadError: getLastLoadError,
        findTemplate: findTemplate
    };
})(window);
