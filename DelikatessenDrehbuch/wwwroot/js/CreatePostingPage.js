// Extracted from CreatePosting.cshtml inline scripts (Smart Creator + page behavior)
        window.onerror = function(msg, url, line, col, error) {
            alert('GLOBAL JS ERROR: ' + msg + '\nZeile: ' + line + ' Spalte: ' + col + '\nDatei: ' + (url || '') + '\nStack: ' + (error && error.stack ? error.stack.substring(0, 300) : ''));
            return false;
        };


       

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
        var nextThemePreference = null;
        let tempStepIdCounter = -1;
        let ingredientArticleRules = null;
        let activeIngredientConfigRow = null;

        const fallbackUnitsLocal = [
            { de: 'Stk.', en: 'pcs.', esp: 'uds.', prt: 'un.' },
            { de: 'EL.', en: 'tbsp.', esp: 'cda.', prt: 'colh. sopa' },
            { de: 'TL.', en: 'tsp.', esp: 'cdta.', prt: 'colh. cha' },
            { de: 'Pkg.', en: 'pack', esp: 'paq.', prt: 'pac.' },
            { de: 'Prise', en: 'pinch', esp: 'pizca', prt: 'pitada' },
            { de: 'g.', en: 'g.', esp: 'g.', prt: 'g.' },
            { de: 'Kg', en: 'kg', esp: 'kg', prt: 'kg' },
            { de: 'Bund', en: 'bunch', esp: 'manojo', prt: 'molho' },
            { de: 'Dose', en: 'can', esp: 'lata', prt: 'lata' },
            { de: 'etwas', en: 'some', esp: 'algo', prt: 'um pouco' },
            { de: 'spritzer', en: 'dash', esp: 'chorrito', prt: 'golpe' },
            { de: 'Becher', en: 'cup', esp: 'taza', prt: 'copo' },
            { de: 'Scheiben', en: 'slices', esp: 'rebanadas', prt: 'fatias' },
            { de: 'Blatt', en: 'leaf', esp: 'hoja', prt: 'folha' },
            { de: 'BlÃ¤tter', en: 'leaves', esp: 'hojas', prt: 'folhas' },
            { de: 'Handvoll', en: 'handful', esp: 'puÃ±ado', prt: 'punhado' },
            { de: 'cm', en: 'cm', esp: 'cm', prt: 'cm' },
            { de: 'Zweig', en: 'sprig', esp: 'rama', prt: 'ramo' },
            { de: 'ml', en: 'ml', esp: 'ml', prt: 'ml' },
            { de: 'Gekocht', en: 'cooked', esp: 'cocido', prt: 'cozido' },
            { de: 'Portionen', en: 'portions', esp: 'porciones', prt: 'porcoes' },
            { de: 'l', en: 'l', esp: 'l', prt: 'l' },
            { de: 'Glas', en: 'glass', esp: 'vaso', prt: 'copo' },
            { de: 'kcal', en: 'kcal', esp: 'kcal', prt: 'kcal' },
            { de: 'mg', en: 'mg', esp: 'mg', prt: 'mg' },
            { de: 'Âµg', en: 'ug', esp: 'ug', prt: 'ug' }
        ];

        function getAvailableUnits() {
            if (window.IngredientManager && typeof window.IngredientManager.getUnits === 'function') {
                const units = window.IngredientManager.getUnits();
                if (Array.isArray(units) && units.length) return units;
            }
            return fallbackUnitsLocal;
        }

        function findUnitByDe(unitDe) {
            const normalized = (unitDe || '').toString().trim().toLowerCase();
            if (!normalized) return null;

            const units = getAvailableUnits();
            return units.find(u => (u?.de || '').toString().trim().toLowerCase() === normalized) || null;
        }

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

        function buildUnitOptionsHtml(selectedDe) {
            if (window.IngredientManager && typeof window.IngredientManager.buildUnitOptionsHtml === 'function') {
                return window.IngredientManager.buildUnitOptionsHtml(selectedDe, currentLang);
            }

            const selected = (selectedDe || '').toString().trim().toLowerCase();
            const units = [...getAvailableUnits()];
            if (selected && !units.some(u => (u?.de || '').toString().trim().toLowerCase() === selected)) {
                units.push({ de: selectedDe, en: selectedDe, esp: selectedDe, prt: selectedDe });
            }

            return units.map(u => {
                const de = (u?.de || '').toString();
                const label = getUnitLabel(u, currentLang) || de;
                const isSelected = de.toLowerCase() === selected;
                return `<option value="${$("<div>").text(de).html()}" ${isSelected ? 'selected' : ''}>${$("<div>").text(label).html()}</option>`;
            }).join('');
        }

        function dockIngredientConfigUnder(anchorElement) {
            const overlay = $('#ingredientConfigOverlay');
            const dock = $('#ingredientConfigDock');
            if (!overlay.length || !dock.length) return;
            dock.removeClass('ing-active');
            if (anchorElement && anchorElement.length) {
                anchorElement.after(dock);
            }
            overlay.addClass('ing-active').attr('aria-hidden', 'false');
            void dock[0].offsetWidth; // force reflow for animation
            dock.addClass('ing-active');
            $('#ingredientConfigQty').focus();
        }

        function openIngredientConfigPopup(row) {
            const target = $(row);
            if (!target.length) return;
            activeIngredientConfigRow = target;
            const name = (target.find('.display-name-selected').first().text() || '').trim();
            const qty = (target.find('.ingredient-qty-hidden').val() || '0').toString();
            const unitDe = (target.find('.ingredient-unit-hidden').val() || '').toString();
            $('#ingredientConfigTitle').text(name || 'Zutat');
            $('#ingredientConfigQty').val(qty);
            $('#ingredientConfigUnit').html(buildUnitOptionsHtml(unitDe));
            dockIngredientConfigUnder(target);
        }

        function closeIngredientConfigPopup() {
            activeIngredientConfigRow = null;
            $('#ingredientConfigDock').removeClass('ing-active');
            $('#ingredientConfigOverlay').removeClass('ing-active').attr('aria-hidden', 'true');
            $('#ingredientConfigDockParking').append($('#ingredientConfigDock'));
        }

        function applyIngredientConfigPopup() {
            const qty = normalizeDecimalInputValue($('#ingredientConfigQty').val()) || '0';
            const unitDe = ($('#ingredientConfigUnit').val() || '').toString();
            const unitObj = findUnitByDe(unitDe);
            const unitLabel = getUnitLabel(unitObj, currentLang) || unitDe;

            if (activeIngredientConfigRow && activeIngredientConfigRow.length) {
                activeIngredientConfigRow.find('.ingredient-qty-hidden').val(qty);
                activeIngredientConfigRow.find('.ingredient-unit-hidden').val(unitDe);
                activeIngredientConfigRow.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
                const ingredientId = (activeIngredientConfigRow.find('input[name$="IngredientsAndNutrients.Id"]').val() || '').toString();
                if (ingredientId) {
                    const sourceRow = $('.ingredient-db-row').filter(function () {
                        return ($(this).data('ingredient-id') || '').toString() === ingredientId;
                    }).first();
                    if (sourceRow.length) {
                        sourceRow.attr('data-selected-qty', qty);
                        sourceRow.attr('data-selected-unit', unitDe);
                    }
                }
                closeIngredientConfigPopup();
                return;
            }
        }

        /* ===== Probability Variable Editor ===== */
        let probVarEditorCallback = null;
        let probVarEditorAnchor = null;
        let probVarIngredientArticleValue = '';

        function renderProbVarIngredientArticleOptions(selectedArticle) {
            const wrap = $('#probVarIngredientArticleChips');
            if (!wrap.length) return;

            const options = getEditorArticleOptions(currentLang) || [];
            const current = (selectedArticle || '').toString().trim().toLowerCase();
            const noArticleLabelByLang = { de: 'ohne', en: 'none', esp: 'sin', prt: 'sem', nl: 'zonder' };
            const noArticleLabel = noArticleLabelByLang[resolveLangKey(currentLang)] || 'none';

            const noArticleActive = !current ? ' active' : '';
            const noArticleBtnClass = noArticleActive ? "btn-light text-dark" : "btn-outline-light";
            const noArticle = `<button type="button" class="btn btn-sm ${noArticleBtnClass} inline-article-opt js-prob-ingredient-article-chip${noArticleActive}" data-value="">${$('<div>').text(noArticleLabel).html()}</button>`;
            const chips = options.map(article => {
                const articleText = (article || '').toString();
                const isActive = current && current === articleText.toLowerCase();
                const active = isActive ? ' active' : '';
                const safe = $('<div>').text(articleText).html();
                const btnClass = isActive ? "btn-light text-dark" : "btn-outline-light";
                return `<button type="button" class="btn btn-sm ${btnClass} inline-article-opt js-prob-ingredient-article-chip${active}" data-value="${safe}">${safe}</button>`;
            }).join('');

            wrap.html(noArticle + chips);
        }

        function openProbVarEditor(masterId, varKey, currentVal, onApply, anchorElement) {
            probVarEditorCallback = onApply;
            probVarEditorAnchor = anchorElement ? $(anchorElement).closest('.probability-template-wrap') : null;
            const cleanKey = (varKey || '').toLowerCase().replace(/_/g, ' ');
            $('#probVarEditorTitle').text(cleanKey || 'Wert');
            $('#probVarIngredientArea, #probVarDurationArea, #probVarCountArea, #probVarTempArea, #probVarOptionsArea, #probVarTextArea').addClass('d-none');
            $('#probVarIngredientArticleChips').empty();
            $('#probVarApplyRow').addClass('d-none');

            const varType = typeof getPlaceholderType === 'function' ? getPlaceholderType(varKey) : '';

            if (varType === 'ingredient') {
                probVarIngredientArticleValue = '';
                $('#probVarIngredientArticleChips').empty();

                const selectedIds = (creatorState.selectedIngredientIds || []).map(x => x.toString());
                const ings = typeof getSelectedIngredientsForSandbox === 'function' ? getSelectedIngredientsForSandbox() : [];
                const chips = (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.buildIngredientChipsHtml === 'function')
                    ? window.MasterStepCreatorHelpers.buildIngredientChipsHtml(ings, selectedIds)
                    : '';
                $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewÃ¤hlt</span>');
                $('#probVarIngredientArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else if (varType === 'duration') {
                const num = parseInt((currentVal || '').split(' ')[0], 10) || 5;
                const isHour = (currentVal || '').toLowerCase().includes('stund') || (currentVal || '').toLowerCase().includes('hour');
                $('#probVarDurationVal').val(num);
                $('#probVarDurationUnit').val(isHour ? 'hour' : 'minute');
                $('#probVarDurationArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else if (varType === 'count') {
                $('#probVarCountVal').val(parseInt(currentVal, 10) || 1);
                $('#probVarCountArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else if (varType === 'temperature') {
                const tempNum = parseInt(currentVal, 10) || 180;
                const isFahr = (currentVal || '').includes('Â°F') || (currentVal || '').includes('F');
                $('#probVarTempVal').val(tempNum);
                $('#probVarTempUnit').val(isFahr ? 'fahrenheit' : 'celsius');
                $('#probVarTempArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else {
                // Options or text fallback â€” try to get presets from MasterStepRenderer
                let options = [];
                if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                    options = MasterStepRenderer.getVariablePresets(varKey, currentLang || 'de') || [];
                }
                if (!options.length && varType === 'heat' && typeof getHeatOptions === 'function') options = getHeatOptions();
                if (!options.length && varType === 'mode') options = (modeOptionsByLang[resolveLangKey(currentLang)] || modeOptionsByLang.de || []);
                if (!options.length && varType === 'tool' && typeof getToolOptions === 'function') options = getToolOptions();

                if (options.length) {
                    const chips = options.map(opt => {
                        const safe = $('<div>').text(opt).html();
                        const active = opt === currentVal ? ' active' : '';
                        return `<button type="button" class="btn btn-sm prob-var-chip js-prob-option-chip${active}" data-value="${safe}">${safe}</button>`;
                    }).join('');
                    $('#probVarOptionChips').html(chips);
                    $('#probVarOptionsArea').removeClass('d-none');
                } else {
                    $('#probVarTextVal').val(currentVal || '');
                    $('#probVarTextArea').removeClass('d-none');
                    $('#probVarApplyRow').removeClass('d-none');
                }
            }

            const dock = $('#probVarEditorDock');
            const overlay = $('#probVarEditorOverlay');
            dock.removeClass('prob-inline-mode');

            if (probVarEditorAnchor && probVarEditorAnchor.length) {
                overlay.removeClass('ing-active').attr('aria-hidden', 'true');
                probVarEditorAnchor.append(dock);
                setTimeout(function () { dock.addClass('ing-active prob-inline-mode'); }, 10);
                return;
            }

            $('#probVarEditorDockParking').append(dock);
            overlay.addClass('ing-active').attr('aria-hidden', 'false');
            setTimeout(function () { dock.addClass('ing-active'); }, 10);
        }

        function closeProbVarEditor() {
            probVarEditorCallback = null;
            probVarEditorAnchor = null;
            probVarIngredientArticleValue = '';
            const dock = $('#probVarEditorDock');
            dock.removeClass('ing-active prob-inline-mode');
            $('#probVarEditorDockParking').append(dock);
            $('#probVarEditorOverlay').removeClass('ing-active').attr('aria-hidden', 'true');
        }

        function applyProbVarEditor() {
            let value = null;
            if (!$('#probVarIngredientArea').hasClass('d-none')) {
                value = getSelectedIngredientValueForInsert();
            } else if (!$('#probVarDurationArea').hasClass('d-none')) {
                const num = parseInt($('#probVarDurationVal').val(), 10) || 1;
                const unit = $('#probVarDurationUnit').val() || 'minute';
                const label = typeof getDurationUnitLabel === 'function' ? getDurationUnitLabel(unit, currentLang) : unit;
                value = `${num} ${label}`;
            } else if (!$('#probVarCountArea').hasClass('d-none')) {
                value = (parseInt($('#probVarCountVal').val(), 10) || 1).toString();
            } else if (!$('#probVarTempArea').hasClass('d-none')) {
                const num = $('#probVarTempVal').val() || '180';
                const unit = $('#probVarTempUnit').val() === 'fahrenheit' ? 'Â°F' : 'Â°C';
                value = `${num} ${unit}`;
            } else if (!$('#probVarTextArea').hasClass('d-none')) {
                value = ($('#probVarTextVal').val() || '').toString().trim();
            }
            if (value !== null && probVarEditorCallback) {
                probVarEditorCallback(value);
            }
            closeProbVarEditor();
        }

        /* ===== End Probability Variable Editor ===== */

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
            if (!Array.isArray(window.stepCatalog)) {
                window.stepCatalog = [];
            }
            return window.stepCatalog;
        }

        function getPhaseLabel(phase) {
            const map = {
                1: { text: 'Vorb.', cls: 'phase-badge-1' },
                2: { text: 'Kochen', cls: 'phase-badge-2' },
                3: { text: 'WÃ¼rzen', cls: 'phase-badge-3' },
                4: { text: 'Finish', cls: 'phase-badge-4' }
            };
            const info = map[phase];
            if (!info) return '';
            return `<span class="phase-badge ${info.cls}">${info.text}</span>`;
        }


        // Hilfsfunktion: Gibt die Zutat-IDs zurÃ¼ck, an die ein Step gebunden ist
        function getStepBoundIngredientIds(stepId) {
            const bindings = window.stepIngredientBindings || {};
            return (bindings[stepId] ?? bindings[stepId?.toString?.()] ?? []).map(x => parseInt(x, 10));
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

        const createPostingThemeStorageKey = 'createPostingTheme';
        const profileThemeStorageKey = 'profile_theme';
        const availableCreatePostingThemes = ['color', 'black', 'white', 'rose', 'lavender'];
        const createPostingThemeMap = {
            color: 'gold',
            black: 'navy',
            white: 'gold',
            rose: 'rosa',
            lavender: 'navy'
        };
        const currentThema = (() => {
            const domTheme = (document.querySelector('.feed-shell')?.getAttribute('data-theme') || '').toString().trim().toLowerCase();
            if (availableCreatePostingThemes.includes(domTheme)) return domTheme;

            const mappedProfileTheme = Object.keys(createPostingThemeMap).find(function (key) {
                return createPostingThemeMap[key] === domTheme;
            });

            return mappedProfileTheme || 'color';
        })();

        function getStoredProfileTheme() {
            try {
                return (localStorage.getItem(profileThemeStorageKey) || 'color').toString().trim().toLowerCase();
            } catch (error) {
                return 'color';
            }
        }

        function normalizeCreatePostingTheme(theme) {
            const normalized = (theme || '').toString().trim().toLowerCase();
            if (normalized === 'profile') return normalizeCreatePostingTheme(getStoredProfileTheme());
            if (normalized === 'dark' || normalized === 'navy') return 'black';
            if (normalized === 'rosa') return 'rose';
            if (normalized === 'gold' || normalized === 'light') return 'color';
            return availableCreatePostingThemes.includes(normalized) ? normalized : currentThema;
        }

        function getVisualCreatePostingTheme(theme) {
            const normalized = normalizeCreatePostingTheme(theme);
            return createPostingThemeMap[normalized] || createPostingThemeMap.color;
        }

        function readStoredCreatePostingTheme() {
            try {
                const storedThemeMode = (localStorage.getItem(createPostingThemeStorageKey) || '').toString().trim().toLowerCase();
                if (storedThemeMode === 'profile') {
                    return normalizeCreatePostingTheme(getStoredProfileTheme());
                }

                const storedCreatePostingTheme = normalizeCreatePostingTheme(storedThemeMode);
                if (availableCreatePostingThemes.includes(storedCreatePostingTheme)) {
                    return storedCreatePostingTheme;
                }

                return currentThema;
            } catch (error) {
                return currentThema;
            }
        }

        window.CreatePostingCurrentTheme = readStoredCreatePostingTheme();
        window.CreatePostingThemeMode = (() => {
            try {
                return (localStorage.getItem(createPostingThemeStorageKey) || '').toString().trim().toLowerCase() || window.CreatePostingCurrentTheme;
            } catch (error) {
                return window.CreatePostingCurrentTheme;
            }
        })();

        window.getCreatePostingTheme = function () {
            return normalizeCreatePostingTheme(window.CreatePostingCurrentTheme || currentThema || 'color');
        };

        window.getCreatePostingVisualTheme = function () {
            return getVisualCreatePostingTheme(window.getCreatePostingTheme());
        };

        window.getThemeMutedTextClass = function () {
            return window.getCreatePostingVisualTheme() === 'rosa' ? 'text-dark' : 'text-white-50';
        };

        function syncCreatePostingThemeToggleState() {
            const theme = window.getCreatePostingTheme();
            const storedThemeMode = (window.CreatePostingThemeMode || '').toString().trim().toLowerCase();

            $('[data-create-posting-theme]').each(function () {
                const btn = $(this);
                const rawTheme = (btn.data('create-posting-theme') || '').toString().trim().toLowerCase();
                const isProfileButton = rawTheme === 'profile';
                const isActive = isProfileButton
                    ? storedThemeMode === 'profile'
                    : storedThemeMode !== 'profile' && normalizeCreatePostingTheme(rawTheme) === theme;
                btn.toggleClass('is-active', isActive);
                btn.attr('aria-pressed', isActive ? 'true' : 'false');
            });
        }

        function applyCurrentThemeAttributes() {
            const theme = window.getCreatePostingTheme();
            const visualTheme = getVisualCreatePostingTheme(theme);
            const selectors = [
                '.creator-topbar',
                '.feed-shell',
                '.creator-hero',
                '.creator-card',
                '.smart-step-creator',
                '.upload-drop',
                '.input-pill',
                '.select-pill',
                '.preview-step-card',
                '.template-card',
                '.ingredient-db-row'
            ];
            $(selectors.join(', ')).attr('data-theme', visualTheme);
            $('.feed-shell').attr('data-profile-theme', theme);
            syncCreatePostingThemeToggleState();
        }

        window.setCreatePostingTheme = function (theme, options) {
            const settings = options || {};
            const requestedTheme = (theme || '').toString().trim().toLowerCase();
            const nextTheme = normalizeCreatePostingTheme(theme);
            const nextThemeMode = requestedTheme === 'profile' ? 'profile' : nextTheme;
            nextThemePreference = nextThemeMode;
            window.CreatePostingThemeMode = nextThemeMode;
            window.CreatePostingCurrentTheme = nextTheme;

            if (settings.persist !== false) {
                try {
                    localStorage.setItem(createPostingThemeStorageKey, nextThemeMode);
                } catch (error) {
                }
            }

            applyCurrentThemeAttributes();

            if (settings.refresh === false) {
                return nextTheme;
            }

            if (typeof refreshMasterTemplateBuilder === 'function') {
                refreshMasterTemplateBuilder();
            }
            if (typeof refreshIngredientProbabilityHints === 'function') {
                refreshIngredientProbabilityHints();
            }
            if (typeof renderSc2TemplateCards === 'function') {
                renderSc2TemplateCards();
            }
            if (typeof updateLanguageLabels === 'function') {
                updateLanguageLabels();
            }

            return nextTheme;
        };
        const creatorState = {
            selectedIngredientIds: [],
            selectedTemplateId: '',
            activeStepIngredientRow: null,
            isStepIngredientEditMode: false,
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
            shapeValue: 'WÃ¼rfel',
            activeItemTokenId: '',
            itemValue: 'den Teig',
            itemArticleValue: 'den',
            baseValue: 'den Teig',
            baseArticleValue: 'den',
            activeBalanceTokenId: '',
            balanceValue: 'die SÃ¤ure',
            balanceArticleValue: 'die',
            activeSeasoningsTokenId: '',
            seasoningsValue: 'Salz und Pfeffer'
        };

        const upsertStepUrl = window.CreatePostingPageConfig?.upsertStepUrl || '/WorldMiniApp/Home/UpsertStep';

        function getIngredientEmoji(name) {
            const text = (name || '').toLowerCase();
            if (text.includes('basil')) return '&#127807;';
            if (text.includes('tomat')) return '&#127813;';
            if (text.includes('zwiebel')) return '&#129477;';
            if (text.includes('knoblauch')) return '&#129476;';
            if (text.includes('reis')) return '&#127834;';
            if (text.includes('salat')) return '&#129367;';
            return '&#127860;';
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
                    f: ['tomate', 'zwiebel', 'paprika', 'karotte', 'kartoffel', 'soÃŸe', 'sauce'],
                    n: ['salz', 'Ã¶l', 'wasser', 'ei', 'mehl', 'fleisch', 'brot']
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
            const names = getSelectedIngredientNames();
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.resolveIngredientInsertValue === 'function') {
                return window.MasterStepCreatorHelpers.resolveIngredientInsertValue(names, currentLang, '');
            }
            const list = (names || []).map(x => (x || '').toString().trim()).filter(Boolean);
            if (!list.length) return '';
            if (list.length === 1) return list[0];
            if (list.length === 2) return `${list[0]} und ${list[1]}`;
            const head = list.slice(0, -1).join(', ');
            const tail = list[list.length - 1];
            return `${head}, und ${tail}`;
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

        function normalizePlaceholderKey(key) {
            return (key || '').toString().trim().toLowerCase();
        }

        const placeholderTypeMatchers = [
            { type: 'ingredient', match: key => key === 'ingredient' || key === 'ingredients' || key === 'liquid' || key === 'fat' },
            { type: 'duration', match: key => key.includes('duration') },
            { type: 'count', match: key => key === 'count' || key === 'servings' || key.includes('count') },
            { type: 'pronoun', match: key => key === 'pronoun' },
            { type: 'temperature', match: key => key === 'temp' || key === 'temperature' },
            { type: 'heat', match: key => key === 'heat' || key.includes('heat') },
            { type: 'mode', match: key => key === 'mode' || key.includes('mode') },
            { type: 'equipment', match: key => key === 'equipment' },
            { type: 'state', match: key => key === 'state' || key.includes('state') || key.includes('consistency') },
            { type: 'tool', match: key => key === 'tool' },
            { type: 'grindSize', match: key => key === 'grind_size' || key === 'grindsize' || key.includes('grind') },
            { type: 'shape', match: key => key === 'shape' },
            { type: 'base', match: key => key === 'base' },
            { type: 'item', match: key => key === 'item' || key === 'dish' || key.includes('item') },
            { type: 'balance', match: key => key === 'balance' },
            { type: 'seasonings', match: key => key === 'seasonings' || key === 'spices' }
        ];

        function getPlaceholderType(key) {
            const normalizedKey = normalizePlaceholderKey(key);
            const matched = placeholderTypeMatchers.find(entry => entry.match(normalizedKey));
            return matched ? matched.type : '';
        }

        const placeholderEditorDispatch = {
            duration: { open: openDurationEditorForToken, toast: 'Zeit setzen' },
            count: { open: openCountEditorForToken, toast: 'Anzahl setzen' },
            pronoun: { open: openPronounEditorForToken, toast: 'Pronomen wÃ¤hlen', keepTokenActive: true },
            temperature: { open: openTemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat: { open: openHeatEditorForToken, toast: 'Hitze-Stufe wÃ¤hlen' },
            mode: { open: openModeEditorForToken, toast: 'Ofenmodus wÃ¤hlen' },
            equipment: { open: openEquipmentEditorForToken, toast: 'Tool / GerÃ¤t wÃ¤hlen' },
            state: { open: openStateEditorForToken, toast: 'Zustand wÃ¤hlen' },
            tool: { open: openToolEditorForToken, toast: 'Tool / GerÃ¤t wÃ¤hlen' },
            grindSize: { open: openGrindSizeEditorForToken, toast: 'SchnittgrÃ¶ÃŸe wÃ¤hlen' },
            shape: { open: openShapeEditorForToken, toast: 'Schnittform wÃ¤hlen' },
            base: { open: openBaseEditorForToken, toast: 'Basis wÃ¤hlen' },
            item: { open: openItemEditorForToken, toast: 'Item wÃ¤hlen' },
            balance: { open: openBalanceEditorForToken, toast: 'Balance wÃ¤hlen' },
            seasonings: { open: openSeasoningsEditorForToken, toast: 'Seasonings wÃ¤hlen' }
        };

        const supportedLanguages = ['de', 'en', 'esp', 'prt', 'id', 'nl', 'sv', 'da', 'no', 'ms'];
        const durationUnitLabels = {
            minute: { de: 'Minuten', en: 'minutes', esp: 'minutos', prt: 'minutos', id: 'menit', nl: 'minuten', sv: 'minuter', da: 'minutter', no: 'minutter', ms: 'minit' },
            hour: { de: 'Stunden', en: 'hours', esp: 'horas', prt: 'horas', id: 'jam', nl: 'uur', sv: 'timmar', da: 'timer', no: 'timer', ms: 'jam' }
        };
        const modeOptionsByLang = {
            de: ['Oberhitze', 'Unterhitze', 'Ober- und Unterhitze', 'Umluft'],
            en: ['top heat', 'bottom heat', 'top and bottom heat', 'convection'],
            esp: ['calor superior', 'calor inferior', 'calor superior e inferior', 'convecciÃ³n'],
            prt: ['calor superior', 'calor inferior', 'calor superior e inferior', 'convecÃ§Ã£o'],
            id: ['panas atas', 'panas bawah', 'panas atas dan bawah', 'konveksi'],
            nl: ['bovenwarmte', 'onderwarmte', 'boven- en onderwarmte', 'hetelucht'],
            sv: ['Ã¶vervÃ¤rme', 'undervÃ¤rme', 'Ã¶ver- och undervÃ¤rme', 'varmluft'],
            da: ['overvarme', 'undervarme', 'over- og undervarme', 'varmluft'],
            no: ['overvarme', 'undervarme', 'over- og undervarme', 'varmluft'],
            ms: ['haba atas', 'haba bawah', 'haba atas dan bawah', 'peredaran udara']
        };
        const closeEditorHandlers = [
            closeDurationEditor,
            closeCountEditor,
            closeTemperatureEditor,
            closeHeatEditor,
            closeModeEditor,
            closePronounEditor,
            closeEquipmentEditor,
            closeStateEditor,
            closeToolEditor,
            closeGrindSizeEditor,
            closeShapeEditor,
            closeBaseEditor,
            closeItemEditor,
            closeBalanceEditor,
            closeSeasoningsEditor
        ];

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
            const key = (unitKey || 'minute').toString().toLowerCase();
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();
            return durationUnitLabels[key]?.[lang] || durationUnitLabels[key]?.de || '';
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
            const unit = creatorState.temperatureUnit === 'fahrenheit' ? 'Â°F' : 'Â°C';
            return `${safeValue}${unit}`;
        }

        function openTemperatureEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeTemperatureTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s*Â°?\s*(C|F)?/i);
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
            renderInlineChoiceButtons('#inlineHeatOptions', 'inline-heat-opt', getHeatOptions(), creatorState.heatValue || '');
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
            const lang = (currentLang || 'de').toString().toLowerCase();
            return modeOptionsByLang[lang] || modeOptionsByLang.de;
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
            renderInlineChoiceButtons('#inlineModeOptions', 'inline-mode-opt', getModeOptions(), creatorState.modeValue || '');
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
            renderInlineChoiceButtons(
                '#inlinePronounOptions',
                'inline-pronoun-opt',
                getPronounSuggestionsForLang(currentLang),
                creatorState.pronounValue || creatorState.computedPronoun || ''
            );
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
                    'brÃ¤ter': 'den',
                    'topf': 'den',
                    'kÃ¼chenmaschine': 'die',
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
                    'sartÃ©n': 'la',
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
                de: ['Pfanne', 'Topf', 'Backofen', 'RÃ¼hrschÃ¼ssel', 'Sieb', 'Mixer', 'PÃ¼rierstab', 'KÃ¼chenmaschine', 'BrÃ¤ter', 'Wok', 'Grill', 'Dampfgarer', 'Auflaufform', 'Zange', 'Schneidebrett'],
                en: ['pan', 'pot', 'oven', 'mixing bowl', 'strainer', 'blender', 'immersion blender', 'food processor', 'roaster', 'wok', 'grill', 'steamer', 'baking dish', 'tongs', 'cutting board'],
                esp: ['sartÃ©n', 'olla', 'horno', 'bol para mezclar', 'colador', 'batidora', 'batidora de mano', 'procesador de alimentos', 'asador', 'wok', 'parrilla', 'vaporera', 'fuente para horno', 'pinzas', 'tabla de cortar'],
                prt: ['frigideira', 'panela', 'forno', 'tigela de mistura', 'coador', 'liquidificador', 'mixer de mÃ£o', 'processador de alimentos', 'assadeira', 'wok', 'grelha', 'cozedor a vapor', 'travessa de forno', 'pinÃ§a', 'tÃ¡bua de corte'],
                id: ['wajan', 'panci', 'oven', 'mangkuk adonan', 'saringan', 'blender', 'blender tangan', 'food processor', 'loyang panggang', 'wok', 'pemanggang', 'kukusan', 'pinggan oven', 'penjepit', 'talenan'],
                nl: ['pan', 'kookpot', 'oven', 'mengkom', 'zeef', 'blender', 'staafmixer', 'keukenmachine', 'braadslede', 'wok', 'grill', 'stoomkoker', 'ovenschaal', 'tang', 'snijplank'],
                sv: ['stekpanna', 'gryta', 'ugn', 'blandningsskÃ¥l', 'sil', 'mixer', 'stavmixer', 'matberedare', 'stekgryta', 'wok', 'grill', 'Ã¥ngkokare', 'ugnsform', 'tÃ¥ng', 'skÃ¤rbrÃ¤da'],
                da: ['pande', 'gryde', 'ovn', 'rÃ¸reskÃ¥l', 'si', 'blender', 'stavblender', 'foodprocessor', 'bradepande', 'wok', 'grill', 'dampkoger', 'ovnfast fad', 'tang', 'skÃ¦rebrÃ¦t'],
                no: ['stekepanne', 'gryte', 'ovn', 'miksebolle', 'sil', 'blender', 'stavmikser', 'kjÃ¸kkenmaskin', 'stekeform', 'wok', 'grill', 'dampkoker', 'ildfast form', 'klype', 'skjÃ¦refjÃ¸l'],
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
                de: ['Messer', 'SparschÃ¤ler', 'Reibe', 'Schneebesen', 'Spatel', 'HolzlÃ¶ffel', 'Suppenkelle', 'Messbecher', 'Nudelholz', 'Teigschaber'],
                en: ['knife', 'peeler', 'grater', 'whisk', 'spatula', 'wooden spoon', 'ladle', 'measuring cup', 'rolling pin', 'dough scraper'],
                esp: ['cuchillo', 'pelador', 'rallador', 'batidor', 'espÃ¡tula', 'cuchara de madera', 'cucharÃ³n', 'vaso medidor', 'rodillo', 'rasqueta de masa'],
                prt: ['faca', 'descascador', 'ralador', 'batedor', 'espÃ¡tula', 'colher de pau', 'concha', 'copo medidor', 'rolo de massa', 'raspador de massa'],
                id: ['pisau', 'pengupas', 'parutan', 'pengocok', 'spatula', 'sendok kayu', 'sendok sayur', 'gelas ukur', 'rolling pin', 'scraper adonan'],
                nl: ['mes', 'dunschiller', 'rasp', 'garde', 'spatel', 'houten lepel', 'soeplepel', 'maatbeker', 'deegroller', 'deegschraper'],
                sv: ['kniv', 'potatisskalare', 'rivjÃ¤rn', 'visp', 'stekspade', 'trÃ¤slev', 'slev', 'mÃ¥ttkopp', 'kavel', 'degskrapa'],
                da: ['kniv', 'skrÃ¦ller', 'rivejern', 'piskeris', 'spatel', 'trÃ¦ske', 'suppeske', 'mÃ¥lebÃ¦ger', 'kagerulle', 'dejskraber'],
                no: ['kniv', 'skreller', 'rivjern', 'visp', 'stekespade', 'tresleiv', 'Ã¸se', 'mÃ¥lebeger', 'kjevle', 'deigskrape'],
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
                de: ['fein', 'feine', 'mittel', 'mittlere', 'grob', 'grobe', 'dÃ¼nn', 'dÃ¼nne', 'breit', 'breite', 'klein', 'kleine', 'groÃŸ', 'groÃŸe', 'ca. 1 cm groÃŸ', 'ca. 1 cm groÃŸe', 'ca. 0,5 mm groÃŸ', 'ca. 0,5 mm groÃŸe'],
                en: ['fine', 'medium', 'coarse', 'thin', 'wide', 'small', 'large', 'about 3/8-inch', 'about 0.02-inch'],
                esp: ['finas', 'medianas', 'gruesas', 'delgadas', 'anchas', 'pequeÃ±as', 'grandes'],
                prt: ['finas', 'mÃ©dias', 'grossas', 'finas', 'largas', 'pequenas', 'grandes'],
                id: ['halus', 'sedang', 'kasar', 'tipis', 'tebal', 'kecil', 'besar'],
                nl: ['fijne', 'middelgrote', 'grove', 'dunne', 'brede', 'kleine', 'grote'],
                sv: ['fina', 'medelgrova', 'grova', 'tunna', 'breda', 'smÃ¥', 'stora'],
                da: ['fine', 'mellemstore', 'grove', 'tynde', 'brede', 'smÃ¥', 'store'],
                no: ['fine', 'middels', 'grove', 'tynne', 'brede', 'smÃ¥', 'store'],
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
            return ['WÃ¼rfel', 'Scheiben', 'Streifen', 'Spalten', 'grobe StÃ¼cke', 'Ringe', 'Julienne', 'Stifte'];
        }

        function openShapeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeShapeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.shapeValue || '').toString().trim();
            creatorState.shapeValue = currentText || 'WÃ¼rfel';
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
            return ['die SÃ¤ure', 'die SÃ¼ÃŸe', 'die SchÃ¤rfe'];
        }

        function openBalanceEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBalanceTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.balanceValue || '').toString().trim();
            const options = getBalanceOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || 'die SÃ¤ure';
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
            return ['Salz', 'Pfeffer', 'Salz und Pfeffer', 'KrÃ¤uter', 'GewÃ¼rze'];
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
            renderInlineChoiceButtons('#inlineSeasoningsOptions', 'inline-seasonings-opt', getSeasoningsOptions(), creatorState.seasoningsValue || '');
        }

        function renderInlineChoiceButtons(wrapSelector, optionClass, options, currentValue) {
            const wrap = $(wrapSelector);
            if (!wrap.length) return;
            const currentVal = (currentValue || '').toString().trim().toLowerCase();
            const html = (options || []).map(x => {
                const value = (x || '').toString();
                const isActive = currentVal === value.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} ${optionClass}" data-value="${$('<div>').text(value).html()}">${$('<div>').text(value).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function closeAllEditors() {
            closeEditorHandlers.forEach(function (closeEditor) {
                if (typeof closeEditor === 'function') {
                    closeEditor();
                }
            });
        }

        function placeEditorLikeTemperature(editorSelector) {
            const editor = $(editorSelector);
            const tempEditor = $('#temperatureEditor');
            if (!editor.length || !tempEditor.length) return;

            editor.removeClass('bottom-sheet-editor').addClass('duration-editor mt-2');
            tempEditor.after(editor);
        }

        function renderInlineShapeOptions() {
            renderInlineChoiceButtons('#inlineShapeOptions', 'inline-shape-opt', getShapeOptions(), creatorState.shapeValue || '');
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
                    de: 'Mehl', en: 'flour', esp: 'harina', prt: 'farinha', id: 'tepung', nl: 'bloem', sv: 'mjÃ¶l', da: 'mel', no: 'mel', ms: 'tepung'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step2') {
                const map = {
                    de: 'Ei', en: 'egg', esp: 'huevo', prt: 'ovo', id: 'telur', nl: 'ei', sv: 'Ã¤gg', da: 'Ã¦g', no: 'egg', ms: 'telur'
                };
                return map[lang] || map.de;
            }

            if (keyNorm === 'step3') {
                const map = {
                    de: 'BrÃ¶sel', en: 'breadcrumbs', esp: 'pan rallado', prt: 'farinha de rosca', id: 'tepung roti', nl: 'paneermeel', sv: 'strÃ¶brÃ¶d', da: 'rasp', no: 'brÃ¸dsmuler', ms: 'serbuk roti'
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
                return getBalanceOptions()[0] || 'die SÃ¤ure';
            }

            if (keyNorm === 'seasonings' || keyNorm === 'spices') {
                return getSeasoningsOptions()[2] || getSeasoningsOptions()[0] || 'Salz und Pfeffer';
            }

            return '';
        }

        function getSelectedIngredientValueForLanguage(langKey) {
            const names = getSelectedIngredientNames();
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.resolveIngredientInsertValue === 'function') {
                return window.MasterStepCreatorHelpers.resolveIngredientInsertValue(names, langKey, '');
            }
            const list = (names || []).map(x => (x || '').toString().trim()).filter(Boolean);
            if (!list.length) return '';
            if (list.length === 1) return list[0];
            if (list.length === 2) return `${list[0]} und ${list[1]}`;
            const head = list.slice(0, -1).join(', ');
            const tail = list[list.length - 1];
            return `${head}, und ${tail}`;
        }

        function resolveDefaultVariableValueForLanguage(key, langKey) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const placeholderType = getPlaceholderType(key);
            if (key === 'ingredient' || key === 'ingredients' || key === 'liquid') {
                const ingredientValue = getSelectedIngredientValueForLanguage(lang);
                return ingredientValue || getLocalizedFallbackForVariable('ingredient', lang) || key;
            }
            if (key === 'pronoun') {
                const selectedIngredient = getSelectedIngredientForCreator();
                const grammar = resolveGrammarForIngredient(selectedIngredient?.name || '', selectedIngredient?.genusByLang || {}, lang);
                return creatorState.advancedPronounOverride || creatorState.pronounValue || grammar.pronoun || getLocalizedFallbackForVariable('pronoun', lang) || 'it';
            }
            if (key === 'article') {
                const selectedIngredient = getSelectedIngredientForCreator();
                const grammar = resolveGrammarForIngredient(selectedIngredient?.name || '', selectedIngredient?.genusByLang || {}, lang);
                return creatorState.advancedArticleOverride || grammar.article || getLocalizedFallbackForVariable('article', lang) || '';
            }
            if (placeholderType === 'duration' || key.includes('duration')) {
                const unit = getDurationUnitLabel(creatorState.durationUnit, lang);
                return `${creatorState.durationValue || 10} ${unit}`;
            }
            if (placeholderType === 'count') return getCountInsertText();
            if (placeholderType === 'temperature') return getTemperatureInsertText();
            if (placeholderType === 'heat' || key.includes('heat')) return creatorState.heatValue || getLocalizedFallbackForVariable('heat', lang) || 'medium';
            if (placeholderType === 'mode') return creatorState.modeValue || (modeOptionsByLang[lang] || modeOptionsByLang.de || [])[0] || '';
            if (placeholderType === 'tool' || key.includes('tool')) return creatorState.toolValue || getToolOptions()[0] || getLocalizedFallbackForVariable('tool', lang) || 'knife';
            if (placeholderType === 'grindSize' || key.includes('grind')) return creatorState.grindSizeValue || getGrindSizeOptions()[0] || getLocalizedFallbackForVariable('grind_size', lang) || '';
            if (placeholderType === 'item') return creatorState.itemValue || getItemOptions()[0] || getLocalizedFallbackForVariable('item', lang) || '';
            if (placeholderType === 'balance') return creatorState.balanceValue || getBalanceOptions()[0] || getLocalizedFallbackForVariable('balance', lang) || '';
            if (placeholderType === 'seasonings') return creatorState.seasoningsValue || getSeasoningsOptions()[2] || getSeasoningsOptions()[0] || getLocalizedFallbackForVariable('seasonings', lang) || '';
            if (placeholderType === 'shape' || key.includes('shape')) return getLocalizedFallbackForVariable('shape', lang) || 'pieces';
            if (placeholderType === 'base') return creatorState.baseValue || getLocalizedFallbackForVariable('base', lang) || '';
            const localizedFallback = getLocalizedFallbackForVariable(key, lang);
            return localizedFallback || key;
        }

        function resolveDefaultVariableValue(key) {
            return resolveDefaultVariableValueForLanguage(key, currentLang);
        }

        function prefillTemplatePlaceholdersInText(text, langKey) {
            const source = (text || '').toString();
            if (!source.includes('{{')) return source;
            return source.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const value = resolveDefaultVariableValueForLanguage(String(key || '').trim(), langKey);
                return (value || String(key || '')).toString();
            }).replace(/\s{2,}/g, ' ').trim();
        }

        function prefillUneditedStepPlaceholders() {
            $('#selectedSteps .step-row').each(function () {
                const row = $(this);
                if (row.data('step-edited') === true || row.attr('data-step-edited') === 'true') {
                    return;
                }

                const mappings = [
                    { selector: '.step-hidden-de', lang: 'de' },
                    { selector: '.step-hidden-en', lang: 'en' },
                    { selector: '.step-hidden-esp', lang: 'esp' },
                    { selector: '.step-hidden-prt', lang: 'prt' }
                ];

                mappings.forEach(function (entry) {
                    const input = row.find(entry.selector);
                    if (!input.length) return;
                    input.val(prefillTemplatePlaceholdersInText(input.val(), entry.lang));
                });

                const currentInput = row.find(`.step-hidden-${currentLang}`);
                const fallbackInput = row.find('.step-hidden-de');
                const visibleText = (currentInput.val() || fallbackInput.val() || '').toString();
                row.find('.step-text-content').text(visibleText);
            });
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
                    vars[key] = resolveDefaultVariableValue(key);
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
                creatorState.previewText = 'WÃ¤hle eine Zutat und ein Template.';
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
                    <button type="button" class="placeholder-reset" data-placeholder-token-id="${tokenId}" data-default-value="${safeFallback}" title="ZurÃ¼cksetzen">&#8630;</button>
                </span>`;
            });

            $('#masterPreviewText').html(previewHtml || 'Keine Vorschau verfÃ¼gbar.');
            creatorState.previewText = $('#masterPreviewText').text().trim() || 'Keine Vorschau verfÃ¼gbar.';

            if (!creatorState.ingredientReplaceArmed) { $('#masterPreviewCard').removeClass('token-replace-active'); }
            animatePreview();
            updateSc2PreviewText();
        }

        function clearSelectedIngredientChips() {
            creatorState.selectedIngredientIds = [];
            renderIngredientChips();
        }

        function updateStoryProgress() {
            const count = $('#selectedSteps .step-row').length;
            $('#storyStepCount').text(`Schritte: ${count}`);
            $('#sc2StoryStepCount').text(`Schritte: ${count}`);
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
            const wrap = $("#currentStepIngredientButtons");
            const stepsWrap = $("#stepsIngredientButtons");
            const mutedTextClass = window.getThemeMutedTextClass();
            const ingredients = getSelectedIngredientsForSandbox();
            wrap.empty();
            stepsWrap.empty();

            if (!ingredients.length) {
                wrap.append(`<div class="small ${mutedTextClass}">Keine Zutaten vorhanden.</div>`);
                stepsWrap.append(`<div class="small ${mutedTextClass}">Keine Zutaten vorhanden.</div>`);
                creatorState.selectedIngredientIds = [];
                updatePreviewText();
                return;
            }

            creatorState.selectedIngredientIds = (creatorState.selectedIngredientIds || []).filter(id => ingredients.some(x => x.id === id));
            if (!creatorState.selectedIngredientIds.length) {
                if (creatorState.isStepIngredientEditMode) {
                    creatorState.selectedIngredientIds = [];
                } else {
                    creatorState.selectedIngredientIds = ingredients.map(x => x.id).filter(Boolean);
                }
            }

            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const emoji = getIngredientEmoji(item.name);
                const displayName = (item.name || '').toString();
                const chipHtml = `<button type="button" class="ingredient-chip ${active}" draggable="true" data-id="${item.id}" data-name="${displayName}">${emoji} ${displayName}</button>`;
                wrap.append(chipHtml);
                stepsWrap.append(chipHtml);
            });

            updatePreviewText();
            renderSc2IngredientChips();
        }

        function renderTemplateCards() {
            const box = $('#masterTemplateCards');
            box.empty();

            if (!window.MasterStepRenderer || typeof MasterStepRenderer.getAllTemplates !== 'function') {
                box.append(`<div class="small ${window.getThemeMutedTextClass()}">Templates werden geladen ...</div>`);
                return;
            }

            const loadError = typeof MasterStepRenderer.getLastLoadError === 'function' ? MasterStepRenderer.getLastLoadError() : '';
            if (loadError) {
                setMasterTemplateError(`Template-Fehler: ${loadError}`);
                return;
            }

            const templates = MasterStepRenderer.getAllTemplates();
            if (!templates.length) {
                setMasterTemplateError('Keine Templates gefunden. PrÃ¼fe /data/master_steps.json.');
                creatorState.selectedTemplateId = '';
                updatePreviewText();
                return;
            }

            if (!creatorState.selectedTemplateId || !templates.some(x => x.master_id === creatorState.selectedTemplateId)) {
                creatorState.selectedTemplateId = templates[0].master_id;
            }

            templates.forEach((step, index) => {
                const active = step.master_id === creatorState.selectedTemplateId ? 'active' : '';
                const icon = step.categoryIcon || (index % 3 === 0 ? '&#128293;' : index % 3 === 1 ? '&#128298;' : '&#129532;');
                const vars = buildVariablesForTemplate(step.master_id);
                const snippet = MasterStepRenderer.render(step.master_id, vars, currentLang) || step.master_id;
                const title = (step.description || '').toString().trim() || `Template ${index + 1}`;

                box.append(`<button type="button" class="template-card ${active}" data-theme="${window.getCreatePostingTheme()}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
            });

            updatePreviewText();
            renderSc2TemplateCards();
        }

        function showMasterStepCreatorError(error, context) {
            const message = error && error.message ? error.message : (error || 'Unbekannter Fehler');
            const details = error && error.stack ? `\nStack: ${error.stack.substring(0, 500)}` : '';
            console.error(`Master-Step-Creator Fehler (${context})`, error);
            alert(`Master-Step-Creator Fehler (${context}): ${message}${details}`);
        }

        function setMasterTemplateError(message) {
            const box = $("#masterTemplateCards");
            if (!box.length) return;
            box.html(`<div class="small text-warning">${message}</div>`);
        }

        function refreshMasterTemplateBuilder() {
            try {
                applyCurrentThemeAttributes();
                renderIngredientChips();
                renderTemplateCards();
                applyCurrentThemeAttributes();
                updateStoryProgress();
                renderAcceptedRecipeTextCard();
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

                if (getPlaceholderType(key) === 'ingredient') {
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
            return supportedLanguages.reduce((payload, lang) => {
                payload[lang] = renderTemplateForPersist(template, lang, vars);
                return payload;
            }, {});
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

        async function addRenderedMasterStep(templateIdOverride = null, varsOverride = null) {
            const templateId = templateIdOverride || getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) return;

            const template = MasterStepRenderer.findTemplate(templateId);
            if (!template) return;

            const rawVars = (varsOverride && typeof varsOverride === 'object') ? varsOverride : collectMasterVariables();
            const vars = normalizeVariablesForPersist(template, rawVars);
            const rendered = buildRenderedPayloadFromTemplate(template, vars);
            let ingredientName = (vars.ingredient || vars.ingredients || vars.liquid || vars.fat || '').toString().trim();
            if (!ingredientName && typeof getSelectedIngredientValueForInsert === 'function') {
                ingredientName = (getSelectedIngredientValueForInsert() || '').toString().trim();
            }
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

                addStep(newId.toString(), null, rendered[currentLang] || rendered.de || `Schritt ${newId}`, { skipRender: true, ingredientName, masterTemplateId: templateId });
                updateStepIndices();
            } catch (error) {
                console.warn('Master-Step Upsert nicht mÃ¶glich, nutze lokalen Fallback-Step', error);

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
                    masterTemplateId: templateId,
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

        function normalizeSearchText(value) {
            return (value || '')
                .toString()
                .toLowerCase()
                .replace(/\u00e4/g, 'ae')
                .replace(/\u00f6/g, 'oe')
                .replace(/\u00fc/g, 'ue')
                .replace(/\u00df/g, 'ss')
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '');
        }

        function syncIngredientSourceVisibility() {
            const query = normalizeSearchText((($('#ingredientSearch').val() || '').toString().trim()));
            const selectedIds = $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                return $(this).val()?.toString();
            }).get();

            $('.ingredient-db-row').each(function () {
                const row = $(this);
                const id = row.data('ingredient-id')?.toString() || '';
                const text = normalizeSearchText((row.data('name-' + currentLang) || row.data('name-de') || '') + '');
                const isSelected = !!id && selectedIds.includes(id);
                const matchesSearch = !query || text.includes(query);
                row.toggle(!isSelected && matchesSearch);
            });
        }

        // Prüft ob ein Step bereits in der Auswahl ist
        function isStepAlreadySelected(stepId) {
            return $('#selectedSteps .step-row').filter(function () {
                return $(this).data('step-id')?.toString() === stepId.toString();
            }).length > 0;
        }












        function getCommonUnitChipLabel(chip, langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            return chip.data('label-' + lang) || chip.data('label-de') || chip.text();
        }

        function resolveSelectUnitValueByKey(selectEl, unitKey) {
            const options = Array.from(selectEl.options || []);
            if (!options.length) return '';

            const normalize = (v) => (v || '').toString().toLowerCase().replace(/\s+/g, '').replace(/\./g, '');
            const key = (unitKey || '').toString().toLowerCase();
            const aliases = {
                g: ['g', 'gr', 'gramm', 'gram'],
                ml: ['ml', 'milliliter', 'millilitre'],
                piece: ['stk', 'stk.', 'stueck', 'stÃ¼ck', 'piece', 'pieces', 'pcs', 'pcs.', 'unit', 'units', 'uds', 'un']
            };
            const accepted = aliases[key] || [key];
            const match = options.find(opt => accepted.includes(normalize(opt.value)) || accepted.includes(normalize(opt.text)));
            return match ? match.value : '';
        }

        function updateCommonUnitChipLabels() {
            $('.js-common-unit-chip').each(function () {
                const chip = $(this);
                chip.text(getCommonUnitChipLabel(chip, currentLang));
            });
        }

        const probabilityFeature = window.CreatePostingProbability?.create({
            getCurrentLang: () => currentLang,
            getSelectedIngredientIds: () => $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                const value = parseInt($(this).val(), 10);
                return Number.isNaN(value) ? null : value;
            }).get().filter(v => v !== null),
            getSelectedIngredientNames: () => getSelectedIngredientsForSandbox().map(x => x.name).filter(Boolean),
            findTemplate: (masterId) => MasterStepRenderer.findTemplate(masterId),
            renderTemplate: (masterId, vars, lang) => MasterStepRenderer.render(masterId, vars, lang),
            buildVariablesForTemplate,
            detectRecipeTypes: (ingredientIds) => window.RecipeStepSuggest.detectRecipeTypes(ingredientIds) || [],
            loadMapping: () => window.RecipeStepSuggest.loadMapping(),
            getMasterStepPreview: (ingredients, options) => window.RecipeStepSuggest.getMasterStepPreview(ingredients, options) || [],
            /**
             * Returns typical ingredients for a category that aren't yet selected,
             * augmented with their localized name from the catalog.
             */
            getTypicalIngredientSuggestions: (typeId) => {
                if (!window.RecipeStepSuggest || !window.RecipeStepSuggest.getCategoryById) return [];
                const cat = window.RecipeStepSuggest.getCategoryById(typeId);
                if (!cat || !Array.isArray(cat.typical_ingredients)) return [];

                const alreadySelected = new Set(
                    $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]')
                        .map(function () { return parseInt($(this).val(), 10); })
                        .get().filter(v => !isNaN(v))
                );

                const suggestions = [];
                cat.typical_ingredients.forEach(ingId => {
                    if (alreadySelected.has(ingId)) return;
                    const catalogRow = $(`.ingredient-db-row[data-ingredient-id="${ingId}"]`).first();
                    if (!catalogRow.length) return; // not in this creator's catalog
                    const name = catalogRow.data('name-' + (currentLang || 'de')) || catalogRow.data('name-de') || String(ingId);
                    suggestions.push({ id: ingId, name: name });
                });
                return suggestions;
            },
            openProbVarEditor: (masterId, varKey, currentVal, onApply, anchorElement) => openProbVarEditor(masterId, varKey, currentVal, onApply, anchorElement),
            onAcceptTemplateStep: async (masterId, probabilityVars = null) => {
                creatorState.selectedTemplateId = masterId;
                const beforeCount = $(`#selectedSteps .step-row[data-master-template-id="${CSS.escape(masterId)}"]`).length;
                await addRenderedMasterStep(masterId, probabilityVars);

                const rows = $(`#selectedSteps .step-row[data-master-template-id="${CSS.escape(masterId)}"]`);
                const targetRow = rows.last();
                if (targetRow.length) {
                    targetRow[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                    targetRow.addClass('preview-animate');
                    setTimeout(() => targetRow.removeClass('preview-animate'), 350);
                }

                const afterCount = rows.length;
                showCreatorToast(afterCount > beforeCount ? 'Step akzeptiert' : 'Step bereits vorhanden');
            }
        });

        async function refreshIngredientProbabilityHints() {
            if (!probabilityFeature) return;
            await probabilityFeature.refresh();
        }

        async function showProbabilityTemplateSuggestions(typeId, typeName, score) {
            if (!probabilityFeature) return;
            await probabilityFeature.showSuggestions(typeId, typeName, score);
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
                const unitObj = findUnitByDe(unitDe);
                const unitLabel = getUnitLabel(unitObj, langKey) || unitDe;
                row.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
            });
            syncIngredientSourceVisibility();
            $('.keyword-btn').each(function () { $(this).text($(this).data('word-' + langKey)); });
            $('.keyword-pill').each(function () { $(this).find('.keyword-text').text($(this).data('word-' + langKey)); });
            refreshDurationUnitControls();
            updateCommonUnitChipLabels();
            renderAcceptedRecipeTextCard();
            refreshMasterTemplateBuilder();
            refreshIngredientProbabilityHints();
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

        function sanitizeQuantityInputValue(value) {
            const raw = (value || '').toString();
            let out = '';
            let hasSeparator = false;
            for (const ch of raw) {
                if (ch >= '0' && ch <= '9') {
                    out += ch;
                    continue;
                }
                if (!hasSeparator && (ch === '.' || ch === ',')) {
                    out += ch;
                    hasSeparator = true;
                }
            }
            return out;
        }


        function escapeAttr(value) {
            return $('<div>').text((value ?? '').toString()).html();
        }

        function getLocalizedIngredientData(row, fallbackName) {
            const names = {};
            const genus = {};
            supportedLanguages.forEach(lang => {
                names[lang] = (row.data('name-' + lang) || fallbackName || '').toString();
                genus[lang] = (row.data('genus-' + lang) || '').toString().trim();
            });
            return { names, genus };
        }

        function buildIngredientDataAttributes(localizedData) {
            return supportedLanguages.map(lang =>
                `data-name-${lang}="${escapeAttr(localizedData.names[lang])}" data-genus-${lang}="${escapeAttr(localizedData.genus[lang])}"`
            ).join(' ');
        }

        function buildIngredientRowHtml({ id, localizedData, selectedQuantity, selectedUnit, selectedUnitLabel, displayName }) {
            return `
            <div class="dynamic-item ingredient-row shadow-sm" onclick="openIngredientConfigPopup(this)" title="Zum Bearbeiten antippen" ${buildIngredientDataAttributes(localizedData)}>
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].IngredientsAndNutrients.Id" value="${id}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Quantity.Quantitys" class="ingredient-qty-hidden" value="${selectedQuantity}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Measure.Metriks_DE" class="ingredient-unit-hidden" value="${escapeAttr(selectedUnit)}" />

                <div class="ingredient-row-main">
                    <div class="fw-bold display-name-selected">${escapeAttr(displayName)}</div>
                </div>

                <div class="ingredient-row-right">
                    <div class="ingredient-row-meta">${escapeAttr(selectedQuantity)} ${escapeAttr(selectedUnitLabel)}</div>
                    <div class="ingredient-row-actions">
                        <button type="button" class="btn btn-sm text-danger p-0" onclick="event.stopPropagation(); removeIngredientRow(this)" title="Zutat entfernen" aria-label="Zutat entfernen">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </div>
                </div>
            </div>`;
        }

        function addIngredient(id, rowElement) {
            const existing = $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').filter(function () {
                return $(this).val()?.toString() === id.toString();
            }).length > 0;

            if (existing) return;

            const row = $(rowElement).closest('.ingredient-db-row');
            const displayName = row.data('name-' + currentLang) || row.data('name-de');
            const localizedData = getLocalizedIngredientData(row, displayName);
            const selectedQuantity = normalizeDecimalInputValue(row.attr('data-selected-qty')) || '0';
            const fallbackUnit = (window.IngredientManager && typeof window.IngredientManager.getDefaultUnitDe === 'function')
                ? window.IngredientManager.getDefaultUnitDe()
                : 'g.';
            const selectedUnit = (row.attr('data-selected-unit') || fallbackUnit).toString();
            const selectedUnitObj = findUnitByDe(selectedUnit);
            const selectedUnitLabel = getUnitLabel(selectedUnitObj, currentLang) || selectedUnit;

            const newIngredient = buildIngredientRowHtml({
                id,
                localizedData,
                selectedQuantity,
                selectedUnit,
                selectedUnitLabel,
                displayName
            });

            $('#selectedIngredients').append(newIngredient);

            refreshMasterTemplateBuilder();
            if (typeof syncIngredientSourceVisibility === "function") {
                syncIngredientSourceVisibility();
            }
            refreshIngredientProbabilityHints();
        }

        function indexOfIgnoreCase(source, search) {
            const src = (source || '').toString();
            const find = (search || '').toString();
            if (!src || !find) return -1;
            return src.toLowerCase().indexOf(find.toLowerCase());
        }

        const STEP_INGREDIENT_TOKEN = '__STEP_INGREDIENT_TOKEN__';

        function buildIngredientTokenTemplate(sourceText, ingredientName) {
            const source = (sourceText || '').toString();
            const ingredient = (ingredientName || '').toString().trim();

            if (!source.trim()) {
                return STEP_INGREDIENT_TOKEN;
            }

            if (ingredient) {
                const idx = indexOfIgnoreCase(source, ingredient);
                if (idx >= 0) {
                    return `${source.substring(0, idx)}${STEP_INGREDIENT_TOKEN}${source.substring(idx + ingredient.length)}`;
                }
            }

            const placeholderReplaced = source.replace(/\{\{\s*(ingredient|ingredients|liquid|fat)\s*\}\}/i, STEP_INGREDIENT_TOKEN);
            if (placeholderReplaced !== source) return placeholderReplaced;

            return source;
        }

        function materializeStepTextFromTemplate(templateText, ingredientName) {
            const template = (templateText || '').toString();
            const ingredient = (ingredientName || '').toString();
            return template.includes(STEP_INGREDIENT_TOKEN)
                ? template.replace(STEP_INGREDIENT_TOKEN, ingredient)
                : template;
        }

        function renderStepTextWithIngredientToken(templateText, ingredientName, tokenId = 'STEP_INGREDIENT_TOKEN') {
            const template = (templateText || '').toString();
            const ingredient = (ingredientName || '').toString().trim();

            if (!template.includes(STEP_INGREDIENT_TOKEN)) {
                return $('<div>').text(materializeStepTextFromTemplate(template, ingredient)).html();
            }

            // No empty anchor span for missing ingredient.
            if (!ingredient) {
                const plain = template.replace(STEP_INGREDIENT_TOKEN, '').replace(/\s{2,}/g, ' ').trim();
                return $('<div>').text(plain).html();
            }

            const idx = template.indexOf(STEP_INGREDIENT_TOKEN);
            const before = template.substring(0, idx);
            const after = template.substring(idx + STEP_INGREDIENT_TOKEN.length);
            const safeBefore = $('<div>').text(before).html();
            const safeAfter = $('<div>').text(after).html();
            const safeIngredient = $('<div>').text(ingredient).html();
            const safeTokenId = $("<div>").text(tokenId).html();
            return `${safeBefore}<span class="token-highlight placeholder-token template-var step-ingredient-anchor" draggable="false" data-var="ingredient" data-token-id="${safeTokenId}" data-has-value="1" data-step-ingredient-anchor="1">${safeIngredient}</span>${safeAfter}`;
        }

        function ensureStepLanguageTemplate(row, langKey, oldName) {
            const templateInput = row.find(`.step-template-${langKey}`);
            const sourceInput = row.find(`.step-hidden-${langKey}`);
            if (!sourceInput.length) return '';

            let templateValue = templateInput.length ? (templateInput.val() || '').toString() : '';
            if (!templateValue) {
                templateValue = buildIngredientTokenTemplate(sourceInput.val(), oldName);
                if (templateInput.length) {
                    templateInput.val(templateValue);
                } else {
                    row.append(`<input type="hidden" class="step-template-${langKey}" value="${$('<div>').text(templateValue).html()}" />`);
                }
            }

            return templateValue;
        }

        function startStepIngredientEdit(event, btn) {
            if (event) {
                event.preventDefault();
                event.stopPropagation();
            }
            const row = $(btn).closest('.step-row');
            creatorState.activeStepIngredientRow = row;
            creatorState.isStepIngredientEditMode = true;
            creatorState.selectedIngredientIds = [];
            renderIngredientChips();
            $('#stepsChipStrip').removeClass('d-none');
            showCreatorToast('Chip auswÃ¤hlen â€“ dann Einsetzen tippen');
        }

        function applyChipToStep() {
            const row = creatorState.activeStepIngredientRow;
            if (!row) { cancelStepIngredientEdit(); return; }

            const selectedNames = getSelectedIngredientNames();
            if (!selectedNames.length) { showCreatorToast('Bitte zuerst eine Zutat auswÃ¤hlen'); return; }

            const newName = (typeof getSelectedIngredientValueForInsert === 'function' ? getSelectedIngredientValueForInsert() : selectedNames.join(', '));
            const oldName = (row.data('ingredient-name') || row.find('.template-var[data-var="ingredient"], .step-ingredient-anchor').first().text() || '').toString().trim();
            const langKeys = ['de', 'en', 'esp', 'prt'];
            let changed = false;

            langKeys.forEach(langKey => {
                const hiddenInput = row.find(`.step-hidden-${langKey}`);
                if (!hiddenInput.length) return;

                const template = ensureStepLanguageTemplate(row, langKey, oldName);
                const before = (hiddenInput.val() || '').toString();
                const after = materializeStepTextFromTemplate(template, newName);
                if (after !== before) changed = true;
                hiddenInput.val(after);
            });

            const currentTemplate = ensureStepLanguageTemplate(row, currentLang, oldName) || ensureStepLanguageTemplate(row, 'de', oldName);
            const textSpan = row.find('.step-text-content');
            if (textSpan.length) {
                const beforeVisible = (textSpan.text() || '').toString();
                const afterVisible = materializeStepTextFromTemplate(currentTemplate, newName);
                if (afterVisible !== beforeVisible) changed = true;
                textSpan.html(renderStepTextWithIngredientToken(currentTemplate, newName, `step_${row.data('step-id') || 'manual'}_ingredient_0`));
            }

            row.attr('data-ingredient-name', newName);
            row.attr('data-step-edited', 'true').data('step-edited', true);
            creatorState.activeStepIngredientRow = null;
            creatorState.isStepIngredientEditMode = false;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').addClass('d-none');
            showCreatorToast(changed ? 'Zutat im Schritt ersetzt' : 'Keine ersetzbare Zutat im Step gefunden');
        }

        function cancelStepIngredientEdit() {
            creatorState.activeStepIngredientRow = null;
            creatorState.isStepIngredientEditMode = false;
            clearSelectedIngredientChips();
            renderIngredientChips();
            $('#stepsChipStrip').addClass('d-none');
        }

        function removeIngredientRow(btn) {
            $(btn).closest('.ingredient-row').remove();
            refreshMasterTemplateBuilder();
            syncIngredientSourceVisibility();
            refreshIngredientProbabilityHints();
        }

        // Entfernt alle Steps die fÃ¼r dieselben Zutaten+Phase gebunden sind wie der neue Step
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

                // PrÃ¼fe ob der bestehende Step mindestens eine gemeinsame Zutat hat
                const rowBoundIds = getStepBoundIngredientIds(rowStepId).map(x => x.toString());
                const hasOverlap = rowBoundIds.some(id => newStepBoundIds.includes(id));
                if (hasOverlap) {
                    row.remove();
                }
            });
        }

        function slugifyStableStepKey(value) {
            return (value || '')
                .toString()
                .trim()
                .toLowerCase()
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '')
                .replace(/[^a-z0-9]+/g, '_')
                .replace(/^_+|_+$/g, '');
        }

        function normalizeStableKeyArray(value) {
            if (Array.isArray(value)) {
                return value.map(x => (x || '').toString().trim()).filter(Boolean);
            }
            const single = (value || '').toString().trim();
            return single ? [single] : [];
        }

        function sanitizeStableReferenceMetadata(stepData, options) {
            const masterTemplateId = (options?.masterTemplateId || stepData?.masterTemplateId || '').toString().trim();
            const stableReference = stepData?.stableReference && typeof stepData.stableReference === 'object'
                ? stepData.stableReference
                : {};

            const taxonomy = stableReference.taxonomy || {};
            const variables = stableReference.variables || {};
            const sanitizedVariables = {};
            Object.keys(variables).forEach(function (key) {
                const item = variables[key] || {};
                sanitizedVariables[key] = {
                    type_key: (item.type_key || '').toString(),
                    display_value: (item.display_value || '').toString(),
                    reference_key: item.reference_key ? item.reference_key.toString() : null,
                    reference_source: (item.reference_source || 'display_only').toString()
                };
            });

            return {
                schema_version: (stableReference.schema_version || '2.0.0').toString(),
                master_step_key: (stableReference.master_step_key || masterTemplateId).toString(),
                action_key: (stableReference.action_key || '').toString(),
                step_intent_key: (stableReference.step_intent_key || '').toString(),
                phase_key: (stableReference.phase_key || stepData?.phase || 0).toString(),
                equipment_key: (stableReference.equipment_key || stepData?.equipment || 0).toString(),
                taxonomy: {
                    category_keys: normalizeStableKeyArray(taxonomy.category_keys),
                    diet_keys: normalizeStableKeyArray(taxonomy.diet_keys),
                    ingredient_family_keys: normalizeStableKeyArray(taxonomy.ingredient_family_keys),
                    keyword_keys: normalizeStableKeyArray(taxonomy.keyword_keys),
                    quality_rule_keys: normalizeStableKeyArray(taxonomy.quality_rule_keys)
                },
                variables: sanitizedVariables
            };
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
                equipment: parseInt(stepData.equipment ?? 0, 10) || 0,
                masterTemplateId: (options?.masterTemplateId || stepData.masterTemplateId || '').toString(),
                stableReference: sanitizeStableReferenceMetadata(stepData, options)
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

            const stepReferenceJson = JSON.stringify(normalizedStepData.stableReference || {});
            const stepReferenceEncoded = $("<div>").text(stepReferenceJson).html();
            const stepIdAsInt = parseInt(id, 10);
            const postedStepId = Number.isNaN(stepIdAsInt) || stepIdAsInt < 1 ? 0 : stepIdAsInt;
            const phaseBadge = getPhaseLabel(normalizedStepData.phase);
            const stepIngredientName = (options?.ingredientName || '').toString().trim();
            const chipBtnHtml = `<button type="button" class="btn btn-sm btn-outline-light opacity-75" onclick="startStepIngredientEdit(event, this)" title="Zutat per Chip aendern">&#9998;</button>`;
            const templateDe = buildIngredientTokenTemplate(normalizedStepData.de, stepIngredientName);
            const templateEn = buildIngredientTokenTemplate(normalizedStepData.en, stepIngredientName);
            const templateEsp = buildIngredientTokenTemplate(normalizedStepData.esp, stepIngredientName);
            const templatePrt = buildIngredientTokenTemplate(normalizedStepData.prt, stepIngredientName);
            const visibleTemplate = ({ de: templateDe, en: templateEn, esp: templateEsp, prt: templatePrt })[currentLang] || templateDe;

            $("#selectedSteps").append(`<div class="dynamic-item d-flex align-items-center step-row" draggable="true" data-step-id="${id}" data-step-edited="false" data-master-template-id="${$("<div>").text((normalizedStepData.masterTemplateId || options?.masterTemplateId || "")).html()}" data-step-reference-json="${stepReferenceEncoded}" data-ingredient-name="${$("<div>").text(stepIngredientName).html()}">
                <input type="hidden" name="RecipePreperationSteps[INDEX].PreperationStepId" value="${postedStepId}" />
                <input type="hidden" class="step-index-input" name="RecipePreperationSteps[INDEX].StepIndex" value="0" />
                <input type="hidden" class="step-hidden-de" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_DE" value="${$('<div>').text(materializeStepTextFromTemplate(templateDe, stepIngredientName)).html()}" />
                <input type="hidden" class="step-hidden-en" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_EN" value="${$('<div>').text(materializeStepTextFromTemplate(templateEn, stepIngredientName)).html()}" />
                <input type="hidden" class="step-hidden-esp" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_ESP" value="${$('<div>').text(materializeStepTextFromTemplate(templateEsp, stepIngredientName)).html()}" />
                <input type="hidden" class="step-hidden-prt" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Step_PRT" value="${$('<div>').text(materializeStepTextFromTemplate(templatePrt, stepIngredientName)).html()}" />
                <input type="hidden" class="step-template-de" value="${$('<div>').text(templateDe).html()}" />
                <input type="hidden" class="step-template-en" value="${$('<div>').text(templateEn).html()}" />
                <input type="hidden" class="step-template-esp" value="${$('<div>').text(templateEsp).html()}" />
                <input type="hidden" class="step-template-prt" value="${$('<div>').text(templatePrt).html()}" />
                <input type="hidden" class="step-hidden-phase" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Phase" value="${normalizedStepData.phase}" />
                <input type="hidden" class="step-hidden-equipment" name="RecipePreperationSteps[INDEX].RecipePreperationStep.Equipment" value="${normalizedStepData.equipment}" />
                <input type="hidden" class="step-hidden-master-template-id" name="SmartStepReferences[INDEX].MasterStepKey" value="${$("<div>").text(normalizedStepData.stableReference?.master_step_key || normalizedStepData.masterTemplateId || "").html()}" />
                <input type="hidden" class="step-hidden-reference-json" name="SmartStepReferences[INDEX].MetadataJson" value="${stepReferenceEncoded}" />
                <div class="badge candy-purple rounded-pill me-3 step-badge">0</div>
                <div class="small flex-grow-1 display-step-selected">
                    ${phaseBadge}
                    <span class="step-text-content">${renderStepTextWithIngredientToken(visibleTemplate, stepIngredientName, `step_${id}_ingredient_0`)}</span>
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

        function removeStepsByMasterTemplateId(masterId) {
            const key = (masterId || '').toString();
            if (!key) return;

            const rows = $(`#selectedSteps .step-row[data-master-template-id="${CSS.escape(key)}"]`);
            if (!rows.length) {
                showCreatorToast('Kein Step zum Template gefunden');
                return;
            }

            rows.remove();
            updateStepIndices();
            showCreatorToast('Step(s) entfernt');
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

        function renderAcceptedRecipeTextCard() {
            const card = $("#acceptedStepsRecipeCard");
            const box = $("#acceptedStepsRecipeText");
            if (!card.length || !box.length) return;

            const langKey = resolveLangKey(currentLang);
            const rows = $("#selectedSteps .step-row");
            if (!rows.length) {
                box.html('<div class="accepted-step-line"><span class="accepted-step-no">-</span><span class="accepted-step-content">Noch keine akzeptierten Steps.</span></div>');
                card.removeClass("d-none");
                return;
            }

            const lines = [];
            rows.each(function (i) {
                const row = $(this);
                const raw = (row.find(`.step-hidden-${langKey}`).val() || row.find(".step-text-content").text() || "").toString().replace(/\s+/g, " ").trim();
                if (!raw) return;
                const safe = $("<div>").text(raw).html();
                lines.push(`<div class="accepted-step-line"><span class="accepted-step-no">${i + 1}.</span><span class="accepted-step-content">${safe}</span></div>`);
            });

            if (!lines.length) {
                box.html('<div class="accepted-step-line"><span class="accepted-step-no">-</span><span class="accepted-step-content">Noch keine akzeptierten Steps.</span></div>');
                card.removeClass("d-none");
                return;
            }

            box.html(lines.join(""));
            card.removeClass("d-none");
        }

        function updateStepIndices() {
            $('#selectedSteps .step-row').each(function (i) {
                $(this).find('.step-badge').text(i + 1);
                $(this).find('.step-index-input').val(i + 1);
            });
            updateStoryProgress();
            renderAcceptedRecipeTextCard();
        }

        function prepareBinding() {
            prefillUneditedStepPlaceholders();
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


        // =========================================================
        // SC2 â€“ Eingebetteter Smart Step Creator im Steps-Bereich
        // Shares creatorState with the main creator; separate DOM
        // =========================================================

        function updateSc2PreviewText() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) {
                $('#sc2MasterPreviewText').text('WÃ¤hle eine Zutat und ein Template.');
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
                    <button type="button" class="placeholder-reset" data-placeholder-token-id="${tokenId}" data-default-value="${safeFallback}" title="ZurÃ¼cksetzen">&#8630;</button>
                </span>`;
            });
            $('#sc2MasterPreviewText').html(previewHtml || 'Keine Vorschau verfÃ¼gbar.');
            if (!creatorState.ingredientReplaceArmed) { $('#sc2MasterPreviewCard').removeClass('token-replace-active'); }
            const sc2Card = $('#sc2MasterPreviewCard');
            sc2Card.addClass('preview-animate');
            setTimeout(() => sc2Card.removeClass('preview-animate'), 250);
        }

        function renderSc2IngredientChips() {
            const wrap = $('#sc2MasterIngredientButtons');
            if (!wrap.length) return;
            const ingredients = getSelectedIngredientsForSandbox();
            wrap.empty();
            if (!ingredients.length) {
                wrap.append('<div class="small text-white-50">WÃ¤hle zuerst Zutaten aus.</div>');
                return;
            }
            creatorState.selectedIngredientIds = (creatorState.selectedIngredientIds || []).filter(id => ingredients.some(x => x.id === id));
            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const emoji = getIngredientEmoji(item.name);
                const displayName = (item.name || '').toString();
                wrap.append(`<button type="button" class="ingredient-chip ${active}" data-id="${item.id}" data-name="${displayName}">${emoji} ${displayName}</button>`);
            });
        }

        function renderSc2TemplateCards() {
            const box = $('#sc2MasterTemplateCards');
            if (!box.length) return;
            box.empty();
            if (!window.MasterStepRenderer || typeof MasterStepRenderer.getAllTemplates !== 'function') {
                box.append(`<div class="small ${window.getThemeMutedTextClass()}">Templates werden geladen ...</div>`);
                return;
            }
            const loadError = typeof MasterStepRenderer.getLastLoadError === 'function' ? MasterStepRenderer.getLastLoadError() : '';
            if (loadError) { box.html(`<div class="small text-warning">Template-Fehler: ${loadError}</div>`); return; }
            const templates = MasterStepRenderer.getAllTemplates();
            if (!templates.length) { box.html(`<div class="small ${window.getThemeMutedTextClass()}">Keine Templates gefunden.</div>`); return; }
            templates.forEach((step, index) => {
                const active = step.master_id === creatorState.selectedTemplateId ? 'active' : '';
                const icon = step.categoryIcon || (index % 3 === 0 ? '&#128293;' : index % 3 === 1 ? '&#128298;' : '&#129532;');
                const vars = buildVariablesForTemplate(step.master_id);
                const snippet = MasterStepRenderer.render(step.master_id, vars, currentLang) || step.master_id;
                const title = (step.description || '').toString().trim() || `Template ${index + 1}`;
                box.append(`<button type="button" class="template-card ${active}" data-theme="${window.getCreatePostingTheme()}" data-id="${step.master_id}" data-title="${title}">
                    <div class="template-title">${icon} ${title}</div>
                    <div class="template-snippet">${snippet}</div>
                </button>`);
            });
        }

        function showSc2CreatorToast(message) {
            const toast = $('#sc2CreatorToast');
            toast.text(message).addClass('show');
            setTimeout(() => toast.removeClass('show'), 1200);
        }

        // SC2 close functions
        function closeSc2DurationEditor()    { $('#sc2DurationEditor').addClass('d-none'); }
        function closeSc2CountEditor()       { $('#sc2CountEditor').addClass('d-none'); }
        function closeSc2TemperatureEditor() { $('#sc2TemperatureEditor').addClass('d-none'); }
        function closeSc2PronounEditor()     { $('#sc2PronounEditor').addClass('d-none'); }
        function closeSc2HeatEditor()        { $('#sc2HeatEditor').addClass('d-none'); }
        function closeSc2ModeEditor()        { $('#sc2ModeEditor').addClass('d-none'); }
        function closeSc2ToolEditor()        { $('#sc2ToolEditor').addClass('d-none'); }
        function closeSc2GrindSizeEditor()   { $('#sc2GrindSizeEditor').addClass('d-none'); }
        function closeSc2ShapeEditor()       { $('#sc2ShapeEditor').addClass('d-none'); }
        function closeSc2BaseEditor()        { $('#sc2BaseEditor').addClass('d-none'); }
        function closeSc2ItemEditor()        { $('#sc2ItemEditor').addClass('d-none'); }
        function closeSc2BalanceEditor()     { $('#sc2BalanceEditor').addClass('d-none'); }
        function closeSc2SeasoningsEditor()  { $('#sc2SeasoningsEditor').addClass('d-none'); }
        function closeSc2EquipmentEditor()   { $('#sc2EquipmentEditor').addClass('d-none'); }
        function closeSc2StateEditor()       { $('#sc2StateEditor').addClass('d-none'); }

        function closeSc2AllEditors() {
            [closeSc2DurationEditor, closeSc2CountEditor, closeSc2TemperatureEditor,
             closeSc2PronounEditor, closeSc2HeatEditor, closeSc2ModeEditor,
             closeSc2ToolEditor, closeSc2GrindSizeEditor, closeSc2ShapeEditor,
             closeSc2BaseEditor, closeSc2ItemEditor, closeSc2BalanceEditor,
             closeSc2SeasoningsEditor, closeSc2EquipmentEditor, closeSc2StateEditor
            ].forEach(fn => fn());
        }

        // SC2 open functions
        function openSc2DurationEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeDurationTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s+/);
            if (parsed) creatorState.durationValue = parseInt(parsed[1], 10);
            $('#sc2DurationValueInput').val(creatorState.durationValue || 10);
            $('#sc2DurationUnitSelect option[value="minute"]').text(getDurationUnitLabel('minute', currentLang));
            $('#sc2DurationUnitSelect option[value="hour"]').text(getDurationUnitLabel('hour', currentLang));
            $('#sc2DurationUnitSelect').val(creatorState.durationUnit || 'minute');
            $('#sc2DurationEditor').removeClass('d-none');
        }
        function openSc2CountEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeCountTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)/);
            if (parsed) creatorState.countValue = parseInt(parsed[1], 10);
            $('#sc2CountValueInput').val(creatorState.countValue || 2);
            $('#sc2CountEditor').removeClass('d-none');
        }
        function openSc2TemperatureEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeTemperatureTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)/);
            if (parsed) {
                creatorState.temperatureValue = parseInt(parsed[1], 10);
                creatorState.temperatureUnit = currentText.includes('Â°F') ? 'fahrenheit' : 'celsius';
            }
            $('#sc2TemperatureValueInput').val(creatorState.temperatureValue || 180);
            $('#sc2TemperatureUnitSelect').val(creatorState.temperatureUnit || 'celsius');
            $('#sc2TemperatureEditor').removeClass('d-none');
        }
        function openSc2HeatEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeHeatTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineHeatOptions', 'inline-heat-opt', getHeatOptions(), creatorState.heatValue || '');
            $('#sc2HeatEditor').removeClass('d-none');
        }
        function openSc2ModeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeModeTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineModeOptions', 'inline-mode-opt', getModeOptions(), creatorState.modeValue || '');
            $('#sc2ModeEditor').removeClass('d-none');
        }
        function openSc2PronounEditorForToken(tokenId) {
            creatorState.activePronounTokenId = tokenId || '__global__';
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.computedPronoun || '').toString().trim();
            creatorState.pronounValue = currentText;
            renderInlineChoiceButtons('#sc2InlinePronounOptions', 'inline-pronoun-opt', getPronounSuggestionsForLang(currentLang), creatorState.pronounValue || creatorState.computedPronoun || '');
            $('#sc2PronounEditor').removeClass('d-none');
        }
        function openSc2EquipmentEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeEquipmentTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.equipmentValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.equipmentArticleValue = parts.article;
            creatorState.equipmentValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineEquipmentArticleOptions', creatorState.equipmentArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getEquipmentOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-equipment-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineEquipmentOptions').html(chips);
            $('#sc2EquipmentEditor').removeClass('d-none');
        }
        function openSc2StateEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeStateTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineStateOptions', 'inline-state-opt', getStateOptions(), creatorState.stateValue || '');
            $('#sc2StateEditor').removeClass('d-none');
        }
        function openSc2ToolEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeToolTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.toolValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.toolArticleValue = parts.article;
            creatorState.toolValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineToolArticleOptions', creatorState.toolArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getToolOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-tool-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineToolOptions').html(chips);
            $('#sc2ToolEditor').removeClass('d-none');
        }
        function openSc2GrindSizeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeGrindSizeTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineGrindSizeOptions', 'inline-grind-size-opt', getGrindSizeOptions(), creatorState.grindSizeValue || '');
            $('#sc2GrindSizeEditor').removeClass('d-none');
        }
        function openSc2ShapeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeShapeTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineShapeOptions', 'inline-shape-opt', getShapeOptions(), creatorState.shapeValue || '');
            $('#sc2ShapeEditor').removeClass('d-none');
        }
        function openSc2BaseEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBaseTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.baseValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.baseArticleValue = parts.article;
            creatorState.baseValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineBaseArticleOptions', creatorState.baseArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getBaseOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-base-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineBaseOptions').html(chips);
            $('#sc2BaseEditor').removeClass('d-none');
        }
        function openSc2ItemEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeItemTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.itemValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.itemArticleValue = parts.article;
            creatorState.itemValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineItemArticleOptions', creatorState.itemArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getItemOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-item-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineItemOptions').html(chips);
            $('#sc2ItemEditor').removeClass('d-none');
        }
        function openSc2BalanceEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBalanceTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.balanceValue || '').toString().trim();
            const parts = splitLeadingArticle(currentText, currentLang);
            creatorState.balanceArticleValue = parts.article;
            creatorState.balanceValue = parts.noun || currentText;
            renderArticleOptions('#sc2InlineBalanceArticleOptions', creatorState.balanceArticleValue);
            const currentVal = (parts.noun || currentText).toLowerCase();
            const chips = getNounOptions(getBalanceOptions(), currentLang).map(opt => {
                const isActive = currentVal && currentVal === opt.toLowerCase();
                const safe = $('<div>').text(opt).html();
                return `<button type="button" class="btn btn-sm ${isActive ? 'btn-light text-dark active' : 'btn-outline-light'} inline-balance-opt" data-value="${safe}">${safe}</button>`;
            }).join('');
            $('#sc2InlineBalanceOptions').html(chips);
            $('#sc2BalanceEditor').removeClass('d-none');
        }
        function openSc2SeasoningsEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeSeasoningsTokenId = tokenId;
            renderInlineChoiceButtons('#sc2InlineSeasoningsOptions', 'inline-seasonings-opt', getSeasoningsOptions(), creatorState.seasoningsValue || '');
            $('#sc2SeasoningsEditor').removeClass('d-none');
        }

        const sc2EditorDispatch = {
            duration:    { open: openSc2DurationEditorForToken,    toast: 'Zeit setzen' },
            count:       { open: openSc2CountEditorForToken,       toast: 'Anzahl setzen' },
            pronoun:     { open: openSc2PronounEditorForToken,     toast: 'Pronomen wÃ¤hlen', keepTokenActive: true },
            temperature: { open: openSc2TemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat:        { open: openSc2HeatEditorForToken,        toast: 'Hitze-Stufe wÃ¤hlen' },
            mode:        { open: openSc2ModeEditorForToken,        toast: 'Ofenmodus wÃ¤hlen' },
            equipment:   { open: openSc2EquipmentEditorForToken,   toast: 'Tool / GerÃ¤t wÃ¤hlen' },
            state:       { open: openSc2StateEditorForToken,       toast: 'Zustand wÃ¤hlen' },
            tool:        { open: openSc2ToolEditorForToken,        toast: 'Tool / GerÃ¤t wÃ¤hlen' },
            grindSize:   { open: openSc2GrindSizeEditorForToken,   toast: 'SchnittgrÃ¶ÃŸe wÃ¤hlen' },
            shape:       { open: openSc2ShapeEditorForToken,       toast: 'Schnittform wÃ¤hlen' },
            base:        { open: openSc2BaseEditorForToken,        toast: 'Basis wÃ¤hlen' },
            item:        { open: openSc2ItemEditorForToken,        toast: 'Item wÃ¤hlen' },
            balance:     { open: openSc2BalanceEditorForToken,     toast: 'Balance wÃ¤hlen' },
            seasonings:  { open: openSc2SeasoningsEditorForToken,  toast: 'Seasonings wÃ¤hlen' }
        };

        function openSc2EditorForPlaceholderToken(key, tokenId) {
            try {
                if (!key || !tokenId) return;
                closeSc2AllEditors();
                creatorState.ingredientReplaceArmed = false;
                $('#sc2MasterPreviewCard').removeClass('token-replace-active');
                const placeholderType = getPlaceholderType(key);
                if (placeholderType === 'ingredient') {
                    creatorState.activePlaceholderTokenId = tokenId;
                    creatorState.ingredientReplaceArmed = true;
                    $('#sc2MasterPreviewCard').addClass('token-replace-active');
                    showSc2CreatorToast('Zutat auswÃ¤hlen');
                } else if (sc2EditorDispatch[placeholderType]) {
                    creatorState.activePlaceholderTokenId = sc2EditorDispatch[placeholderType].keepTokenActive ? tokenId : '';
                    sc2EditorDispatch[placeholderType].open(tokenId);
                    showSc2CreatorToast(sc2EditorDispatch[placeholderType].toast);
                } else {
                    creatorState.activePlaceholderTokenId = '';
                }
                renderSc2TemplateCards();
            } catch (err) {
                console.error('SC2 openSc2EditorForPlaceholderToken error:', err);
            }
        }

        $(document).ready(function () {
            const token = sessionStorage.getItem('UserToken') || localStorage.getItem('UserToken');
            if (token) $('#hiddenUserTokenField').val(token);


            window.setCreatePostingTheme(readStoredCreatePostingTheme(), { persist: false, refresh: false });

            $(document).on('click', '[data-create-posting-theme]', function () {
                const nextTheme = ($(this).data('create-posting-theme') || '').toString();
                window.setCreatePostingTheme(nextTheme);
            });
            $('#ingredientSearch').on('input', function () {
                syncIngredientSourceVisibility();
            });
            refreshIngredientProbabilityHints();

            $(document).on('input', '#ingredientConfigQty, .js-db-qty', function () {
                const sanitized = sanitizeQuantityInputValue(this.value);
                if (this.value !== sanitized) {
                    this.value = sanitized;
                }
            });

            $(document).on('click', '.js-common-unit-chip', function (e) {
                e.preventDefault();
                e.stopPropagation();
                const row = $(this).closest('.ingredient-db-row');
                const select = row.find('.js-db-unit').first();
                if (!select.length) return;

                const unitValue = resolveSelectUnitValueByKey(select[0], $(this).data('unit-key'));
                if (!unitValue) return;
                select.val(unitValue).trigger('change');
            });

            // Quick unit chips inside the ingredient config dock
            $(document).on('click', '.js-config-unit-chip', function (e) {
                e.preventDefault();
                e.stopPropagation();
                const select = $('#ingredientConfigUnit');
                if (!select.length) return;

                let unitValue = resolveSelectUnitValueByKey(select[0], $(this).data('unit-key'));
                if (!unitValue) {
                    const key = ($(this).data('unit-key') || '').toString().toLowerCase();
                    const fallbackMap = { g: 'g.', ml: 'ml', piece: 'Stk.' };
                    const fallbackValue = fallbackMap[key] || '';
                    if (fallbackValue && !select.find(`option[value="${fallbackValue}"]`).length) {
                        select.append(`<option value="${fallbackValue}">${fallbackValue}</option>`);
                    }
                    unitValue = fallbackValue;
                }
                if (!unitValue) return;
                select.val(unitValue);
            });

            $(document).on('click', '.js-probability-type', async function () {
                const typeId = ($(this).data('type') || '').toString();
                const typeName = ($(this).data('name') || '').toString();
                const score = parseInt($(this).data('score'), 10) || 0;
                await showProbabilityTemplateSuggestions(typeId, typeName, score);
            });

            $(document).on('click', '.js-probability-template', function () {
                const wrap = $(this).closest('.probability-template-wrap');
                const swipeTs = parseInt(wrap.data('swipeJustHandled') || 0, 10);
                if (swipeTs && (Date.now() - swipeTs) < 350) return;
                const masterId = ($(this).data('master-id') || '').toString();
                if (!masterId) return;

                creatorState.selectedTemplateId = masterId;
                creatorState.activePlaceholderTokenId = '';
                creatorState.placeholderAssignments = {};
                closeAllEditors();
                renderTemplateCards();
                updatePreviewText();
                showCreatorToast('Template aus Wahrscheinlichkeits-Hinweis gewÃ¤hlt');
            });

            // Vorgeschlagene Zutat hinzufÃ¼gen: findet die Zeile im Katalog und ruft addIngredient auf
            $(document).on('click', '.js-typical-ingredient-chip', function () {
                const ingId = ($(this).data('ingredient-id') || '').toString();
                if (!ingId) return;

                const catalogRow = $(`.ingredient-db-row[data-ingredient-id="${ingId}"]`).first();
                if (!catalogRow.length) {
                    showCreatorToast('Zutat nicht im Katalog gefunden');
                    return;
                }

                addIngredient(ingId, catalogRow[0]);
                showCreatorToast(`Zutat hinzugefÃ¼gt`);

                // Chip deaktivieren nach dem HinzufÃ¼gen
                $(this).prop('disabled', true).addClass('opacity-50');

                // Kurz zum Katalog scrollen, damit der User die Zutat sehen kann
                catalogRow[0]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            });
            refreshDurationUnitControls();
            loadIngredientArticleRules().finally(() => {
                const renderer = window.MasterStepRenderer;
                if (!renderer || typeof renderer.load !== 'function') {
                    setMasterTemplateError('Template-Fehler: MasterStepRenderer ist nicht geladen.');
                    return;
                }

                renderer.load().then((result) => {
                    if (!result) {
                        const loadError = typeof renderer.getLastLoadError === 'function'
                            ? renderer.getLastLoadError()
                            : 'Template-Datei konnte nicht geladen werden.';
                        setMasterTemplateError(`Template-Fehler: ${loadError}`);
                        return;
                    }
                    refreshMasterTemplateBuilder();
                }).catch((error) => {
                    const msg = error && error.message ? error.message : 'Unbekannter Fehler beim Laden.';
                    setMasterTemplateError(`Template-Fehler: ${msg}`);
                });
            });

            $("#currentStepIngredientButtons").on('click', '.ingredient-chip', function () {
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
                    if (getPlaceholderType(key) === 'ingredient') {
                        const selectedValue = getSelectedIngredientValueForInsert();
                        if (selectedValue) creatorState.placeholderAssignments[creatorState.activePlaceholderTokenId] = selectedValue;
                        creatorState.ingredientReplaceArmed = false;
                        clearSelectedIngredientChips();
                        creatorState.activePlaceholderTokenId = '';
                        $("#masterPreviewCard").removeClass('token-replace-active');
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
                showCreatorToast('Template gewÃ¤hlt');
            });

            function handleIngredientPlaceholderSelection(tokenId) {
                creatorState.activePlaceholderTokenId = tokenId;
                creatorState.ingredientReplaceArmed = true;
                $('#masterPreviewCard').addClass('token-replace-active');

                const hasExistingAssignment = !!(creatorState.placeholderAssignments[tokenId] || '').toString().trim();
                if (hasExistingAssignment) {
                    clearSelectedIngredientChips();
                    showCreatorToast('Neue Zutat auswÃ¤hlen zum Ersetzen');
                    return;
                }

                const selectedValue = getSelectedIngredientValueForInsert();
                if (selectedValue) {
                    creatorState.placeholderAssignments[tokenId] = selectedValue;
                    clearSelectedIngredientChips();
                    creatorState.activePlaceholderTokenId = '';
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');
                    showCreatorToast('Platzhalter ersetzt');
                    return;
                }

                showCreatorToast('Erst Zutaten auswÃ¤hlen');
            }

            function openEditorForPlaceholderToken(key, tokenId) {
                try {
                    if (!key || !tokenId) return;

                    closeAllEditors();
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');

                    const placeholderType = getPlaceholderType(key);

                    if (placeholderType === 'ingredient') {
                        handleIngredientPlaceholderSelection(tokenId);
                    } else if (placeholderEditorDispatch[placeholderType]) {
                        creatorState.activePlaceholderTokenId = placeholderEditorDispatch[placeholderType].keepTokenActive ? tokenId : '';
                        placeholderEditorDispatch[placeholderType].open(tokenId);
                        showCreatorToast(placeholderEditorDispatch[placeholderType].toast);
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

            $("#currentStepIngredientButtons").on('dragstart', '.ingredient-chip', function (e) {
                const name = ($(this).data('name') || '').toString();
                if (!name) return;
                creatorState.dragIngredientName = name;
                e.originalEvent.dataTransfer.effectAllowed = 'copy';
                e.originalEvent.dataTransfer.setData('text/plain', name);
            });

            $('#masterPreviewText').on('dragover', '.placeholder-token', function (e) {
                const key = ($(this).data('placeholder-key') || '').toString();
                if (getPlaceholderType(key) !== 'ingredient') return;
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
                if (getPlaceholderType(key) !== 'ingredient') {
                    showCreatorToast('Drag & Drop nur fÃ¼r ingredient/ingredients');
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
                showCreatorToast('Platzhalter zurÃ¼ckgesetzt');
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

            function bindSimpleEditorOptionHandlers(config) {
                const {
                    optionsContainer,
                    optionButtonClass,
                    stateKey,
                    render,
                    applyButton,
                    activeTokenKey,
                    closeEditor,
                    toast
                } = config;

                $(optionsContainer).on('click', optionButtonClass, function () {
                    const val = ($(this).data('value') || '').toString().trim();
                    creatorState[stateKey] = val;
                    render();
                });

                $(applyButton).on('click', function () {
                    const tokenId = creatorState[activeTokenKey];
                    const val = (creatorState[stateKey] || '').toString().trim();
                    if (!tokenId || !val) return;
                    creatorState[stateKey] = val;
                    creatorState.placeholderAssignments[tokenId] = val;
                    closeEditor();
                    showCreatorToast(toast);
                    renderTemplateCards();
                });
            }

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineHeatOptions',
                optionButtonClass: '.inline-heat-opt',
                stateKey: 'heatValue',
                render: renderInlineHeatOptions,
                applyButton: '#btnApplyHeat',
                activeTokenKey: 'activeHeatTokenId',
                closeEditor: closeHeatEditor,
                toast: 'Hitze-Stufe eingesetzt'
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineModeOptions',
                optionButtonClass: '.inline-mode-opt',
                stateKey: 'modeValue',
                render: renderInlineModeOptions,
                applyButton: '#btnApplyMode',
                activeTokenKey: 'activeModeTokenId',
                closeEditor: closeModeEditor,
                toast: 'Ofenmodus eingesetzt'
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
                showCreatorToast('Tool / GerÃ¤t eingesetzt');
                renderTemplateCards();
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineStateOptions',
                optionButtonClass: '.inline-state-opt',
                stateKey: 'stateValue',
                render: renderInlineStateOptions,
                applyButton: '#btnApplyState',
                activeTokenKey: 'activeStateTokenId',
                closeEditor: closeStateEditor,
                toast: 'Zustand eingesetzt'
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

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineGrindSizeOptions',
                optionButtonClass: '.inline-grind-size-opt',
                stateKey: 'grindSizeValue',
                render: renderInlineGrindSizeOptions,
                applyButton: '#btnApplyGrindSize',
                activeTokenKey: 'activeGrindSizeTokenId',
                closeEditor: closeGrindSizeEditor,
                toast: 'SchnittgrÃ¶ÃŸe eingesetzt'
            });

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineShapeOptions',
                optionButtonClass: '.inline-shape-opt',
                stateKey: 'shapeValue',
                render: renderInlineShapeOptions,
                applyButton: '#btnApplyShape',
                activeTokenKey: 'activeShapeTokenId',
                closeEditor: closeShapeEditor,
                toast: 'Schnittform eingesetzt'
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

            bindSimpleEditorOptionHandlers({
                optionsContainer: '#inlineSeasoningsOptions',
                optionButtonClass: '.inline-seasonings-opt',
                stateKey: 'seasoningsValue',
                render: renderInlineSeasoningsOptions,
                applyButton: '#btnApplySeasonings',
                activeTokenKey: 'activeSeasoningsTokenId',
                closeEditor: closeSeasoningsEditor,
                toast: 'Seasonings eingesetzt'
            });

            $(document).on('click', '.ingredient-db-row', function (e) {
                if ($(e.target).closest('button, input, select, label').length) return;
                const details = $(this).find('.ingredient-db-details').first();
                if (!details.length) return;
                const shouldOpen = details.hasClass('d-none');
                $('.ingredient-db-details').addClass('d-none');
                if (shouldOpen) {
                    details.removeClass('d-none');
                }
            });

            $('#btnCloseIngredientConfig, #btnCancelIngredientConfig').on('click', function () {
                closeIngredientConfigPopup();
            });

            $('#btnApplyIngredientConfig').on('click', function () {
                applyIngredientConfigPopup();
            });

            $('#ingredientConfigOverlay').on('click', function (e) {
                if (e.target === this) {
                    closeIngredientConfigPopup();
                }
            });

            // === Probability Variable Editor handlers ===
            $('#btnCloseProbVarEditor, #btnCancelProbVar').on('click', function () {
                closeProbVarEditor();
            });

            $('#btnApplyProbVar').on('click', function () {
                applyProbVarEditor();
            });

            $('#probVarEditorOverlay').on('click', function (e) {
                if (e.target === this) closeProbVarEditor();
            });

            // Ingredient chip - toggle selection in editor (apply via Einsetzen)
            $(document).on('click', '.js-prob-ingredient-chip', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;

                const selected = (creatorState.selectedIngredientIds || []).map(x => x.toString());
                if (selected.includes(id)) {
                    creatorState.selectedIngredientIds = selected.filter(x => x !== id);
                } else {
                    creatorState.selectedIngredientIds = [...selected, id];
                }

                const ings = typeof getSelectedIngredientsForSandbox === 'function' ? getSelectedIngredientsForSandbox() : [];
                const chips = (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.buildIngredientChipsHtml === 'function')
                    ? window.MasterStepCreatorHelpers.buildIngredientChipsHtml(ings, creatorState.selectedIngredientIds)
                    : '';
                $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewÃ¤hlt</span>');
            });

            // Option chip ? auto-apply and close
            $(document).on('click', '.js-prob-option-chip', function () {
                const value = ($(this).data('value') || $(this).text()).toString();
                if (probVarEditorCallback) probVarEditorCallback(value);
                closeProbVarEditor();
            });

            $('#btnAddRenderedStep').on('click', async function () {
                await addRenderedMasterStep();
                updateStoryProgress();
                renderAcceptedRecipeTextCard();
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

            $('#selectedIngredients').on('click', '.ingredient-row', function (e) {
                if ($(e.target).closest('button, input, select, label').length) return;
                openIngredientConfigPopup(this);
            });

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

            // SC2 Event handlers
            $('#sc2MasterTemplateCards').on('click', '.template-card', function () {
                const id = ($(this).data('id') || '').toString();
                if (!id) return;
                creatorState.selectedTemplateId = id;
                creatorState.activePlaceholderTokenId = '';
                creatorState.placeholderAssignments = {};
                closeSc2AllEditors();
                closeAllEditors();
                $('.template-card').removeClass('active');
                $(this).addClass('active template-tap');
                setTimeout(() => $(this).removeClass('template-tap'), 180);
                const ghost = $('#sc2TemplateFlyGhost');
                ghost.text($(this).data('title') || 'Template').removeClass('fly');
                void ghost[0]?.offsetWidth;
                ghost.addClass('fly');
                updatePreviewText();
                showSc2CreatorToast('Template gewÃ¤hlt');
            });

            $('#sc2MasterIngredientButtons').on('click', '.ingredient-chip', function () {
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

            $('#sc2MasterPreviewText').on('click', '.placeholder-token, .placeholder-wrap', function (e) {
                try {
                    if ($(e.target).closest('.placeholder-reset').length) return;
                    const token = $(this).hasClass('placeholder-token') ? $(this) : $(this).find('.placeholder-token').first();
                    const key = (token.data('placeholder-key') || '').toString();
                    const tokenId = (token.data('placeholder-token-id') || '').toString();
                    openSc2EditorForPlaceholderToken(key, tokenId);
                } catch (err) { console.error('SC2 token click error:', err); }
            });

            $('#sc2MasterPreviewText').on('click', '.placeholder-reset', function (e) {
                e.stopPropagation();
                const tokenId = ($(this).data('placeholder-token-id') || '').toString();
                const defaultValue = ($(this).data('default-value') || '').toString();
                if (!tokenId) return;
                if (defaultValue) creatorState.placeholderAssignments[tokenId] = defaultValue;
                else delete creatorState.placeholderAssignments[tokenId];
                if (creatorState.activePlaceholderTokenId === tokenId) {
                    creatorState.activePlaceholderTokenId = '';
                    creatorState.ingredientReplaceArmed = false;
                    $('#sc2MasterPreviewCard').removeClass('token-replace-active');
                }
                closeSc2AllEditors();
                showSc2CreatorToast('Platzhalter zurÃ¼ckgesetzt');
                renderIngredientChips();
                renderTemplateCards();
            });

            $('#sc2BtnAddRenderedStep').on('click', async function () {
                await addRenderedMasterStep();
                updateStoryProgress();
                renderAcceptedRecipeTextCard();
                showSc2CreatorToast('Step akzeptiert');
            });

            $('#sc2BtnOpenAdvancedSettings').on('click', function () {
                openSc2PronounEditorForToken(creatorState.activePronounTokenId || '__global__');
            });

            // SC2 Duration
            $('#sc2DurationValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.durationValue = Number.isFinite(value) && value > 0 ? value : 1;
            });
            $('#sc2DurationUnitSelect').on('change', function () {
                creatorState.durationUnit = ($(this).val() || 'minute').toString();
            });
            $('#sc2BtnApplyDuration').on('click', function () {
                const tokenId = creatorState.activeDurationTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getDurationInsertText();
                closeSc2DurationEditor();
                showSc2CreatorToast('Zeit eingesetzt');
                renderTemplateCards();
            });

            // SC2 Count
            $('#sc2CountValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.countValue = Number.isFinite(value) && value > 0 ? value : 1;
            });
            $('#sc2BtnApplyCount').on('click', function () {
                const tokenId = creatorState.activeCountTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getCountInsertText();
                closeSc2CountEditor();
                showSc2CreatorToast('Anzahl eingesetzt');
                renderTemplateCards();
            });

            // SC2 Temperature
            $('#sc2TemperatureValueInput').on('input', function () {
                const value = parseInt($(this).val(), 10);
                creatorState.temperatureValue = Number.isFinite(value) && value > 0 ? value : 180;
            });
            $('#sc2TemperatureUnitSelect').on('change', function () {
                creatorState.temperatureUnit = ($(this).val() || 'celsius').toString();
            });
            $('#sc2BtnApplyTemperature').on('click', function () {
                const tokenId = creatorState.activeTemperatureTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = getTemperatureInsertText();
                closeSc2TemperatureEditor();
                showSc2CreatorToast('Temperatur eingesetzt');
                renderTemplateCards();
            });

            // SC2 Pronoun
            $('#sc2InlinePronounOptions').on('click', '.inline-pronoun-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.pronounValue = val;
                renderInlineChoiceButtons('#sc2InlinePronounOptions', 'inline-pronoun-opt', getPronounSuggestionsForLang(currentLang), val);
            });
            $('#sc2BtnApplyPronoun').on('click', function () {
                const tokenId = creatorState.activePronounTokenId;
                const val = (creatorState.pronounValue || creatorState.computedPronoun || '').toString().trim();
                if (!val) return;
                creatorState.advancedPronounOverride = val;
                creatorState.computedPronoun = val;
                if (tokenId && tokenId !== '__global__') creatorState.placeholderAssignments[tokenId] = val;
                closeSc2PronounEditor();
                showSc2CreatorToast('Pronomen gesetzt');
                renderTemplateCards();
            });

            // SC2 Heat
            $('#sc2InlineHeatOptions').on('click', '.inline-heat-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.heatValue = val;
                renderInlineChoiceButtons('#sc2InlineHeatOptions', 'inline-heat-opt', getHeatOptions(), val);
            });
            $('#sc2BtnApplyHeat').on('click', function () {
                const tokenId = creatorState.activeHeatTokenId;
                const val = (creatorState.heatValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2HeatEditor();
                showSc2CreatorToast('Hitze-Stufe eingesetzt');
                renderTemplateCards();
            });

            // SC2 Mode
            $('#sc2InlineModeOptions').on('click', '.inline-mode-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.modeValue = val;
                renderInlineChoiceButtons('#sc2InlineModeOptions', 'inline-mode-opt', getModeOptions(), val);
            });
            $('#sc2BtnApplyMode').on('click', function () {
                const tokenId = creatorState.activeModeTokenId;
                const val = (creatorState.modeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2ModeEditor();
                showSc2CreatorToast('Ofenmodus eingesetzt');
                renderTemplateCards();
            });

            // SC2 Equipment
            $('#sc2InlineEquipmentArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.equipmentArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineEquipmentArticleOptions', creatorState.equipmentArticleValue);
            });
            $('#sc2InlineEquipmentOptions').on('click', '.inline-equipment-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.equipmentValue = val;
                $('#sc2InlineEquipmentOptions .inline-equipment-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyEquipment').on('click', function () {
                const tokenId = creatorState.activeEquipmentTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.equipmentArticleValue, creatorState.equipmentValue);
                closeSc2EquipmentEditor();
                showSc2CreatorToast('Tool eingesetzt');
                renderTemplateCards();
            });

            // SC2 State
            $('#sc2InlineStateOptions').on('click', '.inline-state-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.stateValue = val;
                renderInlineChoiceButtons('#sc2InlineStateOptions', 'inline-state-opt', getStateOptions(), val);
            });
            $('#sc2BtnApplyState').on('click', function () {
                const tokenId = creatorState.activeStateTokenId;
                const val = (creatorState.stateValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2StateEditor();
                showSc2CreatorToast('Zustand eingesetzt');
                renderTemplateCards();
            });

            // SC2 Tool
            $('#sc2InlineToolArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.toolArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineToolArticleOptions', creatorState.toolArticleValue);
            });
            $('#sc2InlineToolOptions').on('click', '.inline-tool-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.toolValue = val;
                $('#sc2InlineToolOptions .inline-tool-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyTool').on('click', function () {
                const tokenId = creatorState.activeToolTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.toolArticleValue, creatorState.toolValue);
                closeSc2ToolEditor();
                showSc2CreatorToast('Werkzeug eingesetzt');
                renderTemplateCards();
            });

            // SC2 GrindSize
            $('#sc2InlineGrindSizeOptions').on('click', '.inline-grind-size-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.grindSizeValue = val;
                renderInlineChoiceButtons('#sc2InlineGrindSizeOptions', 'inline-grind-size-opt', getGrindSizeOptions(), val);
            });
            $('#sc2BtnApplyGrindSize').on('click', function () {
                const tokenId = creatorState.activeGrindSizeTokenId;
                const val = (creatorState.grindSizeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2GrindSizeEditor();
                showSc2CreatorToast('SchnittgrÃ¶ÃŸe eingesetzt');
                renderTemplateCards();
            });

            // SC2 Shape
            $('#sc2InlineShapeOptions').on('click', '.inline-shape-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.shapeValue = val;
                renderInlineChoiceButtons('#sc2InlineShapeOptions', 'inline-shape-opt', getShapeOptions(), val);
            });
            $('#sc2BtnApplyShape').on('click', function () {
                const tokenId = creatorState.activeShapeTokenId;
                const val = (creatorState.shapeValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2ShapeEditor();
                showSc2CreatorToast('Schnittform eingesetzt');
                renderTemplateCards();
            });

            // SC2 Base
            $('#sc2InlineBaseArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.baseArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineBaseArticleOptions', creatorState.baseArticleValue);
            });
            $('#sc2InlineBaseOptions').on('click', '.inline-base-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.baseValue = val;
                $('#sc2InlineBaseOptions .inline-base-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyBase').on('click', function () {
                const tokenId = creatorState.activeBaseTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.baseArticleValue, creatorState.baseValue);
                closeSc2BaseEditor();
                showSc2CreatorToast('Basis eingesetzt');
                renderTemplateCards();
            });

            // SC2 Item
            $('#sc2InlineItemArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.itemArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineItemArticleOptions', creatorState.itemArticleValue);
            });
            $('#sc2InlineItemOptions').on('click', '.inline-item-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.itemValue = val;
                $('#sc2InlineItemOptions .inline-item-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyItem').on('click', function () {
                const tokenId = creatorState.activeItemTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.itemArticleValue, creatorState.itemValue);
                closeSc2ItemEditor();
                showSc2CreatorToast('Item eingesetzt');
                renderTemplateCards();
            });

            // SC2 Balance
            $('#sc2InlineBalanceArticleOptions').on('click', '.inline-article-opt', function () {
                creatorState.balanceArticleValue = ($(this).data('value') || '').toString();
                renderArticleOptions('#sc2InlineBalanceArticleOptions', creatorState.balanceArticleValue);
            });
            $('#sc2InlineBalanceOptions').on('click', '.inline-balance-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.balanceValue = val;
                $('#sc2InlineBalanceOptions .inline-balance-opt').removeClass('btn-light text-dark active').addClass('btn-outline-light');
                $(this).removeClass('btn-outline-light').addClass('btn-light text-dark active');
            });
            $('#sc2BtnApplyBalance').on('click', function () {
                const tokenId = creatorState.activeBalanceTokenId;
                if (!tokenId) return;
                creatorState.placeholderAssignments[tokenId] = composeArticleAndNoun(creatorState.balanceArticleValue, creatorState.balanceValue);
                closeSc2BalanceEditor();
                showSc2CreatorToast('Balance eingesetzt');
                renderTemplateCards();
            });

            // SC2 Seasonings
            $('#sc2InlineSeasoningsOptions').on('click', '.inline-seasonings-opt', function () {
                const val = ($(this).data('value') || '').toString().trim();
                creatorState.seasoningsValue = val;
                renderInlineChoiceButtons('#sc2InlineSeasoningsOptions', 'inline-seasonings-opt', getSeasoningsOptions(), val);
            });
            $('#sc2BtnApplySeasonings').on('click', function () {
                const tokenId = creatorState.activeSeasoningsTokenId;
                const val = (creatorState.seasoningsValue || '').toString().trim();
                if (!tokenId || !val) return;
                creatorState.placeholderAssignments[tokenId] = val;
                closeSc2SeasoningsEditor();
                showSc2CreatorToast('Seasonings eingesetzt');
                renderTemplateCards();
            });
            // === Ende SC2 ===

            updateLanguageLabels();
            syncIngredientSourceVisibility();

            // Page fully ready â€” hide overlay, show content
            $('#datatableLoadingOverlay').addClass('d-none');
            $('.creator-topbar, .feed-shell').css('visibility', 'visible');
        });























