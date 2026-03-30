// Extracted from CreatePosting.cshtml inline scripts (Smart Creator + page behavior)
        window.onerror = function(msg, url, line, col, error) {
            // Suppress benign errors: screen lock, tab close, ResizeObserver, Script error (CORS)
            if (line === 0 && col === 0) return true;
            if (typeof msg === 'string' && (
                msg.indexOf('Script error') !== -1 ||
                msg.indexOf('ResizeObserver') !== -1
            )) return true;
            window.CreatePostingFeedback.reportError('JavaScript-Fehler auf der Seite', error || new Error(String(msg || 'Unknown error')), {
                prefix: 'CreatePostingPage',
                selector: '#creatorToast'
            });
            return false;
        };


       

         function getCookieValue(name) {
             const cookie = document.cookie.split('; ').find(row => row.startsWith(`${name}=`));
             return cookie ? decodeURIComponent(cookie.split('=')[1]) : null;
         }

        var resolveLangKey = window.CreatePostingUtils.resolveLangKey;

        let currentLang = resolveLangKey(getCookieValue('deli-lang'));
        var nextThemePreference = null;
        let tempStepIdCounter = -1;
        let ingredientArticleRules = null;
        let ingredientTransforms = null;
        let activeIngredientConfigRow = null;
        const createPostingDraftStorageKey = 'createPostingDraft:v1';
        const createPostingDraftResetCookieName = 'createPostingDraftReset';
        let createPostingDraftSaveTimer = null;
        let isRestoringCreatePostingDraft = false;
        let createPostingDraftAutosaveHandle = null;

        function withCreatePostingDraftStorage(action, fallbackValue) {
            try {
                return action();
            } catch (error) {
                console.warn('CreatePosting draft storage unavailable', error);
                return fallbackValue;
            }
        }

        function readCreatePostingDraft() {
            return withCreatePostingDraftStorage(function () {
                const raw = localStorage.getItem(createPostingDraftStorageKey);
                if (!raw) return null;
                const parsed = JSON.parse(raw);
                return parsed && typeof parsed === 'object' ? parsed : null;
            }, null);
        }

        function writeCreatePostingDraft(draft) {
            return withCreatePostingDraftStorage(function () {
                localStorage.setItem(createPostingDraftStorageKey, JSON.stringify(draft));
                return true;
            }, false);
        }

        function clearCreatePostingDraft() {
            withCreatePostingDraftStorage(function () {
                localStorage.removeItem(createPostingDraftStorageKey);
            });
        }

        function clearCreatePostingDraftResetCookie() {
            document.cookie = `${createPostingDraftResetCookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; SameSite=Lax`;
        }

        function consumeCreatePostingDraftResetFlag() {
            if (getCookieValue(createPostingDraftResetCookieName) !== '1') return false;
            clearCreatePostingDraft();
            clearCreatePostingDraftResetCookie();
            return true;
        }

        function syncSelectedKeywordButtonStates() {
            const selectedIds = new Set($('#selectedKeywords .keyword-pill').map(function () {
                return ($(this).data('keyword-id') || '').toString();
            }).get().filter(Boolean));

            $('.keyword-btn').each(function () {
                const id = ($(this).data('keyword-id') || '').toString();
                $(this)
                    .toggleClass('btn-secondary', selectedIds.has(id))
                    .toggleClass('btn-outline-secondary', !selectedIds.has(id));
            });
        }

        function captureCreatePostingDraft() {
            return {
                updatedAt: new Date().toISOString(),
                currentLang: resolveLangKey(currentLang),
                basics: {
                    title: ($('input[name="Title"]').val() || '').toString(),
                    category: ($('select[name="Recipe.Category"]').val() || '').toString(),
                    preferences: ($('select[name="Recipe.Preferences"]').val() || '').toString(),
                    personCount: ($('input[name="Recipe.PersonCount"]').val() || '').toString(),
                    preparationTime: ($('input[name="Recipe.PreparationTime"]').val() || '').toString(),
                    ingredientSearch: ($('#ingredientSearch').val() || '').toString()
                },
                selectedIngredientsHtml: ($('#selectedIngredients').html() || '').toString(),
                selectedStepsHtml: ($('#selectedSteps').html() || '').toString(),
                selectedKeywordsHtml: ($('#selectedKeywords').html() || '').toString()
            };
        }

        function persistCreatePostingDraftNow() {
            if (isRestoringCreatePostingDraft) return;
            writeCreatePostingDraft(captureCreatePostingDraft());
        }

        function scheduleCreatePostingDraftSave(delay) {
            if (isRestoringCreatePostingDraft) return;
            clearTimeout(createPostingDraftSaveTimer);
            createPostingDraftSaveTimer = setTimeout(persistCreatePostingDraftNow, typeof delay === 'number' ? delay : 180);
        }

        function startCreatePostingFresh() {
            clearTimeout(createPostingDraftSaveTimer);
            clearCreatePostingDraft();
            clearCreatePostingDraftResetCookie();
            window.location.reload();
        }

        function hasMeaningfulCreatePostingDraft(draft) {
            if (!draft) return false;
            const basics = draft.basics || {};
            return !!(
                (basics.title || '').toString().trim() ||
                (basics.category || '').toString().trim() ||
                (basics.preferences || '').toString().trim() ||
                (basics.personCount || '').toString().trim() ||
                (basics.preparationTime || '').toString().trim() ||
                (draft.selectedIngredientsHtml || '').toString().trim() ||
                (draft.selectedStepsHtml || '').toString().trim() ||
                (draft.selectedKeywordsHtml || '').toString().trim()
            );
        }

        function restoreCreatePostingDraft() {
            if (consumeCreatePostingDraftResetFlag()) return false;

            const draft = readCreatePostingDraft();
            if (!hasMeaningfulCreatePostingDraft(draft)) return false;

            const basics = draft.basics || {};
            isRestoringCreatePostingDraft = true;
            try {
                $('input[name="Title"]').val(basics.title || '');
                $('select[name="Recipe.Category"]').val(basics.category || '');
                $('select[name="Recipe.Preferences"]').val(basics.preferences || '');
                $('input[name="Recipe.PersonCount"]').val(basics.personCount || '');
                $('input[name="Recipe.PreparationTime"]').val(basics.preparationTime || '');
                $('#ingredientSearch').val(basics.ingredientSearch || '');

                $('#selectedIngredients').html((draft.selectedIngredientsHtml || '').toString());
                $('#selectedSteps').html((draft.selectedStepsHtml || '').toString());
                $('#selectedKeywords').html((draft.selectedKeywordsHtml || '').toString());

                updateStepIndices();
                applyDerivedIngredientRowVisuals();
                renderIngredientChips();
                syncIngredientSourceVisibility();
                syncSelectedKeywordButtonStates();
                updateLanguageLabels();
                refreshMasterTemplateBuilder();
                refreshIngredientProbabilityHints();
            } finally {
                isRestoringCreatePostingDraft = false;
            }

            return true;
        }

        function showDraftRestoreBannerIfNeeded() {
            if (consumeCreatePostingDraftResetFlag()) return;
            const draft = readCreatePostingDraft();
            if (!hasMeaningfulCreatePostingDraft(draft)) return;
            $('#draftRestoreBanner').removeClass('d-none');
        }

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
            const qtyInput = $('#ingredientConfigQty')[0];
            if (qtyInput && typeof qtyInput.focus === 'function') {
                try { qtyInput.focus({ preventScroll: true }); } catch (_) { qtyInput.focus(); }
            }
        }

        function openIngredientConfigPopup(row) {
            const target = $(row);
            if (!target.length) return;
            activeIngredientConfigRow = target;
            const name = (target.find('.ingredient-name-text').first().text() || '').trim();
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
                scheduleCreatePostingDraftSave();
                return;
            }
        }

        /* ===== Probability Variable Editor ===== */
        let probVarEditorCallback = null;
        let probVarEditorAnchor = null;
        let probVarIngredientArticleValue = '';
        let probVarEditorCurrentVarKey = '';

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

        /* ===== OLD PROBABILITY EDITOR FUNCTIONS - AUSKOMMENTIERT 2026-03-27 =====
         * Diese Funktionen sind für das alte #probVarEditorDock System.
         * Jetzt wird openProbVarInlineEditor und das Universal Overlay verwendet.
         * Kann gelöscht werden, wenn alles funktioniert.
         */

        /*
        function openProbVarEditor(masterId, varKey, currentVal, onApply, anchorElement) {
            probVarEditorCallback = onApply;
            probVarEditorAnchor = anchorElement ? $(anchorElement).closest('.probability-template-wrap') : null;
            probVarEditorCurrentVarKey = (varKey || '').toString();
            const cleanKey = (varKey || '').toLowerCase().replace(/_/g, ' ');
            $('#probVarEditorTitle').text((cleanKey || 'Wert') + ' auswÃ¤hlen');
            $('#probVarIngredientArea, #probVarDurationArea, #probVarCountArea, #probVarTempArea, #probVarOptionsArea, #probVarTextArea, #probVarPronounStateArea').addClass('d-none');
            $('#probVarIngredientArticleChips').empty();
            $('#probVarApplyRow').addClass('d-none');

            const varType = typeof getPlaceholderType === 'function' ? getPlaceholderType(varKey) : '';

            if (varType === 'ingredient') {
                probVarIngredientArticleValue = '';
                $('#probVarIngredientArticleChips').empty();

                const selectedIds = (creatorState.selectedIngredientIds || []).map(x => x.toString());
                let ings = typeof getSelectedIngredientsForSandbox === 'function' ? getSelectedIngredientsForSandbox() : [];
                const lowerKey = (varKey || '').toLowerCase();
                if (lowerKey === 'liquid') ings = ings.filter(i => i.isLiquid);
                else if (lowerKey === 'fat') ings = ings.filter(i => i.isFat);
                else if (lowerKey === 'hard') ings = ings.filter(i => i.isHard);
                else if (lowerKey === 'soft') ings = ings.filter(i => i.isSoft);
                const chips = (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.buildIngredientChipsHtml === 'function')
                    ? window.MasterStepCreatorHelpers.buildIngredientChipsHtml(ings, selectedIds)
                    : '';
                $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewÃ¤hlt</span>');
                renderProbVarIngredientArticleOptions('');
                $('#probVarIngredientArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else if (varType === 'duration') {
                const num = parseInt((currentVal || '').split(' ')[0], 10) || 5;
                const loweredCurrentVal = (currentVal || '').toLowerCase();
                const isHour = loweredCurrentVal.includes('stund') || loweredCurrentVal.includes('hour');
                const isShort = loweredCurrentVal === getDurationUnitLabel('short', currentLang).toLowerCase();
                const isPerPackage = loweredCurrentVal === getDurationUnitLabel('per_package', currentLang).toLowerCase();
                $('#probVarDurationVal').val(num);
                $('#probVarDurationUnit').val(isPerPackage ? 'per_package' : isShort ? 'short' : isHour ? 'hour' : 'minute');
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
                // Check if this is pronoun or state â†' show combined pronoun+state area
                const lowerVarKey = (varKey || '').toLowerCase();
                const isPronounOrState = lowerVarKey === 'pronoun' || lowerVarKey === 'state' || lowerVarKey === 'pronoun2';

                if (isPronounOrState && window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                    // Get pronoun options
                    const pronounOptions = MasterStepRenderer.getVariablePresets('pronoun', currentLang || 'de') || [];
                    console.log('[openProbVarEditor] pronounOptions:', pronounOptions);
                    const pronounChips = pronounOptions.map(opt => {
                        const safe = $('<div>').text(opt).html();
                        const active = lowerVarKey === 'pronoun' && opt === currentVal ? ' active' : '';
                        return `<button type="button" class="btn btn-sm prob-var-chip js-prob-pronoun-chip${active}" data-value="${safe}">${safe}</button>`;
                    }).join('');

                    // Get state options
                    const stateOptions = MasterStepRenderer.getVariablePresets('state', currentLang || 'de') || [];
                    console.log('[openProbVarEditor] stateOptions:', stateOptions);
                    const stateChips = stateOptions.map(opt => {
                        const safe = $('<div>').text(opt).html();
                        const active = lowerVarKey === 'state' && opt === currentVal ? ' active' : '';
                        return `<button type="button" class="btn btn-sm prob-var-chip js-prob-state-chip${active}" data-value="${safe}">${safe}</button>`;
                    }).join('');

                    console.log('[openProbVarEditor] HTML generiert:', {
                        pronounChipsLength: pronounChips.length,
                        stateChipsLength: stateChips.length,
                        varKey,
                        lowerVarKey
                    });

                    if (pronounChips || stateChips) {
                        $('#probVarPronounChips').html(pronounChips || '<span class="small text-white-50">Keine Optionen</span>');
                        $('#probVarStateChips').html(stateChips || '<span class="small text-white-50">Keine Optionen</span>');
                        $('#probVarPronounStateArea').removeClass('d-none');
                    } else {
                        // Fallback to text input
                        $('#probVarTextVal').val(currentVal || '');
                        $('#probVarTextArea').removeClass('d-none');
                        $('#probVarApplyRow').removeClass('d-none');
                    }
                } else {
                    // Options or text fallback â†' try to get presets from MasterStepRenderer
                    let options = [];
                    if (window.MasterStepRenderer && typeof MasterStepRenderer.getVariablePresets === 'function') {
                        options = MasterStepRenderer.getVariablePresets(varKey, currentLang || 'de') || [];
                    }
                    if (!options.length && varType === 'heat' && typeof getHeatOptions === 'function') options = getHeatOptions();
                    if (!options.length && varType === 'mode') options = getModeOptions();

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
            }

            const dock = $('#probVarEditorDock');
            const overlay = $('#probVarEditorOverlay');
            dock.removeClass('prob-inline-mode');

            if (probVarEditorAnchor && probVarEditorAnchor.length) {
                overlay.removeClass('ing-active').attr('aria-hidden', 'true');
                probVarEditorAnchor.append(dock);
                requestAnimationFrame(function () { dock.addClass('ing-active prob-inline-mode'); });
                return;
            }

            $('#probVarEditorDockParking').append(dock);
            overlay.addClass('ing-active').attr('aria-hidden', 'false');
            requestAnimationFrame(function () { dock.addClass('ing-active'); });
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
                if (value && probVarIngredientArticleValue && probVarIngredientArticleValue !== 'ohne') {
                    value = `${probVarIngredientArticleValue} ${value}`.trim();
                }
            } else if (!$('#probVarDurationArea').hasClass('d-none')) {
                const unit = $('#probVarDurationUnit').val() || 'minute';
                const label = typeof getDurationUnitLabel === 'function' ? getDurationUnitLabel(unit, currentLang) : unit;
                if (unit === 'short' || unit === 'per_package') {
                    value = label;
                } else {
                    const num = parseInt($('#probVarDurationVal').val(), 10) || 1;
                    value = `${num} ${label}`;
                }
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
        */

        /* ===== END OLD FUNCTIONS ===== */

        // ══════════════════════════════════════════════════════════════════════════════
        // WRAPPER: openProbVarInlineEditor → Uses Unified Overlay System
        // ══════════════════════════════════════════════════════════════════════════════
        // This function is now a thin wrapper around the unified overlay system.
        // All existing calls to openProbVarInlineEditor automatically use the unified system.
        function openProbVarInlineEditor(masterId, varKey, currentVal, onApply, anchorEl, onApplyExtras, flowMode, carriedPronoun) {
            const helpers = window.MasterStepCreatorHelpers;
            if (!helpers || typeof helpers.openUniversalVariableEditor !== 'function') {
                console.error("[openProbVarInlineEditor] Unified system not available!");
                return;
            }

            // Get template preview HTML for the overlay header
            const escapedMasterId = (window.CSS && typeof window.CSS.escape === 'function')
                ? CSS.escape(masterId)
                : String(masterId || '').replace(/"/g, '\\"');
            const $anchor = $(`.probability-template-wrap[data-master-id="${escapedMasterId}"]`).first();
            const templateCard = $anchor.find('.probability-template-card')[0];
            const templatePreviewHtml = templateCard ? templateCard.innerHTML : '';

            console.log("[openProbVarInlineEditor] Wrapper calling unified system for:", { masterId, varKey, currentVal });

            // Call unified system
            helpers.openUniversalVariableEditor({
                varName: varKey,
                currentVal: currentVal || '',
                masterId: masterId,
                context: {
                    type: 'probability',
                    probabilityMasterId: masterId,
                    masterId: masterId,
                    tokenElement: anchorEl,
                    templatePreviewHtml: templatePreviewHtml,
                    flowMode: flowMode,
                    carriedPronoun: carriedPronoun
                },
                onApply: function (newVal, extras) {
                    console.log("[openProbVarInlineEditor] Unified onApply:", { newVal, extras });

                    // Call original onApply
                    if (typeof onApply === 'function') {
                        onApply(newVal);
                    }

                    // Call onApplyExtras if provided
                    if (extras && typeof onApplyExtras === 'function') {
                        onApplyExtras(extras);
                    }

                    // Handle extras.pronoun (for state variables)
                    if (extras && extras.pronoun && $anchor.length) {
                        const pronounToken = $anchor.find('.js-probability-var[data-var-key="pronoun"]').first();
                        if (pronounToken.length) {
                            pronounToken.text(extras.pronoun);
                            pronounToken.attr('data-has-value', '1');
                        }
                    }
                },
                onClose: function () {
                    console.log("[openProbVarInlineEditor] Unified onClose");
                }
            });
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

        const ingredientArticlePrefixesByLang = {
            de: ['der', 'die', 'das', 'den', 'dem', 'des', 'ein', 'eine', 'einen', 'einem', 'einer'],
            en: ['the', 'a', 'an'],
            esp: ['el', 'la', 'los', 'las', 'un', 'una', 'unos', 'unas'],
            prt: ['o', 'a', 'os', 'as', 'um', 'uma', 'uns', 'umas'],
            nl: ['de', 'het', 'een'],
            sv: ['en', 'ett'],
            da: ['en', 'et'],
            no: ['en', 'et'],
            id: [],
            ms: []
        };

        function stripLeadingArticle(value, langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const raw = (value || '').toString().trim();
            if (!raw) return '';
            const lower = raw.toLowerCase();
            const prefixes = ingredientArticlePrefixesByLang[lang] || [];
            const found = prefixes.find(prefix => lower === prefix || lower.startsWith(`${prefix} `));
            return found ? raw.slice(found.length).trim() : raw;
        }

        function normalizeIngredientMatchValue(value, langKey = currentLang) {
            return normalizeSearchText(stripLeadingArticle(value, langKey));
        }

        function splitIngredientNames(rawValue, langKey = currentLang) {
            const stripped = stripLeadingArticle(rawValue, langKey);
            return (stripped || '')
                .split(/,|\bund\b|\band\b|\by\b|\be\b/gi)
                .map(x => x.trim())
                .filter(Boolean);
        }

        function getBaseSandboxIngredients() {
            return $('#selectedIngredients .ingredient-row').filter(function () {
                return $(this).attr('data-derived-row') !== 'true';
            }).map(function () {
                const row = $(this);
                const id = row.find('input[name$="IngredientsAndNutrients.Id"]').val()?.toString() || '';
                const namesByLang = {
                    de: (row.data('name-de') || row.find('.ingredient-name-text').text() || '').toString().trim(),
                    en: (row.data('name-en') || row.data('name-de') || '').toString().trim(),
                    esp: (row.data('name-esp') || row.data('name-de') || '').toString().trim(),
                    prt: (row.data('name-prt') || row.data('name-de') || '').toString().trim(),
                    id: (row.data('name-id') || row.data('name-de') || '').toString().trim(),
                    nl: (row.data('name-nl') || row.data('name-de') || '').toString().trim(),
                    sv: (row.data('name-sv') || row.data('name-de') || '').toString().trim(),
                    da: (row.data('name-da') || row.data('name-de') || '').toString().trim(),
                    no: (row.data('name-no') || row.data('name-de') || '').toString().trim(),
                    ms: (row.data('name-ms') || row.data('name-de') || '').toString().trim()
                };
                const genusByLang = {
                    de: (row.data('genus-de') || '').toString().trim(),
                    en: (row.data('genus-en') || row.data('genus-de') || '').toString().trim(),
                    esp: (row.data('genus-esp') || row.data('genus-de') || '').toString().trim(),
                    prt: (row.data('genus-prt') || row.data('genus-de') || '').toString().trim(),
                    id: (row.data('genus-id') || row.data('genus-de') || '').toString().trim(),
                    nl: (row.data('genus-nl') || row.data('genus-de') || '').toString().trim(),
                    sv: (row.data('genus-sv') || row.data('genus-de') || '').toString().trim(),
                    da: (row.data('genus-da') || row.data('genus-de') || '').toString().trim(),
                    no: (row.data('genus-no') || row.data('genus-de') || '').toString().trim(),
                    ms: (row.data('genus-ms') || row.data('genus-de') || '').toString().trim()
                };
                return {
                    id,
                    name: namesByLang[resolveLangKey(currentLang)] || namesByLang.de || 'Zutat',
                    namesByLang,
                    genusDe: genusByLang.de || '',
                    genusLocalized: genusByLang[resolveLangKey(currentLang)] || genusByLang.de || '',
                    genusByLang,
                    iconHtml: (row.data('group-icon') || '').toString(),
                    groupId: (row.data('group-id') || '').toString(),
                    isLiquid: row.data('is-liquid') === true || row.data('is-liquid') === 'true',
                    isFat: row.data('is-fat') === true || row.data('is-fat') === 'true',
                    isHard: row.data('is-hard') === true || row.data('is-hard') === 'true',
                    isSoft: row.data('is-soft') === true || row.data('is-soft') === 'true',
                    sourceBaseId: id
                };
            }).get().filter(x => x.id || x.name);
        }

        function buildDerivedIngredientName(baseName, transformType, langKey, genusRaw) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const noun = (baseName || '').toString().trim();
            if (!noun || !ingredientTransforms) return noun;

            const pattern = ingredientTransforms.adjective_patterns?.[transformType]?.patterns?.[lang];
            if (!pattern) return noun;

            let template;
            if (typeof pattern === 'string') {
                template = pattern;
            } else {
                const genusKey = { 'masc': 'm', 'fem': 'f', 'neut': 'n' }[normalizeGenusKey(genusRaw)] || 'm';
                template = pattern[genusKey] || pattern.m || Object.values(pattern)[0];
            }

            return template.replace('{{noun}}', noun);
        }

        function getStepVariableDisplayValue(stableReference, key) {
            const vars = stableReference && stableReference.variables ? stableReference.variables : {};
            const item = vars && vars[key] ? vars[key] : null;
            return (item?.display_value || '').toString().trim();
        }

        function resolveCutTransformationType(stableReference) {
            const shape = normalizeSearchText(getStepVariableDisplayValue(stableReference, 'shape'));
            if (!shape) return 'piece';

            if (ingredientTransforms) {
                const patterns = ingredientTransforms.adjective_patterns || {};
                for (const [key, def] of Object.entries(patterns)) {
                    if (!def.trigger_steps?.includes('PREP_CUT_01') || !def.shape_match) continue;
                    if (def.shape_match.some(term => shape.includes(normalizeSearchText(term)))) return key;
                }
            }
            return 'piece';
        }

        function matchSandboxItemsByStep(items, stepRow, langKey = currentLang) {
            const stableReferenceRaw = (stepRow.data('step-reference-json') || stepRow.attr('data-step-reference-json') || '').toString();
            let stableReference = {};
            try { stableReference = stableReferenceRaw ? JSON.parse(stableReferenceRaw) : {}; } catch (_) { stableReference = {}; }

            const candidateNames = [];
            ['ingredient', 'ingredients', 'liquid', 'fat'].forEach(function (key) {
                const value = getStepVariableDisplayValue(stableReference, key);
                if (value) {
                    splitIngredientNames(value, langKey).forEach(name => candidateNames.push(name));
                }
            });

            const ingredientNameAttr = (stepRow.data('ingredient-name') || stepRow.attr('data-ingredient-name') || '').toString();
            if (ingredientNameAttr) {
                splitIngredientNames(ingredientNameAttr, langKey).forEach(name => candidateNames.push(name));
            }

            const normalizedCandidates = Array.from(new Set(candidateNames.map(name => normalizeIngredientMatchValue(name, langKey)).filter(Boolean)));
            if (!normalizedCandidates.length) return [];

            return items.filter(item => {
                const localizedName = item.namesByLang?.[resolveLangKey(langKey)] || item.name || '';
                const normalizedItem = normalizeIngredientMatchValue(localizedName, langKey);
                return normalizedCandidates.includes(normalizedItem);
            });
        }

        function buildSpecialTransformItems(item, transformDef) {
            return (transformDef.outputs || []).map(function (output) {
                const genusByLang = output.genus || {};
                return {
                    id: `${item.id || item.sourceBaseId || 'ingredient'}__${output.key}`,
                    name: output.names[resolveLangKey(currentLang)] || output.names.de,
                    namesByLang: output.names,
                    genusDe: genusByLang.de || 'n',
                    genusLocalized: genusByLang[resolveLangKey(currentLang)] || genusByLang.de || 'n',
                    genusByLang,
                    iconHtml: output.icon || item.iconHtml || '',
                    sourceBaseId: item.sourceBaseId || item.id || ''
                };
            });
        }

        function collectSelectedStepDerivationDescriptors() {
            return $('#selectedSteps .step-row').map(function () {
                const row = $(this);
                const masterId = (row.data('master-template-id') || row.attr('data-master-template-id') || '').toString();
                if (!masterId) return null;

                const stableReferenceRaw = (row.data('step-reference-json') || row.attr('data-step-reference-json') || '').toString();
                let stableReference = {};
                try { stableReference = stableReferenceRaw ? JSON.parse(stableReferenceRaw) : {}; } catch (_) { stableReference = {}; }

                return {
                    masterId,
                    ingredientName: (row.data('ingredient-name') || row.attr('data-ingredient-name') || '').toString(),
                    stableReference
                };
            }).get().filter(Boolean);
        }

        function matchesWholeWord(name, term) {
            if (name === term) return true;
            const escaped = term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            return new RegExp('(?:^|[\\s,;/()\\-])' + escaped + '(?:$|[\\s,;/()\\-])', 'i').test(name);
        }

        function deriveIngredientsForSteps(baseItems, stepDescriptors, langKey = currentLang) {
            let items = Array.isArray(baseItems) ? [...baseItems] : [];
            const descriptors = Array.isArray(stepDescriptors) ? stepDescriptors : [];
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const allLangs = ['de', 'en', 'esp', 'prt', 'id', 'nl', 'sv', 'da', 'no', 'ms'];

            if (!ingredientTransforms) return items;

            // auto_show transforms FIRST: show sub-ingredients (e.g. Ei → Eiklar/Eigelb)
            // so they are available for trigger_step transforms (e.g. Eiklar → Eischnee)
            const triggeredKeys = new Set(descriptors.map(d => d?.masterId).filter(Boolean));
            (ingredientTransforms.special_transforms || []).forEach(function (t) {
                if (!t.auto_show) return;
                if (triggeredKeys.has(t.trigger_step)) return; // will be handled in descriptor loop
                const matchTerms = t.match?.[lang] || t.match?.de || [];
                const targets = items.filter(function (item) {
                    const name = normalizeIngredientMatchValue(
                        item.namesByLang?.[lang] || item.name, lang);
                    return matchTerms.some(function (term) { return matchesWholeWord(name, term); });
                });
                if (!targets.length) return;
                targets.forEach(function (target) {
                    const subs = buildSpecialTransformItems(target, t);
                    subs.forEach(function (sub) {
                        sub.isAutoShowOptional = true;
                        if (!items.some(function (i) { return i.id === sub.id; })) {
                            items.push(sub);
                        }
                    });
                });
            });

            descriptors.forEach(function (descriptor) {
                const masterId = (descriptor?.masterId || '').toString();
                if (!masterId) return;

                // 1. Special transforms prüfen
                const specialMatch = (ingredientTransforms.special_transforms || [])
                    .find(function (t) { return t.trigger_step === masterId; });
                if (specialMatch) {
                    const matchTerms = specialMatch.match?.[lang] || specialMatch.match?.de || [];
                    const targets = items.filter(function (item) {
                        const name = normalizeIngredientMatchValue(
                            item.namesByLang?.[lang] || item.name, lang);
                        return matchTerms.some(function (t) { return matchesWholeWord(name, t); });
                    });
                    if (targets.length) {
                        const targetIds = new Set(targets.map(function (i) { return i.id; }));
                        // Build outputs only ONCE per transform (not per matching target)
                        const replacements = buildSpecialTransformItems(targets[0], specialMatch);
                        items = items.filter(function (i) { return !targetIds.has(i.id); }).concat(replacements);
                    }
                    return;
                }

                // 2. Adjective transforms prüfen
                const adjEntry = Object.entries(ingredientTransforms.adjective_patterns || {})
                    .find(function (entry) {
                        var def = entry[1];
                        if (!def.trigger_steps || !def.trigger_steps.includes(masterId)) return false;
                        if (masterId === 'PREP_CUT_01' && def.shape_match) {
                            var shape = resolveCutTransformationType(descriptor?.stableReference || {});
                            return entry[0] === shape;
                        }
                        return !def.shape_match;
                    });
                if (!adjEntry) return;

                var transformType = adjEntry[0];

                const matched = matchSandboxItemsByStep(items, {
                    data: function (key) {
                        if (key === 'step-reference-json') return JSON.stringify(descriptor?.stableReference || {});
                        if (key === 'ingredient-name') return descriptor?.ingredientName || '';
                        return '';
                    },
                    attr: function (key) {
                        if (key === 'data-step-reference-json') return JSON.stringify(descriptor?.stableReference || {});
                        if (key === 'data-ingredient-name') return descriptor?.ingredientName || '';
                        return '';
                    }
                }, lang);
                if (!matched.length) return;

                const matchedIds = new Set(matched.map(function (item) { return item.id; }));
                const replacements = matched.map(function (item) {
                    const namesByLang = {};
                    allLangs.forEach(function (l) {
                        namesByLang[l] = buildDerivedIngredientName(
                            item.namesByLang?.[l] || item.namesByLang?.de || item.name,
                            transformType, l,
                            item.genusByLang?.[l] || item.genusByLang?.de
                        );
                    });
                    return {
                        id: `${item.id || item.sourceBaseId || 'ingredient'}__${transformType}`,
                        name: namesByLang[lang] || namesByLang.de,
                        namesByLang,
                        genusDe: item.genusByLang?.de || item.genusDe || '',
                        genusLocalized: item.genusByLang?.[lang] || item.genusLocalized || item.genusDe || '',
                        genusByLang: item.genusByLang || {},
                        iconHtml: item.iconHtml || '',
                        sourceBaseId: item.sourceBaseId || item.id || ''
                    };
                });

                items = items.filter(function (item) { return !matchedIds.has(item.id); }).concat(replacements);
            });

            return items;
        }

        function deriveSandboxIngredients(baseItems) {
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.deriveIngredientsForSteps === 'function') {
                return window.MasterStepCreatorHelpers.deriveIngredientsForSteps(baseItems, collectSelectedStepDerivationDescriptors(), currentLang);
            }
            return deriveIngredientsForSteps(baseItems, collectSelectedStepDerivationDescriptors(), currentLang);
        }

        function getSelectedIngredientsForSandbox(langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const derived = deriveSandboxIngredients(getBaseSandboxIngredients());
            return derived.map(item => ({
                id: (item.id || '').toString(),
                name: item.namesByLang?.[lang] || item.namesByLang?.de || item.name || 'Zutat',
                namesByLang: item.namesByLang || {},
                genusDe: item.genusByLang?.de || item.genusDe || '',
                genusLocalized: item.genusByLang?.[lang] || item.genusLocalized || item.genusDe || '',
                genusByLang: item.genusByLang || {},
                iconHtml: item.iconHtml || '',
                groupId: item.groupId || '',
                isLiquid: !!item.isLiquid,
                isFat: !!item.isFat,
                isHard: !!item.isHard,
                isSoft: !!item.isSoft,
                sourceBaseId: item.sourceBaseId || item.id || ''
            }));
        }

        function localizeIngredientValueForSandbox(rawValue, targetLang, sourceLang = currentLang) {
            const value = (rawValue || '').toString().trim();
            if (!value) return '';

            const sourceItems = getSelectedIngredientsForSandbox(sourceLang);
            const targetItems = getSelectedIngredientsForSandbox(targetLang);
            const byId = new Map(targetItems.map(item => [item.id, item]));
            const tokens = splitIngredientNames(value, sourceLang);
            if (!tokens.length) return value;

            const localizedNames = tokens.map(token => {
                const normalizedToken = normalizeIngredientMatchValue(token, sourceLang);
                const sourceItem = sourceItems.find(item =>
                    normalizeIngredientMatchValue(item.name || item.namesByLang?.[resolveLangKey(sourceLang)] || '', sourceLang) === normalizedToken
                );
                if (!sourceItem) return '';
                const targetItem = byId.get(sourceItem.id);
                return targetItem?.name || '';
            }).filter(Boolean);

            if (!localizedNames.length) return value;
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.resolveIngredientInsertValue === 'function') {
                return window.MasterStepCreatorHelpers.resolveIngredientInsertValue(localizedNames, targetLang, value);
            }
            return localizedNames.join(', ');
        }

        function findCatalogIngredientRowByNames(namesByLang = {}) {
            const entries = Object.entries(namesByLang)
                .map(([lang, value]) => [lang, normalizeIngredientMatchValue(value, lang)])
                .filter(([, value]) => !!value);

            if (!entries.length) return $();

            return $('.ingredient-db-row').filter(function () {
                const row = $(this);
                return entries.some(([lang, value]) => {
                    const rowName = (row.data('name-' + lang) || '').toString().trim();
                    return rowName && normalizeIngredientMatchValue(rowName, lang) === value;
                });
            }).first();
        }

        function setIngredientRowDisabled(row, disabled) {
            row.find('input, select, textarea').prop('disabled', !!disabled);
        }

        function buildDerivedIngredientRowHtml(sourceRow, item, catalogRow) {
            const qty = (sourceRow.find('.ingredient-qty-hidden').val() || '').toString();
            const unitDe = (sourceRow.find('.ingredient-unit-hidden').val() || '').toString();
            const unitObj = findUnitByDe(unitDe);
            const unitLabel = getUnitLabel(unitObj, currentLang) || unitDe;
            const fallbackName = item.namesByLang?.[resolveLangKey(currentLang)] || item.name || '';
            const localizedData = catalogRow && catalogRow.length
                ? getLocalizedIngredientData(catalogRow, fallbackName)
                : {
                    names: {
                        de: item.namesByLang?.de || fallbackName,
                        en: item.namesByLang?.en || item.namesByLang?.de || fallbackName,
                        esp: item.namesByLang?.esp || item.namesByLang?.de || fallbackName,
                        prt: item.namesByLang?.prt || item.namesByLang?.de || fallbackName,
                        id: item.namesByLang?.id || item.namesByLang?.de || fallbackName,
                        nl: item.namesByLang?.nl || item.namesByLang?.de || fallbackName,
                        sv: item.namesByLang?.sv || item.namesByLang?.de || fallbackName,
                        da: item.namesByLang?.da || item.namesByLang?.de || fallbackName,
                        no: item.namesByLang?.no || item.namesByLang?.de || fallbackName,
                        ms: item.namesByLang?.ms || item.namesByLang?.de || fallbackName
                    },
                    genus: {
                        de: item.genusByLang?.de || '',
                        en: item.genusByLang?.en || '',
                        esp: item.genusByLang?.esp || '',
                        prt: item.genusByLang?.prt || '',
                        id: item.genusByLang?.id || '',
                        nl: item.genusByLang?.nl || '',
                        sv: item.genusByLang?.sv || '',
                        da: item.genusByLang?.da || '',
                        no: item.genusByLang?.no || '',
                        ms: item.genusByLang?.ms || ''
                    }
                };
            const iconHtml = (catalogRow && catalogRow.length ? catalogRow.data('group-icon') : '') || item.iconHtml || sourceRow.data('group-icon') || '';
            const catalogId = (catalogRow && catalogRow.length ? (catalogRow.data('ingredient-id') || '') : '').toString();
            let html = buildIngredientRowHtml({
                id: catalogId || item.id || '',
                localizedData,
                selectedQuantity: qty,
                selectedUnit: unitDe,
                selectedUnitLabel: unitLabel,
                displayName: localizedData.names[resolveLangKey(currentLang)] || localizedData.names.de || fallbackName,
                iconHtml
            });
            html = html.replace(
                '<div class="dynamic-item ingredient-row shadow-sm"',
                `<div class="dynamic-item ingredient-row shadow-sm" data-derived-row="true" data-derived-source-base-id="${escapeAttr(item.sourceBaseId || '')}" data-derived-key="${escapeAttr(item.id || '')}"`
            );
            return html;
        }

        function applyDerivedIngredientRowVisuals() {
            const selectedWrap = $('#selectedIngredients');
            if (!selectedWrap.length) return;

            const derivedItems = deriveSandboxIngredients(getBaseSandboxIngredients());
            const derivedBySourceId = derivedItems.reduce((map, item) => {
                const sourceId = (item.sourceBaseId || item.id || '').toString();
                if (!sourceId) return map;
                if (!map[sourceId]) map[sourceId] = [];
                map[sourceId].push(item);
                return map;
            }, {});

            selectedWrap.find('.ingredient-row[data-derived-row="true"]').remove();

            selectedWrap.find('.ingredient-row').filter(function () {
                return $(this).attr('data-derived-row') !== 'true';
            }).each(function () {
                const row = $(this);
                const rowId = (row.find('input[name$="IngredientsAndNutrients.Id"]').val() || '').toString();
                const names = derivedBySourceId[rowId] || [];
                const isEggSplit = names.length > 1 && names.every(item => /__(egg_white|egg_yolk)$/.test((item.id || '').toString()));

                if (isEggSplit) {
                    row.addClass('d-none').attr('data-derived-hidden-source', 'true');
                    setIngredientRowDisabled(row, true);
                    names.forEach(function (item) {
                        const catalogRow = findCatalogIngredientRowByNames(item.namesByLang || {});
                        const derivedHtml = buildDerivedIngredientRowHtml(row, item, catalogRow);
                        row.after(derivedHtml);
                    });
                    return;
                }

                // auto_show optional sub-ingredients (e.g. Eiklar/Eigelb under Ei)
                const autoShowItems = names.filter(item => item.isAutoShowOptional);
                const regularItems = names.filter(item => !item.isAutoShowOptional);

                if (autoShowItems.length) {
                    row.removeClass('d-none').removeAttr('data-derived-hidden-source');
                    setIngredientRowDisabled(row, false);
                    autoShowItems.forEach(function (item) {
                        const catalogRow = findCatalogIngredientRowByNames(item.namesByLang || {});
                        let derivedHtml = buildDerivedIngredientRowHtml(row, item, catalogRow);
                        derivedHtml = derivedHtml.replace(
                            'class="dynamic-item ingredient-row shadow-sm"',
                            'class="dynamic-item ingredient-row shadow-sm ingredient-optional-sub"'
                        );
                        row.after(derivedHtml);
                    });
                    if (!regularItems.length) return;
                }

                row.removeClass('d-none').removeAttr('data-derived-hidden-source');
                setIngredientRowDisabled(row, false);

                const visibleItem = names.length === 1 ? names[0] : null;
                const fallbackName = (row.data('name-' + resolveLangKey(currentLang)) || row.data('name-de') || '').toString().trim();
                const displayName = visibleItem
                    ? (visibleItem.namesByLang?.[resolveLangKey(currentLang)] || visibleItem.namesByLang?.de || visibleItem.name || fallbackName)
                    : fallbackName;
                row.find('.ingredient-name-text').text(displayName || fallbackName);
            });
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
            if (normalized === 'gold' || normalized === 'light' || normalized === 'gruen') return 'color';
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
            heatValue: '',
            activeModeTokenId: '',
            modeValue: '',
            activePronounTokenId: '',
            pronounValue: '',
            pendingStateTokenId: '',
            pendingSc2StateTokenId: '',
            activeEquipmentTokenId: '',
            equipmentValue: '',
            equipmentArticleValue: '',
            activeStateTokenId: '',
            stateValue: '',
            activeToolTokenId: '',
            toolValue: '',
            toolArticleValue: '',
            activeShapeTokenId: '',
            activeGrindSizeTokenId: '',
            activeBaseTokenId: '',
            grindSizeValue: '',
            shapeValue: '',
            activeItemTokenId: '',
            itemValue: '',
            itemArticleValue: '',
            baseValue: '',
            baseArticleValue: '',
            activeBalanceTokenId: '',
            balanceValue: '',
            balanceArticleValue: '',
            activeSeasoningsTokenId: '',
            seasoningsValue: 'Salz und Pfeffer'
        };

        const upsertStepUrl = window.CreatePostingPageConfig?.upsertStepUrl || '/WorldMiniApp/Home/UpsertStep';



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
            const selectedIds = (creatorState.selectedIngredientIds || []).map(x => (x || '').toString()).filter(Boolean);
            if (!selectedIds.length) return [];
            const byId = new Map(ingredients.map(x => [(x.id || '').toString(), x]));
            return selectedIds.map(id => byId.get(id)?.name || '').filter(Boolean);
        }

        function getSelectedIngredientNamesWithArticle(langKey = currentLang) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const ingredients = getSelectedIngredientsForSandbox();
            if (!ingredients.length) return [];

            const selectedIds = (creatorState.selectedIngredientIds || []).map(x => (x || '').toString()).filter(Boolean);
            if (!selectedIds.length) return [];

            const byId = new Map(ingredients.map(x => [(x.id || '').toString(), x]));
            return selectedIds.map(id => {
                const ingredient = byId.get(id);
                if (!ingredient) return '';
                const grammar = resolveGrammarForIngredient(ingredient.name, ingredient.genusByLang || {}, lang);
                return applyArticleToName(ingredient.name, grammar.article, lang) || ingredient.name;
            }).filter(Boolean);
        }

        function getSelectedIngredientForCreator() {
            const ingredients = getSelectedIngredientsForSandbox();
            const selectedId = ((creatorState.selectedIngredientIds || [])[0] || '').toString();
            if (!selectedId) return null;

            const selected = ingredients.find(x => (x.id || '').toString() === selectedId);
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

        function prioritizeSelectedIngredient(id, options = {}) {
            const ingredientId = (id || '').toString().trim();
            if (!ingredientId) return [];

            const keepOthers = options.keepOthers !== false;
            const current = (creatorState.selectedIngredientIds || []).map(x => (x || '').toString()).filter(Boolean);
            const rest = keepOthers ? current.filter(x => x !== ingredientId) : [];
            creatorState.selectedIngredientIds = [ingredientId, ...rest];
            return creatorState.selectedIngredientIds;
        }

        function assignIngredientPlaceholderValue(tokenId, ingredientId) {
            const safeTokenId = (tokenId || '').toString().trim();
            const safeIngredientId = (ingredientId || '').toString().trim();
            if (!safeTokenId || !safeIngredientId) return false;

            const ingredient = getSelectedIngredientsForSandbox().find(x => (x.id || '').toString() === safeIngredientId);
            if (!ingredient || !ingredient.name) return false;

            prioritizeSelectedIngredient(safeIngredientId);
            creatorState.placeholderAssignments[safeTokenId] = ingredient.name.toString().trim();
            creatorState.activePlaceholderTokenId = '';
            creatorState.ingredientReplaceArmed = false;
            $('#masterPreviewCard, #sc2MasterPreviewCard').removeClass('token-replace-active');
            return true;
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
            { type: 'ingredient', match: key => key === 'ingredient' || key === 'ingredients' || key === 'liquid' || key === 'fat' || key === 'components' },
            { type: 'duration', match: key => key.includes('duration') },
            { type: 'count', match: key => key === 'count' || key === 'servings' || key.includes('count') },
            { type: 'pronoun', match: key => key === 'pronoun' || key === 'pronoun2' },
            { type: 'temperature', match: key => key === 'temp' || key === 'temperature' },
            { type: 'heat', match: key => key === 'heat' || key.includes('heat') },
            { type: 'mode', match: key => key === 'mode' || key.includes('mode') },
            { type: 'equipment', match: key => key === 'equipment' },
            { type: 'state', match: key => key === 'state' || key.includes('state') || key.includes('consistency') },
            { type: 'tool', match: key => key === 'tool' },
            { type: 'grindSize', match: key => key === 'grind_size' || key.includes('grind') },
            { type: 'shape', match: key => key === 'shape' },
            { type: 'base', match: key => key === 'base' },
            { type: 'item', match: key => key === 'item' || key.includes('item') },
            { type: 'balance', match: key => key === 'balance' },
            { type: 'seasonings', match: key => key === 'seasonings' }
        ];

        function getPlaceholderType(key) {
            const normalizedKey = normalizePlaceholderKey(key);
            const matched = placeholderTypeMatchers.find(entry => entry.match(normalizedKey));
            return matched ? matched.type : '';
        }

        function getJsonVariableOptions(variableName, langKey = currentLang) {
            if (!window.MasterStepRenderer || typeof window.MasterStepRenderer.getVariablePresets !== 'function') {
                return [];
            }

            const options = window.MasterStepRenderer.getVariablePresets(variableName, resolveLangKey(langKey || currentLang || 'de'));
            if (!Array.isArray(options)) {
                return [];
            }

            return options
                .map(x => (x || '').toString().trim())
                .filter(Boolean);
        }

        function getFirstJsonVariableOption(variableName, langKey = currentLang) {
            return getJsonVariableOptions(variableName, langKey)[0] || '';
        }

        const placeholderEditorDispatch = {
            duration: { open: openDurationEditorForToken, toast: 'Zeit setzen' },
            count: { open: openCountEditorForToken, toast: 'Anzahl setzen' },
            // pronoun and state removed: use combined editor via openProbVarInlineEditor fallback
            // pronoun: { open: openPronounEditorForToken, toast: 'Pronomen wÃ¤hlen', keepTokenActive: true },
            temperature: { open: openTemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat: { open: openHeatEditorForToken, toast: 'Hitze-Stufe wÃ¤hlen' },
            mode: { open: openModeEditorForToken, toast: 'Ofenmodus wÃ¤hlen' },
            equipment: { open: openEquipmentEditorForToken, toast: 'Tool / GerÃ¤t wÃ¤hlen' },
            // state: { open: openStateEditorForToken, toast: 'Zustand wÃ¤hlen' },
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
            hour: { de: 'Stunden', en: 'hours', esp: 'horas', prt: 'horas', id: 'jam', nl: 'uur', sv: 'timmar', da: 'timer', no: 'timer', ms: 'jam' },
            short: { de: 'kurz', en: 'briefly', esp: 'brevemente', prt: 'brevemente', id: 'sebentar', nl: 'kort', sv: 'kort', da: 'kort', no: 'kort', ms: 'sebentar' },
            per_package: { de: 'laut Packungsanweisung', en: 'according to package instructions', esp: 'segun las instrucciones del paquete', prt: 'conforme as instrucoes da embalagem', id: 'sesuai petunjuk kemasan', nl: 'volgens de verpakkingsinstructies', sv: 'enligt forpackningens anvisningar', da: 'ifolge pakkens anvisninger', no: 'ifolge pakkens anvisninger', ms: 'mengikut arahan pembungkusan' }
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
            if (creatorState.durationUnit === 'per_package' || creatorState.durationUnit === 'short') {
                return getDurationUnitLabel(creatorState.durationUnit, currentLang);
            }
            const value = parseInt(creatorState.durationValue, 10);
            const safeValue = Number.isFinite(value) && value > 0 ? value : 1;
            const unitLabel = getDurationUnitLabel(creatorState.durationUnit, currentLang);
            return `${safeValue} ${unitLabel}`.trim();
        }

        function refreshDurationUnitControls() {
            const minuteLabel = getDurationUnitLabel('minute', currentLang);
            const hourLabel = getDurationUnitLabel('hour', currentLang);
            const shortLabel = getDurationUnitLabel('short', currentLang);
            const perPackageLabel = getDurationUnitLabel('per_package', currentLang);
            const minuteOption = $('#durationUnitSelect option[value="minute"]');
            const hourOption = $('#durationUnitSelect option[value="hour"]');
            const shortOption = $('#durationUnitSelect option[value="short"]');
            const perPackageOption = $('#durationUnitSelect option[value="per_package"]');
            if (minuteOption.length) minuteOption.text(minuteLabel);
            if (hourOption.length) hourOption.text(hourLabel);
            if (shortOption.length) shortOption.text(shortLabel);
            if (perPackageOption.length) perPackageOption.text(perPackageLabel);
        }

        function openDurationEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeDurationTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s+/);
            const loweredCurrentText = currentText.toLowerCase();
            const shortLabel = getDurationUnitLabel('short', currentLang).toLowerCase();
            const perPackageLabel = getDurationUnitLabel('per_package', currentLang).toLowerCase();
            if (loweredCurrentText === shortLabel) {
                creatorState.durationUnit = 'short';
            } else if (loweredCurrentText === perPackageLabel) {
                creatorState.durationUnit = 'per_package';
            } else if ((currentText || '').toLowerCase().includes('stund') || (currentText || '').toLowerCase().includes('hour')) {
                creatorState.durationUnit = 'hour';
            } else if (parsed) {
                creatorState.durationUnit = 'minute';
            }
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
            return getJsonVariableOptions('heat');
        }

        function renderInlineHeatOptions() {
            renderInlineChoiceButtons('#inlineHeatOptions', 'inline-heat-opt', getHeatOptions(), creatorState.heatValue || '');
        }

        function openHeatEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeHeatTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.heatValue || '').toString().trim().toLowerCase();
            const options = getHeatOptions();
            const selected = options.find(x => x.toLowerCase() === currentText) || options[0] || '';
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
            return getJsonVariableOptions('mode', lang);
        }

        function openModeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeModeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.modeValue || '').toString().trim().toLowerCase();
            const options = getModeOptions();
            const selected = options.find(x => x.toLowerCase() === currentText) || options[0] || '';
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
            return getJsonVariableOptions('articles', langKey);
        }

        function splitLeadingArticle(value, langKey = currentLang) {
            const options = getEditorArticleOptions(langKey);
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.splitLeadingArticleByOptions === 'function') {
                return window.MasterStepCreatorHelpers.splitLeadingArticleByOptions(value, options);
            }
            const raw = (value || '').toString().trim();
            if (!raw) return { article: '', noun: '' };
            const lower = raw.toLowerCase();
            const found = options.find(x => lower.startsWith(`${x.toLowerCase()} `));
            if (!found) return { article: '', noun: raw };
            return { article: found, noun: raw.substring(found.length).trim() };
        }

        function composeArticleAndNoun(article, noun) {
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.composeArticleAndNoun === 'function') {
                return window.MasterStepCreatorHelpers.composeArticleAndNoun(article, noun);
            }
            const art = (article || '').toString().trim();
            const n = (noun || '').toString().trim();
            if (!n) return '';
            return art ? `${art} ${n}` : n;
        }

        function getNounOptions(options, langKey = currentLang) {
            const articleOptions = getEditorArticleOptions(langKey);
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.getNounOptionsFromValues === 'function') {
                return window.MasterStepCreatorHelpers.getNounOptionsFromValues(options, articleOptions);
            }
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

        function buildArticleChoiceSelectionState(currentText, options, config) {
            const opts = config || {};
            const mode = (opts.mode || 'full').toString();
            const fallback = (opts.fallback || '').toString();
            const current = (currentText || '').toString().trim();
            const available = Array.isArray(options) ? options : [];
            const currentParts = splitLeadingArticle(current, currentLang);
            const currentNoun = currentParts.noun || current;

            let selected = '';
            if (mode === 'noun') {
                selected = available.find(x => (x || '').toString().trim().toLowerCase() === currentNoun.toLowerCase()) || '';
            } else {
                selected = available.find(x => (x || '').toString().trim().toLowerCase() === current.toLowerCase()) || '';
                if (!selected && currentNoun) {
                    selected = available.find(x => splitLeadingArticle(x, currentLang).noun.toLowerCase() === currentNoun.toLowerCase()) || '';
                }
            }
            selected = selected || available[0] || fallback;

            const selectedParts = splitLeadingArticle(selected, currentLang);
            return {
                article: currentParts.article || selectedParts.article || '',
                noun: selectedParts.noun || currentNoun || selected,
                selected
            };
        }

        function renderArticleChoiceOptions(config) {
            const opts = config || {};
            const wrap = $(opts.optionsSelector);
            if (!wrap.length) return;
            renderArticleOptions(opts.articleSelector, opts.selectedArticle || '');
            const optionClass = (opts.optionClass || '').toString().trim();
            const options = getNounOptions(opts.options || [], currentLang);
            const currentVal = splitLeadingArticle(opts.currentValue || '', currentLang).noun.toLowerCase();
            const html = options.map(x => {
                const isActive = currentVal === x.toLowerCase();
                const btnClass = isActive ? 'btn-light text-dark' : 'btn-outline-light';
                return `<button type="button" class="btn btn-sm ${btnClass} ${optionClass}" data-value="${$('<div>').text(x).html()}">${$('<div>').text(x).html()}</button>`;
            }).join('');
            wrap.html(html);
        }

        function getEquipmentOptions() {
            const lang = (currentLang || 'de').toString().toLowerCase();
            const merged = getJsonVariableOptions('equipment', lang)
                .map(x => ensureEquipmentArticle((x || '').toString().trim(), lang))
                .filter(Boolean);
            return Array.from(new Set(merged));
        }

        function openEquipmentEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeEquipmentTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.equipmentValue || '').toString().trim();
            const options = getEquipmentOptions();
            const selection = buildArticleChoiceSelectionState(currentText, options, { fallback: '', mode: 'full' });
            creatorState.equipmentArticleValue = selection.article;
            creatorState.equipmentValue = selection.selected;
            renderInlineEquipmentOptions();
            placeEditorLikeTemperature('#equipmentEditor');
            $('#equipmentEditor').removeClass('d-none');
        }

        function closeEquipmentEditor() {
            creatorState.activeEquipmentTokenId = '';
            $('#equipmentEditor').addClass('d-none');
        }

        function renderInlineEquipmentOptions() {
            renderArticleChoiceOptions({
                optionsSelector: '#inlineEquipmentOptions',
                articleSelector: '#inlineEquipmentArticleOptions',
                options: getEquipmentOptions(),
                selectedArticle: creatorState.equipmentArticleValue,
                currentValue: creatorState.equipmentValue,
                optionClass: 'inline-equipment-opt'
            });
        }

        function getStateOptions() {
            return getJsonVariableOptions('state');
        }

        function openStateEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeStateTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const options = getStateOptions();
            const selected = options.find(x => x.toLowerCase() === (currentText || '').toLowerCase()) || options[0] || '';
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
            const merged = getJsonVariableOptions('tool')
                .map(x => (x || '').toString().trim())
                .filter(Boolean);
            return Array.from(new Set(merged));
        }

        function openToolEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeToolTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.toolValue || '').toString().trim();
            const options = getToolOptions();
            const selection = buildArticleChoiceSelectionState(currentText, options, { fallback: '', mode: 'noun' });
            creatorState.toolArticleValue = selection.article;
            creatorState.toolValue = selection.noun;
            renderInlineToolOptions();
            $('#toolEditor').removeClass('d-none');
        }

        function closeToolEditor() {
            creatorState.activeToolTokenId = '';
            $('#toolEditor').addClass('d-none');
        }

        function renderInlineToolOptions() {
            renderArticleChoiceOptions({
                optionsSelector: '#inlineToolOptions',
                articleSelector: '#inlineToolArticleOptions',
                options: getToolOptions(),
                selectedArticle: creatorState.toolArticleValue,
                currentValue: creatorState.toolValue,
                optionClass: 'inline-tool-opt'
            });
        }

        function getGrindSizeOptions() {
            return getJsonVariableOptions('grind_size');
        }

        function openGrindSizeEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeGrindSizeTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.grindSizeValue || '').toString().trim();
            const options = getGrindSizeOptions();
            const selected = options.find(x => x.toLowerCase() === currentText.toLowerCase()) || options[0] || '';
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
            return getJsonVariableOptions('shape');
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
            const merged = [
                ...getJsonVariableOptions('base'),
                ...getJsonVariableOptions('item')
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
            const selection = buildArticleChoiceSelectionState(currentText, options, { fallback: '', mode: 'full' });
            creatorState.baseArticleValue = selection.article;
            creatorState.baseValue = selection.selected;
            renderInlineBaseOptions();
            $('#baseEditor').removeClass('d-none');
        }

        function closeBaseEditor() {
            creatorState.activeBaseTokenId = '';
            $('#baseEditor').addClass('d-none');
        }

        function renderInlineBaseOptions() {
            renderArticleChoiceOptions({
                optionsSelector: '#inlineBaseOptions',
                articleSelector: '#inlineBaseArticleOptions',
                options: getBaseOptions(),
                selectedArticle: creatorState.baseArticleValue,
                currentValue: creatorState.baseValue,
                optionClass: 'inline-base-opt'
            });
        }
        function getItemOptions() {
            return getNounOptions(getBaseAndItemOptions(), currentLang);
        }

        function openItemEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeItemTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.itemValue || '').toString().trim();
            const options = getItemOptions();
            const selection = buildArticleChoiceSelectionState(currentText, options, { fallback: '', mode: 'noun' });
            creatorState.itemArticleValue = selection.article || creatorState.itemArticleValue || '';
            creatorState.itemValue = selection.noun;
            renderInlineItemOptions();
            placeEditorLikeTemperature('#itemEditor');
            $('#itemEditor').removeClass('d-none');
        }

        function closeItemEditor() {
            creatorState.activeItemTokenId = '';
            $('#itemEditor').addClass('d-none');
        }

        function renderInlineItemOptions() {
            renderArticleChoiceOptions({
                optionsSelector: '#inlineItemOptions',
                articleSelector: '#inlineItemArticleOptions',
                options: getItemOptions(),
                selectedArticle: creatorState.itemArticleValue,
                currentValue: creatorState.itemValue,
                optionClass: 'inline-item-opt'
            });
        }

        function getBalanceOptions() {
            return getNounOptions(getJsonVariableOptions('balance'), currentLang);
        }

        function openBalanceEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBalanceTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.balanceValue || '').toString().trim();
            const options = getBalanceOptions();
            const selection = buildArticleChoiceSelectionState(currentText, options, { fallback: 'SÃ¤ure', mode: 'noun' });
            creatorState.balanceArticleValue = selection.article || creatorState.balanceArticleValue || '';
            creatorState.balanceValue = selection.noun;
            renderInlineBalanceOptions();
            placeEditorLikeTemperature('#balanceEditor');
            $('#balanceEditor').removeClass('d-none');
        }

        function closeBalanceEditor() {
            creatorState.activeBalanceTokenId = '';
            $('#balanceEditor').addClass('d-none');
        }

        function renderInlineBalanceOptions() {
            renderArticleChoiceOptions({
                optionsSelector: '#inlineBalanceOptions',
                articleSelector: '#inlineBalanceArticleOptions',
                options: getBalanceOptions(),
                selectedArticle: creatorState.balanceArticleValue,
                currentValue: creatorState.balanceValue,
                optionClass: 'inline-balance-opt'
            });
        }

        function getSeasoningsOptions() {
            return getJsonVariableOptions('seasonings');
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
            return getJsonVariableOptions('pronoun', langKey);
        }

        function getLocalizedFallbackForVariable(key, langKey) {
            const keyNorm = (key || '').toString().trim().toLowerCase();
            const lang = (langKey || currentLang || 'de').toString().toLowerCase();
            if (keyNorm === 'article') {
                return getFirstJsonVariableOption('articles', lang);
            }
            if (keyNorm === 'pronoun') {
                return getFirstJsonVariableOption('pronoun', lang);
            }
            if (keyNorm === 'heat') {
                return getFirstJsonVariableOption('heat', lang);
            }
            if (keyNorm === 'mode') {
                return getFirstJsonVariableOption('mode', lang);
            }
            if (keyNorm === 'equipment') {
                return getEquipmentOptions()[0] || '';
            }
            if (keyNorm === 'state') {
                return getStateOptions()[0] || '';
            }
            if (keyNorm === 'tool') {
                return getToolOptions()[0] || '';
            }
            if (keyNorm === 'grind_size') {
                return getGrindSizeOptions()[0] || '';
            }
            if (keyNorm === 'shape') {
                return getShapeOptions()[0] || '';
            }
            if (keyNorm === 'base') {
                return getBaseOptions()[0] || '';
            }
            if (keyNorm === 'item') {
                return getItemOptions()[0] || '';
            }
            if (keyNorm === 'balance') {
                return getBalanceOptions()[0] || '';
            }
            if (keyNorm === 'seasonings') {
                return getSeasoningsOptions()[0] || '';
            }
            if (keyNorm === 'step1' || keyNorm === 'step2' || keyNorm === 'step3' || keyNorm === 'dough') {
                return getFirstJsonVariableOption(keyNorm, lang);
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
            if (key === 'ingredient' || key === 'ingredients') {
                return key; // always placeholder â€” user must choose
            }
            if (key === 'pronoun' || key === 'pronoun2') {
                const selectedIngredient = getSelectedIngredientForCreator();
                const grammar = resolveGrammarForIngredient(selectedIngredient?.name || '', selectedIngredient?.genusByLang || {}, lang);
                return creatorState.advancedPronounOverride || creatorState.pronounValue || grammar.pronoun || getLocalizedFallbackForVariable('pronoun', lang);
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
            if (placeholderType === 'heat' || key.includes('heat')) return creatorState.heatValue || getLocalizedFallbackForVariable('heat', lang);
            if (placeholderType === 'mode') return creatorState.modeValue || getModeOptions()[0] || getLocalizedFallbackForVariable('mode', lang);
            if (placeholderType === 'tool' || key.includes('tool')) return creatorState.toolValue || getToolOptions()[0] || getLocalizedFallbackForVariable('tool', lang);
            if (placeholderType === 'grindSize' || key.includes('grind')) return creatorState.grindSizeValue || getGrindSizeOptions()[0] || getLocalizedFallbackForVariable('grind_size', lang);
            if (placeholderType === 'item') return creatorState.itemValue || getItemOptions()[0] || getLocalizedFallbackForVariable('item', lang);
            if (placeholderType === 'balance') return creatorState.balanceValue || getBalanceOptions()[0] || getLocalizedFallbackForVariable('balance', lang);
            if (placeholderType === 'seasonings') return creatorState.seasoningsValue || getSeasoningsOptions()[0] || getLocalizedFallbackForVariable('seasonings', lang);
            if (placeholderType === 'shape' || key.includes('shape')) return getLocalizedFallbackForVariable('shape', lang);
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

        function buildVariablesForTemplate(templateId, recipeType) {
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
                recipeCategory: $('#Recipe_Category').val(),
                recipeType: recipeType || ''
            }) || {};

            const keys = new Set([...(template?.variables || []), ...getPlaceholderKeysFromTemplate(template)]);
            const vars = { ...defaults };

            keys.forEach(key => {
                if (key === 'ingredient' || key === 'ingredients') {
                    // ingredient/ingredients: always placeholder â€” user must choose
                    vars[key] = key;
                    return;
                }

                if (vars[key] == null || String(vars[key]).trim() === '') {
                    vars[key] = resolveDefaultVariableValue(key);
                }
            });

            if (!vars.ingredient || vars.ingredient === 'ingredient') vars.ingredient = vars.ingredient || 'ingredient';
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
            const previewHtml = window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate === 'function'
                ? window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate(tpl, vars, creatorState.placeholderAssignments, {
                    activeTokenId: creatorState.activePlaceholderTokenId
                })
                : (tpl || '');

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
            window.CreatePostingFeedback.showToast(message, {
                selector: '#creatorToast',
                duration: 1200,
                state: creatorState,
                stateKey: 'toastVisible'
            });
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
                }
            }

            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const displayName = (item.name || '').toString();
                const iconHtml = (item.iconHtml || '').toString();
                const chipLabel = `${iconHtml ? `${iconHtml} ` : ''}${displayName}`;
                const chipHtml = `<button type="button" class="ingredient-chip ${active}" draggable="true" data-id="${item.id}" data-name="${displayName}">${chipLabel}</button>`;
                wrap.append(chipHtml);
                stepsWrap.append(chipHtml);
            });
            updatePreviewText();
            renderSc2IngredientChips();
        }

        function renderTemplateCards() {
            return window.CreatePostingTemplateBuilder.renderTemplateCards({
                creatorState,
                buildVariablesForTemplate,
                updatePreviewText,
                renderSc2TemplateCards,
                currentLang: () => currentLang
            });
        }


        function showMasterStepCreatorError(error, context) {
            const message = error && error.message ? error.message : (error || 'Unbekannter Fehler');
            console.error(`Master-Step-Creator Fehler (${context})`, error);
            window.CreatePostingFeedback.reportError(`Master-Step-Creator Fehler (${context})`, error || new Error(String(message)), {
                prefix: 'CreatePostingPage',
                selector: '#creatorToast'
            });
        }

        function setMasterTemplateError(message) {
            return window.CreatePostingTemplateBuilder.setMasterTemplateError(message);
        }

        function refreshMasterTemplateBuilder() {
            return window.CreatePostingTemplateBuilder.refreshMasterTemplateBuilder({
                creatorState,
                buildVariablesForTemplate,
                updatePreviewText,
                renderSc2TemplateCards,
                applyCurrentThemeAttributes,
                applyDerivedIngredientRowVisuals,
                renderIngredientChips,
                updateStoryProgress,
                renderAcceptedRecipeTextCard,
                showMasterStepCreatorError,
                currentLang: () => currentLang
            });
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

                function getTemplateVariablesForPersist(templateText) {
            const found = [];
            (templateText || '').replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_, key) {
                const normalizedKey = String(key || '').trim();
                if (normalizedKey && !found.includes(normalizedKey)) found.push(normalizedKey);
                return _;
            });
            return found;
        }

        function resolveOptionalTemplateSegmentsForPersist(templateText, vars) {
            let output = (templateText || '').toString();
            const optionalPattern = /\(([^()]*\{\{\s*[^}]+?\s*\}\}[^()]*)\)|\[([^\[\]]*\{\{\s*[^}]+?\s*\}\}[^\[\]]*)\]/g;
            let previous = null;

            while (output !== previous) {
                previous = output;
                output = output.replace(optionalPattern, function (_match, parenInner, bracketInner) {
                    const inner = String(parenInner || bracketInner || '');
                    const keys = getTemplateVariablesForPersist(inner);
                    if (!keys.length) return inner;
                    const hasAnyValue = keys.some(function (key) {
                        return ((vars && vars[key] != null ? String(vars[key]) : '').trim().length > 0);
                    });
                    return hasAnyValue ? inner : ' ';
                });
            }

            return output;
        }

        function renderTemplateForPersist(template, lang, vars) {
            const langKey = (lang || 'de').toLowerCase();
            let tpl = template?.templates?.[langKey] || template?.templates?.de || template?.templates?.en || '';
            if (!tpl) return '';

            tpl = resolveOptionalTemplateSegmentsForPersist(tpl, vars || {});

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
                de: rendered.de || rendered.en || '',
                en: rendered.en || rendered.de || '',
                esp: rendered.esp || rendered.de || rendered.en || '',
                prt: rendered.prt || rendered.de || rendered.en || '',
                phase: parseInt(template.phase || 0, 10),
                equipment: parseInt(template.equipment || 0, 10)
            };

            if (!payload.de || !payload.en) {
                const fallbackRenderedText = rendered[currentLang] || rendered.de || rendered.en || template?.templates?.de || template?.templates?.en || '';
                payload.de = payload.de || fallbackRenderedText;
                payload.en = payload.en || fallbackRenderedText;
                payload.esp = payload.esp || fallbackRenderedText;
                payload.prt = payload.prt || fallbackRenderedText;
            }

            if (!payload.de || !payload.en) {
                console.warn('Step-Render lieferte keine DE/EN-Texte', { templateId, vars, rendered, payload });
                return;
            }

            const localStepId = createFallbackStepId();
            getAllStepRows().push({
                id: localStepId,
                de: payload.de,
                en: payload.en,
                esp: payload.esp,
                prt: payload.prt,
                phase: payload.phase,
                equipment: payload.equipment
            });

            addStep(localStepId, null, rendered[currentLang] || payload.de || 'Neuer Schritt', {
                skipRender: true,
                ingredientName,
                masterTemplateId: templateId,
                stepData: {
                    de: payload.de,
                    en: payload.en,
                    esp: payload.esp,
                    prt: payload.prt,
                    phase: payload.phase,
                    equipment: payload.equipment
                }
            });
            updateStepIndices();
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
            const selectedIds = new Set($('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').map(function () {
                return $(this).val()?.toString();
            }).get());

            $('.ingredient-db-row').each(function () {
                const el = this;
                const id = ($(el).data('ingredient-id') || '').toString();
                const text = normalizeSearchText(($(el).data('name-' + currentLang) || $(el).data('name-de') || '') + '');
                const isSelected = !!id && selectedIds.has(id);
                const matchesSearch = !query || text.includes(query);
                el.classList.toggle('d-none', isSelected || !matchesSearch);
            });
        }

        // PrÃ¼ft ob ein Step bereits in der Auswahl ist
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
            openProbVarEditor: (masterId, varKey, currentVal, onApply, anchorElement, onApplyExtras) => openProbVarInlineEditor(masterId, varKey, currentVal, onApply, anchorElement, onApplyExtras),
            onAcceptTemplateStep: async (masterId, probabilityVars = null) => {
                creatorState.selectedTemplateId = masterId;
                const beforeCount = $(`#selectedSteps .step-row[data-master-template-id="${CSS.escape(masterId)}"]`).length;
                await addRenderedMasterStep(masterId, probabilityVars);

                const rows = $(`#selectedSteps .step-row[data-master-template-id="${CSS.escape(masterId)}"]`);
                const targetRow = rows.last();
                if (targetRow.length) {
                    targetRow.addClass('preview-animate');
                    setTimeout(() => targetRow.removeClass('preview-animate'), 350);
                }

                const afterCount = rows.length;
                showCreatorToast(afterCount > beforeCount ? 'Step akzeptiert' : 'Step bereits vorhanden');
            }
        });

        window.CreatePostingIngredientHelpers = Object.assign(window.CreatePostingIngredientHelpers || {}, {
            getSelectedIngredientsForSandbox: getSelectedIngredientsForSandbox,
            localizeIngredientValueForSandbox: localizeIngredientValueForSandbox
        });

        window.MasterStepCreatorHelpers = Object.assign(window.MasterStepCreatorHelpers || {}, {
            deriveIngredientsForSteps: deriveIngredientsForSteps
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
            $('.ingredient-db-row').each(function () { $(this).find('.ingredient-name-text').text($(this).data('name-' + langKey)); });
            $('#selectedIngredients .ingredient-row').each(function () {
                const row = $(this);
                const n = row.data('name-' + langKey) || row.data('name-de') || row.find('.ingredient-name-text').text();
                row.find('.ingredient-name-text').text(n);
                const qty = (row.find('.ingredient-qty-hidden').val() || '').toString();
                const unitDe = (row.find('.ingredient-unit-hidden').val() || '').toString();
                const unitObj = findUnitByDe(unitDe);
                const unitLabel = getUnitLabel(unitObj, langKey) || unitDe;
                row.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
            });
            applyDerivedIngredientRowVisuals();
            syncIngredientSourceVisibility();
            $('.keyword-btn').each(function () { $(this).text($(this).data('word-' + langKey)); });
            $('.keyword-pill').each(function () { $(this).find('.keyword-text').text($(this).data('word-' + langKey)); });
            refreshDurationUnitControls();
            updateCommonUnitChipLabels();
            renderAcceptedRecipeTextCard();
            refreshMasterTemplateBuilder();
            refreshIngredientProbabilityHints();
        }

        var _currentPreviewBlobUrl = null;

        function handleVideoUpload(input) {
            if (input.files && input.files[0]) {
                const file = input.files[0];
                if (_currentPreviewBlobUrl) {
                    URL.revokeObjectURL(_currentPreviewBlobUrl);
                }
                const fileUrl = URL.createObjectURL(file);
                _currentPreviewBlobUrl = fileUrl;
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
            if (_currentPreviewBlobUrl) {
                URL.revokeObjectURL(_currentPreviewBlobUrl);
                _currentPreviewBlobUrl = null;
            }
            $('#videoInput').val('');
            $('#videoPreviewContainer').addClass('d-none');
            $('#uploadLabel').removeClass('d-none');
            $('#videoPreview').attr('src', '');
            $('#imagePreview').attr('src', '').addClass('d-none');
            scheduleCreatePostingDraftSave();
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
            return {
                names,
                genus,
                groupId: (row.data('group-id') || '').toString(),
                isLiquid: (row.data('is-liquid') === true || row.data('is-liquid') === 'true') ? 'true' : 'false',
                isFat: (row.data('is-fat') === true || row.data('is-fat') === 'true') ? 'true' : 'false',
                isHard: (row.data('is-hard') === true || row.data('is-hard') === 'true') ? 'true' : 'false',
                isSoft: (row.data('is-soft') === true || row.data('is-soft') === 'true') ? 'true' : 'false'
            };
        }

        function buildIngredientDataAttributes(localizedData) {
            const langAttrs = supportedLanguages.map(lang =>
                `data-name-${lang}="${escapeAttr(localizedData.names[lang])}" data-genus-${lang}="${escapeAttr(localizedData.genus[lang])}"`
            ).join(' ');
            return `${langAttrs} data-group-id="${escapeAttr(localizedData.groupId || '')}" data-is-liquid="${localizedData.isLiquid || 'false'}" data-is-fat="${localizedData.isFat || 'false'}" data-is-hard="${localizedData.isHard || 'false'}" data-is-soft="${localizedData.isSoft || 'false'}"`;
        }

        function buildIngredientRowHtml({ id, localizedData, selectedQuantity, selectedUnit, selectedUnitLabel, displayName, iconHtml }) {
            return `
            <div class="dynamic-item ingredient-row shadow-sm" onclick="openIngredientConfigPopup(this)" title="Zum Bearbeiten antippen" data-group-icon="${escapeAttr(iconHtml)}" ${buildIngredientDataAttributes(localizedData)}>
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].IngredientsAndNutrients.Id" value="${id}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Quantity.Quantitys" class="ingredient-qty-hidden" value="${selectedQuantity}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Measure.Metrics_DE" class="ingredient-unit-hidden" value="${escapeAttr(selectedUnit)}" />

                <div class="ingredient-row-main">
                    <div class="fw-bold display-name-selected d-flex align-items-center gap-2"><span class="ingredient-group-icon">${iconHtml}</span><span class="ingredient-name-text">${escapeAttr(displayName)}</span></div>
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
            const iconHtml = (row.data('group-icon') || '').toString();
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
                displayName,
                iconHtml
            });

            $('#selectedIngredients').append(newIngredient);

            refreshMasterTemplateBuilder();
            if (typeof syncIngredientSourceVisibility === "function") {
                syncIngredientSourceVisibility();
            }
            refreshIngredientProbabilityHints();
            scheduleCreatePostingDraftSave();
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
            showCreatorToast('Chip auswÃ¤hlen â†’ dann Einsetzen tippen');
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
            closeIngredientConfigPopup();
            $(btn).closest('.ingredient-row').remove();
            refreshMasterTemplateBuilder();
            syncIngredientSourceVisibility();
            refreshIngredientProbabilityHints();
            scheduleCreatePostingDraftSave();
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

            $("#selectedSteps").append(`<div class="dynamic-item d-flex align-items-center step-row" draggable="true" data-step-id="${id}" data-step-edited="false" data-master-template-id="${$("<div>").text((normalizedStepData.masterTemplateId || options?.masterTemplateId || "")).html()}" data-step-reference-json="${stepReferenceEncoded}" data-ingredient-name="${$("<div>").text(stepIngredientName).html()}" data-ingredient-fractions="${$("<div>").text(JSON.stringify(options?.fractionData || null)).html()}">
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
            applyDerivedIngredientRowVisuals();
            renderIngredientChips();
            scheduleCreatePostingDraftSave();

        }

        function removeStep(btn) {
            $(btn).closest('.step-row').remove();
            updateStepIndices();
            applyDerivedIngredientRowVisuals();
            renderIngredientChips();
            scheduleCreatePostingDraftSave();
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
            applyDerivedIngredientRowVisuals();
            renderIngredientChips();
            showCreatorToast('Step(s) entfernt');
            scheduleCreatePostingDraftSave();
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
            scheduleCreatePostingDraftSave();
        }

        function removeKeyword(id) {
            $('#selectedKeywords').find(`.keyword-pill[data-keyword-id="${id}"]`).remove();
            $(`.keyword-btn[data-keyword-id="${id}"]`).removeClass('btn-secondary').addClass('btn-outline-secondary');
            scheduleCreatePostingDraftSave();
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
            scheduleCreatePostingDraftSave();
        }

        let draggedStepRow = null;


        // =========================================================
        // SC2 â†’ Eingebetteter Smart Step Creator im Steps-Bereich
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
            const previewHtml = window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate === 'function'
                ? window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate(tpl, vars, creatorState.placeholderAssignments, {
                    activeTokenId: creatorState.activePlaceholderTokenId
                })
                : (tpl || '');
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
                const displayName = (item.name || '').toString();
                const iconHtml = (item.iconHtml || '').toString();
                const chipLabel = `${iconHtml ? `${iconHtml} ` : ''}${displayName}`;
                wrap.append(`<button type="button" class="ingredient-chip ${active}" data-id="${item.id}" data-name="${displayName}">${chipLabel}</button>`);
            });
        }
        function renderSc2TemplateCards() {
            return window.CreatePostingTemplateBuilder.renderSc2TemplateCards({
                creatorState,
                buildVariablesForTemplate,
                currentLang: () => currentLang
            });
        }

        function showSc2CreatorToast(message) {
            window.CreatePostingFeedback.showToast(message, {
                selector: '#sc2CreatorToast',
                duration: 1200
            });
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
            const selection = buildArticleChoiceSelectionState(currentText, getEquipmentOptions(), { fallback: '', mode: 'noun' });
            creatorState.equipmentArticleValue = selection.article;
            creatorState.equipmentValue = selection.noun;
            renderArticleChoiceOptions({
                optionsSelector: '#sc2InlineEquipmentOptions',
                articleSelector: '#sc2InlineEquipmentArticleOptions',
                options: getEquipmentOptions(),
                selectedArticle: creatorState.equipmentArticleValue,
                currentValue: creatorState.equipmentValue,
                optionClass: 'inline-equipment-opt'
            });
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
            const selection = buildArticleChoiceSelectionState(currentText, getToolOptions(), { fallback: '', mode: 'noun' });
            creatorState.toolArticleValue = selection.article;
            creatorState.toolValue = selection.noun;
            renderArticleChoiceOptions({
                optionsSelector: '#sc2InlineToolOptions',
                articleSelector: '#sc2InlineToolArticleOptions',
                options: getToolOptions(),
                selectedArticle: creatorState.toolArticleValue,
                currentValue: creatorState.toolValue,
                optionClass: 'inline-tool-opt'
            });
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
            const selection = buildArticleChoiceSelectionState(currentText, getBaseOptions(), { fallback: '', mode: 'noun' });
            creatorState.baseArticleValue = selection.article;
            creatorState.baseValue = selection.noun;
            renderArticleChoiceOptions({
                optionsSelector: '#sc2InlineBaseOptions',
                articleSelector: '#sc2InlineBaseArticleOptions',
                options: getBaseOptions(),
                selectedArticle: creatorState.baseArticleValue,
                currentValue: creatorState.baseValue,
                optionClass: 'inline-base-opt'
            });
            $('#sc2BaseEditor').removeClass('d-none');
        }
        function openSc2ItemEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeItemTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.itemValue || '').toString().trim();
            const selection = buildArticleChoiceSelectionState(currentText, getItemOptions(), { fallback: '', mode: 'noun' });
            creatorState.itemArticleValue = selection.article || creatorState.itemArticleValue || '';
            creatorState.itemValue = selection.noun;
            renderArticleChoiceOptions({
                optionsSelector: '#sc2InlineItemOptions',
                articleSelector: '#sc2InlineItemArticleOptions',
                options: getItemOptions(),
                selectedArticle: creatorState.itemArticleValue,
                currentValue: creatorState.itemValue,
                optionClass: 'inline-item-opt'
            });
            $('#sc2ItemEditor').removeClass('d-none');
        }
        function openSc2BalanceEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeBalanceTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || creatorState.balanceValue || '').toString().trim();
            const selection = buildArticleChoiceSelectionState(currentText, getBalanceOptions(), { fallback: '', mode: 'noun' });
            creatorState.balanceArticleValue = selection.article || creatorState.balanceArticleValue || '';
            creatorState.balanceValue = selection.noun;
            renderArticleChoiceOptions({
                optionsSelector: '#sc2InlineBalanceOptions',
                articleSelector: '#sc2InlineBalanceArticleOptions',
                options: getBalanceOptions(),
                selectedArticle: creatorState.balanceArticleValue,
                currentValue: creatorState.balanceValue,
                optionClass: 'inline-balance-opt'
            });
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
            // pronoun and state removed: use combined editor via openProbVarInlineEditor fallback
            // pronoun:     { open: openSc2PronounEditorForToken,     toast: 'Pronomen wÃ¤hlen', keepTokenActive: true },
            temperature: { open: openSc2TemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat:        { open: openSc2HeatEditorForToken,        toast: 'Hitze-Stufe wÃ¤hlen' },
            mode:        { open: openSc2ModeEditorForToken,        toast: 'Ofenmodus wÃ¤hlen' },
            equipment:   { open: openSc2EquipmentEditorForToken,   toast: 'Tool / GerÃ¤t wÃ¤hlen' },
            // state:       { open: openSc2StateEditorForToken,       toast: 'Zustand wÃ¤hlen' },
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
                console.log('[openSc2EditorForPlaceholderToken]', { key, tokenId, placeholderType, hasDispatch: !!sc2EditorDispatch[placeholderType] });

                // Sequential editing DISABLED: User can fill pronoun and state in any order
                // if (window.MasterStepCreatorHelpers && window.MasterStepCreatorHelpers.triggerPronounBeforeState({
                //     placeholderType,
                //     containerSelector: '#sc2MasterPreviewText',
                //     assignments: creatorState.placeholderAssignments,
                //     onTriggered: function (pronounTokenId) {
                //         creatorState.pendingSc2StateTokenId = tokenId;
                //         openSc2PronounEditorForToken(pronounTokenId);
                //         showSc2CreatorToast('Zuerst Pronomen wÃ¤hlen');
                //         renderSc2TemplateCards();
                //     }
                // })) return;

                if (placeholderType === 'ingredient') {
                    creatorState.activePlaceholderTokenId = tokenId;
                    creatorState.ingredientReplaceArmed = true;
                    $('#sc2MasterPreviewCard').addClass('token-replace-active');
                    showSc2CreatorToast('Zutat auswÃ¤hlen');
                } else if (sc2EditorDispatch[placeholderType]) {
                    creatorState.activePlaceholderTokenId = sc2EditorDispatch[placeholderType].keepTokenActive ? tokenId : '';
                    sc2EditorDispatch[placeholderType].open(tokenId);
                    showSc2CreatorToast(sc2EditorDispatch[placeholderType].toast);
                } else if (typeof openProbVarInlineEditor === 'function') {
                    console.log('[openSc2EditorForPlaceholderToken] Using openProbVarInlineEditor fallback for:', key);
                    creatorState.activePlaceholderTokenId = tokenId;
                    openProbVarInlineEditor(
                        creatorState.selectedTemplateId || '',
                        key,
                        creatorState.placeholderAssignments[tokenId] || '',
                        function (selectedValue) {
                            creatorState.placeholderAssignments[tokenId] = selectedValue;
                            renderSc2TemplateCards();
                        },
                        null,
                        function (extras) {
                            if (!extras || !extras.pronoun) return;
                            const pronounTokenId = $(`#sc2MasterPreviewText .placeholder-token[data-placeholder-key="pronoun"]`).data('placeholder-token-id');
                            if (pronounTokenId) {
                                creatorState.placeholderAssignments[pronounTokenId] = extras.pronoun;
                            }
                        }
                    );
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
            showDraftRestoreBannerIfNeeded();

            $('.creator-topbar').on('click', '[data-create-posting-theme]', function () {
                const nextTheme = ($(this).data('create-posting-theme') || '').toString();
                window.setCreatePostingTheme(nextTheme);
            });
            var _ingredientSearchTimer;
            $('#ingredientSearch').on('input', function () {
                clearTimeout(_ingredientSearchTimer);
                _ingredientSearchTimer = setTimeout(syncIngredientSourceVisibility, 200);
                $('#clearIngredientSearch').toggleClass('d-none', !($(this).val() || '').toString().trim());
                scheduleCreatePostingDraftSave();
            });
            $('#clearIngredientSearch').toggleClass('d-none', !(($('#ingredientSearch').val() || '').toString().trim()));
            $('#clearIngredientSearch').on('click', function () {
                $('#ingredientSearch').val('').trigger('input').trigger('focus');
                syncIngredientSourceVisibility();
            });
            $('#restoreDraftBtn').on('click', function () {
                restoreCreatePostingDraft();
                $('#draftRestoreBanner').addClass('d-none');
            });
            $('#discardDraftBtn').on('click', function () {
                clearCreatePostingDraft();
                $('#draftRestoreBanner').addClass('d-none');
            });
            $('#recipeForm').on('input change', 'input, textarea, select', function () {
                scheduleCreatePostingDraftSave();
            });
            $('#recipeForm').on('submit', function () {
                persistCreatePostingDraftNow();
            });
            $(window).on('pagehide beforeunload', function () {
                persistCreatePostingDraftNow();
            });
            if (!createPostingDraftAutosaveHandle) {
                createPostingDraftAutosaveHandle = window.setInterval(function () {
                    persistCreatePostingDraftNow();
                }, 8000);
            }
            refreshIngredientProbabilityHints();

            $('#recipeForm').on('input', '#ingredientConfigQty, .js-db-qty', function () {
                const sanitized = sanitizeQuantityInputValue(this.value);
                if (this.value !== sanitized) {
                    this.value = sanitized;
                }
            });

            $('#recipeForm').on('click', '.js-common-unit-chip', function (e) {
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
            $('#recipeForm').on('click', '.js-config-unit-chip', function (e) {
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

            $('#recipeForm').on('click', '.js-probability-type', async function () {
                const typeId = ($(this).data('type') || '').toString();
                const typeName = ($(this).data('name') || '').toString();
                const score = parseInt($(this).data('score'), 10) || 0;
                await showProbabilityTemplateSuggestions(typeId, typeName, score);
            });

            // Updates the template preview card after a variable is changed
            function updateProbabilityTemplatePreview($anchor, masterId) {
                console.log('[updateProbabilityTemplatePreview] Called for:', masterId);

                // Collect all token values from the template
                const values = {};
                $anchor.find('.js-probability-var').each(function() {
                    const $token = $(this);
                    const varKey = ($token.data('var-key') || $token.data('var') || '').toString();
                    const hasValue = $token.attr('data-has-value') === '1';
                    if (hasValue) {
                        values[varKey] = $token.text().trim();
                    }
                });

                console.log('[updateProbabilityTemplatePreview] Collected values:', values);

                // Get the template raw text from the token's data attribute or reconstruct it
                const templateText = $anchor.find('.probability-template-text').first().text();

                // Render template with values using shared function
                const helpers = window.MasterStepCreatorHelpers;
                if (helpers && typeof helpers.renderTemplate === 'function') {
                    const rendered = helpers.renderTemplate(templateText, masterId, values);
                    console.log('[updateProbabilityTemplatePreview] Rendered:', rendered);

                    // Update the preview text (keep tokens interactive)
                    // We don't replace the HTML, just update the visual preview in the card header
                    // The actual tokens remain unchanged for editing
                } else {
                    console.warn('[updateProbabilityTemplatePreview] renderTemplate not available');
                }
            }

            // Click on Probability Variable Token to edit
            $('#recipeForm').on('click', '.js-probability-var', function (e) {
                e.stopPropagation(); // Prevent template selection
                e.preventDefault();

                const token = $(this);
                const varKey = (token.data('var-key') || token.data('var') || '').toString();
                const masterId = token.closest('.probability-template-wrap').data('master-id') || '';

                if (!varKey || !masterId) {
                    console.error('[Probability Var Click] Missing varKey or masterId:', { varKey, masterId });
                    return;
                }

                // Get current value from token text (or empty if it's the placeholder)
                let currentVal = token.text().trim();
                if (currentVal === varKey) currentVal = ''; // Reset if it's still the placeholder name

                console.log('[Probability Var Click] Opening editor for:', { masterId, varKey, currentVal });

                // Get template preview HTML for the overlay header
                const templateCard = token.closest('.probability-template-wrap').find('.probability-template-card')[0];
                const templatePreviewHtml = templateCard ? templateCard.innerHTML : '';

                // Use UNIFIED overlay system
                const helpers = window.MasterStepCreatorHelpers;
                if (helpers && typeof helpers.openUniversalVariableEditor === 'function') {
                    helpers.openUniversalVariableEditor({
                        varName: varKey,
                        currentVal: currentVal,
                        masterId: masterId,
                        context: {
                            type: 'probability',
                            probabilityMasterId: masterId,
                            masterId: masterId,
                            tokenElement: token[0],
                            templatePreviewHtml: templatePreviewHtml
                        },
                        onApply: function(newVal, extras) {
                            console.log('[Probability Unified onApply] Custom logic (if needed):', newVal, extras);
                            // Context update is now handled by updateContextValue() in unified apply
                            // Token text update is done there
                            // Template preview update is done there
                            // This callback is only for custom extra logic if needed
                        },
                        onClose: function() {
                            console.log('[Probability Unified onClose]');
                            // Cleanup if needed
                        }
                    });
                } else {
                    console.error('[Probability Var Click] Unified system not available');
                }
            });

            $('#recipeForm').on('click', '.js-probability-template', function () {
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

            // Reset Button in Probability Area
            $('#recipeForm').on('click', '.probability-template-wrap .placeholder-reset', function (e) {
                e.stopPropagation();
                e.preventDefault();

                const btn = this;
                const varName = btn.dataset.var;
                const tokenId = btn.dataset.tokenId;
                const masterId = $(btn).closest('.probability-template-wrap').data('master-id');

                if (!varName || !masterId) return;

                console.log("[Probability Reset Button]", { varName, masterId, tokenId });

                // Find the token
                const token = $(btn).siblings(`.js-probability-var[data-var="${varName}"]`).first();
                if (!token.length) return;

                // Clear the token value
                token.text(varName);
                token.attr('data-has-value', '0');

                // Clear multi-ingredients storage
                if (window.ProbabilityMultiIngredients[masterId]) {
                    window.ProbabilityMultiIngredients[masterId][varName] = [];
                }

                console.log("[Probability Reset Button] Cleared:", varName);
            });

            // Multi-Ingredient Plus-Button in Probability Area
            $('#recipeForm').on('click', '.probability-template-wrap .ingredient-plus-btn', function (e) {
                e.stopPropagation();
                const btn = this;
                const varName = btn.dataset.var;
                const masterId = btn.dataset.masterId;
                if (!varName || !masterId) return;

                const wrap = $(btn).closest('.probability-template-wrap');
                const $host = wrap.find('.prob-inline-editor-host');
                const editorEl = $host[0];

                console.log("[Probability Plus Button]", { varName, masterId, editorEl });

                // If editor is closed, open it directly (don't click token - that triggers template selection!)
                if (!editorEl || $host.hasClass('d-none') || $host.html().trim() === '') {
                    console.log("[Probability Plus Button] Editor is closed, opening directly");
                    const token = wrap.find(`.js-probability-var[data-var="${varName}"]`).first()[0];
                    if (token && typeof openProbVarInlineEditor === 'function') {
                        // Get helpers from global
                        const helpersGlobal = window.MasterStepCreatorHelpers;

                        // Get current value from storage
                        const existingList = (window.ProbabilityMultiIngredients[masterId] || {})[varName] || [];
                        const currentVal = helpersGlobal && helpersGlobal.formatSelectedIngredientList
                            ? helpersGlobal.formatSelectedIngredientList(existingList, currentLang || 'de')
                            : '';

                        // Open editor with dummy callback
                        const dummyOnApply = function(val) {
                            $(token).text(val || varName);
                            $(token).attr('data-has-value', val ? '1' : '0');
                        };

                        openProbVarInlineEditor(masterId, varName, currentVal, dummyOnApply, token);
                    }
                    return;
                }

                // Editor is open - add current selection to list
                const helpers = window.MasterStepCreatorHelpers;
                if (!helpers) return;

                const article = editorEl.dataset.selectedArticle || '';
                const fraction = editorEl.dataset.selectedFraction || '';
                const currentSelectedChips = JSON.parse(editorEl.dataset.selectedIngredientValues || '[]');

                console.log("[Probability Plus Button] Selection:", { article, fraction, currentSelectedChips });

                if (currentSelectedChips.length === 0) {
                    alert("Bitte eine Zutat auswählen");
                    return;
                }

                // Get existing list for this master and variable
                if (!window.ProbabilityMultiIngredients[masterId]) {
                    window.ProbabilityMultiIngredients[masterId] = {};
                }
                const existingList = window.ProbabilityMultiIngredients[masterId][varName] || [];

                console.log("[Probability Plus Button] BEFORE adding - existingList:", JSON.parse(JSON.stringify(existingList)));

                // Normalize and add
                const normalized = helpers.normalizeIngredientValues ? helpers.normalizeIngredientValues(currentSelectedChips) : currentSelectedChips;
                normalized.forEach(item => {
                    item.article = article;
                    item.fraction = fraction;
                    existingList.push(item);
                });

                console.log("[Probability Plus Button] AFTER adding - existingList:", JSON.parse(JSON.stringify(existingList)));

                // Store
                window.ProbabilityMultiIngredients[masterId][varName] = existingList;

                // Format and update display
                const composed = helpers.formatSelectedIngredientList ? helpers.formatSelectedIngredientList(existingList, currentLang || 'de') : '';

                console.log("[Probability Plus Button] Composed:", composed, "existingList:", existingList);

                // Re-open editor immediately to show updated chips
                // The editor will display all multi-ingredients as removable chips
                const token = wrap.find(`.js-probability-var[data-var="${varName}"]`).first()[0];
                if (token) {
                    // Call openProbVarInlineEditor with the updated value
                    // This will re-render the editor with the new multi-ingredients list
                    const dummyOnApply = function(val) {
                        // Update token display
                        $(token).text(val || varName);
                        $(token).attr('data-has-value', val ? '1' : '0');
                    };
                    openProbVarInlineEditor(masterId, varName, composed, dummyOnApply, token);
                }
            });

            // Remove Multi-Ingredient Chip in Probability Area
            $('#recipeForm').on('click', '.prob-inline-editor-host button[data-remove-multi-ingredient]', function (e) {
                e.stopPropagation();
                e.preventDefault();
                const btn = this;
                const idx = parseInt(btn.dataset.removeMultiIngredient, 10);

                console.log("[Probability Remove Chip] Button clicked!", { btn, idx });

                const $host = $(btn).closest('.prob-inline-editor-host');
                // editorFor is on the .prob-inline-editor or .duration-editor element inside the host
                const editorEl = $host.find('.prob-inline-editor, .duration-editor').first()[0];
                if (!editorEl) {
                    console.log("[Probability Remove Chip] No editor element found!");
                    return;
                }

                const varName = editorEl.dataset.editorFor;
                const masterId = $host.closest('.probability-template-wrap').attr('data-master-id');

                console.log("[Probability Remove Chip]", { idx, varName, masterId, editorEl });

                if (!masterId || !varName || isNaN(idx)) return;

                const existingList = (window.ProbabilityMultiIngredients[masterId] || {})[varName] || [];
                if (idx < 0 || idx >= existingList.length) return;

                // Remove item
                existingList.splice(idx, 1);
                window.ProbabilityMultiIngredients[masterId][varName] = existingList;

                // Update display
                const helpers = window.MasterStepCreatorHelpers;
                const composed = helpers && helpers.formatSelectedIngredientList
                    ? helpers.formatSelectedIngredientList(existingList, currentLang || 'de')
                    : '';

                console.log("[Probability Remove Chip] New composed:", composed);

                // Re-open editor to show updated chips
                const wrap = $host.closest('.probability-template-wrap');
                const token = wrap.find(`.js-probability-var[data-var="${varName}"]`).first();
                if (token.length) {
                    token.text(composed || varName);
                    token.attr('data-has-value', composed ? '1' : '0');
                }

                const onApply = function () {};
                openProbVarInlineEditor(masterId, varName, composed, onApply, token[0]);
            });

            // Vorgeschlagene Zutat hinzufÃ¼gen: findet die Zeile im Katalog und ruft addIngredient auf
            $('#recipeForm').on('click', '.js-typical-ingredient-chip', function () {
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

            // Helper functions for loading grammar rules
            function loadIngredientArticleRules() {
                if (!window.CreatePostingPageData || typeof window.CreatePostingPageData.loadArticleRules !== 'function') {
                    return Promise.resolve(null);
                }
                return window.CreatePostingPageData.loadArticleRules()
                    .then(rules => {
                        window.articleRules = rules;
                        return rules;
                    })
                    .catch(() => null);
            }

            function loadIngredientTransforms() {
                if (!window.CreatePostingPageData || typeof window.CreatePostingPageData.loadIngredientTransforms !== 'function') {
                    return Promise.resolve(null);
                }
                return window.CreatePostingPageData.loadIngredientTransforms()
                    .then(transforms => {
                        window.ingredientTransforms = transforms;
                        return transforms;
                    })
                    .catch(() => null);
            }

            refreshDurationUnitControls();
            Promise.all([loadIngredientArticleRules(), loadIngredientTransforms()]).finally(() => {
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
                prioritizeSelectedIngredient(id);

                creatorState.advancedPronounOverride = '';
                creatorState.advancedArticleOverride = '';
                syncGrammarAssignmentsForSelectedIngredient();

                if (creatorState.activePlaceholderTokenId) {
                    const key = getPlaceholderKeyByTokenId(creatorState.activePlaceholderTokenId);
                    if (getPlaceholderType(key) === 'ingredient') {
                        if (assignIngredientPlaceholderValue(creatorState.activePlaceholderTokenId, id)) {
                            showCreatorToast('Platzhalter ersetzt');
                        }
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

                const selectedNames = getSelectedIngredientNames();
                if (selectedNames.length === 1) {
                    creatorState.placeholderAssignments[tokenId] = selectedNames[0];
                    creatorState.activePlaceholderTokenId = '';
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');
                    showCreatorToast('Platzhalter ersetzt');
                    return;
                }

                showCreatorToast(selectedNames.length > 1 ? 'Zutat antippen zum Einsetzen' : 'Erst Zutaten auswÃ¤hlen');
            }

            function openEditorForPlaceholderToken(key, tokenId) {
                try {
                    if (!key || !tokenId) return;

                    closeAllEditors();
                    creatorState.ingredientReplaceArmed = false;
                    $('#masterPreviewCard').removeClass('token-replace-active');

                    const placeholderType = getPlaceholderType(key);

                    // Sequential editing DISABLED: User can fill pronoun and state in any order
                    // if (window.MasterStepCreatorHelpers && window.MasterStepCreatorHelpers.triggerPronounBeforeState({
                    //     placeholderType,
                    //     containerSelector: '#masterPreviewText',
                    //     assignments: creatorState.placeholderAssignments,
                    //     onTriggered: function (pronounTokenId) {
                    //         creatorState.pendingStateTokenId = tokenId;
                    //         creatorState.activePlaceholderTokenId = pronounTokenId;
                    //         openPronounEditorForToken(pronounTokenId);
                    //         showCreatorToast('Zuerst Pronomen wählen');
                    //         renderTemplateCards();
                    //     }
                    // })) return;

                    if (placeholderType === 'ingredient') {
                        handleIngredientPlaceholderSelection(tokenId);
                    } else if (placeholderEditorDispatch[placeholderType]) {
                        creatorState.activePlaceholderTokenId = placeholderEditorDispatch[placeholderType].keepTokenActive ? tokenId : '';
                        placeholderEditorDispatch[placeholderType].open(tokenId);
                        showCreatorToast(placeholderEditorDispatch[placeholderType].toast);
                    } else if (typeof openProbVarInlineEditor === 'function') {
                        creatorState.activePlaceholderTokenId = tokenId;
                        openProbVarInlineEditor(
                            creatorState.selectedTemplateId || '',
                            key,
                            creatorState.placeholderAssignments[tokenId] || '',
                            function (selectedValue) {
                                creatorState.placeholderAssignments[tokenId] = selectedValue;
                                renderTemplateCards();
                            },
                            null,
                            function (extras) {
                                if (!extras || !extras.pronoun) return;
                                const pronounTokenId = $(`#masterPreviewText .placeholder-token[data-placeholder-key="pronoun"]`).data('placeholder-token-id');
                                if (pronounTokenId) {
                                    creatorState.placeholderAssignments[pronounTokenId] = extras.pronoun;
                                }
                            }
                        );
                    } else {
                        creatorState.activePlaceholderTokenId = '';
                    }

                    renderTemplateCards();
                } catch (err) {
                    window.CreatePostingFeedback.reportError('Fehler beim Ã–ffnen des Platzhalter-Editors', err, {
                        prefix: 'CreatePostingPage',
                        selector: '#creatorToast'
                    });
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
                    window.CreatePostingFeedback.reportError('Fehler beim Klick auf einen Platzhalter', err, {
                        prefix: 'CreatePostingPage',
                        selector: '#creatorToast'
                    });
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

            function bindArticleChoiceEditorHandlers(config) {
                const {
                    articleContainer,
                    optionsContainer,
                    optionButtonClass,
                    articleStateKey,
                    valueStateKey,
                    render,
                    applyButton,
                    activeTokenKey,
                    closeEditor,
                    toast,
                    toastFn,
                    composeDuringSelection,
                    composeOnApply
                } = config;

                const showToast = typeof toastFn === 'function' ? toastFn : showCreatorToast;

                if (articleContainer) {
                    $(articleContainer).on('click', '.inline-article-opt', function () {
                        const val = ($(this).data('value') || '').toString().trim();
                        creatorState[articleStateKey] = val;
                        if (composeDuringSelection) {
                            const noun = splitLeadingArticle(creatorState[valueStateKey] || '', currentLang).noun;
                            creatorState[valueStateKey] = composeArticleAndNoun(val, noun);
                        }
                        render();
                    });
                }

                $(optionsContainer).on('click', optionButtonClass, function () {
                    const val = ($(this).data('value') || '').toString().trim();
                    creatorState[valueStateKey] = composeDuringSelection
                        ? composeArticleAndNoun(creatorState[articleStateKey], val)
                        : val;
                    render();
                });

                $(applyButton).on('click', function () {
                    const tokenId = creatorState[activeTokenKey];
                    const val = (creatorState[valueStateKey] || '').toString().trim();
                    if (!tokenId || !val) return;
                    creatorState.placeholderAssignments[tokenId] = composeOnApply
                        ? composeArticleAndNoun(creatorState[articleStateKey], val)
                        : val;
                    closeEditor();
                    showToast(toast);
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
                // Sequential editing: open pending state token after DOM update
                const pendingStateId = creatorState.pendingStateTokenId || '';
                creatorState.pendingStateTokenId = '';
                setTimeout(function () {
                    if (pendingStateId) {
                        openStateEditorForToken(pendingStateId);
                    } else {
                        const stateTokenEl = document.querySelector('#masterPreviewText .placeholder-token[data-placeholder-key="state"]');
                        if (stateTokenEl) {
                            const stateTokenId = stateTokenEl.dataset.placeholderTokenId;
                            if (!(creatorState.placeholderAssignments[stateTokenId] || '').toString().trim()) {
                                openStateEditorForToken(stateTokenId);
                            }
                        }
                    }
                }, 50);
            });

            bindArticleChoiceEditorHandlers({
                articleContainer: '#inlineEquipmentArticleOptions',
                optionsContainer: '#inlineEquipmentOptions',
                optionButtonClass: '.inline-equipment-opt',
                articleStateKey: 'equipmentArticleValue',
                valueStateKey: 'equipmentValue',
                render: renderInlineEquipmentOptions,
                applyButton: '#btnApplyEquipment',
                activeTokenKey: 'activeEquipmentTokenId',
                closeEditor: closeEquipmentEditor,
                toast: 'Tool / GerÃƒÂ¤t eingesetzt',
                composeDuringSelection: true,
                composeOnApply: false
            });

            bindArticleChoiceEditorHandlers({
                articleContainer: '#inlineToolArticleOptions',
                optionsContainer: '#inlineToolOptions',
                optionButtonClass: '.inline-tool-opt',
                articleStateKey: 'toolArticleValue',
                valueStateKey: 'toolValue',
                render: renderInlineToolOptions,
                applyButton: '#btnApplyTool',
                activeTokenKey: 'activeToolTokenId',
                closeEditor: closeToolEditor,
                toast: 'Werkzeug eingesetzt',
                composeDuringSelection: true,
                composeOnApply: false
            });

            bindArticleChoiceEditorHandlers({
                articleContainer: '#inlineBaseArticleOptions',
                optionsContainer: '#inlineBaseOptions',
                optionButtonClass: '.inline-base-opt',
                articleStateKey: 'baseArticleValue',
                valueStateKey: 'baseValue',
                render: renderInlineBaseOptions,
                applyButton: '#btnApplyBase',
                activeTokenKey: 'activeBaseTokenId',
                closeEditor: closeBaseEditor,
                toast: 'Basis eingesetzt',
                composeDuringSelection: true,
                composeOnApply: false
            });

            bindArticleChoiceEditorHandlers({
                articleContainer: '#inlineItemArticleOptions',
                optionsContainer: '#inlineItemOptions',
                optionButtonClass: '.inline-item-opt',
                articleStateKey: 'itemArticleValue',
                valueStateKey: 'itemValue',
                render: renderInlineItemOptions,
                applyButton: '#btnApplyItem',
                activeTokenKey: 'activeItemTokenId',
                closeEditor: closeItemEditor,
                toast: 'Item eingesetzt',
                composeDuringSelection: false,
                composeOnApply: true
            });

            bindArticleChoiceEditorHandlers({
                articleContainer: '#inlineBalanceArticleOptions',
                optionsContainer: '#inlineBalanceOptions',
                optionButtonClass: '.inline-balance-opt',
                articleStateKey: 'balanceArticleValue',
                valueStateKey: 'balanceValue',
                render: renderInlineBalanceOptions,
                applyButton: '#btnApplyBalance',
                activeTokenKey: 'activeBalanceTokenId',
                closeEditor: closeBalanceEditor,
                toast: 'Balance eingesetzt',
                composeDuringSelection: false,
                composeOnApply: true
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

            $('#recipeForm').on('click', '.ingredient-db-row', function (e) {
                if ($(e.target).closest('button, input, select, label').length) return;
                const details = $(this).find('.ingredient-db-details').first();
                if (!details.length) return;
                const shouldOpen = details.hasClass('d-none');
                $('.ingredient-db-details').addClass('d-none');
                if (shouldOpen) {
                    details.removeClass('d-none');
                }
            });

            $('#btnCloseIngredientConfig, #btnCancelIngredientConfig').on('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
            });

            $('#btnApplyIngredientConfig').on('click', function () {
                applyIngredientConfigPopup();
            });

            /* ===== OLD PROBABILITY EDITOR HANDLERS - AUSKOMMENTIERT 2026-03-27 =====
             * Diese Handler sind für das alte #probVarEditorDock System.
             * Jetzt wird das Universal Overlay System verwendet.
             * Kann gelöscht werden, wenn alles funktioniert.
             */

            // Close variable editor popups on layout change (resize/orientation)
            // window.addEventListener('resize', function () {
            //     if ($('#probVarEditorDock').hasClass('ing-active')) closeProbVarEditor();
            // });

            // // === Probability Variable Editor handlers ===
            // $('#btnCloseProbVarEditor, #btnCancelProbVar').on('click', function () {
            //     closeProbVarEditor();
            // });

            // $('#btnApplyProbVar').on('click', function () {
            //     applyProbVarEditor();
            // });

            // $('#probVarEditorOverlay').on('click', function (e) {
            //     if (e.target === this) closeProbVarEditor();
            // });

            // // Ingredient chip - toggle selection in editor (apply via Einsetzen)
            // $('#probVarEditorDock').on('click', '.js-prob-ingredient-chip', function () {
            //     const id = ($(this).data('id') || '').toString();
            //     if (!id) return;

            //     const selected = (creatorState.selectedIngredientIds || []).map(x => x.toString());
            //     if (selected.includes(id)) {
            //         creatorState.selectedIngredientIds = selected.filter(x => x !== id);
            //     } else {
            //         creatorState.selectedIngredientIds = [...selected, id];
            //     }

            //     let ings = typeof getSelectedIngredientsForSandbox === 'function' ? getSelectedIngredientsForSandbox() : [];
            //     const lowerVarKey = (probVarEditorCurrentVarKey || '').toLowerCase();
            //     if (lowerVarKey === 'liquid') ings = ings.filter(i => i.isLiquid);
            //     else if (lowerVarKey === 'fat') ings = ings.filter(i => i.isFat);
            //     else if (lowerVarKey === 'hard') ings = ings.filter(i => i.isHard);
            //     else if (lowerVarKey === 'soft') ings = ings.filter(i => i.isSoft);
            //     const chips = (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.buildIngredientChipsHtml === 'function')
            //         ? window.MasterStepCreatorHelpers.buildIngredientChipsHtml(ings, creatorState.selectedIngredientIds)
            //         : '';
            //     $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewÃ¤hlt</span>');
            // });

            // // Article chip for ingredient variable in prob editor
            // $('#probVarEditorDock').on('click', '.js-prob-ingredient-article-chip', function () {
            //     const val = ($(this).data('value') || '').toString();
            //     probVarIngredientArticleValue = val;
            //     $('.js-prob-ingredient-article-chip').removeClass('active btn-light text-dark').addClass('btn-outline-light');
            //     $(this).addClass('active btn-light text-dark').removeClass('btn-outline-light');
            // });

            // // Option chip → auto-apply and close
            // $('#probVarEditorDock').on('click', '.js-prob-option-chip', function () {
            //     const value = ($(this).data('value') || $(this).text()).toString();
            //     if (probVarEditorCallback) probVarEditorCallback(value);
            //     closeProbVarEditor();
            // });

            // // Pronoun chip → select and apply
            // $('#probVarEditorDock').on('click', '.js-prob-pronoun-chip', function () {
            //     const value = ($(this).data('value') || $(this).text()).toString();
            //     if (probVarEditorCallback) probVarEditorCallback(value);
            //     closeProbVarEditor();
            // });

            // // State chip → select and apply
            // $('#probVarEditorDock').on('click', '.js-prob-state-chip', function () {
            //     const value = ($(this).data('value') || $(this).text()).toString();
            //     if (probVarEditorCallback) probVarEditorCallback(value);
            //     closeProbVarEditor();
            // });

            /* ===== END OLD HANDLERS ===== */

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
                prioritizeSelectedIngredient(id);
                if (creatorState.activePlaceholderTokenId && creatorState.ingredientReplaceArmed) {
                    const key = ($('#sc2MasterPreviewText .placeholder-token[data-placeholder-token-id="' + creatorState.activePlaceholderTokenId + '"]').data('placeholder-key') || '').toString();
                    if (getPlaceholderType(key) === 'ingredient' && assignIngredientPlaceholderValue(creatorState.activePlaceholderTokenId, id)) {
                        showSc2CreatorToast('Platzhalter ersetzt');
                    }
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
                    console.log('[SC2 Token Click]', { key, tokenId });
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
                // Sequential editing DISABLED: User can fill pronoun and state independently
                // const pendingSc2StateId = creatorState.pendingSc2StateTokenId || '';
                // creatorState.pendingSc2StateTokenId = '';
                // setTimeout(function () {
                //     if (pendingSc2StateId) {
                //         openSc2StateEditorForToken(pendingSc2StateId);
                //     } else {
                //         const sc2StateTokenEl = document.querySelector('#sc2MasterPreviewText .placeholder-token[data-placeholder-key="state"]');
                //         if (sc2StateTokenEl) {
                //             const sc2StateTokenId = sc2StateTokenEl.dataset.placeholderTokenId;
                //             if (!(creatorState.placeholderAssignments[sc2StateTokenId] || '').toString().trim()) {
                //                 openSc2StateEditorForToken(sc2StateTokenId);
                //             }
                //         }
                //     }
                // }, 50);
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
            bindArticleChoiceEditorHandlers({
                articleContainer: '#sc2InlineEquipmentArticleOptions',
                optionsContainer: '#sc2InlineEquipmentOptions',
                optionButtonClass: '.inline-equipment-opt',
                articleStateKey: 'equipmentArticleValue',
                valueStateKey: 'equipmentValue',
                render: function () {
                    renderArticleChoiceOptions({
                        optionsSelector: '#sc2InlineEquipmentOptions',
                        articleSelector: '#sc2InlineEquipmentArticleOptions',
                        options: getEquipmentOptions(),
                        selectedArticle: creatorState.equipmentArticleValue,
                        currentValue: creatorState.equipmentValue,
                        optionClass: 'inline-equipment-opt'
                    });
                },
                applyButton: '#sc2BtnApplyEquipment',
                activeTokenKey: 'activeEquipmentTokenId',
                closeEditor: closeSc2EquipmentEditor,
                toast: 'Tool eingesetzt',
                toastFn: showSc2CreatorToast,
                composeDuringSelection: false,
                composeOnApply: true
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
            bindArticleChoiceEditorHandlers({
                articleContainer: '#sc2InlineToolArticleOptions',
                optionsContainer: '#sc2InlineToolOptions',
                optionButtonClass: '.inline-tool-opt',
                articleStateKey: 'toolArticleValue',
                valueStateKey: 'toolValue',
                render: function () {
                    renderArticleChoiceOptions({
                        optionsSelector: '#sc2InlineToolOptions',
                        articleSelector: '#sc2InlineToolArticleOptions',
                        options: getToolOptions(),
                        selectedArticle: creatorState.toolArticleValue,
                        currentValue: creatorState.toolValue,
                        optionClass: 'inline-tool-opt'
                    });
                },
                applyButton: '#sc2BtnApplyTool',
                activeTokenKey: 'activeToolTokenId',
                closeEditor: closeSc2ToolEditor,
                toast: 'Werkzeug eingesetzt',
                toastFn: showSc2CreatorToast,
                composeDuringSelection: false,
                composeOnApply: true
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
            bindArticleChoiceEditorHandlers({
                articleContainer: '#sc2InlineBaseArticleOptions',
                optionsContainer: '#sc2InlineBaseOptions',
                optionButtonClass: '.inline-base-opt',
                articleStateKey: 'baseArticleValue',
                valueStateKey: 'baseValue',
                render: function () {
                    renderArticleChoiceOptions({
                        optionsSelector: '#sc2InlineBaseOptions',
                        articleSelector: '#sc2InlineBaseArticleOptions',
                        options: getBaseOptions(),
                        selectedArticle: creatorState.baseArticleValue,
                        currentValue: creatorState.baseValue,
                        optionClass: 'inline-base-opt'
                    });
                },
                applyButton: '#sc2BtnApplyBase',
                activeTokenKey: 'activeBaseTokenId',
                closeEditor: closeSc2BaseEditor,
                toast: 'Basis eingesetzt',
                toastFn: showSc2CreatorToast,
                composeDuringSelection: false,
                composeOnApply: true
            });

            // SC2 Item
            bindArticleChoiceEditorHandlers({
                articleContainer: '#sc2InlineItemArticleOptions',
                optionsContainer: '#sc2InlineItemOptions',
                optionButtonClass: '.inline-item-opt',
                articleStateKey: 'itemArticleValue',
                valueStateKey: 'itemValue',
                render: function () {
                    renderArticleChoiceOptions({
                        optionsSelector: '#sc2InlineItemOptions',
                        articleSelector: '#sc2InlineItemArticleOptions',
                        options: getItemOptions(),
                        selectedArticle: creatorState.itemArticleValue,
                        currentValue: creatorState.itemValue,
                        optionClass: 'inline-item-opt'
                    });
                },
                applyButton: '#sc2BtnApplyItem',
                activeTokenKey: 'activeItemTokenId',
                closeEditor: closeSc2ItemEditor,
                toast: 'Item eingesetzt',
                toastFn: showSc2CreatorToast,
                composeDuringSelection: false,
                composeOnApply: true
            });

            // SC2 Balance
            bindArticleChoiceEditorHandlers({
                articleContainer: '#sc2InlineBalanceArticleOptions',
                optionsContainer: '#sc2InlineBalanceOptions',
                optionButtonClass: '.inline-balance-opt',
                articleStateKey: 'balanceArticleValue',
                valueStateKey: 'balanceValue',
                render: function () {
                    renderArticleChoiceOptions({
                        optionsSelector: '#sc2InlineBalanceOptions',
                        articleSelector: '#sc2InlineBalanceArticleOptions',
                        options: getBalanceOptions(),
                        selectedArticle: creatorState.balanceArticleValue,
                        currentValue: creatorState.balanceValue,
                        optionClass: 'inline-balance-opt'
                    });
                },
                applyButton: '#sc2BtnApplyBalance',
                activeTokenKey: 'activeBalanceTokenId',
                closeEditor: closeSc2BalanceEditor,
                toast: 'Balance eingesetzt',
                toastFn: showSc2CreatorToast,
                composeDuringSelection: false,
                composeOnApply: true
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

            // Page fully ready â†’ hide overlay, show content
            $('#datatableLoadingOverlay').addClass('d-none');
            $('.creator-topbar, .feed-shell').css('visibility', 'visible');
        });




