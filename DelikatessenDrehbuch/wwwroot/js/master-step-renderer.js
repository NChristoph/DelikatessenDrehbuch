(function (window) {
    'use strict';

    let data = null;
    let variableCatalogData = null;
    let recipeTypeStepVarsData = null;
    let optionRulesData = null;
    let ingredientCookingProfilesData = null;
    let stepGroupAffinitiesData = null;
    let loadingPromise = null;
    let lastLoadError = '';
    let _templateIndex = null;  // Performance: Map<master_id, step> for O(1) lookup

    function load() {
        if (loadingPromise) return loadingPromise;

        const configuredUrl = window.MasterStepRendererConfig && window.MasterStepRendererConfig.dataUrl;
        const dataUrl = configuredUrl || window.CreatePostingUtils.getDataUrl('masterSteps') || '/data/master_steps.json?v=2';

        if (window.CreatePostingDataStore && typeof window.CreatePostingDataStore.loadMany === 'function') {
            loadingPromise = window.CreatePostingDataStore.loadMany([
                'masterSteps',
                'masterStepVariables',
                'recipeTypeStepVariables',
                'masterStepOptionRules',
                'ingredientCookingProfiles',
                'stepGroupAffinities'
            ]).then(function (results) {
                data = results.masterSteps;
                _templateIndex = null;  // Invalidate index on reload
                variableCatalogData = results.masterStepVariables || null;
                recipeTypeStepVarsData = results.recipeTypeStepVariables || null;
                optionRulesData = results.masterStepOptionRules || null;
                ingredientCookingProfilesData = results.ingredientCookingProfiles || null;
                stepGroupAffinitiesData = results.stepGroupAffinities || null;
                lastLoadError = '';
                console.log('[MasterStepRenderer] Geladen:', {
                    masterSteps: data && data.master_steps ? data.master_steps.length : 0,
                    variableCatalog: variableCatalogData && variableCatalogData.variables ? Object.keys(variableCatalogData.variables).length : 0,
                    stepVarsDefaults: recipeTypeStepVarsData && recipeTypeStepVarsData.defaults ? Object.keys(recipeTypeStepVarsData.defaults).length : 0,
                    stepVarsTypes: recipeTypeStepVarsData && recipeTypeStepVarsData.types ? Object.keys(recipeTypeStepVarsData.types).length : 0,
                    optionRules: optionRulesData ? 'loaded' : 'not available',
                    cookingProfiles: ingredientCookingProfilesData && ingredientCookingProfilesData.by_group ? Object.keys(ingredientCookingProfilesData.by_group).length + ' groups' : 'not available',
                    groupAffinities: stepGroupAffinitiesData && stepGroupAffinitiesData.by_group ? Object.keys(stepGroupAffinitiesData.by_group).length + ' groups' : 'not available'
                });
                return data;
            }).catch(function (error) {
                data = null;
                _templateIndex = null;
                lastLoadError = error && error.message ? error.message : 'Unbekannter Fehler beim Laden von master_steps.json';
                console.error('MasterStepRenderer.load fehlgeschlagen:', error);
                return null;
            });

            return loadingPromise;
        }

        loadingPromise = Promise.all([
            fetch(dataUrl).then(function (r) {
                if (!r.ok) {
                    throw new Error('master_steps.json konnte nicht geladen werden (' + r.status + ' ' + r.statusText + ') von ' + dataUrl);
                }
                return r.json();
            }),
            fetch(window.CreatePostingUtils.getDataUrl('masterStepVariables') || '/data/master_step_variables.json')
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; }),
            fetch(window.CreatePostingUtils.getDataUrl('recipeTypeStepVariables') || '/data/recipe_type_step_variables.json')
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; }),
            fetch(window.CreatePostingUtils.getDataUrl('masterStepOptionRules') || '/data/master_step_option_rules.json')
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; }),
            fetch(window.CreatePostingUtils.getDataUrl('ingredientCookingProfiles') || '/data/ingredient_cooking_profiles.json')
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; }),
            fetch(window.CreatePostingUtils.getDataUrl('stepGroupAffinities') || '/data/step_group_affinities.json')
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; })
        ])
            .then(function (results) {
                var json = results[0];
                var variableJson = results[1];
                var stepVarsJson = results[2];
                var optionRulesJson = results[3];
                var cookingProfilesJson = results[4];
                var groupAffinitiesJson = results[5];
                if (!json || !Array.isArray(json.master_steps)) {
                    throw new Error('master_steps.json hat ein ungültiges Format.');
                }
                data = json;
                _templateIndex = null;  // Invalidate index on reload
                variableCatalogData = variableJson;
                recipeTypeStepVarsData = stepVarsJson;
                optionRulesData = optionRulesJson;
                ingredientCookingProfilesData = cookingProfilesJson;
                stepGroupAffinitiesData = groupAffinitiesJson;
                lastLoadError = '';
                console.log('[MasterStepRenderer] Geladen:', {
                    masterSteps: json.master_steps ? json.master_steps.length : 0,
                    variableCatalog: variableJson && variableJson.variables ? Object.keys(variableJson.variables).length : 0,
                    stepVarsDefaults: stepVarsJson && stepVarsJson.defaults ? Object.keys(stepVarsJson.defaults).length : 0,
                    stepVarsTypes: stepVarsJson && stepVarsJson.types ? Object.keys(stepVarsJson.types).length : 0,
                    optionRules: optionRulesJson ? 'loaded' : 'not available',
                    cookingProfiles: cookingProfilesJson && cookingProfilesJson.by_group ? Object.keys(cookingProfilesJson.by_group).length + ' groups' : 'not available',
                    groupAffinities: groupAffinitiesJson && groupAffinitiesJson.by_group ? Object.keys(groupAffinitiesJson.by_group).length + ' groups' : 'not available'
                });
                return json;
            })
            .catch(function (error) {
                data = null;
                _templateIndex = null;
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
        // Performance: O(1) Map lookup instead of O(n) Array.find()
        if (!_templateIndex) {
            _templateIndex = new Map();
            getMasterSteps().forEach(function (s) { if (s && s.master_id) _templateIndex.set(s.master_id, s); });
        }
        return _templateIndex.get(masterId) || null;
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

        // Auto-resolve copula (ist/sind) based on pronoun if not explicitly set
        if (!result.copula) {
            var l = (lang || 'de').toString().toLowerCase();
            var pro = String(result.pronoun || '').toLowerCase();
            if (l === 'de') {
                // "sie" can be singular (feminine) or plural — check ingredient for plural hints
                var ing = (result.ingredient || result.base || '').toString().toLowerCase();
                var isPlural = ing.startsWith('die ') && (ing.endsWith('n') || ing.endsWith('en') || ing.endsWith('eln') || ing.endsWith('ern'));
                result.copula = (pro === 'sie' && isPlural) ? 'sind' : 'ist';
            } else if (l === 'en') {
                result.copula = (pro === 'them' || pro === 'they') ? 'are' : 'is';
            } else {
                result.copula = 'ist';
            }
        }

        return result;
    }

    function render(masterId, variables, lang) {
        const step = findTemplate(masterId);
        if (!step) return '';

        const key = (lang || 'de').toLowerCase();

        // Support both old format (templates object) and new format (single template field)
        let tpl = '';
        if (step.template) {
            // New format: single template from language-specific JSON
            tpl = step.template;
        } else if (step.templates) {
            // Old format: templates object with multiple languages
            tpl = step.templates[key] || step.templates.de || step.templates.en || '';
        }

        const localizedVariables = localizeVariables(variables || {}, key);
        return renderText(tpl, localizedVariables);
    }

    function renderAll(masterId, variables) {
        const step = findTemplate(masterId);
        if (!step || (!step.templates && !step.template)) {
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
        const variables = variableCatalogData && variableCatalogData.variables;
        const localized = variables && variables[variableName];

        if (localized && Array.isArray(localized.options)) {
            const entries = localized.options
                .map(function (option) {
                    const labels = option && option.labels ? option.labels : null;
                    if (!labels) return '';
                    return labels[normalizedLang] || labels.de || labels.en || '';
                })
                .filter(function (entry) { return !!entry; });
            if (entries.length) {
                return entries;
            }
        }

        const presets = {
            duration: ['5 Minuten', '10 Minuten', '15 Minuten', '20 Minuten', '30 Minuten', '45 Minuten', '1 Stunde', '2 Stunden'],
            temperature: ['160 C', '180 C', '200 C', '220 C', '350 F', '400 F'],
            temp: ['160 C', '180 C', '200 C', '220 C', '350 F', '400 F'],
            liquid: ['Wasser', 'Gemuesebruehe', 'Milch', 'Kokosmilch'],
            garnish: ['frischen Kraeutern', 'Sesam', 'Parmesan', 'Nuessen']
        };

        const result = presets[variableName] || [];
        return result;
    }

    function getStepDefaults(masterId) {
        if (!recipeTypeStepVarsData || !recipeTypeStepVarsData.defaults) return null;
        return recipeTypeStepVarsData.defaults[masterId] || null;
    }

    function getRecipeTypeStepVars(recipeType, masterId) {
        if (!recipeTypeStepVarsData || !recipeTypeStepVarsData.types) return null;
        var typeData = recipeTypeStepVarsData.types[recipeType];
        if (!typeData || !typeData.step_variables) return null;
        return typeData.step_variables[masterId] || null;
    }

    function resolveParentType(recipeType) {
        if (!recipeTypeStepVarsData || !recipeTypeStepVarsData.types) return null;
        var typeData = recipeTypeStepVarsData.types[recipeType];
        return (typeData && typeData.parent) ? typeData.parent : null;
    }

    function getIngredientGroupDefaults(groupId, masterId) {
        if (!ingredientCookingProfilesData || !ingredientCookingProfilesData.by_group) return null;
        var group = ingredientCookingProfilesData.by_group[groupId];
        if (!group) return null;
        return group[masterId] || null;
    }

    function getSmartDefaults(masterId, context) {
        context = context || {};
        const ingredientName = context.ingredientName || 'die Zutat';
        const recipeType = (context.recipeType || '').toString().toLowerCase();

        const template = findTemplate(masterId);
        if (!template || !Array.isArray(template.variables)) return {};

        // Level 1: Generic defaults from JSON
        var stepDefaults = getStepDefaults(masterId) || {};
        const scoped = {};
        template.variables.forEach(function (key) {
            if (stepDefaults[key] != null && stepDefaults[key] !== '') {
                scoped[key] = stepDefaults[key];
            } else {
                scoped[key] = '';
            }
        });

        if (recipeType) {
            // Level 2: Parent type overrides (if recipeType has a parent)
            var parentType = resolveParentType(recipeType);
            if (parentType) {
                var parentVars = getRecipeTypeStepVars(parentType, masterId);
                if (parentVars) {
                    template.variables.forEach(function (key) {
                        if (parentVars[key] != null && parentVars[key] !== '') {
                            scoped[key] = parentVars[key];
                        }
                    });
                }
            }

            // Level 3: Subtype/direct type overrides (highest recipe-type priority)
            var typeVars = getRecipeTypeStepVars(recipeType, masterId);
            if (typeVars) {
                template.variables.forEach(function (key) {
                    if (typeVars[key] != null && typeVars[key] !== '') {
                        scoped[key] = typeVars[key];
                    }
                });
            }
        }

        // Level 4: Ingredient-Group overrides (höchste Daten-Priorität)
        var ingredientGroupId = (context.ingredientGroupId || '').toString();
        if (ingredientGroupId) {
            var groupVars = getIngredientGroupDefaults(ingredientGroupId, masterId);
            if (groupVars) {
                template.variables.forEach(function (key) {
                    if (groupVars[key] != null && groupVars[key] !== '') {
                        scoped[key] = groupVars[key];
                    }
                });
            }
        }

        // Level 5: ingredient/ingredients: always leave empty — user must set these
        if (template.variables.includes('ingredient')) {
            scoped.ingredient = '';
        }
        if (template.variables.includes('ingredients')) {
            scoped.ingredients = '';
        }

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

    function getStepGroupScore(masterId, groupIds) {
        if (!stepGroupAffinitiesData || !stepGroupAffinitiesData.by_group) return 0;
        if (!Array.isArray(groupIds) || !groupIds.length) return 0;
        var maxScore = 0;
        groupIds.forEach(function (gid) {
            var group = stepGroupAffinitiesData.by_group[gid.toString()];
            if (group && typeof group[masterId] === 'number') {
                maxScore = Math.max(maxScore, group[masterId]);
            }
        });
        return maxScore;
    }

    function getOptionRulesData() {
        return optionRulesData || null;
    }

    function resolveRulesForLang(varRules, lang) {
        var l = (lang || 'de').toLowerCase();
        return {
            preferred: (varRules.preferred && (varRules.preferred[l] || varRules.preferred.de)) || [],
            blocked: (varRules.blocked && (varRules.blocked[l] || varRules.blocked.de)) || []
        };
    }

    function getOptionRulesForAction(action, varName, lang) {
        if (!optionRulesData || !action || !varName) return { preferred: [], blocked: [] };
        var actionRules = optionRulesData.action && optionRulesData.action[action];
        if (!actionRules) return { preferred: [], blocked: [] };
        var varRules = actionRules[varName];
        if (!varRules) return { preferred: [], blocked: [] };
        return resolveRulesForLang(varRules, lang);
    }

    function getOptionRulesForFamily(family, varName, lang) {
        if (!optionRulesData || !family || !varName) return { preferred: [], blocked: [] };
        var familyRules = optionRulesData.ingredient_family && optionRulesData.ingredient_family[family];
        if (!familyRules) return { preferred: [], blocked: [] };
        var varRules = familyRules[varName];
        if (!varRules) return { preferred: [], blocked: [] };
        return resolveRulesForLang(varRules, lang);
    }

    function getOptionRulesForStep(masterId, varName, lang) {
        if (!optionRulesData || !masterId || !varName) return { preferred: [], blocked: [] };
        var stepRules = optionRulesData.steps && optionRulesData.steps[masterId];
        if (!stepRules) return { preferred: [], blocked: [] };
        var varRules = stepRules[varName];
        if (!varRules) return { preferred: [], blocked: [] };
        return resolveRulesForLang(varRules, lang);
    }

    function getStepDefaultValue(masterId, varName, lang) {
        if (!optionRulesData || !masterId || !varName) return '';
        var stepRules = optionRulesData.steps && optionRulesData.steps[masterId];
        if (!stepRules) return '';
        var varRules = stepRules[varName];
        if (!varRules || !varRules['default']) return '';
        var defaults = varRules['default'];
        return defaults[lang] || defaults['de'] || '';
    }

    var _ingredientTagProps = ['peelable','cuttable','grateable','fryable','roastable','grillable',
        'steamable','boilable','searable','poachable','smokable','flambeable','blendable'];

    function collectIngredientTags(ingredients) {
        var tags = new Set();
        (ingredients || []).forEach(function(ing) {
            _ingredientTagProps.forEach(function(prop) {
                if (ing['is' + prop.charAt(0).toUpperCase() + prop.slice(1)]) tags.add(prop);
            });
        });
        return tags;
    }

    function shouldShowStep(masterId, ingredientTagSet) {
        var template = findTemplate(masterId);
        if (!template) return false;
        var required = template.required_ingredient_tags;
        if (!required || !required.length) return true;
        if (!ingredientTagSet || !ingredientTagSet.size) return true;
        return required.some(function(tag) { return ingredientTagSet.has(tag); });
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
        findTemplate: findTemplate,
        getRecipeTypeStepVars: getRecipeTypeStepVars,
        resolveParentType: resolveParentType,
        getOptionRulesData: getOptionRulesData,
        getOptionRulesForAction: getOptionRulesForAction,
        getOptionRulesForFamily: getOptionRulesForFamily,
        getOptionRulesForStep: getOptionRulesForStep,
        getStepDefaultValue: getStepDefaultValue,
        getStepGroupScore: getStepGroupScore,
        collectIngredientTags: collectIngredientTags,
        shouldShowStep: shouldShowStep
    };
})(window);
