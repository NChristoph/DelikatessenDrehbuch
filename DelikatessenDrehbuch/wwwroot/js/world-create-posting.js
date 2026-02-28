(function (window, $) {
    'use strict';

        const config = window.CreatePostingConfig || {};
        const stepCatalog = Array.isArray(config.stepCatalog) ? config.stepCatalog : [];
        const stepIngredientBindings = config.stepIngredientBindings || {};
        let dbUnits = Array.isArray(config.dbUnits) ? config.dbUnits : [];
         if (!Array.isArray(dbUnits) || !dbUnits.length) {
             dbUnits = ($('.js-db-unit option').map(function () {
                 const de = ($(this).val() || $(this).text() || '').toString().trim();
                 return de ? { de, en: de, esp: de, prt: de, id: de, ms: de, nl: de, sv: de, da: de, no: de } : null;
             }).get()).filter(Boolean);
         }

         function getCookieValue(name) {
             const cookie = document.cookie.split('; ').find(row => row.startsWith(`${name}=`));
             return cookie ? decodeURIComponent(cookie.split('=')[1]) : null;
         }

        function resolveLangKey(value) {
            const normalized = (value || 'de').toLowerCase();
            if (normalized === 'es') return 'esp';
            if (normalized === 'pt') return 'prt';
            if (normalized === 'se') return 'sv';
            if (normalized === 'dk') return 'da';
            return normalized;
        }

        let currentLang = resolveLangKey(getCookieValue('deli-lang'));
        let tempStepIdCounter = -1;
        let ingredientArticleRules = null;
        let activeIngredientConfigRow = null;
        let activeIngredientDbRow = null;

        function getUnitLabel(unitObj, langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            if (!unitObj || typeof unitObj !== 'object') return '';
            const map = {
                de: unitObj.de,
                en: unitObj.en,
                esp: unitObj.esp,
                prt: unitObj.prt,
                id: unitObj.id,
                ms: unitObj.ms,
                nl: unitObj.nl,
                sv: unitObj.sv,
                da: unitObj.da,
                no: unitObj.no
            };
            return (map[lang] || unitObj.de || '').toString();
        }

        function renderIngredientUnitButtons(selectedDe) {
            const selected = (selectedDe || '').toString().trim().toLowerCase();
            const html = (dbUnits || []).map(u => {
                const de = (u?.de || '').toString();
                if (!de) return '';
                const label = getUnitLabel(u, currentLang) || de;
                const active = de.toLowerCase() === selected ? 'active' : '';
                return `<button type="button" class="ingredient-unit-btn ${active}" data-unit="${$('<div>').text(de).html()}">${$('<div>').text(label).html()}</button>`;
            }).join('');
            $('#ingredientConfigUnitButtons').html(html);
            if (selected) $('#ingredientConfigUnit').val(selectedDe);
            else {
                const first = (dbUnits?.[0]?.de || '').toString();
                $('#ingredientConfigUnit').val(first);
                $('#ingredientConfigUnitButtons .ingredient-unit-btn').first().addClass('active');
            }
        }

        function placeIngredientPopupUnderTarget(targetElement) {
            const overlay = $('#ingredientConfigOverlay');
            const target = $(targetElement);
            if (!overlay.length || !target.length) return;

            $('.ingredient-config-row-host').remove();

            if (target.is('tr')) {
                const host = $('<tr class="ingredient-config-row-host"><td colspan="2"></td></tr>');
                target.after(host);
                host.find('td').append(overlay);
            } else {
                target.after(overlay);
            }

            overlay.removeClass('d-none');
        }

        function openIngredientConfigPopup(row) {
            const target = $(row);
            if (!target.length) return;
            activeIngredientConfigRow = target;
            activeIngredientDbRow = null;
            const name = (target.find('.display-name-selected').first().text() || '').trim();
            const qty = (target.find('.ingredient-qty-hidden').val() || '0').toString();
            const unitDe = (target.find('.ingredient-unit-hidden').val() || '').toString();
            $('#ingredientConfigTitle').text(name || 'Zutat');
            $('#ingredientConfigQty').val(qty);
            renderIngredientUnitButtons(unitDe);
            $('#ingredientConfigOverlay').removeClass('d-none');
            placeIngredientPopupUnderTarget(target);
        }

        function openIngredientConfigForDbRow(row) {
            const target = $(row);
            if (!target.length) return;
            activeIngredientDbRow = target;
            activeIngredientConfigRow = null;
            const name = (target.data('name-' + currentLang) || target.data('name-de') || '').toString();
            const id = (target.data('ingredient-id') || '').toString();
            const existingRow = $('#selectedIngredients .ingredient-row').filter(function () {
                return ($(this).find('input[name$="IngredientsAndNutrients.Id"]').val() || '').toString() === id;
            }).first();
            const qty = existingRow.length ? (existingRow.find('.ingredient-qty-hidden').val() || '0').toString() : (normalizeDecimalInputValue(target.find('.js-db-qty').val()) || '0');
            const unitDe = existingRow.length ? (existingRow.find('.ingredient-unit-hidden').val() || '').toString() : (target.find('.js-db-unit').val() || (dbUnits?.[0]?.de || ''));
            $('#ingredientConfigTitle').text(name || 'Zutat');
            $('#ingredientConfigQty').val(qty);
            renderIngredientUnitButtons(unitDe);
            $('#ingredientConfigOverlay').removeClass('d-none');
            placeIngredientPopupUnderTarget(target);
        }

        function closeIngredientConfigPopup() {
            activeIngredientConfigRow = null;
            activeIngredientDbRow = null;
            const overlay = $('#ingredientConfigOverlay');
            overlay.addClass('d-none');
            $('.ingredient-config-row-host').remove();
            $('#selectedIngredients').after(overlay);
        }

        function applyIngredientConfigPopup() {
            const qty = normalizeDecimalInputValue($('#ingredientConfigQty').val()) || '0';
            const unitDe = ($('#ingredientConfigUnit').val() || '').toString();
            const unitObj = (dbUnits || []).find(u => (u?.de || '').toString().toLowerCase() === unitDe.toLowerCase());
            const unitLabel = getUnitLabel(unitObj, currentLang) || unitDe;

            if (activeIngredientConfigRow && activeIngredientConfigRow.length) {
                activeIngredientConfigRow.find('.ingredient-qty-hidden').val(qty);
                activeIngredientConfigRow.find('.ingredient-unit-hidden').val(unitDe);
                activeIngredientConfigRow.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
                closeIngredientConfigPopup();
                return;
            }

            if (activeIngredientDbRow && activeIngredientDbRow.length) {
                const ingredientId = (activeIngredientDbRow.data('ingredient-id') || '').toString();
                if (!ingredientId) {
                    closeIngredientConfigPopup();
                    return;
                }
                const existingRow = $('#selectedIngredients .ingredient-row').filter(function () {
                    return ($(this).find('input[name$="IngredientsAndNutrients.Id"]').val() || '').toString() === ingredientId;
                }).first();

                if (existingRow.length) {
                    existingRow.find('.ingredient-qty-hidden').val(qty);
                    existingRow.find('.ingredient-unit-hidden').val(unitDe);
                    existingRow.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
                } else {
                    activeIngredientDbRow.find('.js-db-qty').val(qty);
                    activeIngredientDbRow.find('.js-db-unit').val(unitDe);
                    const addBtn = activeIngredientDbRow.find('button[onclick^="addIngredient"]').first();
                    if (addBtn.length) {
                        addIngredient(ingredientId, addBtn[0]);
                    }
                }
                closeIngredientConfigPopup();
            }
        }

        async function loadIngredientArticleRules() {
            try {
                const response = await fetch('/data/ingredient_article_rules.json', { cache: 'no-store' });
                if (!response.ok) return;
                ingredientArticleRules = await response.json();
            } catch (_) {
                ingredientArticleRules = null;
            }
        }

        function normalizeGenusKey(genusRaw = '') {
            const value = (genusRaw || '').toString().trim().toLowerCase();
            if (!value) return '';
            if (['m', 'masc', 'mask', 'masculine', 'masculin', 'der', 'el', 'o', 'de'].some(x => value === x || value.includes(x))) return 'masc';
            if (['f', 'fem', 'feminine', 'femin', 'die', 'la', 'a'].some(x => value === x || value.includes(x))) return 'fem';
            if (['n', 'neut', 'neuter', 'das', 'het', 'det'].some(x => value === x || value.includes(x))) return 'neut';
            return '';
        }

        function applyArticleToName(name, article, lang) {
            const base = (name || '').toString().trim();
            const art = (article || '').toString().trim();
            if (!base || !art) return base;

            const langRules = {
                de: ['der', 'die', 'das', 'den', 'dem', 'des'],
                en: ['the', 'a', 'an'],
                esp: ['el', 'la', 'los', 'las', 'un', 'una'],
                prt: ['o', 'a', 'os', 'as', 'um', 'uma'],
                nl: ['de', 'het', 'een']
            };

            const prefixList = langRules[resolveLangKey(lang || currentLang || 'de')] || [];
            const lower = base.toLowerCase();
            if (prefixList.some(x => lower.startsWith(`${x} `))) return base;
            return `${art} ${base}`;
        }

        function getAllStepRows() {
            return Array.isArray(stepCatalog) ? stepCatalog : [];
        }

        function getPhaseLabel(phase) {
            const map = {
                1: { text: 'Vorb.', cls: 'phase-badge-1' },
                2: { text: 'Kochen', cls: 'phase-badge-2' },
                3: { text: 'Würzen', cls: 'phase-badge-3' },
                4: { text: 'Finish', cls: 'phase-badge-4' }
            };
            const info = map[phase];
            if (!info) return '';
            return `<span class="phase-badge ${info.cls}">${info.text}</span>`;
        }







        // Hilfsfunktion: Gibt die Zutat-IDs zurück, an die ein Step gebunden ist
        function getStepBoundIngredientIds(stepId) {
            return (stepIngredientBindings[stepId] ?? stepIngredientBindings[stepId?.toString?.()] ?? []).map(x => parseInt(x, 10));
        }







        function getSelectedIngredientIds() {
            return $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                return parseInt($(this).val(), 10);
            }).get().filter(id => !isNaN(id));
        }

        function getSelectedIngredientsForSandbox() {
            const fromSelected = $('#selectedIngredients .ingredient-row').map(function () {
                const row = $(this);
                const id = row.find('input[name$="IngredientsAndNutrients.Id"]').val()?.toString() || '';
                const name = row.find('.display-name-selected').text()?.trim() || '';
                const genusByLang = {
                    de: (row.data('genus-de') || '').toString().trim(),
                    en: (row.data('genus-en') || '').toString().trim(),
                    esp: (row.data('genus-esp') || '').toString().trim(),
                    prt: (row.data('genus-prt') || '').toString().trim(),
                    id: (row.data('genus-id') || '').toString().trim(),
                    nl: (row.data('genus-nl') || '').toString().trim(),
                    sv: (row.data('genus-sv') || '').toString().trim(),
                    da: (row.data('genus-da') || '').toString().trim(),
                    no: (row.data('genus-no') || '').toString().trim(),
                    ms: (row.data('genus-ms') || '').toString().trim()
                };
                return {
                    id,
                    name,
                    genusDe: genusByLang.de || '',
                    genusLocalized: genusByLang[currentLang] || '',
                    genusByLang
                };
            }).get().filter(x => x.id || x.name);

            if (fromSelected.length) {
                return fromSelected.map(x => ({
                    id: x.id || '',
                    name: x.name || 'Zutat',
                    genusDe: x.genusDe || '',
                    genusLocalized: x.genusLocalized || '',
                    genusByLang: x.genusByLang || {}
                }));
            }
            return [];
        }

        const isDarkTheme = true;
        const creatorState = {
            selectedIngredientIds: [],
            selectedTemplateId: '',
            activeStepIngredientRow: null,
            computedPronoun: 'ihn',
            computedArticle: 'den',
            previewText: '',
            ingredientReplaceArmed: false,
            activePlaceholderTokenId: '',
            placeholderAssignments: {},
            dragIngredientName: '',
            toastMessage: '',
            toastVisible: false,
            advancedPronounOverride: '',
            advancedArticleOverride: '',
            activeDurationTokenId: '',
            durationValue: 10,
            durationUnit: 'minute',
            activeCountTokenId: '',
            countValue: 2,
            activeTemperatureTokenId: '',
            temperatureValue: 180,
            temperatureUnit: 'celsius',
            activeHeatTokenId: '',
            heatValue: 'mittlerer',
            activeModeTokenId: '',
            modeValue: 'Ober- und Unterhitze',
            activePronounTokenId: '',
            pronounValue: '',
            activeEquipmentTokenId: '',
            equipmentValue: 'die Pfanne',
            equipmentArticleValue: 'die',
            activeStateTokenId: '',
            stateValue: 'goldbraun',
            activeToolTokenId: '',
            toolValue: 'Messer',
            toolArticleValue: '',
            activeShapeTokenId: '',
            activeGrindSizeTokenId: '',
            activeBaseTokenId: '',
            grindSizeValue: 'feine',
            shapeValue: 'Würfel',
            activeItemTokenId: '',
            itemValue: 'den Teig',
            itemArticleValue: 'den',
            baseValue: 'den Teig',
            baseArticleValue: 'den',
            activeBalanceTokenId: '',
            balanceValue: 'die Säure',
            balanceArticleValue: 'die',
            activeSeasoningsTokenId: '',
            seasoningsValue: 'Salz und Pfeffer'
        };

        const upsertStepUrl = config.upsertStepUrl || '';

        function getIngredientEmoji(name) {
            const text = (name || '').toLowerCase();
            if (text.includes('basil')) return '🌿';
            if (text.includes('tomat')) return '🍅';
            if (text.includes('zwiebel')) return '🧅';
            if (text.includes('knoblauch')) return '🧄';
            if (text.includes('reis')) return '🍚';
            if (text.includes('salat')) return '🥗';
            return '🥣';
        }

        function resolveGrammarForIngredient(ingredientName, genusByLang = {}, langKey = 'de') {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const value = (ingredientName || '').toString().trim().toLowerCase();
            const genusRaw = (genusByLang?.[lang] || genusByLang?.de || '').toString().trim().toLowerCase();
            const genusKey = normalizeGenusKey(genusRaw);

            const fromRules = ingredientArticleRules && ingredientArticleRules[lang] && ingredientArticleRules[lang][genusKey]
                ? ingredientArticleRules[lang][genusKey]
                : null;

            if (fromRules && (fromRules.article != null || fromRules.pronoun != null)) {
                return {
                    pronoun: (fromRules.pronoun || '').toString(),
                    article: (fromRules.article || '').toString()
                };
            }

            const hasAny = (hints) => hints.some(x => genusRaw === x || genusRaw.includes(x));

            if (lang === 'de') {
                const known = {
                    m: ['basilikum', 'reis', 'zucker', 'knoblauch', 'ingwer', 'kohl'],
                    f: ['tomate', 'zwiebel', 'paprika', 'karotte', 'kartoffel', 'soße', 'sauce'],
                    n: ['salz', 'öl', 'wasser', 'ei', 'mehl', 'fleisch', 'brot']
                };
                if (hasAny(['f', 'fem', 'femin', 'die'])) return { pronoun: 'sie', article: 'die' };
                if (hasAny(['n', 'neu', 'neut', 'das'])) return { pronoun: 'es', article: 'das' };
                if (hasAny(['m', 'mas', 'mask', 'der'])) return { pronoun: 'ihn', article: 'den' };

                let gender = 'm';
                if (known.f.some(x => value.includes(x))) gender = 'f';
                else if (known.n.some(x => value.includes(x))) gender = 'n';
                if (gender === 'f') return { pronoun: 'sie', article: 'die' };
                if (gender === 'n') return { pronoun: 'es', article: 'das' };
                return { pronoun: 'ihn', article: 'den' };
            }

            return { pronoun: 'it', article: lang === 'en' ? 'the' : '' };
        }


        function getPlaceholderKeysFromTemplate(template) {
            const tpl = template?.templates?.de || template?.templates?.en || '';
            const keys = new Set();
            const regex = /\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g;
            let match;
            while ((match = regex.exec(tpl)) !== null) {
                keys.add(match[1]);
            }
            return Array.from(keys);
        }

        function getSelectedIngredientNames() {
            const ingredients = getSelectedIngredientsForSandbox();
            if (!ingredients.length) return [];
            const selectedIds = creatorState.selectedIngredientIds || [];
            if (!selectedIds.length) return [];
            const selected = ingredients.filter(x => selectedIds.includes(x.id));
            return selected.map(x => x.name);
        }

        function getSelectedIngredientNamesWithArticle(langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const ingredients = getSelectedIngredientsForSandbox();
            if (!ingredients.length) return [];

            const selectedIds = creatorState.selectedIngredientIds || [];
            if (!selectedIds.length) return [];

            return ingredients
                .filter(x => selectedIds.includes(x.id))
                .map(x => {
                    const grammar = resolveGrammarForIngredient(x.name, x.genusByLang || {}, lang);
                    return applyArticleToName(x.name, grammar.article, lang) || x.name;
                });
        }

        function getSelectedIngredientForCreator() {
            const ingredients = getSelectedIngredientsForSandbox();
            const selectedId = (creatorState.selectedIngredientIds || [])[0] || '';
            if (!selectedId) return null;

            const selected = ingredients.find(x => x.id === selectedId);
            if (!selected) return null;

            return {
                id: selected.id || '',
                name: selected.name || '',
                genusDe: selected.genusDe || '',
                genusLocalized: selected.genusLocalized || '',
                genusByLang: selected.genusByLang || {}
            };
        }

        function getSelectedIngredientValueForInsert() {
            const names = getSelectedIngredientNamesWithArticle(currentLang);
            return names.length ? names.join(', ') : '';
        }

        function syncGrammarAssignmentsForSelectedIngredient() {
            const selectedIngredient = getSelectedIngredientForCreator();
            const grammar = resolveGrammarForIngredient(selectedIngredient?.name || '', selectedIngredient?.genusByLang || {}, currentLang);
            const pronounValue = (creatorState.advancedPronounOverride || grammar.pronoun || '').toString().trim();
            const articleValue = (creatorState.advancedArticleOverride || grammar.article || '').toString().trim();

            creatorState.computedPronoun = pronounValue;
            creatorState.computedArticle = articleValue;

            Object.keys(creatorState.placeholderAssignments || {}).forEach(tokenId => {
                const key = (tokenId || '').toString().split('__')[0].trim().toLowerCase();
                if (key === 'pronoun') {
                    if (pronounValue) creatorState.placeholderAssignments[tokenId] = pronounValue;
                    else delete creatorState.placeholderAssignments[tokenId];
                }
                if (key === 'article') {
                    if (articleValue) creatorState.placeholderAssignments[tokenId] = articleValue;
                    else delete creatorState.placeholderAssignments[tokenId];
                }
            });
        }

        function getEffectiveTemplateId() {
            return creatorState.selectedTemplateId;
        }

        function isIngredientPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'ingredient' || value === 'ingredients' || value === 'liquid' || value === 'fat';
        }

        function isDurationPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value.includes('duration');
        }

        function isPronounPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'pronoun';
        }

        function isTemperaturePlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'temp' || value === 'temperature';
        }

        function isHeatPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'heat' || value.includes('heat');
        }

        function isModePlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'mode' || value.includes('mode');
        }

        function isEquipmentPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'equipment';
        }

        function isStatePlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'state' || value.includes('state') || value.includes('consistency');
        }

        function isToolPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'tool';
        }

        function isGrindSizePlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'grind_size' || value === 'grindsize' || value.includes('grind');
        }

        function isShapePlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'shape';
        }

        function isBasePlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'base';
        }

        function isItemPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'item' || value === 'dish' || value.includes('item');
        }

        function isCountPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'count' || value === 'servings' || value.includes('count');
        }

        function isBalancePlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'balance';
        }

        function isSeasoningsPlaceholderKey(key) {
            const value = (key || '').toString().trim().toLowerCase();
            return value === 'seasonings' || value === 'spices';
        }

        function getPlaceholderKeyByTokenId(tokenId) {
            return ($(`#masterPreviewText .placeholder-token[data-placeholder-token-id="${tokenId}"]`).data('placeholder-key') || '').toString();
        }


        function applyTokenAssignmentsToVariables(vars) {
            const merged = { ...(vars || {}) };
            const assignments = creatorState.placeholderAssignments || {};

            Object.keys(assignments).forEach(function (tokenId) {
                const assignedValue = (assignments[tokenId] || '').toString().trim();
                if (!assignedValue) return;

                const key = (tokenId || '').toString().split('__')[0].trim();
                if (!key) return;

                merged[key] = assignedValue;
            });

            return merged;
        }

        function getDurationUnitLabel(unitKey, langKey) {
            const labels = {
                minute: { de: 'Minuten', en: 'minutes', esp: 'minutos', prt: 'minutos', id: 'menit', nl: 'minuten', sv: 'minuter', da: 'minutter', no: 'minutter', ms: 'minit' },
                hour: { de: 'Stunden', en: 'hours', esp: 'horas', prt: 'horas', id: 'jam', nl: 'uur', sv: 'timmar', da: 'timer', no: 'timer', ms: 'jam' }
            };
            const key = (unitKey || 'minute').toString().toLowerCase();
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();
            return labels[key]?.[lang] || labels[key]?.de || '';
        }

        function getDurationInsertText() {
            const value = parseInt(creatorState.durationValue, 10);
            const safeValue = Number.isFinite(value) && value > 0 ? value : 1;
            const unitLabel = getDurationUnitLabel(creatorState.durationUnit, currentLang);
            return `${safeValue} ${unitLabel}`.trim();
        }

        function refreshDurationUnitControls() {
            const minuteLabel = getDurationUnitLabel('minute', currentLang);
            const hourLabel = getDurationUnitLabel('hour', currentLang);
            const minuteOption = $('#durationUnitSelect option[value="minute"]');
            const hourOption = $('#durationUnitSelect option[value="hour"]');
            if (minuteOption.length) minuteOption.text(minuteLabel);
            if (hourOption.length) hourOption.text(hourLabel);
        }

        function openDurationEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeDurationTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s+/);
            if (parsed) creatorState.durationValue = parseInt(parsed[1], 10);
            $('#durationValueInput').val(creatorState.durationValue || 10);
            refreshDurationUnitControls();
            $('#durationUnitSelect').val(creatorState.durationUnit || 'minute');
            $('#durationEditor').removeClass('d-none');
        }

        function closeDurationEditor() {
            creatorState.activeDurationTokenId = '';
            $('#durationEditor').addClass('d-none');
        }

        function getCountInsertText() {
            const value = parseInt(creatorState.countValue, 10);
            const safeValue = Number.isFinite(value) && value > 0 ? value : 1;
            return `${safeValue}`;
        }

        function openCountEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeCountTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)/);
            if (parsed) creatorState.countValue = parseInt(parsed[1], 10);
            $('#countValueInput').val(creatorState.countValue || 2);
            $('#countEditor').removeClass('d-none');
        }

        function closeCountEditor() {
            creatorState.activeCountTokenId = '';
            $('#countEditor').addClass('d-none');
        }

        function getTemperatureInsertText() {
            const value = parseInt(creatorState.temperatureValue, 10);
            const safeValue = Number.isFinite(value) && value > 0 ? value : 180;
            const unit = creatorState.temperatureUnit === 'fahrenheit' ? '°F' : '°C';
            return `${safeValue}${unit}`;
        }

        function openTemperatureEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeTemperatureTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s*°?\s*(C|F)?/i);
            if (parsed) {
                creatorState.temperatureValue = parseInt(parsed[1], 10);
                if (parsed[2] && parsed[2].toUpperCase() === 'F') {
                    creatorState.temperatureUnit = 'fahrenheit';
                }
            }
            $('#temperatureValueInput').val(creatorState.temperatureValue || 180);
            $('#temperatureUnitSelect').val(creatorState.temperatureUnit || 'celsius');
            $('#temperatureEditor').removeClass('d-none');
        }

        function closeTemperatureEditor() {
            creatorState.activeTemperatureTokenId = '';
            $('#temperatureEditor').addClass('d-none');
        }

        function getHeatOptions() {
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('heat', currentLang || 'de');
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['niedriger', 'mittlerer', 'hoher'];
        }

        function renderInlineHeatOptions() {
            const wrap = $('#inlineHeatOptions');
            if (!wrap.length) return;
            const options = getHeatOptions();
            const currentVal = (creatorState.heatValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-heat-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function openHeatEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeHeatTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.heatValue || '').toString().trim().toLowerCase();
            const options = getHeatOptions();
            const selected = options.find(x => x.toLowerCase() === currentText) || options[1] || options[0] || 'mittlerer';
            creatorState.heatValue = selected;
            renderInlineHeatOptions();
            placeEditorLikeTemperature('#heatEditor');
            $('#heatEditor').removeClass('d-none');
        }

        function closeHeatEditor() {
            creatorState.activeHeatTokenId = '';
            $('#heatEditor').addClass('d-none');
        }

        function getModeOptions() {
            const fallback = {
                de: ['Oberhitze', 'Unterhitze', 'Ober- und Unterhitze', 'Umluft'],
                en: ['top heat', 'bottom heat', 'top and bottom heat', 'convection'],
                esp: ['calor superior', 'calor inferior', 'calor superior e inferior', 'convección'],
                prt: ['calor superior', 'calor inferior', 'calor superior e inferior', 'convecção'],
                id: ['panas atas', 'panas bawah', 'panas atas dan bawah', 'konveksi'],
                nl: ['bovenwarmte', 'onderwarmte', 'boven- en onderwarmte', 'hetelucht'],
                sv: ['övervärme', 'undervärme', 'över- och undervärme', 'varmluft'],
                da: ['overvarme', 'undervarme', 'over- og undervarme', 'varmluft'],
                no: ['overvarme', 'undervarme', 'over- og undervarme', 'varmluft'],
                ms: ['haba atas', 'haba bawah', 'haba atas dan bawah', 'peredaran udara']
            };
            const lang = (currentLang || 'de').toString().toLowerCase();
            return fallback[lang] || fallback.de;
        }

        function openModeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeModeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.modeValue || '').toString().trim().toLowerCase();
            const options = getModeOptions();
            const selected = options.find(x => x.toLowerCase() === currentText) || options[2] || options[0] || 'Ober- und Unterhitze';
            creatorState.modeValue = selected;
            renderInlineModeOptions();
            placeEditorLikeTemperature('#modeEditor');
            $('#modeEditor').removeClass('d-none');
        }

        function closeModeEditor() {
            creatorState.activeModeTokenId = '';
            $('#modeEditor').addClass('d-none');
        }

        function renderInlineModeOptions() {
            const wrap = $('#inlineModeOptions');
            if (!wrap.length) return;
            const options = getModeOptions();
            const currentVal = (creatorState.modeValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-mode-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function openPronounEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activePronounTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.computedPronoun || '').toString().trim();
            creatorState.pronounValue = currentText;
            renderInlinePronounOptions();
            placeEditorLikeTemperature('#pronounEditor');
            $('#pronounEditor').removeClass('d-none');
        }

        function closePronounEditor() {
            creatorState.activePronounTokenId = '';
            $('#pronounEditor').addClass('d-none');
        }

        function renderInlinePronounOptions() {
            const wrap = $('#inlinePronounOptions');
            if (!wrap.length) return;
            const options = getPronounSuggestionsForLang(currentLang);
            const currentVal = (creatorState.pronounValue || creatorState.computedPronoun || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-pronoun-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function ensureEquipmentArticle(value, langKey) {
            const raw = (value || '').toString().trim();
            if (!raw) return '';

            const lang = resolveLangKey(langKey || currentLang || 'de');
            const articlePrefixesByLang = {
                de: ['der', 'die', 'das', 'den', 'dem', 'des'],
                en: ['the'],
                esp: ['el', 'la', 'los', 'las'],
                prt: ['o', 'a', 'os', 'as'],
                nl: ['de', 'het']
            };

            const lower = raw.toLowerCase();
            const existing = articlePrefixesByLang[lang] || [];
            if (existing.some(x => lower.startsWith(`${x} `))) return raw;

            const articleMapByLang = {
                de: {
                    'backofen': 'den',
                    'pfanne': 'die',
                    'bräter': 'den',
                    'topf': 'den',
                    'küchenmaschine': 'die',
                    'auflaufform': 'die'
                },
                en: {
                    'oven': 'the',
                    'pan': 'the',
                    'roaster': 'the',
                    'pot': 'the',
                    'food processor': 'the',
                    'baking dish': 'the'
                },
                esp: {
                    'horno': 'el',
                    'sartén': 'la',
                    'asador': 'el',
                    'olla': 'la',
                    'procesador de alimentos': 'el',
                    'fuente para horno': 'la'
                },
                prt: {
                    'forno': 'o',
                    'frigideira': 'a',
                    'assadeira': 'a',
                    'panela': 'a',
                    'processador de alimentos': 'o',
                    'travessa de forno': 'a'
                },
                nl: {
                    'oven': 'de',
                    'pan': 'de',
                    'braadslede': 'de',
                    'kookpot': 'de',
                    'keukenmachine': 'de',
                    'ovenschaal': 'de'
                }
            };

            const article = articleMapByLang[lang]?.[lower] || '';
            if (!article) return raw;
            return `${article} ${raw}`;
        }

        function getEditorArticleOptions(langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('articles', lang);
                if (Array.isArray(options) && options.length) {
                    return options.filter(x => (x || '').toString().trim() !== '');
                }
            }
            const map = {
                de: ['der', 'die', 'das', 'den'],
                en: ['the'],
                esp: ['el', 'la', 'los', 'las'],
                prt: ['o', 'a', 'os', 'as'],
                nl: ['de', 'het']
            };
            return map[lang] || [];
        }

        function splitLeadingArticle(value, langKey = currentLang) {
            const raw = (value || '').toString().trim();
            if (!raw) return { article: '', noun: '' };
            const options = getEditorArticleOptions(langKey);
            const lower = raw.toLowerCase();
            const found = options.find(x => lower.startsWith(`${x.toLowerCase()} `));
            if (!found) return { article: '', noun: raw };
            return { article: found, noun: raw.substring(found.length).trim() };
        }

        function composeArticleAndNoun(article, noun) {
            const art = (article || '').toString().trim();
            const n = (noun || '').toString().trim();
            if (!n) return '';
            return art ? `${art} ${n}` : n;
        }

        function getNounOptions(options, langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const result = (options || []).map(x => splitLeadingArticle(x, lang).noun).filter(Boolean);
            return Array.from(new Set(result));
        }

        function renderArticleOptions(wrapSelector, selectedArticle) {
            const wrap = $(wrapSelector);
            if (!wrap.length) return;
            const options = getEditorArticleOptions(currentLang);
            if (!options.length) {
                wrap.empty().addClass('d-none');
                return;
            }
            wrap.removeClass('d-none');
            const current = (selectedArticle || '').toString().trim().toLowerCase();
            const noArticleLabelByLang = {
                de: 'ohne',
                en: 'none',
                esp: 'sin',
                prt: 'sem',
                nl: 'zonder'
            };
            const noArticleLabel = noArticleLabelByLang[resolveLangKey(currentLang)] || 'none';
            const optionHtml = options.map(x => {
                const isActive = current === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-article-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            const noArticleIsActive = !current;
            const noArticleBtnClass = noArticleIsActive ? 'btn-light text-dark' : 'btn-outline-light';
            const noArticleHtml = `<button type="button" class="btn btn-sm ${noArticleBtnClass} inline-article-opt" data-value="">${$('<div>').text(noArticleLabel).html()}</button>`;
            wrap.html(noArticleHtml + optionHtml);
        }

        function getEquipmentOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            const fallbackByLang = {
                de: ['Pfanne', 'Topf', 'Backofen', 'Rührschüssel', 'Sieb', 'Mixer', 'Pürierstab', 'Küchenmaschine', 'Bräter', 'Wok', 'Grill', 'Dampfgarer', 'Auflaufform', 'Zange', 'Schneidebrett'],
                en: ['pan', 'pot', 'oven', 'mixing bowl', 'strainer', 'blender', 'immersion blender', 'food processor', 'roaster', 'wok', 'grill', 'steamer', 'baking dish', 'tongs', 'cutting board'],
                esp: ['sartén', 'olla', 'horno', 'bol para mezclar', 'colador', 'batidora', 'batidora de mano', 'procesador de alimentos', 'asador', 'wok', 'parrilla', 'vaporera', 'fuente para horno', 'pinzas', 'tabla de cortar'],
                prt: ['frigideira', 'panela', 'forno', 'tigela de mistura', 'coador', 'liquidificador', 'mixer de mão', 'processador de alimentos', 'assadeira', 'wok', 'grelha', 'cozedor a vapor', 'travessa de forno', 'pinça', 'tábua de corte'],
                id: ['wajan', 'panci', 'oven', 'mangkuk adonan', 'saringan', 'blender', 'blender tangan', 'food processor', 'loyang panggang', 'wok', 'pemanggang', 'kukusan', 'pinggan oven', 'penjepit', 'talenan'],
                nl: ['pan', 'kookpot', 'oven', 'mengkom', 'zeef', 'blender', 'staafmixer', 'keukenmachine', 'braadslede', 'wok', 'grill', 'stoomkoker', 'ovenschaal', 'tang', 'snijplank'],
                sv: ['stekpanna', 'gryta', 'ugn', 'blandningsskål', 'sil', 'mixer', 'stavmixer', 'matberedare', 'stekgryta', 'wok', 'grill', 'ångkokare', 'ugnsform', 'tång', 'skärbräda'],
                da: ['pande', 'gryde', 'ovn', 'røreskål', 'si', 'blender', 'stavblender', 'foodprocessor', 'bradepande', 'wok', 'grill', 'dampkoger', 'ovnfast fad', 'tang', 'skærebræt'],
                no: ['stekepanne', 'gryte', 'ovn', 'miksebolle', 'sil', 'blender', 'stavmikser', 'kjøkkenmaskin', 'stekeform', 'wok', 'grill', 'dampkoker', 'ildfast form', 'klype', 'skjærefjøl'],
                ms: ['kuali', 'periuk', 'ketuhar', 'mangkuk adunan', 'penapis', 'pengisar', 'pengisar tangan', 'pemproses makanan', 'dulang pembakar', 'wok', 'pemanggang', 'pengukus', 'bekas pembakar', 'penyepit', 'papan pemotong']
            };

            const rendererOptions = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('equipment', lang)
                : [];

            const fallback = fallbackByLang[lang] || fallbackByLang.de;
            const merged = [...(Array.isArray(rendererOptions) ? rendererOptions : []), ...fallback]
                .map(x => ensureEquipmentArticle((x || '').toString().trim(), lang))
                .filter(Boolean);

            return Array.from(new Set(merged));
        }

        function openEquipmentEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeEquipmentTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.equipmentValue || '').toString().trim();
            const options = getEquipmentOptions();
            const selected = options.find(x => x.toLowerCase() === (currentText || '').toLowerCase()) || options[0] || 'die Pfanne';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.equipmentArticleValue = parts.article;
            creatorState.equipmentValue = selected;
            renderInlineEquipmentOptions();
            placeEditorLikeTemperature('#equipmentEditor');
            $('#equipmentEditor').removeClass('d-none');
        }

        function closeEquipmentEditor() {
            creatorState.activeEquipmentTokenId = '';
            $('#equipmentEditor').addClass('d-none');
        }

        function renderInlineEquipmentOptions() {
            const wrap = $('#inlineEquipmentOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineEquipmentArticleOptions', creatorState.equipmentArticleValue);
            const options = getNounOptions(getEquipmentOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.equipmentValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-equipment-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getStateOptions() {
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('state', currentLang || 'de');
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['goldbraun', 'weich', 'glasig', 'gar', 'knusprig', 'cremig', 'bissfest', 'eingedickt', 'sprudelnd'];
        }

        function openStateEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeStateTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const options = getStateOptions();
            const selected = options.find(x => x.toLowerCase() === (currentText || '').toLowerCase()) || options[0] || 'goldbraun';
            creatorState.stateValue = selected;
            renderInlineStateOptions();
            placeEditorLikeTemperature('#stateEditor');
            $('#stateEditor').removeClass('d-none');
        }

        function closeStateEditor() {
            creatorState.activeStateTokenId = '';
            $('#stateEditor').addClass('d-none');
        }

        function renderInlineStateOptions() {
            const wrap = $('#inlineStateOptions');
            if (!wrap.length) return;
            const options = getStateOptions();
            const currentVal = (creatorState.stateValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-state-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getToolOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            const fallbackByLang = {
                de: ['Messer', 'Sparschäler', 'Reibe', 'Schneebesen', 'Spatel', 'Holzlöffel', 'Suppenkelle', 'Messbecher', 'Nudelholz', 'Teigschaber'],
                en: ['knife', 'peeler', 'grater', 'whisk', 'spatula', 'wooden spoon', 'ladle', 'measuring cup', 'rolling pin', 'dough scraper'],
                esp: ['cuchillo', 'pelador', 'rallador', 'batidor', 'espátula', 'cuchara de madera', 'cucharón', 'vaso medidor', 'rodillo', 'rasqueta de masa'],
                prt: ['faca', 'descascador', 'ralador', 'batedor', 'espátula', 'colher de pau', 'concha', 'copo medidor', 'rolo de massa', 'raspador de massa'],
                id: ['pisau', 'pengupas', 'parutan', 'pengocok', 'spatula', 'sendok kayu', 'sendok sayur', 'gelas ukur', 'rolling pin', 'scraper adonan'],
                nl: ['mes', 'dunschiller', 'rasp', 'garde', 'spatel', 'houten lepel', 'soeplepel', 'maatbeker', 'deegroller', 'deegschraper'],
                sv: ['kniv', 'potatisskalare', 'rivjärn', 'visp', 'stekspade', 'träslev', 'slev', 'måttkopp', 'kavel', 'degskrapa'],
                da: ['kniv', 'skræller', 'rivejern', 'piskeris', 'spatel', 'træske', 'suppeske', 'målebæger', 'kagerulle', 'dejskraber'],
                no: ['kniv', 'skreller', 'rivjern', 'visp', 'stekespade', 'tresleiv', 'øse', 'målebeger', 'kjevle', 'deigskrape'],
                ms: ['pisau', 'pengupas', 'parut', 'pemukul', 'spatula', 'sudu kayu', 'senduk', 'cawan penyukat', 'penggelek doh', 'pengikis doh']
            };

            const rendererOptions = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('tool', lang)
                : [];

            const fallback = fallbackByLang[lang] || fallbackByLang.de;
            const merged = [...(Array.isArray(rendererOptions) ? rendererOptions : []), ...fallback]
                .map(x => (x || '').toString().trim())
                .filter(Boolean);

            return Array.from(new Set(merged));
        }

        function openToolEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeToolTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.toolValue || '').toString().trim();
            const options = getToolOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'Messer';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.toolArticleValue = parts.article;
            creatorState.toolValue = composeArticleAndNoun(parts.article, parts.noun || selected);
            renderInlineToolOptions();
            $('#toolEditor').removeClass('d-none');
        }

        function closeToolEditor() {
            creatorState.activeToolTokenId = '';
            $('#toolEditor').addClass('d-none');
        }

        function renderInlineToolOptions() {
            const wrap = $('#inlineToolOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineToolArticleOptions', creatorState.toolArticleValue);
            const options = getNounOptions(getToolOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.toolValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-tool-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getGrindSizeOptions() {
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('grind_size', currentLang || 'de');
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            const fallback = {
                de: ['fein', 'feine', 'mittel', 'mittlere', 'grob', 'grobe', 'dünn', 'dünne', 'breit', 'breite', 'klein', 'kleine', 'groß', 'große', 'ca. 1 cm groß', 'ca. 1 cm große', 'ca. 0,5 mm groß', 'ca. 0,5 mm große'],
                en: ['fine', 'medium', 'coarse', 'thin', 'wide', 'small', 'large', 'about 3/8-inch', 'about 0.02-inch'],
                esp: ['finas', 'medianas', 'gruesas', 'delgadas', 'anchas', 'pequeñas', 'grandes'],
                prt: ['finas', 'médias', 'grossas', 'finas', 'largas', 'pequenas', 'grandes'],
                id: ['halus', 'sedang', 'kasar', 'tipis', 'tebal', 'kecil', 'besar'],
                nl: ['fijne', 'middelgrote', 'grove', 'dunne', 'brede', 'kleine', 'grote'],
                sv: ['fina', 'medelgrova', 'grova', 'tunna', 'breda', 'små', 'stora'],
                da: ['fine', 'mellemstore', 'grove', 'tynde', 'brede', 'små', 'store'],
                no: ['fine', 'middels', 'grove', 'tynne', 'brede', 'små', 'store'],
                ms: ['halus', 'sederhana', 'kasar', 'nipis', 'lebar', 'kecil', 'besar']
            };
            return fallback[(currentLang || 'de').toLowerCase()] || fallback.de;
        }

        function openGrindSizeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeGrindSizeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.grindSizeValue || '').toString().trim();
            const options = getGrindSizeOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'feine';
            creatorState.grindSizeValue = selected;
            renderInlineGrindSizeOptions();
            placeEditorLikeTemperature('#grindSizeEditor');
            $('#grindSizeEditor').removeClass('d-none');
        }

        function closeGrindSizeEditor() {
            creatorState.activeGrindSizeTokenId = '';
            $('#grindSizeEditor').addClass('d-none');
        }

        function renderInlineGrindSizeOptions() {
            const wrap = $('#inlineGrindSizeOptions');
            if (!wrap.length) return;
            const options = getGrindSizeOptions();
            const currentVal = (creatorState.grindSizeValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-grind-size-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getShapeOptions() {
            return ['Würfel', 'Scheiben', 'Streifen', 'Spalten', 'grobe Stücke', 'Ringe', 'Julienne', 'Stifte'];
        }

        function openShapeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeShapeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.shapeValue || '').toString().trim();
            creatorState.shapeValue = currentText || 'Würfel';
            renderInlineShapeOptions();
            placeEditorLikeTemperature('#shapeEditor');
            $('#shapeEditor').removeClass('d-none');
        }

        function closeShapeEditor() {
            creatorState.activeShapeTokenId = '';
            $('#shapeEditor').addClass('d-none');
        }
        function getBaseAndItemOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            const rendererBase = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('base', lang)
                : [];
            const rendererItem = window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function'
                ? MasterStepRenderer.getVariablePresets('item', lang)
                : [];

            const fallbackByLang = {
                de: ['den Teig', 'die Masse'],
                en: ['the dough', 'the mixture'],
                esp: ['la masa', 'la mezcla'],
                prt: ['a massa', 'a mistura'],
                id: ['adonan', 'campuran'],
                nl: ['het deeg', 'het mengsel'],
                sv: ['degen', 'blandningen'],
                da: ['dejen', 'blandingen'],
                no: ['deigen', 'blandingen'],
                ms: ['doh', 'campuran']
            };

            const merged = [
                ...(Array.isArray(rendererBase) ? rendererBase : []),
                ...(Array.isArray(rendererItem) ? rendererItem : []),
                ...((fallbackByLang[lang] || fallbackByLang.de))
            ].map(x => (x || '').toString().trim()).filter(Boolean);

            const seen = new Set();
            return merged.filter(x => {
                const key = x.toLowerCase();
                if (seen.has(key)) return false;
                seen.add(key);
                return true;
            });
        }

        function getBaseOptions() {
            return getBaseAndItemOptions();
        }

        function openBaseEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBaseTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.baseValue || '').toString().trim();
            const options = getBaseOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'den Teig';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.baseArticleValue = parts.article;
            creatorState.baseValue = selected;
            renderInlineBaseOptions();
            $('#baseEditor').removeClass('d-none');
        }

        function closeBaseEditor() {
            creatorState.activeBaseTokenId = '';
            $('#baseEditor').addClass('d-none');
        }

        function renderInlineBaseOptions() {
            const wrap = $('#inlineBaseOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineBaseArticleOptions', creatorState.baseArticleValue);
            const options = getNounOptions(getBaseOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.baseValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-base-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }
        function getItemOptions() {
            return getBaseAndItemOptions();
        }

        function openItemEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeItemTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.itemValue || '').toString().trim();
            const options = getItemOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'den Teig';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.itemArticleValue = parts.article;
            creatorState.itemValue = selected;
            renderInlineItemOptions();
            placeEditorLikeTemperature('#itemEditor');
            $('#itemEditor').removeClass('d-none');
        }

        function closeItemEditor() {
            creatorState.activeItemTokenId = '';
            $('#itemEditor').addClass('d-none');
        }

        function renderInlineItemOptions() {
            const wrap = $('#inlineItemOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineItemArticleOptions', creatorState.itemArticleValue);
            const options = getNounOptions(getItemOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.itemValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-item-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getBalanceOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('balance', lang);
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['die Säure', 'die Süße', 'die Schärfe'];
        }

        function openBalanceEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBalanceTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.balanceValue || '').toString().trim();
            const options = getBalanceOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'die Säure';
            const parts = splitLeadingArticle(selected, currentLang);
            creatorState.balanceArticleValue = parts.article;
            creatorState.balanceValue = selected;
            renderInlineBalanceOptions();
            placeEditorLikeTemperature('#balanceEditor');
            $('#balanceEditor').removeClass('d-none');
        }

        function closeBalanceEditor() {
            creatorState.activeBalanceTokenId = '';
            $('#balanceEditor').addClass('d-none');
        }

        function renderInlineBalanceOptions() {
            const wrap = $('#inlineBalanceOptions');
            if (!wrap.length) return;
            renderArticleOptions('#inlineBalanceArticleOptions', creatorState.balanceArticleValue);
            const options = getNounOptions(getBalanceOptions(), currentLang);
            const currentVal = splitLeadingArticle(creatorState.balanceValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-balance-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getSeasoningsOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                const options = MasterStepRenderer.getVariablePresets('seasonings', lang);
                if (Array.isArray(options) && options.length) {
                    return options;
                }
            }
            return ['Salz', 'Pfeffer', 'Salz und Pfeffer', 'Kräuter', 'Gewürze'];
        }

        function openSeasoningsEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeSeasoningsTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.seasoningsValue || '').toString().trim();
            const options = getSeasoningsOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'Salz';
            creatorState.seasoningsValue = selected;
            renderInlineSeasoningsOptions();
            placeEditorLikeTemperature('#seasoningsEditor');
            $('#seasoningsEditor').removeClass('d-none');
        }

        function closeSeasoningsEditor() {
            creatorState.activeSeasoningsTokenId = '';
            $('#seasoningsEditor').addClass('d-none');
        }

        function renderInlineSeasoningsOptions() {
            const wrap = $('#inlineSeasoningsOptions');
            if (!wrap.length) return;
            const options = getSeasoningsOptions();
            const currentVal = (creatorState.seasoningsValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-seasonings-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function closeAllEditors() {
            closeDurationEditor();
            closeCountEditor();
            closeTemperatureEditor();
            closeHeatEditor();
            closeModeEditor();
            closePronounEditor();
            closeEquipmentEditor();
            closeStateEditor();
            closeToolEditor();
            closeGrindSizeEditor();
            closeShapeEditor();
            closeBaseEditor();
            closeItemEditor();
            closeBalanceEditor();
            closeSeasoningsEditor();
        }

        function placeEditorLikeTemperature(editorSelector) {
            const editor = $(editorSelector);
            const tempEditor = $('#temperatureEditor');
            if (!editor.length || !tempEditor.length) return;

            editor.removeClass('bottom-sheet-editor').addClass('duration-editor mt-2');
            tempEditor.after(editor);
        }

        function renderInlineShapeOptions() {
            const wrap = $('#inlineShapeOptions');
            if (!wrap.length) return;
            const options = getShapeOptions();
            const currentVal = (creatorState.shapeValue || '').toString().trim().toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} inline-shape-opt" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getPronounSuggestionsForLang(langKey) {
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();
            const map = {
                de: ['ihn', 'sie', 'es'],
                en: ['him', 'her', 'it', 'them'],
                esp: ['lo', 'la', 'ellos', 'ellas'],
                prt: ['o', 'a', 'eles', 'elas'],
                id: ['dia', 'mereka'],
                nl: ['hem', 'haar', 'het', 'hen'],
                sv: ['honom', 'henne', 'den', 'det'],
                da: ['ham', 'hende', 'den', 'det'],
                no: ['ham', 'henne', 'den', 'det'],
                ms: ['dia', 'mereka']
            };
            return map[lang] || map.de;
        }

        function getLocalizedFallbackForVariable(key, langKey) {
            const keyNorm = (key || '').toString().trim().toLowerCase();
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();

            if (keyNorm === 'base') {
                const map = {
                    de: 'den Teig',
                    en: 'the dough',
                    esp: 'la masa',
                    prt: 'a massa',
                    id: 'adonan',
                    nl: 'het deeg',
                    sv: 'degen',
                    da: 'dejen',
                    no: 'deigen',
                    ms: 'doh'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'dough') {
                const map = {
                    de: 'den Teig',
                    en: 'the dough',
                    esp: 'la masa',
                    prt: 'a massa',
                    id: 'adonan',
                    nl: 'het deeg',
                    sv: 'degen',
                    da: 'dejen',
                    no: 'deigen',
                    ms: 'doh'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step1') {
                const map = {
                    de: 'Mehl', en: 'flour', esp: 'harina', prt: 'farinha', id: 'tepung', nl: 'bloem', sv: 'mjöl', da: 'mel', no: 'mel', ms: 'tepung'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step2') {
                const map = {
                    de: 'Ei', en: 'egg', esp: 'huevo', prt: 'ovo', id: 'telur', nl: 'ei', sv: 'ägg', da: 'æg', no: 'egg', ms: 'telur'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step3') {
                const map = {
                    de: 'Brösel', en: 'breadcrumbs', esp: 'pan rallado', prt: 'farinha de rosca', id: 'tepung roti', nl: 'paneermeel', sv: 'ströbröd', da: 'rasp', no: 'brødsmuler', ms: 'serbuk roti'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'equipment') {
                return getEquipmentOptions()[0] || 'die Pfanne';
            }

            if (keyNorm === 'tool') {
                return getToolOptions()[0] || 'Messer';
            }

            if (keyNorm === 'grind_size' || keyNorm === 'grindsize') {
                return getGrindSizeOptions()[0] || 'feine';
            }

            if (keyNorm === 'item' || keyNorm === 'dish') {
                return getItemOptions()[0] || 'den Teig';
            }

            if (keyNorm === 'balance') {
                return getBalanceOptions()[0] || 'die Säure';
            }

            if (keyNorm === 'seasonings' || keyNorm === 'spices') {
                return getSeasoningsOptions()[2] || getSeasoningsOptions()[0] || 'Salz und Pfeffer';
            }

            return '';
        }

        function buildVariablesForTemplate(templateId) {
            const template = MasterStepRenderer.findTemplate(templateId);
            const selectedIngredient = getSelectedIngredientForCreator();
            const selectedNames = getSelectedIngredientNames();
            const ingredientName = selectedIngredient?.name || (selectedNames.length ? selectedNames.join(', ') : '');
            const grammar = resolveGrammarForIngredient(ingredientName, selectedIngredient?.genusByLang || {}, currentLang);
            const ingredientNameWithArticle = applyArticleToName(ingredientName, grammar.article, currentLang);

            creatorState.computedPronoun = creatorState.advancedPronounOverride || grammar.pronoun;
            creatorState.computedArticle = creatorState.advancedArticleOverride || grammar.article;

            const defaults = MasterStepRenderer.getSmartDefaults(templateId, {
                ingredientName: ingredientNameWithArticle || ingredientName,
                recipeCategory: $('#Recipe_Category').val()
            }) || {};

            const keys = new Set([...(template?.variables || []), ...getPlaceholderKeysFromTemplate(template)]);
            const vars = { ...defaults };

            keys.forEach(key => {
                if (key === 'ingredient' || key === 'ingredients' || key === 'liquid') {
                    vars[key] = key;
                    return;
                }

                if (vars[key] == null || String(vars[key]).trim() === '') {
                    if (key === 'pronoun') vars[key] = creatorState.computedPronoun;
                    else if (key === 'article') vars[key] = creatorState.computedArticle;
                    else if (key.includes('duration')) vars[key] = getDurationInsertText();
                    else if (isCountPlaceholderKey(key)) vars[key] = getCountInsertText();
                    else if (key === 'temp' || key === 'temperature') vars[key] = getTemperatureInsertText();
                    else if (key.includes('heat')) vars[key] = creatorState.heatValue || 'mittlerer';
                    else if (isModePlaceholderKey(key)) vars[key] = creatorState.modeValue || getModeOptions()[2] || 'Ober- und Unterhitze';
                    else if (key.includes('tool')) vars[key] = getToolOptions()[0] || 'Messer';
                    else if (key.includes('grind')) vars[key] = creatorState.grindSizeValue || getGrindSizeOptions()[0] || 'feine';
                    else if (key === 'item' || key === 'dish') vars[key] = creatorState.itemValue || getItemOptions()[0] || 'den Teig';
                    else if (key === 'balance') vars[key] = creatorState.balanceValue || getBalanceOptions()[0] || 'die Säure';
                    else if (key === 'seasonings' || key === 'spices') vars[key] = creatorState.seasoningsValue || getSeasoningsOptions()[2] || 'Salz und Pfeffer';
                    else if (key.includes('shape')) vars[key] = 'feine Stücke';
                    else if (key === 'base') vars[key] = creatorState.baseValue || getLocalizedFallbackForVariable('base', currentLang) || 'den Teig';
                    else {
                        const localizedFallback = getLocalizedFallbackForVariable(key, currentLang);
                        vars[key] = localizedFallback || key;
                    }
                }
            });

            if (!vars.ingredient) vars.ingredient = 'ingredient';
            if (!vars.pronoun) vars.pronoun = creatorState.computedPronoun;
            if (!vars.article) vars.article = creatorState.computedArticle;
            return applyTokenAssignmentsToVariables(vars);
        }

        function animatePreview() {
            const card = $('#masterPreviewCard');
            card.addClass('preview-animate');
            setTimeout(() => card.removeClass('preview-animate'), 250);
        }

        function updatePreviewText() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) {
                creatorState.previewText = 'Wähle eine Zutat und ein Template.';
                $('#masterPreviewText').text(creatorState.previewText);
                return;
            }

            const template = MasterStepRenderer.findTemplate(templateId);
            const langKey = (currentLang || 'de').toLowerCase();
            const tpl = template?.templates?.[langKey] || template?.templates?.de || '';
            const vars = buildVariablesForTemplate(templateId);
            let placeholderOccurrence = 0;

            const previewHtml = (tpl || '').replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const k = String(key || '').trim();
                const tokenId = `${k}__${placeholderOccurrence++}`;
                const fallback = vars[k] != null ? String(vars[k]).trim() : k;
                const assigned = creatorState.placeholderAssignments[tokenId];
                const value = assigned || fallback || k;
                const safeValue = $('<div>').text(value).html();
                const safeFallback = $('<div>').text(fallback || k).html();
                const activeClass = creatorState.activePlaceholderTokenId === tokenId ? ' token-active' : '';
                return `<span class="placeholder-wrap" data-placeholder-token-id="${tokenId}">
                    <span class="token-highlight placeholder-token${activeClass}" draggable="false" data-placeholder-key="${k}" data-placeholder-token-id="${tokenId}">${safeValue}</span>
                    <button type="button" class="placeholder-reset" data-placeholder-token-id="${tokenId}" data-default-value="${safeFallback}" title="Zurücksetzen">↺</button>
                </span>`;
            });

            $('#masterPreviewText').html(previewHtml || 'Keine Vorschau verfügbar.');
            creatorState.previewText = $('#masterPreviewText').text().trim() || 'Keine Vorschau verfügbar.';

            if (!creatorState.ingredientReplaceArmed) { $('#masterPreviewCard').removeClass('token-replace-active'); }
            animatePreview();
        }

        function clearSelectedIngredientChips() {
            creatorState.selectedIngredientIds = [];
            renderIngredientChips();
        }

        function updateStoryProgress() {
            const count = $('#selectedSteps .step-row').length;
            $('#storyStepCount').text(`Schritte: ${count}`);
        }

        function showCreatorToast(message) {
            creatorState.toastMessage = message;
            creatorState.toastVisible = true;
            const toast = $('#creatorToast');
            toast.text(message).addClass('show');
            setTimeout(() => {
                creatorState.toastVisible = false;
                toast.removeClass('show');
            }, 1200);
        }

        function animateTemplateToPreview(templateTitle) {
            const ghost = $('#templateFlyGhost');
            ghost.text(templateTitle || 'Template').removeClass('fly');
            void ghost[0].offsetWidth;
            ghost.addClass('fly');
        }

        function renderIngredientChips() {
            const wrap = $('#masterIngredientButtons');
            const stepsWrap = $('#stepsIngredientButtons');
            const ingredients = getSelectedIngredientsForSandbox();
            wrap.empty();
            stepsWrap.empty();

            if (!ingredients.length) {
                wrap.append('<div class="small text-white-50">Wähle zuerst Zutaten aus.</div>');
                stepsWrap.append('<div class="small text-white-50">Keine Zutaten vorhanden.</div>');
                creatorState.selectedIngredientIds = [];
                updatePreviewText();
                return;
            }

            creatorState.selectedIngredientIds = (creatorState.selectedIngredientIds || []).filter(id => ingredients.some(x => x.id === id));

            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const emoji = getIngredientEmoji(item.name);
                const grammar = resolveGrammarForIngredient(item.name, item.genusByLang || {}, currentLang);
                const displayName = applyArticleToName(item.name, grammar.article, currentLang) || item.name;
                const chipHtml = `<button type="button" class="ingredient-chip ${active}" draggable="true" data-id="${item.id}" data-name="${displayName}">${emoji} ${displayName}</button>`;
                wrap.append(chipHtml);
                stepsWrap.append(chipHtml);
            });

            updatePreviewText();
        }

        function renderTemplateCards() {
            const box = $('#masterTemplateCards');
            box.empty();

            if (!window.MasterStepRenderer || typeof MasterStepRenderer.getAllTemplates !== 'function') {
                box.append('<div class="small text-white-50">Templates werden geladen ...</div>');
                return;
            }

            const loadError = typeof MasterStepRenderer.getLastLoadError === 'function' ? MasterStepRenderer.getLastLoadError() : '';
            if (loadError) {
                setMasterTemplateError(`Template-Fehler: ${loadError}`);
                return;
            }

            const templates = MasterStepRenderer.getAllTemplates();
            if (!templates.length) {
                setMasterTemplateError('Keine Templates gefunden. Prüfe /data/master_steps.json.');
                creatorState.selectedTemplateId = '';
                updatePreviewText();
                return;
            }

            if (!creatorState.selectedTemplateId || !templates.some(x => x.master_id === creatorState.selectedTemplateId)) {
                creatorState.selectedTemplateId = templates[0].master_id;
            }

            templates.forEach((step, index) => {
                const active = step.master_id === creatorState.selectedTemplateId ? 'active' : '';
                const icon = step.categoryIcon || (index % 3 === 0 ? '✨' : index % 3 === 1 ? '🔥' : '🔪');
                const vars = buildVariablesForTemplate(step.master_id);
                const snippet = MasterStepRenderer.render(step.master_id, vars, currentLang) || step.master_id;
                const title = (step.description || '').toString().trim() || `Template ${index + 1}`;

                box.append(`<button type="button" class="template-card ${active}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
            });

            updatePreviewText();
        }

        function showMasterStepCreatorError(error, context) {
            const message = error && error.message ? error.message : (error || 'Unbekannter Fehler');
            const details = error && error.stack ? `\nStack: ${error.stack.substring(0, 500)}` : '';
            console.error(`Master-Step-Creator Fehler (${context})`, error);
            alert(`Master-Step-Creator Fehler (${context}): ${message}${details}`);
        }

        function setMasterTemplateError(message) {
            const box = $('#masterTemplateCards');
            if (!box.length) return;
            box.html(`<div class="small text-warning">${message}</div>`);
        }

        function refreshMasterTemplateBuilder() {
            try {
                const creator = $('.smart-step-creator');
                creator.attr('data-theme', isDarkTheme ? 'dark' : 'light');
                renderIngredientChips();
                renderTemplateCards();
                updateStoryProgress();
            } catch (error) {
                showMasterStepCreatorError(error, 'refreshMasterTemplateBuilder');
            }
        }

        function collectMasterVariables() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) return {};
            return buildVariablesForTemplate(templateId);
        }

        function normalizeVariablesForPersist(template, vars) {
            const normalized = { ...(vars || {}) };
            const keys = new Set([...(template?.variables || []), ...getPlaceholderKeysFromTemplate(template)]);
            const ingredientValue = (getSelectedIngredientValueForInsert() || '').toString().trim();

            keys.forEach(function (key) {
                const current = (normalized[key] || '').toString().trim();

                if (isIngredientPlaceholderKey(key)) {
                    normalized[key] = ingredientValue || current;
                    return;
                }

                if (!current || current.toLowerCase() === key.toLowerCase()) {
                    normalized[key] = '';
                }
            });

            return normalized;
        }

        function renderTemplateForPersist(template, lang, vars) {
            const langKey = (lang || 'de').toLowerCase();
            let tpl = template?.templates?.[langKey] || template?.templates?.de || template?.templates?.en || '';
            if (!tpl) return '';

            Object.keys(vars || {}).forEach(function (key) {
                const val = (vars[key] || '').toString().trim();
                if (val) return;
                const esc = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                tpl = tpl.replace(new RegExp(`\\([^)]*\\{\\{\\s*${esc}\\s*\\}\\}[^)]*\\)`, 'gi'), ' ');
                tpl = tpl.replace(new RegExp(`\\[[^\\]]*\\{\\{\\s*${esc}\\s*\\}\\}[^\\]]*\\]`, 'gi'), ' ');
            });

            let rendered = tpl.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const value = vars && vars[key] != null ? String(vars[key]).trim() : '';
                return value;
            });

            rendered = rendered
                .replace(/\(\s*\)/g, ' ')
                .replace(/\[\s*\]/g, ' ')
                .replace(/\s+([,.;:!?])/g, '$1')
                .replace(/\s{2,}/g, ' ')
                .trim();

            return rendered;
        }

        function buildRenderedPayloadFromTemplate(template, vars) {
            return {
                de: renderTemplateForPersist(template, 'de', vars),
                en: renderTemplateForPersist(template, 'en', vars),
                esp: renderTemplateForPersist(template, 'esp', vars),
                prt: renderTemplateForPersist(template, 'prt', vars)
            };
        }

        function getCurrentUserHashForRequests() {
            const fromCookie = getCookieValue('WorldMiniAppUserHash');
            if (fromCookie) return fromCookie;

            const fromHidden = ($('#hiddenUserTokenField').val() || '').toString().trim();
            if (fromHidden) return fromHidden;

            const fromQuery = new URLSearchParams(window.location.search).get('userHash');
            return (fromQuery || '').toString().trim();
        }


        function createFallbackStepId() {
            tempStepIdCounter -= 1;
            return tempStepIdCounter.toString();
        }

        async function addRenderedMasterStep() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) return;

            const template = MasterStepRenderer.findTemplate(templateId);
            if (!template) return;

            const rawVars = collectMasterVariables();
            const vars = normalizeVariablesForPersist(template, rawVars);
            const rendered = buildRenderedPayloadFromTemplate(template, vars);
            const ingredientName = (vars.ingredient || vars.ingredients || vars.liquid || vars.fat || '').toString().trim();
            const payload = {
                de: rendered.de,
                en: rendered.en,
                esp: rendered.esp,
                prt: rendered.prt,
                phase: parseInt(template.phase || 0, 10),
                equipment: parseInt(template.equipment || 0, 10)
            };

            try {
                const userHash = getCurrentUserHashForRequests();
                const url = userHash ? `${upsertStepUrl}?userHash=${encodeURIComponent(userHash)}` : upsertStepUrl;
                const response = await fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) {
                    const errorText = await response.text();
                    throw new Error(errorText || 'Upsert fehlgeschlagen');
                }

                const result = await response.json();
                const newId = result?.id;
                if (!newId) {
                    throw new Error('Keine Step-ID erhalten');
                }

                const alreadyExists = getAllStepRows().some(x => x && x.id?.toString() === newId.toString());
                if (!alreadyExists) {
                    getAllStepRows().push({
                        id: newId,
                        de: rendered.de,
                        en: rendered.en,
                        esp: rendered.esp,
                        prt: rendered.prt,
                        phase: payload.phase,
                        equipment: payload.equipment
                    });
                }

                addStep(newId.toString(), null, rendered[currentLang] || rendered.de || `Schritt ${newId}`, { skipRender: true, ingredientName });
                updateStepIndices();
            } catch (error) {
                console.warn('Master-Step Upsert nicht möglich, nutze lokalen Fallback-Step', error);
                showMasterStepCreatorError(error, 'addRenderedMasterStep');

                const fallbackId = createFallbackStepId();
                getAllStepRows().push({
                    id: fallbackId,
                    de: rendered.de,
                    en: rendered.en,
                    esp: rendered.esp,
                    prt: rendered.prt,
                    phase: payload.phase,
                    equipment: payload.equipment
                });

                addStep(fallbackId, null, rendered[currentLang] || rendered.de || 'Neuer Schritt', {
                    skipRender: true,
                    ingredientName,
                    stepData: {
                        de: rendered.de,
                        en: rendered.en,
                        esp: rendered.esp,
                        prt: rendered.prt,
                        phase: payload.phase,
                        equipment: payload.equipment
                    }
                });
                updateStepIndices();
            }
        }

        function syncIngredientSourceVisibility() {
            const query = ($('#ingredientSearch').val() || '').toString().trim().toLowerCase();
            const selectedIds = $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                return $(this).val()?.toString();
            }).get();

            $('.ingredient-db-row').each(function () {
                const row = $(this);
                const id = row.data('ingredient-id')?.toString() || '';
                const text = ((row.data('name-' + currentLang) || row.data('name-de') || '') + '').toLowerCase();
                const isSelected = !!id && selectedIds.includes(id);
                const matchesSearch = !query || text.includes(query);
                row.toggle(!isSelected && matchesSearch);
            });
        }

        function getStepLabelFromRow(row) {
            if (!row) return '';
            return row[currentLang] || row.de || `Step ${row.id}`;
        }

        // Prüft ob ein Step bereits in der Auswahl ist
        function isStepAlreadySelected(stepId) {
            return $('#selectedSteps .step-row').filter(function () {
                return $(this).data('step-id')?.toString() === stepId.toString();
            }).length > 0;
        }











        function updateLanguageLabels() {
            const langKey = resolveLangKey(currentLang);
            $('.label-lang').each(function () { $(this).text($(this).data(langKey)); });
            $('.lang-ph').each(function () { $(this).attr('placeholder', $(this).data('ph-' + langKey)); });
            $('.lang-opt').each(function () { $(this).text($(this).data('txt-' + langKey)); });
            $('.ingredient-db-row').each(function () { $(this).find('.display-name').text($(this).data('name-' + langKey)); });
            $('#selectedIngredients .ingredient-row').each(function () {
                const row = $(this);
                const n = row.data('name-' + langKey) || row.data('name-de') || row.find('.display-name-selected').text();
                row.find('.display-name-selected').text(n);
                const qty = (row.find('.ingredient-qty-hidden').val() || '').toString();
                const unitDe = (row.find('.ingredient-unit-hidden').val() || '').toString();
                const unitObj = (dbUnits || []).find(u => (u?.de || '').toString().toLowerCase() === unitDe.toLowerCase());
                const unitLabel = getUnitLabel(unitObj, langKey) || unitDe;
                row.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
            });
            syncIngredientSourceVisibility();
            $('.keyword-btn').each(function () { $(this).text($(this).data('word-' + langKey)); });
            $('.keyword-pill').each(function () { $(this).find('.keyword-text').text($(this).data('word-' + langKey)); });
            refreshDurationUnitControls();
            refreshMasterTemplateBuilder();
        }

        function handleVideoUpload(input) {
            if (input.files && input.files[0]) {
                const file = input.files[0];
                const fileUrl = URL.createObjectURL(file);
                const isVideo = file.type.startsWith('video/');

                if (isVideo) {
                    $('#videoPreview').attr('src', fileUrl).removeClass('d-none');
                    $('#imagePreview').addClass('d-none').attr('src', '');
                } else {
                    $('#imagePreview').attr('src', fileUrl).removeClass('d-none');
                    $('#videoPreview').addClass('d-none').attr('src', '');
                }

                $('#videoPreviewContainer').removeClass('d-none');
                $('#uploadLabel').addClass('d-none');
            }
        }

        function resetVideo() {
            $('#videoInput').val('');
            $('#videoPreviewContainer').addClass('d-none');
            $('#uploadLabel').removeClass('d-none');
            $('#videoPreview').attr('src', '');
            $('#imagePreview').attr('src', '').addClass('d-none');
        }

        function normalizeDecimalInputValue(value) {
            if (!value) return value;
            return value.toString().trim().replace(/\s/g, '').replace(',', '.');
        }

               function addIngredient(id, btn) {
            const existing = $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').filter(function () {
                return $(this).val()?.toString() === id.toString();
            }).length > 0;

            if (existing) return;

            const row = $(btn).closest('.ingredient-db-row');
            const name = row.data('name-' + currentLang) || row.data('name-de');
            const genusDe = (row.data('genus-de') || '').toString().trim();
            const genusEn = (row.data('genus-en') || '').toString().trim();
            const genusEsp = (row.data('genus-esp') || '').toString().trim();
            const genusPrt = (row.data('genus-prt') || '').toString().trim();
            const genusId = (row.data('genus-id') || '').toString().trim();
            const genusNl = (row.data('genus-nl') || '').toString().trim();
            const genusSv = (row.data('genus-sv') || '').toString().trim();
            const genusDa = (row.data('genus-da') || '').toString().trim();
            const genusNo = (row.data('genus-no') || '').toString().trim();
            const genusMs = (row.data('genus-ms') || '').toString().trim();
            const selectedQuantity = normalizeDecimalInputValue(row.find('.js-db-qty').val()) || '0';
            const selectedUnit = (row.find('.js-db-unit').val() || (dbUnits?.[0]?.de ?? 'g.')).toString();
            const selectedUnitObj = (dbUnits || []).find(x => (x?.de || '').toString().toLowerCase() === selectedUnit.toLowerCase());
            const selectedUnitLabel = getUnitLabel(selectedUnitObj, currentLang) || selectedUnit;

            const newIngredient = `
            <div class="dynamic-item ingredient-row shadow-sm" onclick="openIngredientConfigPopup(this)" data-name-de="${$('<div>').text(row.data('name-de') || name).html()}" data-name-en="${$('<div>').text(row.data('name-en') || name).html()}" data-name-esp="${$('<div>').text(row.data('name-esp') || name).html()}" data-name-prt="${$('<div>').text(row.data('name-prt') || name).html()}" data-name-id="${$('<div>').text(row.data('name-id') || name).html()}" data-name-nl="${$('<div>').text(row.data('name-nl') || name).html()}" data-name-sv="${$('<div>').text(row.data('name-sv') || name).html()}" data-name-da="${$('<div>').text(row.data('name-da') || name).html()}" data-name-no="${$('<div>').text(row.data('name-no') || name).html()}" data-name-ms="${$('<div>').text(row.data('name-ms') || name).html()}" data-genus-de="${$('<div>').text(genusDe).html()}" data-genus-en="${$('<div>').text(genusEn).html()}" data-genus-esp="${$('<div>').text(genusEsp).html()}" data-genus-prt="${$('<div>').text(genusPrt).html()}" data-genus-id="${$('<div>').text(genusId).html()}" data-genus-nl="${$('<div>').text(genusNl).html()}" data-genus-sv="${$('<div>').text(genusSv).html()}" data-genus-da="${$('<div>').text(genusDa).html()}" data-genus-no="${$('<div>').text(genusNo).html()}" data-genus-ms="${$('<div>').text(genusMs).html()}">
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].IngredientsAndNutrients.Id" value="${id}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Quantity.Quantitys" class="ingredient-qty-hidden" value="${selectedQuantity}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Measure.Metriks_DE" class="ingredient-unit-hidden" value="${$('<div>').text(selectedUnit).html()}" />

                <div>
                    <div class="fw-bold display-name-selected">${name}</div>
                    <div class="ingredient-row-meta">${selectedQuantity} ${selectedUnitLabel}</div>
                </div>

                <button type="button" class="btn btn-sm text-danger p-0" onclick="event.stopPropagation(); removeIngredientRow(this)">
                    <i class="bi bi-trash3"></i>
                </button>
            </div>`;

            $('#selectedIngredients').append(newIngredient);

            refreshMasterTemplateBuilder();
            if (typeof syncIngredientSourceVisibility === "function") {
                syncIngredientSourceVisibility();
            }
        }

        function startStepIngredientEdit(btn) {
            const row = $(btn).closest('.step-row');
            creatorState.activeStepIngredientRow = row;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').removeClass('d-none');
            $('#card-steps')[0]?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            showCreatorToast('Chip auswählen – dann Einsetzen tippen');
        }

        function applyChipToStep() {
            const row = creatorState.activeStepIngredientRow;
            if (!row) { cancelStepIngredientEdit(); return; }

            const selectedNames = getSelectedIngredientNames();
            if (!selectedNames.length) { showCreatorToast('Bitte zuerst eine Zutat auswählen'); return; }

            const newName = selectedNames.join(', ');
            const oldName = (row.data('ingredient-name') || '').toString().trim();

            if (oldName) {
                const oldRx = new RegExp(oldName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
                ['step-hidden-de', 'step-hidden-en', 'step-hidden-esp', 'step-hidden-prt'].forEach(cls => {
                    const input = row.find('.' + cls);
                    if (input.length) input.val((input.val() || '').replace(oldRx, newName));
                });
                const textSpan = row.find('.step-text-content');
                textSpan.text((textSpan.text() || '').replace(oldRx, newName));
            }

            row.attr('data-ingredient-name', newName);
            creatorState.activeStepIngredientRow = null;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').addClass('d-none');
            showCreatorToast('Zutat im Schritt ersetzt');
        }

        function cancelStepIngredientEdit() {
            creatorState.activeStepIngredientRow = null;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').addClass('d-none');
        }

        function removeIngredientRow(btn) {
            $(btn).closest('.ingredient-row').remove();
            refreshMasterTemplateBuilder();
            syncIngredientSourceVisibility();
        }

        // Entfernt alle Steps die für dieselben Zutaten+Phase gebunden sind wie der neue Step
        function removeConflictingSteps(newStepId) {
            const newStep = getAllStepRows().find(x => x && x.id?.toString() === newStepId.toString());
            if (!newStep) return;

            const newStepPhase = parseInt(newStep.phase ?? 0, 10);
            if (newStepPhase < 1) return;

            const newStepBoundIds = getStepBoundIngredientIds(newStepId).map(x => x.toString());
            if (!newStepBoundIds.length) return;

            $('#selectedSteps .step-row').each(function () {
                const row = $(this);
                const rowStepId = row.data('step-id')?.toString();
                if (!rowStepId || rowStepId === newStepId.toString()) return;

                const rowStep = getAllStepRows().find(x => x && x.id?.toString() === rowStepId);
                if (!rowStep) return;

                const rowPhase = parseInt(rowStep.phase ?? 0, 10);
                if (rowPhase !== newStepPhase) return;

                // Prüfe ob der bestehende Step mindestens eine gemeinsame Zutat hat
                const rowBoundIds = getStepBoundIngredientIds(rowStepId).map(x => x.toString());
                const hasOverlap = rowBoundIds.some(id => newStepBoundIds.includes(id));
                if (hasOverlap) {
                    row.remove();
                }
            });
        }

        function addStep(id, btn, manualText = null, options = {}) {
            if (isStepAlreadySelected(id)) return;

            const enforceUniqueness = options?.enforceIngredientPhaseUniqueness === true;
            if (enforceUniqueness) {
                removeConflictingSteps(id);
            }

            const sourceRow = getAllStepRows().find(x => x && x.id?.toString() === id.toString());
            const stepData = options?.stepData || sourceRow || {};

            const normalizedStepData = {
                de: (stepData.de || '').toString(),
                en: (stepData.en || '').toString(),
                esp: (stepData.esp || '').toString(),
                prt: (stepData.prt || '').toString(),
                phase: parseInt(stepData.phase ?? 0, 10) || 0,
                equipment: parseInt(stepData.equipment ?? 0, 10) || 0
            };

            let text = manualText;
            if (!text) {
                text = normalizedStepData[currentLang] || normalizedStepData.de || `Schritt ${id}`;
            }

            if (!normalizedStepData[currentLang]) {
                normalizedStepData[currentLang] = text;
            }

            if (!normalizedStepData.de) normalizedStepData.de = normalizedStepData.en || text;
            if (!normalizedStepData.en) normalizedStepData.en = normalizedStepData.de || text;

            const stepIdAsInt = parseInt(id, 10);
            const postedStepId = Number.isNaN(stepIdAsInt) || stepIdAsInt < 1 ? 0 : stepIdAsInt;
            const phaseBadge = getPhaseLabel(normalizedStepData.phase);
            const stepIngredientName = (options?.ingredientName || '').toString().trim();
            const chipBtnHtml = stepIngredientName
                ? `<button type="button" class="btn btn-sm btn-outline-light opacity-75" onclick="startStepIngredientEdit(this)" title="Zutat per Chip ändern">🥣</button>`
                : '';

            $('#selectedSteps').append(`<div class="dynamic-item d-flex align-items-center step-row" draggable="true" data-step-id="${id}" data-ingredient-name="${$('<div>').text(stepIngredientName).html()}">
                <input type="hidden" name="RecipePreperationSteps[INDEX].PreperationStepId" value="${postedStepId}" />
                <input type="hidden" class="step-index-input" name="RecipePreperationSteps[INDEX].StepIndex" value="0" />
                <input type="hidden" class="step-hidden-de" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_DE" value="${$('<div>').text(normalizedStepData.de).html()}" />
                <input type="hidden" class="step-hidden-en" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_EN" value="${$('<div>').text(normalizedStepData.en).html()}" />
                <input type="hidden" class="step-hidden-esp" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_ESP" value="${$('<div>').text(normalizedStepData.esp).html()}" />
                <input type="hidden" class="step-hidden-prt" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_PRT" value="${$('<div>').text(normalizedStepData.prt).html()}" />
                <input type="hidden" class="step-hidden-phase" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Phase" value="${normalizedStepData.phase}" />
                <input type="hidden" class="step-hidden-equipment" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Equipment" value="${normalizedStepData.equipment}" />
                <div class="badge candy-purple rounded-pill me-3 step-badge">0</div>
                <div class="small flex-grow-1 display-step-selected">
                    ${phaseBadge}
                    <span class="step-text-content">${text}</span>
                    <div class="step-row-actions">
                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="moveStepRow(this, -1)"><i class="bi bi-arrow-up"></i></button>
                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="moveStepRow(this, 1)"><i class="bi bi-arrow-down"></i></button>
                        ${chipBtnHtml}
                        <button type="button" class="btn btn-sm text-danger opacity-50" onclick="removeStep(this)"><i class="bi bi-trash3"></i></button>
                    </div>
                </div>
            </div>`);

            updateStepIndices();

        }

        function removeStep(btn) {
            $(btn).closest('.step-row').remove();
            updateStepIndices();
        }

        function toggleKeyword(id, btn) {
            const container = $('#selectedKeywords');
            const existing = container.find(`.keyword-pill[data-keyword-id="${id}"]`);
            if (existing.length > 0) {
                existing.remove();
                $(btn).removeClass('btn-secondary').addClass('btn-outline-secondary');
                return;
            }

            const word = $(btn).data('word-' + currentLang) || $(btn).data('word-de');
            container.append(`<div class="keyword-pill badge rounded-pill bg-secondary text-white d-inline-flex align-items-center me-2 mb-2"
                data-keyword-id="${id}"
                data-word-de="${$(btn).data('word-de')}"
                data-word-en="${$(btn).data('word-en')}"
                data-word-esp="${$(btn).data('word-esp')}"
                data-word-prt="${$(btn).data('word-prt')}">
                <input type="hidden" name="SelectedKeywordIds" value="${id}" />
                <span class="me-2 keyword-text">${word}</span>
                <button type="button" class="btn btn-sm btn-light rounded-circle p-0" style="width:20px;height:20px;line-height:1;" onclick="removeKeyword('${id}')"><i class="bi bi-x"></i></button>
            </div>`);
            $(btn).removeClass('btn-outline-secondary').addClass('btn-secondary');
        }

        function removeKeyword(id) {
            $('#selectedKeywords').find(`.keyword-pill[data-keyword-id="${id}"]`).remove();
            $(`.keyword-btn[data-keyword-id="${id}"]`).removeClass('btn-secondary').addClass('btn-outline-secondary');
        }

        function updateStepIndices() {
            $('#selectedSteps .step-row').each(function (i) {
                $(this).find('.step-badge').text(i + 1);
                $(this).find('.step-index-input').val(i + 1);
            });
            updateStoryProgress();
        }

        function prepareBinding() {
            updateStepIndices();
            $('.ingredient-row').each(function (i) {
                $(this).find('input, select').each(function () {
                    if (this.name) this.name = this.name.replace(/\[.*?\]/, '[' + i + ']');
                });
            });
            $('.step-row').each(function (i) {
                $(this).find('input, select').each(function () {
                    if (this.name) this.name = this.name.replace(/\[.*?\]/, '[' + i + ']');
                });
            });
            $('input[name$=".Quantity.Quantitys"], .js-decimal-input').each(function () {
                this.value = normalizeDecimalInputValue(this.value);
            });
            return true;
        }

        function moveStepRow(btn, direction) {
            const row = $(btn).closest('.step-row');
            if (direction < 0) {
                const prev = row.prev('.step-row');
                if (prev.length) prev.before(row);
            } else {
                const next = row.next('.step-row');
                if (next.length) next.after(row);
            }
            updateStepIndices();
        }

        let draggedStepRow = null;

        $(document).ready(function () {
            const token = sessionStorage.getItem('UserToken') || localStorage.getItem('UserToken');
            if (token) $('#hiddenUserTokenField').val(token);

            $('#ingredientSearch').on('input', function () {
                syncIngredientSourceVisibility();
            });
            refreshDurationUnitControls();
            loadIngredientArticleRules().finally(() => {
            MasterStepRenderer.load().then((result) => {
                if (!result) {
                    const loadError = typeof MasterStepRenderer.getLastLoadError === 'function' ? MasterStepRenderer.getLastLoadError() : 'Template-Datei konnte nicht geladen werden.';
                    setMasterTemplateError(`Template-Fehler: ${loadError}`);
                    return;
                }
                refreshMasterTemplateBuilder();
            });
            });

            $('#masterIngredientButtons').on('click', '.ingredient-chip', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                const selected = creatorState.selectedIngredientIds || [];
                if (selected.includes(id)) {
                    creatorState.selectedIngredientIds = selected.filter(x => x !== id);
                } else {
                    creatorState.selectedIngredientIds = [...selected, id];
                }

                creatorState.advancedPronounOverride = '';
                creatorState.advancedArticleOverride = '';
                syncGrammarAssignmentsForSelectedIngredient();

                if (creatorState.activePlaceholderTokenId) {
                    const key = getPlaceholderKeyByTokenId(creatorState.activePlaceholderTokenId);
                    if (isIngredientPlaceholderKey(key)) {
                        const selectedValue = getSelectedIngredientValueForInsert();
                        if (selectedValue) creatorState.placeholderAssignments[creatorState.activePlaceholderTokenId] = selectedValue;
                        creatorState.ingredientReplaceArmed = false;
                        clearSelectedIngredientChips();
                        creatorState.activePlaceholderTokenId = '';
                        $('#masterPreviewCard').removeClass('token-replace-active');
                        showCreatorToast('Platzhalter ersetzt');
                    }
                }

                renderIngredientChips();
                renderTemplateCards();
            });

            $('#masterTemplateCards').on('click', '.template-card', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                creatorState.selectedTemplateId = id;
                creatorState.activePlaceholderTokenId = '';
                creatorState.placeholderAssignments = {};
                closeAllEditors();
                $('.template-card').removeClass('active');
                $(this).addClass('active template-tap');
                setTimeout(() => $(this).removeClass('template-tap'), 180);
                animateTemplateToPreview($(this).data('title'));
                updatePreviewText();
                showCreatorToast('Template gewählt');
            });

            function openEditorForPlaceholderToken(key, tokenId) {
                try {
                if (!key || !tokenId) { return; }

                closeAllEditors();
                creatorState.ingredientReplaceArmed = false;
                $('#masterPreviewCard').removeClass('token-replace-active');

                if (isIngredientPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = tokenId;
                    creatorState.ingredientReplaceArmed = true;
                    $('#masterPreviewCard').addClass('token-replace-active');
                    const hasExistingAssignment = !!(creatorState.placeholderAssignments[tokenId] || '').toString().trim();
                    if (hasExistingAssignment) {
                        // Bereits belegt – Chips leeren und Ersetzen-Modus aktivieren
                        clearSelectedIngredientChips();
                        showCreatorToast('Neue Zutat auswählen zum Ersetzen');
                    } else {
                        const selectedValue = getSelectedIngredientValueForInsert();
                        if (selectedValue) {
                            creatorState.placeholderAssignments[tokenId] = selectedValue;
                            clearSelectedIngredientChips();
                            creatorState.activePlaceholderTokenId = '';
                            creatorState.ingredientReplaceArmed = false;
                            $('#masterPreviewCard').removeClass('token-replace-active');
                            showCreatorToast('Platzhalter ersetzt');
                        } else {
                            showCreatorToast('Erst Zutaten auswählen');
                        }
                    }
                } else if (isDurationPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openDurationEditorForToken(tokenId);
                    showCreatorToast('Zeit setzen');
                } else if (isCountPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openCountEditorForToken(tokenId);
                    showCreatorToast('Anzahl setzen');
                } else if (isPronounPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = tokenId;
                    openPronounEditorForToken(tokenId);
                    showCreatorToast('Pronomen wählen');
                } else if (isTemperaturePlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openTemperatureEditorForToken(tokenId);
                    showCreatorToast('Temperatur setzen');
                } else if (isHeatPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openHeatEditorForToken(tokenId);
                    showCreatorToast('Hitze-Stufe wählen');
                } else if (isModePlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openModeEditorForToken(tokenId);
                    showCreatorToast('Ofenmodus wählen');
                } else if (isEquipmentPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openEquipmentEditorForToken(tokenId);
                    showCreatorToast('Tool / Gerät wählen');
                } else if (isStatePlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openStateEditorForToken(tokenId);
                    showCreatorToast('Zustand wählen');
                } else if (isToolPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openToolEditorForToken(tokenId);
                    showCreatorToast('Tool / Gerät wählen');
                } else if (isGrindSizePlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openGrindSizeEditorForToken(tokenId);
                    showCreatorToast('Schnittgröße wählen');
                } else if (isShapePlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openShapeEditorForToken(tokenId);
                    showCreatorToast('Schnittform wählen');
                } else if (isBasePlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openBaseEditorForToken(tokenId);
                    showCreatorToast('Basis wählen');
                } else if (isItemPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openItemEditorForToken(tokenId);
                    showCreatorToast('Item wählen');
                } else if (isBalancePlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openBalanceEditorForToken(tokenId);
                    showCreatorToast('Balance wählen');
                } else if (isSeasoningsPlaceholderKey(key)) {
                    creatorState.activePlaceholderTokenId = '';
                    openSeasoningsEditorForToken(tokenId);
                    showCreatorToast('Seasonings wählen');
                } else {
                    creatorState.activePlaceholderTokenId = '';
                }

                renderTemplateCards();
                } catch (err) {
                    alert('ERROR in openEditorForPlaceholderToken: ' + (err.message || err) + '\nStack: ' + (err.stack || ''));
                }
            }

            $('#masterPreviewText').on('click', '.placeholder-token, .placeholder-wrap', function (e) {
                try {
                if ($(e.target).closest('.placeholder-reset').length) return;

                const token = $(this).hasClass('placeholder-token') ? $(this) : $(this).find('.placeholder-token').first();
                const key = (token.data('placeholder-key') || '').toString();
                const tokenId = (token.data('placeholder-token-id') || '').toString();
                openEditorForPlaceholderToken(key, tokenId);
                } catch (err) {
                    alert('ERROR in token click: ' + (err.message || err));
                }
            });

            $('#masterIngredientButtons').on('dragstart', '.ingredient-chip', function (e) {
                const name = ($(this).data('name') || '').toString();
                if (!name) return;
                creatorState.dragIngredientName = name;
                e.originalEvent.dataTransfer.effectAllowed = 'copy';
                e.originalEvent.dataTransfer.setData('text/plain', name);
            });

            $('#masterPreviewText').on('dragover', '.placeholder-token', function (e) {
                const key = ($(this).data('placeholder-key') || '').toString();
                if (!isIngredientPlaceholderKey(key)) return;
                e.preventDefault();
                $(this).addClass('drag-over');
                e.originalEvent.dataTransfer.dropEffect = 'copy';
            });

            $('#masterPreviewText').on('dragleave', '.placeholder-token', function () {
                $(this).removeClass('drag-over');
            });

            $('#masterPreviewText').on('drop', '.placeholder-token', function (e) {
                e.preventDefault();
                $(this).removeClass('drag-over');
                const tokenId = ($(this).data('placeholder-token-id') || '').toString();
                const key = ($(this).data('placeholder-key') || '').toString();
                const dropped = e.originalEvent.dataTransfer.getData('text/plain') || creatorState.dragIngredientName || '';
                if (!tokenId || !dropped) return;
                if (!isIngredientPlaceholderKey(key)) {
                    showCreatorToast('Drag & Drop nur für ingredient/ingredients');
                    return;
                }
                creatorState.activePlaceholderTokenId = tokenId;
                const existingValue = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
                const dropValue = dropped.toString().trim();
                creatorState.placeholderAssignments[tokenId] = existingValue
                    ? (existingValue.toLowerCase().includes(dropValue.toLowerCase()) ? existingValue : `${existingValue}, ${dropValue}`)
                    : dropValue;
                creatorState.ingredientReplaceArmed = false;
                clearSelectedIngredientChips();
                creatorState.activePlaceholderTokenId = '';
                $('#masterPreviewCard').removeClass('token-replace-active');
                closeAllEditors();
                showCreatorToast('Zutat per Drag & Drop eingesetzt');
                renderTemplateCards();
            });

            $('#masterPreviewText').on('click', '.placeholder-reset', function (e) {
                e.stopPropagation();
                const tokenId = ($(this).data('placeholder-token-id') || '').toString();
                const defaultValue = ($(this).data('default-value') || '').toString();
                if (!tokenId) return;
                if (defaultValue) creatorState.placeholderAssignments[tokenId] = defaultValue;
                else delete creatorState.placeholderAssignments[tokenId];
                if (creatorState.activePlaceholderTokenId === tokenId) {
                    creatorState.activePlaceholderTokenId = '';
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');
                }
                closeAllEditors();
                showCreatorToast('Platzhalter zurückgesetzt');
                renderIngredientChips();
                renderTemplateCards();
            });

            $('#durationValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.durationValue = Number.isFinite(value) && value > 0 ? value : 1;
            });

            $('#durationUnitSelect').on('change', function () {
                creatorState.durationUnit = ($(this).val() || 'minute').toString();
            });

            $('#btnApplyDuration').on('click', function () {
                const tokenId = creatorState.activeDurationTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getDurationInsertText();
                closeDurationEditor();
                showCreatorToast('Zeit eingesetzt');
                renderTemplateCards();
            });

            $('#countValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.countValue = Number.isFinite(value) && value > 0 ? value : 1;
            });

            $('#btnApplyCount').on('click', function () {
                const tokenId = creatorState.activeCountTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getCountInsertText();
                closeCountEditor();
                showCreatorToast('Anzahl eingesetzt');
                renderTemplateCards();
            });

            $('#temperatureValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.temperatureValue = Number.isFinite(value) && value > 0 ? value : 180;
            });

            $('#temperatureUnitSelect').on('change', function () {
                creatorState.temperatureUnit = ($(this).val() || 'celsius').toString();
            });

            $('#btnApplyTemperature').on('click', function () {
                const tokenId = creatorState.activeTemperatureTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getTemperatureInsertText();
                closeTemperatureEditor();
                showCreatorToast('Temperatur eingesetzt');
                renderTemplateCards();
            });

            $('#inlineHeatOptions').on('click', '.inline-heat-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.heatValue = val;
                renderInlineHeatOptions();
            });

            $('#btnApplyHeat').on('click', function () {
                const tokenId = creatorState.activeHeatTokenId;
                const val = (creatorState.heatValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.heatValue = val;
                creatorState.placeholderAssignments[tokenId] = val;
                closeHeatEditor();
                showCreatorToast('Hitze-Stufe eingesetzt');
                renderTemplateCards();
            });

            $('#inlineModeOptions').on('click', '.inline-mode-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.modeValue = val;
                renderInlineModeOptions();
            });

            $('#btnApplyMode').on('click', function () {
                const tokenId = creatorState.activeModeTokenId;
                const val = (creatorState.modeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.modeValue = val;
                creatorState.placeholderAssignments[tokenId] = val;
                closeModeEditor();
                showCreatorToast('Ofenmodus eingesetzt');
                renderTemplateCards();
            });

            $('#btnOpenAdvancedSettings').on('click', function () {
                openPronounEditorForToken(creatorState.activePronounTokenId || '__global__');
            });

            $('#inlinePronounOptions').on('click', '.inline-pronoun-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.pronounValue = val;
                renderInlinePronounOptions();
            });

            $('#btnApplyPronoun').on('click', function () {
                const tokenId = creatorState.activePronounTokenId;
                const val = (creatorState.pronounValue || creatorState.computedPronoun || '').toString().trim();
                if (!val) return;
                creatorState.advancedPronounOverride = val;
                creatorState.computedPronoun = val;
                if (tokenId && tokenId !== '__global__') {
                    creatorState.placeholderAssignments[tokenId] = val;
                }
                closePronounEditor();
                showCreatorToast('Pronomen eingesetzt');
                renderTemplateCards();
            });

            $('#inlineEquipmentOptions').on('click', '.inline-equipment-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.equipmentValue = composeArticleAndNoun(creatorState.equipmentArticleValue, val);
                renderInlineEquipmentOptions();
            });

            $('#inlineEquipmentArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.equipmentArticleValue = val;
                const noun = splitLeadingArticle(creatorState.equipmentValue || '', currentLang).noun;
                creatorState.equipmentValue = composeArticleAndNoun(val, noun);
                renderInlineEquipmentOptions();
            });

            $('#inlineToolArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.toolArticleValue = val;
                const noun = splitLeadingArticle(creatorState.toolValue || '', currentLang).noun;
                creatorState.toolValue = composeArticleAndNoun(val, noun);
                renderInlineToolOptions();
            });

            $('#inlineBaseArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.baseArticleValue = val;
                const noun = splitLeadingArticle(creatorState.baseValue || '', currentLang).noun;
                creatorState.baseValue = composeArticleAndNoun(val, noun);
                renderInlineBaseOptions();
            });

            $('#inlineItemArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.itemArticleValue = val;
                const noun = splitLeadingArticle(creatorState.itemValue || '', currentLang).noun;
                creatorState.itemValue = composeArticleAndNoun(val, noun);
                renderInlineItemOptions();
            });

            $('#inlineBalanceArticleOptions').on('click', '.inline-article-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.balanceArticleValue = val;
                const noun = splitLeadingArticle(creatorState.balanceValue || '', currentLang).noun;
                creatorState.balanceValue = composeArticleAndNoun(val, noun);
                renderInlineBalanceOptions();
            });

            $('#btnApplyEquipment').on('click', function () {
                const tokenId = creatorState.activeEquipmentTokenId;
                const val = (creatorState.equipmentValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.equipmentValue = val;
                creatorState.placeholderAssignments[tokenId] = val;
                closeEquipmentEditor();
                showCreatorToast('Tool / Gerät eingesetzt');
                renderTemplateCards();
            });

            $('#inlineStateOptions').on('click', '.inline-state-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.stateValue = val;
                renderInlineStateOptions();
            });

            $('#btnApplyState').on('click', function () {
                const tokenId = creatorState.activeStateTokenId;
                const val = (creatorState.stateValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeStateEditor();
                showCreatorToast('Zustand eingesetzt');
                renderTemplateCards();
            });

            $('#inlineToolOptions').on('click', '.inline-tool-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.toolValue = composeArticleAndNoun(creatorState.toolArticleValue, val);
                renderInlineToolOptions();
            });

            $('#btnApplyTool').on('click', function () {
                const tokenId = creatorState.activeToolTokenId;
                const val = (creatorState.toolValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeToolEditor();
                showCreatorToast('Werkzeug eingesetzt');
                renderTemplateCards();
            });

            $('#inlineGrindSizeOptions').on('click', '.inline-grind-size-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.grindSizeValue = val;
                renderInlineGrindSizeOptions();
            });

            $('#btnApplyGrindSize').on('click', function () {
                const tokenId = creatorState.activeGrindSizeTokenId;
                const val = (creatorState.grindSizeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeGrindSizeEditor();
                showCreatorToast('Schnittgröße eingesetzt');
                renderTemplateCards();
            });

            $('#inlineShapeOptions').on('click', '.inline-shape-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.shapeValue = val;
                renderInlineShapeOptions();
            });

            $('#btnApplyShape').on('click', function () {
                const tokenId = creatorState.activeShapeTokenId;
                const val = (creatorState.shapeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeShapeEditor();
                showCreatorToast('Schnittform eingesetzt');
                renderTemplateCards();
            });

            $('#inlineBaseOptions').on('click', '.inline-base-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.baseValue = composeArticleAndNoun(creatorState.baseArticleValue, val);
                renderInlineBaseOptions();
            });

            $('#btnApplyBase').on('click', function () {
                const tokenId = creatorState.activeBaseTokenId;
                const val = (creatorState.baseValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.baseValue = val;
                creatorState.placeholderAssignments[tokenId] = val;
                closeBaseEditor();
                showCreatorToast('Basis eingesetzt');
                renderTemplateCards();
            });

            $('#inlineItemOptions').on('click', '.inline-item-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.itemValue = composeArticleAndNoun(creatorState.itemArticleValue, val);
                renderInlineItemOptions();
            });

            $('#btnApplyItem').on('click', function () {
                const tokenId = creatorState.activeItemTokenId;
                const val = (creatorState.itemValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeItemEditor();
                showCreatorToast('Item eingesetzt');
                renderTemplateCards();
            });

            $('#inlineBalanceOptions').on('click', '.inline-balance-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.balanceValue = composeArticleAndNoun(creatorState.balanceArticleValue, val);
                renderInlineBalanceOptions();
            });

            $('#btnApplyBalance').on('click', function () {
                const tokenId = creatorState.activeBalanceTokenId;
                const val = (creatorState.balanceValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeBalanceEditor();
                showCreatorToast('Balance eingesetzt');
                renderTemplateCards();
            });

            $('#inlineSeasoningsOptions').on('click', '.inline-seasonings-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.seasoningsValue = val;
                renderInlineSeasoningsOptions();
            });

            $('#btnApplySeasonings').on('click', function () {
                const tokenId = creatorState.activeSeasoningsTokenId;
                const val = (creatorState.seasoningsValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSeasoningsEditor();
                showCreatorToast('Seasonings eingesetzt');
                renderTemplateCards();
            });

            $(document).on('click', '.ingredient-db-row', function (e) {
                if ($(e.target).closest('button, input, select, label').length) return;
                openIngredientConfigForDbRow(this);
            });

            $('#btnCloseIngredientConfig, #btnCancelIngredientConfig').on('click', function () {
                closeIngredientConfigPopup();
            });

            $('#btnApplyIngredientConfig').on('click', function () {
                applyIngredientConfigPopup();
            });

            $('#ingredientConfigUnitButtons').on('click', '.ingredient-unit-btn', function () {
                const unit = ($(this).data('unit') || '').toString();
                if (!unit) return;
                $('#ingredientConfigUnit').val(unit);
                $('#ingredientConfigUnitButtons .ingredient-unit-btn').removeClass('active');
                $(this).addClass('active');
            });

            $('#ingredientConfigOverlay').on('click', function (e) {
                if (e.target === this) {
                    closeIngredientConfigPopup();
                }
            });

            $('#btnAddRenderedStep').on('click', async function () {
                await addRenderedMasterStep();
                updateStoryProgress();
                showCreatorToast('Step akzeptiert');
            });

            $('#stepsIngredientButtons').on('click', '.ingredient-chip', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                const selected = creatorState.selectedIngredientIds || [];
                if (selected.includes(id)) {
                    creatorState.selectedIngredientIds = selected.filter(x => x !== id);
                } else {
                    creatorState.selectedIngredientIds = [...selected, id];
                }
                syncGrammarAssignmentsForSelectedIngredient();
                renderIngredientChips();
            });

            $('#btnApplyStepIngredient').on('click', function () { applyChipToStep(); });
            $('#btnCancelStepIngredient').on('click', function () { cancelStepIngredientEdit(); });

            $('#selectedSteps').on('dragstart', '.step-row', function (e) {
                draggedStepRow = this;
                e.originalEvent.dataTransfer.effectAllowed = 'move';
                $(this).addClass('opacity-50');
            });

            $('#selectedSteps').on('dragend', '.step-row', function () {
                $(this).removeClass('opacity-50');
                draggedStepRow = null;
            });

            $('#selectedSteps').on('dragover', '.step-row', function (e) {
                e.preventDefault();
                if (!draggedStepRow || draggedStepRow === this) return;
                const rect = this.getBoundingClientRect();
                const offset = e.originalEvent.clientY - rect.top;
                if (offset > rect.height / 2) $(this).after(draggedStepRow);
                else $(this).before(draggedStepRow);
                updateStepIndices();
            });

            updateLanguageLabels();
            syncIngredientSourceVisibility();

            // Page fully ready — hide overlay, show content
            $('#datatableLoadingOverlay').addClass('d-none');
            $('.creator-topbar, .feed-shell').css('visibility', 'visible');
        });

})(window, window.jQuery);
