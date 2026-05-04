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
        const draftEngine = window.CreatePostingTemplateDrafts || null;

        // ── Performance: Generation-Counter Cache für Sandbox-Ingredients ──
        let _sandboxGeneration = 0;
        const _sandboxCache = {};  // { generation, lang, result }

        // ── Performance: Debounce für syncIngredientSourceVisibility ──
        let _syncVisibilityTimer = null;

        // ── Performance: Animation-Suppress für Batch-Operationen ──
        let _suppressPreviewAnimation = false;

        // ── Performance: Dirty-Flag-System mit requestAnimationFrame ──
        const _dirtyFlags = {
            stepIndices: false,
            derivedVisuals: false,
            ingredientChips: false,
            sourceVisibility: false,
            keywordStates: false,
            languageLabels: false,
            masterTemplateBuilder: false,
            probabilityHints: false
        };
        let _rafScheduled = false;

        function invalidateSandboxCache() {
            _sandboxGeneration++;
        }

        function markDirty(/* ...flags */) {
            for (let i = 0; i < arguments.length; i++) {
                if (_dirtyFlags.hasOwnProperty(arguments[i])) {
                    _dirtyFlags[arguments[i]] = true;
                }
            }
            if (!_rafScheduled) {
                _rafScheduled = true;
                requestAnimationFrame(_executeUIRefresh);
            }
        }

        function _executeUIRefresh() {
            _rafScheduled = false;
            if (_dirtyFlags.stepIndices)          { _dirtyFlags.stepIndices = false;          updateStepIndices(); }
            if (_dirtyFlags.derivedVisuals)        { _dirtyFlags.derivedVisuals = false;       applyDerivedIngredientRowVisuals(); }
            if (_dirtyFlags.ingredientChips)        { _dirtyFlags.ingredientChips = false;      renderIngredientChips(); }
            if (_dirtyFlags.sourceVisibility)       { _dirtyFlags.sourceVisibility = false;     syncIngredientSourceVisibility(); }
            if (_dirtyFlags.keywordStates)          { _dirtyFlags.keywordStates = false;        syncSelectedKeywordButtonStates(); }
            if (_dirtyFlags.languageLabels)         { _dirtyFlags.languageLabels = false;       updateLanguageLabels(); }
            if (_dirtyFlags.masterTemplateBuilder)  { _dirtyFlags.masterTemplateBuilder = false; refreshMasterTemplateBuilder(); }
            if (_dirtyFlags.probabilityHints)       { _dirtyFlags.probabilityHints = false;     refreshIngredientProbabilityHints(); }
        }

        function flushUIRefresh() {
            if (_rafScheduled) {
                _rafScheduled = false;
                _executeUIRefresh();
            }
        }

        // Probability States: Object-based data model (wie Smart Step Creator)
        const probabilityStates = {};  // Dictionary: masterId â†’ { masterId, templateRaw, values, _multiIngredients }
        window.probabilityStates = probabilityStates;  // Export for use in CreatePostingSmartStepCreator.js

        if (draftEngine && typeof draftEngine.getProbabilityStates === 'function') {
            Object.assign(probabilityStates, draftEngine.getProbabilityStates());
            window.probabilityStates = draftEngine.getProbabilityStates();
        }

        function ensureProbabilityDraftState(masterId) {
            const safeMasterId = (masterId || '').toString().trim();
            if (!safeMasterId) return null;

            if (draftEngine && typeof draftEngine.ensureProbabilityDraft === 'function') {
                const draft = draftEngine.ensureProbabilityDraft(safeMasterId, function () {
                    const template = MasterStepRenderer.findTemplate(safeMasterId);
                    const activeLang = $('html').attr('lang') || 'de';
                    const templateRaw = template?.templates?.[activeLang] || template?.templates?.de || '';
                    const currentTypeId = creatorState?.activeRecipeType || '';
                    const defaultVars = buildVariablesForTemplate(safeMasterId, currentTypeId);
                    return {
                        masterId: safeMasterId,
                        templateRaw: templateRaw,
                        values: defaultVars,
                        _multiIngredients: {},
                        _explicitValues: {}
                    };
                });
                probabilityStates[safeMasterId] = draft;
                return draft;
            }

            if (!probabilityStates[safeMasterId]) {
                const template = MasterStepRenderer.findTemplate(safeMasterId);
                const activeLang = $('html').attr('lang') || 'de';
                const templateRaw = template?.templates?.[activeLang] || template?.templates?.de || '';
                const currentTypeId = creatorState?.activeRecipeType || '';
                const defaultVars = buildVariablesForTemplate(safeMasterId, currentTypeId);
                probabilityStates[safeMasterId] = {
                    masterId: safeMasterId,
                    templateRaw: templateRaw,
                    values: defaultVars,
                    _multiIngredients: {},
                    _explicitValues: {}
                };
            }

            return probabilityStates[safeMasterId];
        }

        function buildProbabilityPreviewDraft(masterId) {
            const safeMasterId = (masterId || '').toString().trim();
            if (!safeMasterId) return null;

            const draft = ensureProbabilityDraftState(safeMasterId);
            if (!draft) return null;

            const resolvedLang = resolveLangKey(currentLang || 'de');
            const masterTemplate = MasterStepRenderer.findTemplate(safeMasterId);
            const templateRaw = (
                masterTemplate?.templates?.[resolvedLang] ||
                masterTemplate?.templates?.de ||
                draft.templateRaw ||
                ''
            ).toString();
            const defaultVars = buildVariablesForTemplate(safeMasterId, creatorState?.activeRecipeType || '');

            return {
                masterId: safeMasterId,
                templateRaw,
                values: {
                    ...(defaultVars || {}),
                    ...(draft.values || {})
                },
                _multiIngredients: draft._multiIngredients || {}
            };
        }

        window.ensureCreatePostingProbabilityDraftState = ensureProbabilityDraftState;
        window.buildCreatePostingProbabilityPreviewDraft = buildProbabilityPreviewDraft;

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

                invalidateSandboxCache();
                _suppressPreviewAnimation = true;
                markDirty('stepIndices', 'derivedVisuals', 'ingredientChips', 'sourceVisibility',
                          'keywordStates', 'languageLabels', 'masterTemplateBuilder', 'probabilityHints');
                flushUIRefresh();
                _suppressPreviewAnimation = false;
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
            { de: 'BlÃƒÂ¤tter', en: 'leaves', esp: 'hojas', prt: 'folhas' },
            { de: 'Handvoll', en: 'handful', esp: 'puÃƒÂ±ado', prt: 'punhado' },
            { de: 'cm', en: 'cm', esp: 'cm', prt: 'cm' },
            { de: 'Zweig', en: 'sprig', esp: 'rama', prt: 'ramo' },
            { de: 'ml', en: 'ml', esp: 'ml', prt: 'ml' },
            { de: 'Gekocht', en: 'cooked', esp: 'cocido', prt: 'cozido' },
            { de: 'Portionen', en: 'portions', esp: 'porciones', prt: 'porcoes' },
            { de: 'l', en: 'l', esp: 'l', prt: 'l' },
            { de: 'Glas', en: 'glass', esp: 'vaso', prt: 'copo' },
            { de: 'kcal', en: 'kcal', esp: 'kcal', prt: 'kcal' },
            { de: 'mg', en: 'mg', esp: 'mg', prt: 'mg' },
            { de: 'Ã‚Âµg', en: 'ug', esp: 'ug', prt: 'ug' }
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

                    // âœ… NEU: Wenn Ei bearbeitet wird, aktualisiere auch Eiklar/Eigelb Mengen
                    $('#selectedIngredients .ingredient-row[data-derived-row="true"]').each(function() {
                        const derivedRow = $(this);
                        const sourceBaseId = derivedRow.attr('data-derived-source-base-id');
                        if (sourceBaseId === ingredientId) {
                            derivedRow.find('.ingredient-qty-hidden').val(qty);
                            derivedRow.find('.ingredient-unit-hidden').val(unitDe);
                            derivedRow.find('.ingredient-row-meta').text(`${qty} ${unitLabel}`.trim());
                        }
                    });
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
         * Diese Funktionen sind fÃ¼r das alte #probVarEditorDock System.
         * Jetzt wird openProbVarInlineEditor und das Universal Overlay verwendet.
         * Kann gelÃ¶scht werden, wenn alles funktioniert.
         */

        /*
        function openProbVarEditor(masterId, varKey, currentVal, onApply, anchorElement) {
            probVarEditorCallback = onApply;
            probVarEditorAnchor = anchorElement ? $(anchorElement).closest('.probability-template-wrap') : null;
            probVarEditorCurrentVarKey = (varKey || '').toString();
            const cleanKey = (varKey || '').toLowerCase().replace(/_/g, ' ');
            $('#probVarEditorTitle').text((cleanKey || 'Wert') + ' auswÃƒÂ¤hlen');
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
                $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewÃƒÂ¤hlt</span>');
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
                const isFahr = (currentVal || '').includes('Ã‚Â°F') || (currentVal || '').includes('F');
                $('#probVarTempVal').val(tempNum);
                $('#probVarTempUnit').val(isFahr ? 'fahrenheit' : 'celsius');
                $('#probVarTempArea').removeClass('d-none');
                $('#probVarApplyRow').removeClass('d-none');

            } else {
                // Check if this is pronoun or state Ã¢â€ ' show combined pronoun+state area
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
                    // Options or text fallback Ã¢â€ ' try to get presets from MasterStepRenderer
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
                const unit = $('#probVarTempUnit').val() === 'fahrenheit' ? 'Ã‚Â°F' : 'Ã‚Â°C';
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        function buildProbabilityEditorConfig(masterId, varKey, currentVal, anchorEl, extraContext, callbacks) {
            const helpers = window.MasterStepCreatorHelpers;
            if (!helpers || typeof helpers.openStepDraftEditor !== 'function') {
                console.error("[Probability Editor] Unified system not available!");
                return null;
            }

            const escapedMasterId = (window.CSS && typeof window.CSS.escape === 'function')
                ? CSS.escape(masterId)
                : String(masterId || '').replace(/"/g, '\\"');
            const $anchor = $(`.probability-template-wrap[data-master-id="${escapedMasterId}"]`).first();
            const templateCard = $anchor.find('.probability-template-card')[0];
            const templatePreviewHtml = templateCard ? templateCard.innerHTML : '';

            return {
                helpers,
                source: 'suggested',
                varName: varKey,
                currentVal: currentVal || '',
                masterId: masterId,
                context: Object.assign({
                    type: 'probability',
                    source: 'suggested',
                    probabilityMasterId: masterId,
                    masterId: masterId,
                    tokenElement: anchorEl,
                    templatePreviewHtml: templatePreviewHtml
                }, extraContext || {}),
                onApply: callbacks && callbacks.onApply,
                onClose: callbacks && callbacks.onClose
            };
        }

        // WRAPPER: openProbVarInlineEditor â†’ Uses Unified Overlay System
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // This function is now a thin wrapper around the unified overlay system.
        // All existing calls to openProbVarInlineEditor automatically use the unified system.
        function openProbVarInlineEditor(masterId, varKey, currentVal, onApply, anchorEl, onApplyExtras, flowMode, carriedPronoun) {
            const config = buildProbabilityEditorConfig(
                masterId,
                varKey,
                currentVal,
                anchorEl,
                {
                    flowMode: flowMode,
                    carriedPronoun: carriedPronoun
                },
                {
                    onApply: function (newVal, extras) {
                        if (typeof onApply === 'function') {
                            onApply(newVal);
                        }
                        if (extras && typeof onApplyExtras === 'function') {
                            onApplyExtras(extras);
                        }
                    }
                }
            );

            if (!config) {
                return;
            }

            config.helpers.openStepDraftEditor({
                source: config.source,
                varName: config.varName,
                currentVal: config.currentVal,
                masterId: config.masterId,
                context: config.context,
                onApply: config.onApply,
                onClose: config.onClose
            });
        }

        function normalizeGenusKey(genusRaw = '') {
            const value = (genusRaw || '').toString().trim().toLowerCase();
            if (!value) return '';
            if (['pl', 'plural', 'die(pl)', 'die plur', 'pluralis'].some(x => value === x || value.includes(x))) return 'plural';
            if (['m', 'masc', 'mask', 'masculine', 'masculin', 'der', 'el', 'o', 'de', 'en', 'common'].some(x => value === x || value.includes(x))) return 'masc';
            if (['f', 'fem', 'feminine', 'femin', 'die', 'la', 'a', 'ei'].some(x => value === x || value.includes(x))) return 'fem';
            if (['n', 'neut', 'neuter', 'das', 'het', 'det', 'ett', 'et'].some(x => value === x || value.includes(x))) return 'neut';
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
                3: { text: 'WÃƒÂ¼rzen', cls: 'phase-badge-3' },
                4: { text: 'Finish', cls: 'phase-badge-4' }
            };
            const info = map[phase];
            if (!info) return '';
            return `<span class="phase-badge ${info.cls}">${info.text}</span>`;
        }


        // Hilfsfunktion: Gibt die Zutat-IDs zurÃƒÂ¼ck, an die ein Step gebunden ist
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
                    isPeelable: row.data('is-peelable') === true || row.data('is-peelable') === 'true',
                    isCuttable: row.data('is-cuttable') === true || row.data('is-cuttable') === 'true',
                    isGrateable: row.data('is-grateable') === true || row.data('is-grateable') === 'true',
                    isFryable: row.data('is-fryable') === true || row.data('is-fryable') === 'true',
                    isRoastable: row.data('is-roastable') === true || row.data('is-roastable') === 'true',
                    isGrillable: row.data('is-grillable') === true || row.data('is-grillable') === 'true',
                    isSteamable: row.data('is-steamable') === true || row.data('is-steamable') === 'true',
                    isBoilable: row.data('is-boilable') === true || row.data('is-boilable') === 'true',
                    isSearable: row.data('is-searable') === true || row.data('is-searable') === 'true',
                    isPoachable: row.data('is-poachable') === true || row.data('is-poachable') === 'true',
                    isSmokable: row.data('is-smokable') === true || row.data('is-smokable') === 'true',
                isFlambeable: row.data('is-flambeable') === true || row.data('is-flambeable') === 'true',
                isBlendable: row.data('is-blendable') === true || row.data('is-blendable') === 'true',
                isPowder: row.data('is-powder') === true || row.data('is-powder') === 'true',
                quantity: (row.find('.ingredient-qty-hidden').val() || '').toString().trim(),
                unitDe: (row.find('.ingredient-unit-hidden').val() || '').toString().trim(),
                sourceBaseId: id
            };
        }).get().filter(x => x.id || x.name);
        }

        function buildDerivedIngredientName(baseName, transformType, langKey, genusRaw, article) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const noun = (baseName || '').toString().trim();
            if (!noun || !ingredientTransforms) return noun;

            const pattern = ingredientTransforms.adjective_patterns?.[transformType]?.patterns?.[lang];
            if (!pattern) return noun;

            let template;
            if (typeof pattern === 'string') {
                template = pattern;
            } else {
                // Wenn ein Artikel übergeben wird und Sprache Deutsch: Endung vom Artikel ableiten
                const art = (article || '').toString().trim().toLowerCase();
                if (lang === 'de' && art && art !== 'ohne' && pattern.m) {
                    // Deutsche Adjektiv-Deklination mit bestimmtem Artikel:
                    // die/das → -e, der/den/dem/des → -en
                    const ending = (art === 'die' || art === 'das') ? 'e' : 'en';
                    // Basis-Adjektiv aus maskulin-Form extrahieren (z.B. "geschnittener" → "geschnitten")
                    const mTemplate = pattern.m; // z.B. "geschnittener {{noun}}"
                    const adjMatch = mTemplate.match(/^(.*?)(er)\s*\{\{noun\}\}$/);
                    if (adjMatch) {
                        template = adjMatch[1] + ending + ' {{noun}}';
                    } else {
                        // Komplexere Muster wie "in Scheiben geschnittener {{noun}}"
                        const complexMatch = mTemplate.match(/^(.*?)(er)\s+(\{\{noun\}\})$/);
                        if (complexMatch) {
                            template = complexMatch[1] + ending + ' ' + complexMatch[3];
                        } else {
                            // Fallback auf Genus-basiert
                            const genusKey = getGenusKey(noun, genusRaw, lang);
                            template = pattern[genusKey] || pattern.m || Object.values(pattern)[0];
                        }
                    }
                } else {
                    const genusKey = getGenusKey(noun, genusRaw, lang);
                    template = pattern[genusKey] || pattern.m || Object.values(pattern)[0];
                }
            }

            return template.replace('{{noun}}', noun);
        }

        function getGenusKey(noun, genusRaw, lang) {
            let normalizedGenus = normalizeGenusKey(genusRaw);
            if (!normalizedGenus && lang === 'de') {
                const lower = (noun || '').toLowerCase();
                const femWords = ['tomate', 'zwiebel', 'paprika', 'karotte', 'kartoffel', 'schulter', 'brust', 'keule', 'zehe', 'soße', 'sauce', 'butter', 'sahne', 'milch', 'gurke', 'birne', 'kirsche', 'pflaume', 'bohne', 'erbse', 'linse', 'nudel', 'nuss'];
                const neutWords = ['salz', 'öl', 'wasser', 'ei', 'mehl', 'fleisch', 'brot', 'gemüse', 'kraut', 'pulver'];
                if (femWords.some(w => lower.includes(w))) normalizedGenus = 'fem';
                else if (neutWords.some(w => lower.includes(w))) normalizedGenus = 'neut';
                else normalizedGenus = 'masc';
            }
            return { 'masc': 'm', 'fem': 'f', 'neut': 'n' }[normalizedGenus] || 'm';
        }

        // Passt die Adjektiv-Endung eines abgeleiteten Zutatennamen an den gewählten Artikel an.
        // z.B. "geschnittene Zwiebel" + "den" → "geschnittenen Zwiebel"
        //      "gewürfelter Knoblauch" + "den" → "gewürfelten Knoblauch"
        //      "in Scheiben geschnittener Lauch" + "den" → "in Scheiben geschnittenen Lauch"
        function adjustAdjectiveEndingForArticle(derivedName, article, lang) {
            if (!derivedName || !article || lang !== 'de') return derivedName;
            const art = article.toLowerCase().trim();
            if (!art || art === 'ohne') return derivedName;

            // Bestimmte Artikel → schwache Deklination: die/das → -e, der/den/dem/des → -en
            const ending = (art === 'die' || art === 'das') ? 'e' : 'en';

            // Finde das letzte Adjektiv vor dem Nomen (= letztes Wort mit -er/-e/-es/-en Endung vor einem Großbuchstaben-Wort)
            // z.B. "in Scheiben geschnittener Lauch" → "geschnittener" ist das Adjektiv
            // z.B. "gehackte Zwiebel" → "gehackte" ist das Adjektiv
            const words = derivedName.split(' ');
            for (let i = words.length - 2; i >= 0; i--) {
                const word = words[i];
                // Adjektiv erkennen: kleingeschrieben, endet auf -er/-e/-es/-en, nächstes Wort ist Nomen (großgeschrieben)
                const nextWord = words[i + 1];
                if (word && nextWord && /^[a-zäöüß]/.test(word) && /^[A-ZÄÖÜ]/.test(nextWord)) {
                    const adjMatch = word.match(/^(.+?)(er|es|en|e)$/);
                    if (adjMatch) {
                        words[i] = adjMatch[1] + ending;
                        return words.join(' ');
                    }
                }
            }
            return derivedName;
        }

        // Export für SmartStepCreator
        window.adjustAdjectiveEndingForArticle = adjustAdjectiveEndingForArticle;

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
                    if (!(def.trigger_steps?.includes('PREP_CUT_01') || def.trigger_steps?.includes('PREP_SCORE_01')) || !def.shape_match) continue;
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
                    quantity: item.quantity || '',
                    unitDe: item.unitDe || '',
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

            if (!ingredientTransforms) {
                return items;
            }

            // auto_show transforms FIRST: show sub-ingredients (e.g. Ei â†’ Eiklar/Eigelb)
            const triggeredKeys = new Set(descriptors.map(d => d?.masterId).filter(Boolean));

            (ingredientTransforms.special_transforms || []).forEach(function (t) {
                if (!t.auto_show) return;
                if (triggeredKeys.has(t.trigger_step)) return;
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

                // 1. Special transforms prÃ¼fen
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
                        const replacements = buildSpecialTransformItems(targets[0], specialMatch);
                        items = items.filter(function (i) { return !targetIds.has(i.id); }).concat(replacements);
                    }
                    return;
                }

                // 2. Adjective transforms prÃ¼fen
                const adjEntry = Object.entries(ingredientTransforms.adjective_patterns || {})
                    .find(function (entry) {
                        var def = entry[1];
                        if (!def.trigger_steps || !def.trigger_steps.includes(masterId)) return false;
                        if ((masterId === 'PREP_CUT_01' || masterId === 'PREP_SCORE_01') && def.shape_match) {
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
                        quantity: item.quantity || '',
                        unitDe: item.unitDe || '',
                        sourceBaseId: item.sourceBaseId || item.id || ''
                    };
                });

                items = items.filter(function (item) { return !matchedIds.has(item.id); }).concat(replacements);
            });

            // âœ… Duplikate entfernen (gleicher Name) - bevorzuge Items mit Icon
            const seenByName = new Map();
            items.forEach(function (item) {
                const key = (item.namesByLang?.[lang] || item.name || '').toLowerCase();
                const existing = seenByName.get(key);
                if (!existing || (item.iconHtml && !existing.iconHtml)) {
                    seenByName.set(key, item);
                }
            });
            items = Array.from(seenByName.values());

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

            // Performance: Return cached result if generation hasn't changed
            if (_sandboxCache.generation === _sandboxGeneration && _sandboxCache.lang === lang && _sandboxCache.result) {
                return _sandboxCache.result;
            }

            const derived = deriveSandboxIngredients(getBaseSandboxIngredients());
            const result = derived.map(item => ({
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
                isPeelable: !!item.isPeelable,
                isCuttable: !!item.isCuttable,
                isGrateable: !!item.isGrateable,
                isFryable: !!item.isFryable,
                isRoastable: !!item.isRoastable,
                isGrillable: !!item.isGrillable,
                isSteamable: !!item.isSteamable,
                isBoilable: !!item.isBoilable,
                isSearable: !!item.isSearable,
                isPoachable: !!item.isPoachable,
                isSmokable: !!item.isSmokable,
                isFlambeable: !!item.isFlambeable,
                isBlendable: !!item.isBlendable,
                isPowder: !!item.isPowder,
                quantity: (item.quantity || '').toString().trim(),
                unitDe: (item.unitDe || '').toString().trim(),
                sourceBaseId: item.sourceBaseId || item.id || ''
            }));

            // Performance: Cache the result
            _sandboxCache.generation = _sandboxGeneration;
            _sandboxCache.lang = lang;
            _sandboxCache.result = result;
            return result;
        }

        function localizeIngredientValueForSandbox(rawValue, targetLang, sourceLang = currentLang) {
            const value = (rawValue || '').toString().trim();
            if (!value) return '';

            // âœ… FIX (2026-04-02): If source and target languages are the same, return original value
            // to preserve articles and formatting
            if (resolveLangKey(sourceLang) === resolveLangKey(targetLang)) {
                return value;
            }

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

        function buildTransformedIngredientDisplayRow(sourceRow, transformedItem) {
            const qty = (sourceRow.find('.ingredient-qty-hidden').val() || '').toString();
            const unitDe = (sourceRow.find('.ingredient-unit-hidden').val() || '').toString();
            const unitObj = findUnitByDe(unitDe);
            const unitLabel = getUnitLabel(unitObj, currentLang) || unitDe;
            const displayName = transformedItem.namesByLang?.[resolveLangKey(currentLang)] || transformedItem.name || '';
            const icon = transformedItem.iconHtml || sourceRow.data('group-icon') || '';

            return `<div class="dynamic-item ingredient-row shadow-sm ingredient-transformed-display"
                         data-transformed-from="${escapeAttr(transformedItem.sourceBaseId || '')}"
                         data-transformed-key="${escapeAttr(transformedItem.id || '')}"
                         style="opacity: 0.85; pointer-events: none;">
                <div class="ingredient-row-main">
                    <div class="fw-bold display-name-selected d-flex align-items-center gap-2">
                        <span class="ingredient-group-icon">${icon}</span>
                        <span class="ingredient-name-text">${escapeAttr(displayName)}</span>
                        <span class="badge bg-secondary" style="font-size: 0.7em;">Transformiert</span>
                    </div>
                </div>
                <div class="ingredient-row-right">
                    <div class="ingredient-row-meta">${escapeAttr(qty)} ${escapeAttr(unitLabel)}</div>
                </div>
            </div>`;
        }

        function applyDerivedIngredientRowVisuals() {
            const selectedWrap = $('#selectedIngredients');
            if (!selectedWrap.length) return;

            const baseItems = getBaseSandboxIngredients();
            const derivedItems = deriveSandboxIngredients(baseItems);

            // Gruppiere derived items nach sourceBaseId
            const derivedBySourceId = derivedItems.reduce((map, item) => {
                const sourceId = (item.sourceBaseId || item.id || '').toString();
                if (!sourceId) return map;
                if (!map[sourceId]) map[sourceId] = [];
                map[sourceId].push(item);
                return map;
            }, {});

            // Entferne alte abgeleitete Rows
            selectedWrap.find('.ingredient-row[data-derived-row="true"], .ingredient-transformed-display').remove();

            // Entferne Grau-Markierung von allen Rows
            selectedWrap.find('.ingredient-row').removeClass('ingredient-used-in-transform').css('opacity', '');

            // Finde alle verwendeten Zutaten-IDs (die transformiert wurden)
            const usedIngredientIds = new Set();
            derivedItems.forEach(item => {
                // Nur tatsÃ¤chlich transformierte Items (keine auto-show)
                if (item.sourceBaseId && item.id !== item.sourceBaseId && !item.isAutoShowOptional) {
                    usedIngredientIds.add(item.sourceBaseId);
                }
            });

            selectedWrap.find('.ingredient-row').filter(function () {
                return !$(this).hasClass('ingredient-transformed-display');
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
                }

                // âœ… NEU: Transformierte Zutaten (z.B. Eischnee) als Display-Only anzeigen
                const transformedItems = regularItems.filter(item =>
                    item.id !== rowId && item.sourceBaseId === rowId && !item.isAutoShowOptional
                );

                if (transformedItems.length > 0) {
                    // Original-Zutat ausgrauen (wurde verwendet)
                    row.addClass('ingredient-used-in-transform').css('opacity', '0.5');

                    // Display-Only Rows fÃ¼r transformierte Zutaten hinzufÃ¼gen
                    transformedItems.forEach(function (transformedItem) {
                        const displayHtml = buildTransformedIngredientDisplayRow(row, transformedItem);
                        row.after(displayHtml);
                    });
                }

                // âœ… NEU: Basis-Zutat (z.B. Ei) ausgrauen, wenn eines ihrer Sub-Items verwendet wurde
                const hasUsedSubItem = names.some(item => usedIngredientIds.has(item.id));
                if (hasUsedSubItem) {
                    row.addClass('ingredient-used-in-transform').css('opacity', '0.5');
                }
            });
        }

        const createPostingThemeStorageKey = 'createPostingTheme';
        const profileThemeStorageKey = 'profile_theme';
        const availableCreatePostingThemes = ['color', 'black', 'white', 'rose', 'lavender'];
        const createPostingThemeMap = {
            color: 'navy',
            black: 'navy',
            white: 'navy',
            rose: 'navy',
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
            return 'text-white-50';
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
            lastEquipmentValue: '',
            lastEquipmentArticleValue: '',
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
        const suggestMissingIngredientUrl = window.CreatePostingPageConfig?.suggestMissingIngredientUrl || '/WorldMiniApp/Home/SuggestMissingIngredient';
        const saveSuggestedIngredientUrl = window.CreatePostingPageConfig?.saveSuggestedIngredientUrl || '/WorldMiniApp/Home/SaveSuggestedIngredient';
        let missingIngredientCurrentSuggestion = null;



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
                    f: ['tomate', 'zwiebel', 'paprika', 'karotte', 'kartoffel', 'schulter', 'brust', 'keule', 'soÃƒÅ¸e', 'sauce'],
                    n: ['salz', 'ÃƒÂ¶l', 'wasser', 'ei', 'mehl', 'fleisch', 'brot']
                };
                if (hasAny(['pl', 'plural'])) return { pronoun: 'sie', article: 'die' };
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
                genusByLang: selected.genusByLang || {},
                groupId: selected.groupId || ''
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
            // pronoun: { open: openPronounEditorForToken, toast: 'Pronomen wÃƒÂ¤hlen', keepTokenActive: true },
            temperature: { open: openTemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat: { open: openHeatEditorForToken, toast: 'Hitze-Stufe wÃƒÂ¤hlen' },
            mode: { open: openModeEditorForToken, toast: 'Ofenmodus wÃƒÂ¤hlen' },
            equipment: { open: openEquipmentEditorForToken, toast: 'Tool / GerÃƒÂ¤t wÃƒÂ¤hlen' },
            // state: { open: openStateEditorForToken, toast: 'Zustand wÃƒÂ¤hlen' },
            tool: { open: openToolEditorForToken, toast: 'Tool / GerÃƒÂ¤t wÃƒÂ¤hlen' },
            grindSize: { open: openGrindSizeEditorForToken, toast: 'SchnittgrÃƒÂ¶ÃƒÅ¸e wÃƒÂ¤hlen' },
            shape: { open: openShapeEditorForToken, toast: 'Schnittform wÃƒÂ¤hlen' },
            base: { open: openBaseEditorForToken, toast: 'Basis wÃƒÂ¤hlen' },
            item: { open: openItemEditorForToken, toast: 'Item wÃƒÂ¤hlen' },
            balance: { open: openBalanceEditorForToken, toast: 'Balance wÃƒÂ¤hlen' },
            seasonings: { open: openSeasoningsEditorForToken, toast: 'Seasonings wÃƒÂ¤hlen' }
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
            const unit = creatorState.temperatureUnit === 'fahrenheit' ? 'Ã‚Â°F' : 'Ã‚Â°C';
            return `${safeValue}${unit}`;
        }

        function openTemperatureEditorForToken(tokenId) {
            if (!tokenId) return;
            creatorState.activeTemperatureTokenId = tokenId;
            const currentText = (creatorState.placeholderAssignments[tokenId] || '').toString().trim();
            const parsed = currentText.match(/^(\d+)\s*Ã‚Â°?\s*(C|F)?/i);
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
                    'brÃƒÂ¤ter': 'den',
                    'topf': 'den',
                    'kÃƒÂ¼chenmaschine': 'die',
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
                    'sartÃƒÂ©n': 'la',
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
            creatorState.shapeValue = currentText || 'WÃƒÂ¼rfel';
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
            const selection = buildArticleChoiceSelectionState(currentText, options, { fallback: 'SÃƒÂ¤ure', mode: 'noun' });
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

        function resolveDefaultVariableValueForLanguage(key, langKey) {
            const lang = resolveLangKey(langKey || currentLang || 'de');
            const placeholderType = getPlaceholderType(key);
            if (key === 'ingredient' || key === 'ingredients' || key === 'ingredient2') {
                return '';
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
            if (key === 'extra') return getFirstJsonVariableOption('extra', lang);
            if (key === 'liquid') return (getFirstJsonVariableOption('liquid', lang) || '').replace(/[\u{1F300}-\u{1FAD6}\u{2600}-\u{27BF}]\s*/gu, '').trim();
            const localizedFallback = getLocalizedFallbackForVariable(key, lang);
            return localizedFallback || '';
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

        // Ingredient-Variable-Typen die automatisch gematcht werden können
        const _ingredientVarNames = new Set([
            'ingredient','ingredient2','ingredients','fat','liquid',
            'seasoning','seasonings','base','marinade','thickener','components','extra'
        ]);

        /**
         * Matcht Seitenzutaten automatisch auf eine Ingredient-Variable.
         * Nutzt filterIngredientsByVarType aus SC2 + required_ingredient_tags.
         * Gibt { text, ingredients } zurück — text ist formatiert ("die Kartoffeln und die Zwiebeln"),
         * ingredients ist Array von { name, fraction, article } für Multi-Ingredient-Chips im Overlay.
         */
        function matchIngredientsToVariable(varName, masterId, allIngredients, lang) {
            var empty = { text: '', ingredients: [] };
            var helpers = window.MasterStepCreatorHelpers;
            if (!helpers?.filterIngredientsByVarType || !allIngredients.length) return empty;

            // Einziger Filter: JSON-Regelwerk via ingredient_match_rules.json
            // (variable_rules + step_variable_rules decken Tags, GroupId-Ausschlüsse und
            //  step-spezifische Sonderfälle wie PREP_MINCE_01, FINISH_SPRINKLE_01 ab)
            var result = helpers.filterIngredientsByVarType(allIngredients, varName, masterId);
            if (!result.filtered.length) return empty;
            var matched = result.filtered;

            var displayItems = typeof helpers.selectIngredientsForDisplay === 'function'
                ? helpers.selectIngredientsForDisplay(matched, varName, masterId)
                : matched;
            if (!displayItems.length) {
                displayItems = matched;
            }

            // Ingredient-Objekte für Overlay-Chips aufbauen + Text formatieren
            var ingredientObjects = [];
            var parts = displayItems.map(function (ing) {
                var g = resolveGrammarForIngredient(ing.name, ing.genusByLang || {}, lang);
                ingredientObjects.push({ name: ing.name, fraction: '', article: g.article || '' });
                return applyArticleToName(ing.name, g.article, lang) || ing.name;
            });

            var text;
            if (parts.length === 1) text = parts[0];
            else if (parts.length === 2) text = parts[0] + ' und ' + parts[1];
            else text = parts.slice(0, -1).join(', ') + ' und ' + parts[parts.length - 1];

            return { text: text, ingredients: ingredientObjects };
        }

        function buildVariablesForTemplate(templateId, recipeType) {
            var template = MasterStepRenderer.findTemplate(templateId);
            var allIngredients = getSelectedIngredientsForSandbox();
            var lang = currentLang || 'de';

            var requiredVars = new Set(template?.required_variables || []);
            var keys = new Set([...(template?.variables || []), ...getPlaceholderKeysFromTemplate(template)]);
            var vars = {};

            // Required ingredient-Variablen automatisch vorausfüllen,
            // alles andere leer → renderTemplateWithConfig zeigt Display-Namen als klickbare Tokens
            keys.forEach(function (key) {
                // Hybrid-Variablen (base, extra) → Options-Default statt Ingredient-Matching
                // (z.B. FINISH_SERVE_01: extra → "sofort heiß" statt alle Zutaten aufzulisten)
                // Auch step-spezifisch: prefer_option_default in ingredient_match_rules.json
                // (z.B. COOK_BOIL_01: liquid → "Salzwasser" statt gematchte Flüssig-Zutaten)
                var stepRule = window.MasterStepCreatorHelpers?.getIngredientMatchRule?.(key, templateId);
                var prefersOptionDefault = stepRule?.stepRule?.prefer_option_default === true;
                if (requiredVars.has(key) && (key === 'base' || key === 'extra' || prefersOptionDefault)) {
                    vars[key] = resolveDefaultVariableValueForLanguage(key, lang);
                    if (window._autoMatchedIngredients?.[templateId]) {
                        delete window._autoMatchedIngredients[templateId][key];
                    }
                } else if (requiredVars.has(key) && _ingredientVarNames.has(key)) {
                    var match = matchIngredientsToVariable(key, templateId, allIngredients, lang);
                    vars[key] = match.text;

                    // Gematchte Zutaten in einfachem Global speichern → Overlay nutzt als Fallback
                    if (match.ingredients.length) {
                        if (!window._autoMatchedIngredients) window._autoMatchedIngredients = {};
                        if (!window._autoMatchedIngredients[templateId]) window._autoMatchedIngredients[templateId] = {};
                        window._autoMatchedIngredients[templateId][key] = match.ingredients;
                    }
                } else {
                    // Check for step-specific default from option rules
                    var stepDefault = window.MasterStepRenderer?.getStepDefaultValue?.(templateId, key, lang);
                    vars[key] = stepDefault || '';
                }
            });

            // Pronoun/Article aus erster gematchter oder ausgewählter Zutat
            var selectedIngredient = getSelectedIngredientForCreator();
            var fallbackIng = selectedIngredient || allIngredients[0];
            var grammar = resolveGrammarForIngredient(
                fallbackIng?.name || '',
                fallbackIng?.genusByLang || {},
                lang
            );

            creatorState.computedPronoun = creatorState.advancedPronounOverride || grammar.pronoun;
            creatorState.computedArticle = creatorState.advancedArticleOverride || grammar.article;

            if (!vars.pronoun) vars.pronoun = creatorState.computedPronoun;
            if (!vars.article) vars.article = creatorState.computedArticle;
            return applyTokenAssignmentsToVariables(vars);
        }

        window.createPostingBuildVariablesForTemplate = buildVariablesForTemplate;

        function animatePreview() {
            if (_suppressPreviewAnimation) return;
            const card = $('#masterPreviewCard');
            card.addClass('preview-animate');
            setTimeout(() => card.removeClass('preview-animate'), 250);
        }

        function updatePreviewText() {
            const templateId = getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) {
                creatorState.previewText = 'WÃƒÂ¤hle eine Zutat und ein Template.';
                $('#masterPreviewText').text(creatorState.previewText);
                return;
            }

            const template = MasterStepRenderer.findTemplate(templateId);
            const langKey = (currentLang || 'de').toLowerCase();
            const tpl = template?.templates?.[langKey] || template?.templates?.de || '';
            // Performance: Compute vars once, reuse for SC2 preview
            const vars = buildVariablesForTemplate(templateId);
            const previewHtml = window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate === 'function'
                ? window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate(tpl, vars, creatorState.placeholderAssignments, {
                    activeTokenId: creatorState.activePlaceholderTokenId
                })
                : (tpl || '');

            $('#masterPreviewText').html(previewHtml || 'Keine Vorschau verfÃƒÂ¼gbar.');
            creatorState.previewText = $('#masterPreviewText').text().trim() || 'Keine Vorschau verfÃƒÂ¼gbar.';

            if (!creatorState.ingredientReplaceArmed) { $('#masterPreviewCard').removeClass('token-replace-active'); }
            animatePreview();
            updateSc2PreviewText(templateId, tpl, vars);
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

            if (!ingredients.length) {
                const emptyHtml = `<div class="small ${mutedTextClass}">Keine Zutaten vorhanden.</div>`;
                wrap.html(emptyHtml);
                stepsWrap.html(emptyHtml);
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

            // Performance: Build HTML string once, insert with single .html() call
            const htmlParts = [];
            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const displayName = (item.name || '').toString();
                const iconHtml = (item.iconHtml || '').toString();
                const chipLabel = `${iconHtml ? `${iconHtml} ` : ''}${displayName}`;
                htmlParts.push(`<button type="button" class="ingredient-chip ${active}" draggable="true" data-id="${item.id}" data-name="${displayName}">${chipLabel}</button>`);
            });
            const chipsHtml = htmlParts.join('');
            wrap.html(chipsHtml);
            stepsWrap.html(chipsHtml);
            updatePreviewText();
            renderSc2IngredientChips();
        }

        function renderTemplateCards() {
            return window.CreatePostingTemplateBuilder.renderTemplateCards({
                creatorState,
                buildVariablesForTemplate,
                updatePreviewText,
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
            console.log(`[resolveOptionalSegmentsForPersist] vars.removal:`, vars?.removal);
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
                        const val = (vars && vars[key] != null ? String(vars[key]) : '').trim();
                        console.log(`[resolveOptionalSegments] key="${key}", val="${val}", hasValue=${val.length > 0}`);
                        return val.length > 0;
                    });
                    console.log(`[resolveOptionalSegments] inner="${inner}", hasAnyValue=${hasAnyValue}`);
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

            const defaultVars = buildVariablesForTemplate(templateId, creatorState?.activeRecipeType || '');
            const rawVars = (varsOverride && typeof varsOverride === 'object')
                ? { ...(defaultVars || {}), ...varsOverride }
                : collectMasterVariables();
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

        // Performance: Cached selectedIds Set using generation counter
        let _selectedIdsCache = { generation: -1, ids: null };

        function _getSelectedIdsSet() {
            if (_selectedIdsCache.generation === _sandboxGeneration && _selectedIdsCache.ids) {
                return _selectedIdsCache.ids;
            }
            const ids = new Set();
            const inputs = document.querySelectorAll('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]');
            for (let i = 0; i < inputs.length; i++) {
                const v = inputs[i].value;
                if (v) ids.add(v.toString());
            }
            _selectedIdsCache.generation = _sandboxGeneration;
            _selectedIdsCache.ids = ids;
            return ids;
        }

        function syncIngredientSourceVisibility() {
            const query = normalizeSearchText((($('#ingredientSearch').val() || '').toString().trim()));
            const selectedIds = _getSelectedIdsSet();
            // data-name-de → dataset.nameDe, data-name-esp → dataset.nameEsp, etc.
            const langKey = 'name' + currentLang.charAt(0).toUpperCase() + currentLang.slice(1);

            // Performance: Native DOM instead of jQuery in inner loop (3-5× faster per element)
            const rows = document.querySelectorAll('.ingredient-db-row');
            for (let i = 0; i < rows.length; i++) {
                const el = rows[i];
                const id = (el.dataset.ingredientId || '').toString();
                const text = normalizeSearchText((el.dataset[langKey] || el.dataset.nameDe || '') + '');
                const isSelected = !!id && selectedIds.has(id);
                const matchesSearch = !query || text.includes(query);
                el.classList.toggle('d-none', isSelected || !matchesSearch);
            }
        }

        // PrÃƒÂ¼ft ob ein Step bereits in der Auswahl ist
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
                piece: ['stk', 'stk.', 'stueck', 'stÃƒÂ¼ck', 'piece', 'pieces', 'pcs', 'pcs.', 'unit', 'units', 'uds', 'un']
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
            detectRecipeTypes: (ingredientIds) => {
                // Enrich plain IDs with groupId from sandbox ingredients for hybrid group_weights scoring
                const sandboxIngs = getSelectedIngredientsForSandbox();
                const byId = new Map(sandboxIngs.map(i => [i.id.toString(), i]));
                const enriched = ingredientIds.map(id => {
                    const s = byId.get(id.toString());
                    return s ? { id: parseInt(s.id, 10), groupId: s.groupId || '' } : { id: parseInt(id, 10), groupId: '' };
                });
                return window.RecipeStepSuggest.detectRecipeTypes(enriched) || [];
            },
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
            deriveIngredientsForSteps: deriveIngredientsForSteps,
            acceptProbabilityTemplateStep: async function (masterId, probabilityVars) {
                if (!masterId) return;
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
            invalidateSandboxCache();
            _suppressPreviewAnimation = true;
            applyDerivedIngredientRowVisuals();
            syncIngredientSourceVisibility();
            $('.keyword-btn').each(function () { $(this).text($(this).data('word-' + langKey)); });
            $('.keyword-pill').each(function () { $(this).find('.keyword-text').text($(this).data('word-' + langKey)); });
            refreshDurationUnitControls();
            updateCommonUnitChipLabels();
            renderAcceptedRecipeTextCard();
            refreshMasterTemplateBuilder();
            refreshIngredientProbabilityHints();
            _suppressPreviewAnimation = false;
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
                isSoft: (row.data('is-soft') === true || row.data('is-soft') === 'true') ? 'true' : 'false',
                isPeelable: (row.data('is-peelable') === true || row.data('is-peelable') === 'true') ? 'true' : 'false',
                isCuttable: (row.data('is-cuttable') === true || row.data('is-cuttable') === 'true') ? 'true' : 'false',
                isGrateable: (row.data('is-grateable') === true || row.data('is-grateable') === 'true') ? 'true' : 'false',
                isFryable: (row.data('is-fryable') === true || row.data('is-fryable') === 'true') ? 'true' : 'false',
                isRoastable: (row.data('is-roastable') === true || row.data('is-roastable') === 'true') ? 'true' : 'false',
                isGrillable: (row.data('is-grillable') === true || row.data('is-grillable') === 'true') ? 'true' : 'false',
                isSteamable: (row.data('is-steamable') === true || row.data('is-steamable') === 'true') ? 'true' : 'false',
                isBoilable: (row.data('is-boilable') === true || row.data('is-boilable') === 'true') ? 'true' : 'false',
                isSearable: (row.data('is-searable') === true || row.data('is-searable') === 'true') ? 'true' : 'false',
                isPoachable: (row.data('is-poachable') === true || row.data('is-poachable') === 'true') ? 'true' : 'false',
                isSmokable: (row.data('is-smokable') === true || row.data('is-smokable') === 'true') ? 'true' : 'false',
                isFlambeable: (row.data('is-flambeable') === true || row.data('is-flambeable') === 'true') ? 'true' : 'false',
                isBlendable: (row.data('is-blendable') === true || row.data('is-blendable') === 'true') ? 'true' : 'false',
                isPowder: (row.data('is-powder') === true || row.data('is-powder') === 'true') ? 'true' : 'false'
            };
        }

        function buildIngredientDataAttributes(localizedData) {
            const langAttrs = supportedLanguages.map(lang =>
                `data-name-${lang}="${escapeAttr(localizedData.names[lang])}" data-genus-${lang}="${escapeAttr(localizedData.genus[lang])}"`
            ).join(' ');
            var tagAttrs = ' data-is-liquid="' + (localizedData.isLiquid || 'false') + '"'
                + ' data-is-fat="' + (localizedData.isFat || 'false') + '"'
                + ' data-is-hard="' + (localizedData.isHard || 'false') + '"'
                + ' data-is-soft="' + (localizedData.isSoft || 'false') + '"'
                + ' data-is-peelable="' + (localizedData.isPeelable || 'false') + '"'
                + ' data-is-cuttable="' + (localizedData.isCuttable || 'false') + '"'
                + ' data-is-grateable="' + (localizedData.isGrateable || 'false') + '"'
                + ' data-is-fryable="' + (localizedData.isFryable || 'false') + '"'
                + ' data-is-roastable="' + (localizedData.isRoastable || 'false') + '"'
                + ' data-is-grillable="' + (localizedData.isGrillable || 'false') + '"'
                + ' data-is-steamable="' + (localizedData.isSteamable || 'false') + '"'
                + ' data-is-boilable="' + (localizedData.isBoilable || 'false') + '"'
                + ' data-is-searable="' + (localizedData.isSearable || 'false') + '"'
                + ' data-is-poachable="' + (localizedData.isPoachable || 'false') + '"'
                + ' data-is-smokable="' + (localizedData.isSmokable || 'false') + '"'
                + ' data-is-flambeable="' + (localizedData.isFlambeable || 'false') + '"'
                + ' data-is-blendable="' + (localizedData.isBlendable || 'false') + '"'
                + ' data-is-powder="' + (localizedData.isPowder || 'false') + '"';
            return `${langAttrs} data-group-id="${escapeAttr(localizedData.groupId || '')}"${tagAttrs}`;
        }

        function buildIngredientRowHtml({ id, localizedData, selectedQuantity, selectedUnit, selectedUnitLabel, displayName, iconHtml }) {
            return `
            <div class="dynamic-item ingredient-row" onclick="openIngredientConfigPopup(this)" title="Zum Bearbeiten antippen" data-group-icon="${escapeAttr(iconHtml)}" ${buildIngredientDataAttributes(localizedData)}>
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].IngredientsAndNutrients.Id" value="${id}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Quantity.Quantitys" class="ingredient-qty-hidden" value="${selectedQuantity}" />
                <input type="hidden" name="IngredientMeasureQuantity[INDEX].Measure.Metrics_DE" class="ingredient-unit-hidden" value="${escapeAttr(selectedUnit)}" />

                <span class="ingredient-group-icon">${iconHtml}</span>
                <span class="ingredient-name-text">${escapeAttr(displayName)}</span>

                <div class="ingredient-row-right">
                    <button type="button" class="ingredient-stepper-btn minus" title="Menge verringern (gedrückt halten zum Wiederholen)" aria-label="Menge verringern">−</button>
                    <span class="ingredient-stepper-value">${escapeAttr(selectedQuantity)} ${escapeAttr(selectedUnitLabel)}</span>
                    <button type="button" class="ingredient-stepper-btn plus" title="Menge erhöhen (gedrückt halten zum Wiederholen)" aria-label="Menge erhöhen">+</button>
                    <button type="button" class="ingredient-delete-btn" title="Zutat entfernen" aria-label="Zutat entfernen"><i class="bi bi-x-lg"></i></button>
                </div>
            </div>`;
        }

        function getAntiForgeryToken() {
            return ($('input[name="__RequestVerificationToken"]').first().val() || '').toString();
        }

        function getMissingIngredientUserHash() {
            return ($('#hiddenUserTokenField').val() || '').toString();
        }

        function normalizeMissingIngredientInputValue(value) {
            const raw = (value || '').toString().trim();
            if (!raw) return '';
            return raw
                .replace(/\s+/g, ' ')
                .split(' ')
                .map(function (part) {
                    if (!part) return '';
                    return part.charAt(0).toUpperCase() + part.slice(1).toLowerCase();
                })
                .join(' ');
        }

        function getCatalogUnitOptionsHtml() {
            const existingSelect = $('#ingredientsCatalog .js-db-unit').first();
            return existingSelect.length ? existingSelect.html() : '<option value="g.">g.</option><option value="ml.">ml.</option><option value="Stk.">Stk.</option>';
        }

        function buildLocalizedDataFromCatalogItem(item) {
            return {
                names: {
                    de: (item?.name_DE || '').toString(),
                    en: (item?.name_EN || '').toString(),
                    esp: (item?.name_ESP || '').toString(),
                    prt: (item?.name_PRT || '').toString(),
                    id: (item?.name_ID || '').toString(),
                    nl: (item?.name_NL || '').toString(),
                    sv: (item?.name_SE || '').toString(),
                    da: (item?.name_DK || '').toString(),
                    no: (item?.name_NO || '').toString(),
                    ms: (item?.name_MS || '').toString()
                },
                genus: {
                    de: (item?.genus_DE || '').toString(),
                    en: (item?.genus_EN || '').toString(),
                    esp: (item?.genus_ESP || '').toString(),
                    prt: (item?.genus_PRT || '').toString(),
                    id: (item?.genus_ID || '').toString(),
                    nl: (item?.genus_NL || '').toString(),
                    sv: (item?.genus_SE || '').toString(),
                    da: (item?.genus_DK || '').toString(),
                    no: (item?.genus_NO || '').toString(),
                    ms: (item?.genus_MS || '').toString()
                },
                groupId: item?.groupId || '',
                isLiquid: item?.is_liquid ? 'true' : 'false',
                isFat: item?.is_fat ? 'true' : 'false',
                isHard: item?.is_hard ? 'true' : 'false',
                isSoft: item?.is_soft ? 'true' : 'false',
                isPeelable: item?.is_peelable ? 'true' : 'false',
                isCuttable: item?.is_cuttable ? 'true' : 'false',
                isGrateable: item?.is_grateable ? 'true' : 'false',
                isFryable: item?.is_fryable ? 'true' : 'false',
                isRoastable: item?.is_roastable ? 'true' : 'false',
                isGrillable: item?.is_grillable ? 'true' : 'false',
                isSteamable: item?.is_steamable ? 'true' : 'false',
                isBoilable: item?.is_boilable ? 'true' : 'false',
                isSearable: item?.is_searable ? 'true' : 'false',
                isPoachable: item?.is_poachable ? 'true' : 'false',
                isSmokable: item?.is_smokable ? 'true' : 'false',
                isFlambeable: item?.is_flambeable ? 'true' : 'false',
                isBlendable: item?.is_blendable ? 'true' : 'false',
                isPowder: item?.is_powder ? 'true' : 'false'
            };
        }

        function buildIngredientCatalogRowHtml(item) {
            const localizedData = buildLocalizedDataFromCatalogItem(item);
            const displayName = localizedData.names[currentLang] || localizedData.names.de || localizedData.names.en || item?.id || '';
            const groupIcon = (item?.icon || item?.groupIcon || '').toString();
            return `
                <div class="preview-step-card mb-1 ingredient-db-row"
                     data-ingredient-id="${escapeAttr(item?.id || '')}"
                     data-group-icon="${escapeAttr(groupIcon)}"
                     data-selected-qty="1"
                     data-selected-unit="g."
                     data-theme="navy"
                     ${buildIngredientDataAttributes(localizedData)}>
                    <div class="d-flex align-items-center gap-2 mb-1 ingredient-card-head" style="min-height:max-content">
                        <div class="preview-step-text display-name mb-0 flex-grow-1 d-flex align-items-center gap-2" style="min-height:max-content">
                            <span class="ingredient-group-icon">${groupIcon}</span>
                            <span class="ingredient-name-text">${escapeAttr(displayName)}</span>
                        </div>
                        <button type="button" class="btn btn-sm creator-cta-primary ms-auto" onclick="addIngredient('${escapeAttr(item?.id || '')}', this)" title="Zutat hinzufügen" aria-label="Zutat hinzufügen">
                            <i class="bi bi-plus-lg"></i>
                        </button>
                    </div>
                    <div class="duration-editor mt-2 ingredient-db-details d-none">
                        <div class="d-flex gap-2 align-items-center">
                            <input type="text" inputmode="decimal"
                                   class="cp-input js-db-qty js-decimal-input"
                                   style="max-width:85px;" placeholder="1"
                                   oninput="$(this).closest('.preview-step-card').attr('data-selected-qty', this.value)" />
                            <select class="cp-select js-db-unit"
                                    style="max-width:140px;"
                                    onchange="$(this).closest('.preview-step-card').attr('data-selected-unit', this.value)">
                                ${getCatalogUnitOptionsHtml()}
                            </select>
                        </div>
                        <div class="common-unit-chips mt-2" role="group" aria-label="Schnelle Einheiten">
                            <button type="button" class="cp-pill common-unit-chip js-common-unit-chip" data-unit-key="g">g.</button>
                            <button type="button" class="cp-pill common-unit-chip js-common-unit-chip" data-unit-key="ml">ml.</button>
                            <button type="button" class="cp-pill common-unit-chip js-common-unit-chip" data-unit-key="piece">Stk.</button>
                        </div>
                    </div>
                </div>`;
        }

        function reloadIngredientCatalogRow(item) {
            const safeId = (item?.id || '').toString();
            if (!safeId) return $();
            $(`#ingredientsCatalog .ingredient-db-row[data-ingredient-id="${safeId}"]`).remove();
            $('#ingredientsCatalog').prepend(buildIngredientCatalogRowHtml(item));
            const row = $(`#ingredientsCatalog .ingredient-db-row[data-ingredient-id="${safeId}"]`).first();
            return row;
        }

        function renderMissingIngredientResult(html) {
            const wrap = $('#missingIngredientResult');
            wrap.html(html || '').toggleClass('d-none', !(html || '').toString().trim());
        }

        function setMissingIngredientBusy(isBusy) {
            $('#btnAnalyzeMissingIngredient, #missingIngredientResult .js-save-ai-ingredient, #missingIngredientResult .js-use-existing-ingredient')
                .prop('disabled', !!isBusy);
            $('#btnAnalyzeMissingIngredient').toggleClass('opacity-75', !!isBusy);
            $('#missingIngredientButtonSpinner').toggleClass('d-none', !isBusy);
        }

        function renderExistingIngredientMatches(matches) {
            const html = `
                <div class="fw-bold text-white mb-2">Schon im Katalog gefunden</div>
                <div class="small ingredient-ai-meta mb-3">Wähle einen Treffer aus und füge ihn direkt hinzu.</div>
                <div class="d-flex flex-wrap gap-2">
                    ${(matches || []).map(match => `
                        <button type="button"
                                class="btn btn-sm ingredient-ai-chip js-use-existing-ingredient"
                                data-id="${escapeAttr(match.id)}"
                                data-name-de="${escapeAttr(match.nameDe || '')}"
                                data-name-en="${escapeAttr(match.nameEn || '')}"
                                data-icon="${escapeAttr(match.icon || '')}"
                                data-group-id="${escapeAttr((match.groupId || '').toString())}"
                                data-unit-de="${escapeAttr(match.unitDe || 'g.')}">
                            ${escapeAttr(match.icon || '')} ${escapeAttr(match.nameDe || match.nameEn || '')}${match.exactMatch ? ' • exakt' : ''}
                        </button>`).join('')}
                </div>`;
            renderMissingIngredientResult(html);
        }

        function renderAiIngredientSuggestion(response) {
            const suggestion = response?.suggestion || null;
            if (!suggestion) {
                renderMissingIngredientResult('<div class="text-white">Kein KI-Vorschlag verfügbar.</div>');
                return;
            }

            missingIngredientCurrentSuggestion = suggestion;
            const confidencePercent = Math.round(Math.max(0, Math.min(1, Number(suggestion.confidence || 0))) * 100);
            const isDebugFallback = !!suggestion.isDebugFallback || (response?.model || '') === 'debug-fallback';
            const html = `
                <div class="d-flex justify-content-between align-items-start gap-3 flex-wrap">
                    <div>
                        <div class="fw-bold text-white mb-1">${escapeAttr(suggestion.confirmationPrompt || 'Meintest du diese Zutat?')}</div>
                        <div class="ingredient-ai-meta">${escapeAttr(response.model || '')}</div>
                    </div>
                    <div class="ingredient-ai-chip">${confidencePercent}% Treffer</div>
                </div>
                <div class="mt-3">
                    <div class="fw-bold text-white">${escapeAttr((suggestion.icon || '').toString())} ${escapeAttr(suggestion.name_DE || suggestion.canonicalName || '')}</div>
                    <div class="ingredient-ai-meta mt-1">
                        EN: ${escapeAttr(suggestion.name_EN || '')} • Gruppe: ${escapeAttr((suggestion.groupId ?? '').toString())}
                    </div>
                    <div class="ingredient-ai-meta mt-1">
                        Flags: ${[
                            suggestion.is_liquid ? 'liquid' : '',
                            suggestion.is_hard ? 'hard' : '',
                            suggestion.is_soft ? 'soft' : '',
                            suggestion.is_cuttable ? 'cuttable' : '',
                            suggestion.is_boilable ? 'boilable' : '',
                            suggestion.is_fryable ? 'fryable' : '',
                            suggestion.is_powder ? 'powder' : ''
                        ].filter(Boolean).join(', ') || 'keine'}
                    </div>
                    ${(suggestion.notes || '').toString().trim()
                        ? `<div class="ingredient-ai-meta mt-2">${escapeAttr(suggestion.notes || '')}</div>`
                        : ''}
                </div>
                <div class="d-flex flex-wrap gap-2 mt-3">
                    ${isDebugFallback
                        ? `<div class="small text-warning">Debug-Fallback aktiv: ohne SecretKeyOpenAi wird nichts gespeichert.</div>`
                        : `<button type="button" class="btn creator-cta-primary js-save-ai-ingredient">
                            <i class="bi bi-database-add me-1"></i>Speichern und hinzufügen
                        </button>`}
                </div>`;
            renderMissingIngredientResult(html);
        }

        async function requestMissingIngredientSuggestion() {
            const ingredientName = normalizeMissingIngredientInputValue($('#missingIngredientInput').val());
            if (!ingredientName) {
                showCreatorToast('Bitte zuerst eine Zutat eingeben');
                return;
            }
            $('#missingIngredientInput').val(ingredientName);

            missingIngredientCurrentSuggestion = null;
            renderMissingIngredientResult(`
                <div class="d-flex align-items-center gap-2 text-white-50">
                    <div class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></div>
                    <div class="small">Prüfe Katalog und KI-Vorschlag …</div>
                </div>`);
            setMissingIngredientBusy(true);

            try {
                const token = getAntiForgeryToken();
                const headers = { 'Content-Type': 'application/json' };
                if (token) {
                    headers['RequestVerificationToken'] = token;
                }

                const resp = await fetch(suggestMissingIngredientUrl, {
                    method: 'POST',
                    headers,
                    body: JSON.stringify({
                        ingredientName,
                        userHash: getMissingIngredientUserHash()
                    })
                });

                const data = await resp.json();
                if (!resp.ok) {
                    throw new Error(data?.message || 'Fehler beim Prüfen der Zutat.');
                }

                if (!data?.success) {
                    renderMissingIngredientResult(`<div class="text-warning">${escapeAttr(data?.message || 'Kein Ergebnis erhalten.')}</div>`);
                    return;
                }

                if (data.mode === 'existing_match') {
                    renderExistingIngredientMatches(data.matches || []);
                    return;
                }

                if (data.mode === 'ai_suggestion') {
                    renderAiIngredientSuggestion(data);
                    return;
                }

                renderMissingIngredientResult(`<div class="text-white">${escapeAttr(data?.message || 'Keine Ausgabe vorhanden.')}</div>`);
            } catch (error) {
                window.CreatePostingFeedback.reportError('Fehler beim Prüfen der fehlenden Zutat', error, {
                    prefix: 'CreatePostingPage',
                    selector: '#creatorToast'
                });
                renderMissingIngredientResult(`<div class="text-warning">${escapeAttr(error?.message || 'Fehler beim Prüfen der Zutat.')}</div>`);
            } finally {
                setMissingIngredientBusy(false);
            }
        }

        async function saveAiSuggestedIngredient() {
            if (!missingIngredientCurrentSuggestion) {
                showCreatorToast('Kein KI-Vorschlag zum Speichern vorhanden');
                return;
            }

            setMissingIngredientBusy(true);
            try {
                renderMissingIngredientResult(`
                    <div class="d-flex align-items-center gap-2 text-white-50">
                        <div class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></div>
                        <div class="small">Speichere neue Zutat …</div>
                    </div>`);
                const token = getAntiForgeryToken();
                const headers = { 'Content-Type': 'application/json' };
                if (token) {
                    headers['RequestVerificationToken'] = token;
                }

                const resp = await fetch(saveSuggestedIngredientUrl, {
                    method: 'POST',
                    headers,
                    body: JSON.stringify({
                        userHash: getMissingIngredientUserHash(),
                        suggestion: missingIngredientCurrentSuggestion
                    })
                });

                const data = await resp.json();
                if (!resp.ok) {
                    throw new Error(data?.message || 'Zutat konnte nicht gespeichert werden.');
                }
                if (!data?.success || !data?.ingredient) {
                    throw new Error(data?.message || 'Zutat konnte nicht gespeichert werden.');
                }

                const row = reloadIngredientCatalogRow(data.ingredient);
                const localizedName = data.ingredient?.name_DE || data.ingredient?.name_EN || '';
                $('#ingredientSearch').val(localizedName);

                if (row.length) {
                    // ✅ Normal: Katalog-Row erfolgreich eingefügt
                    addIngredient(data.ingredient.id, row[0]);
                    row[0]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                } else {
                    // ✅ Fallback: Direkt mit Server-Daten hinzufügen (kein Katalog-Element)
                    const ing = data.ingredient;
                    const matchData = {
                        id: ing.id,
                        nameDe: ing.name_DE || '',
                        nameEn: ing.name_EN || '',
                        nameEs: ing.name_ES || '',
                        namePt: ing.name_PT || '',
                        nameId: ing.name_ID || '',
                        nameNl: ing.name_NL || '',
                        nameSv: ing.name_SV || '',
                        nameDa: ing.name_DA || '',
                        nameNo: ing.name_NO || '',
                        nameMs: ing.name_MS || '',
                        icon: ing.icon || '',
                        groupId: (ing.groupId || '').toString(),
                        unitDe: ing.unitDe || 'g.'
                    };
                    addIngredientFromMatchData(matchData);
                }

                syncIngredientSourceVisibility();
                renderMissingIngredientResult(`<div class="text-success">${escapeAttr(data.message || 'Zutat gespeichert und hinzugefügt.')}</div>`);
                showCreatorToast(data.reusedExisting ? 'Vorhandene Zutat verwendet' : 'Neue Zutat gespeichert');
            } catch (error) {
                window.CreatePostingFeedback.reportError('Fehler beim Speichern der KI-Zutat', error, {
                    prefix: 'CreatePostingPage',
                    selector: '#creatorToast'
                });
                renderMissingIngredientResult(`<div class="text-warning">${escapeAttr(error?.message || 'Zutat konnte nicht gespeichert werden.')}</div>`);
            } finally {
                setMissingIngredientBusy(false);
            }
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

            // âœ… FIX (2026-03-28): Alte System-Funktion aufrufen fÃ¼r Sub-Zutaten (Ei â†' Eiklar/Eigelb)
            invalidateSandboxCache();
            markDirty('derivedVisuals', 'masterTemplateBuilder', 'sourceVisibility', 'probabilityHints');
            window.MasterStepCreatorHelpers?.renderStepButtons?.();
            scheduleCreatePostingDraftSave();
        }

        /**
         * ✅ NEU (2026-05-04): Zutat direkt mit Match-Daten hinzufügen (ohne DOM-Element aus Katalog)
         * Wird verwendet, wenn AI-Match gefunden wurde, aber Zutat nicht im sichtbaren Katalog ist
         */
        function addIngredientFromMatchData(matchData) {
            const id = (matchData.id || '').toString();
            if (!id) return;

            // Prüfen ob bereits vorhanden
            const existing = $('#selectedIngredients input[name$="IngredientsAndNutrients.Id"]').filter(function () {
                return $(this).val()?.toString() === id.toString();
            }).length > 0;

            if (existing) return;

            // Basis-Daten aus matchData
            const displayName = matchData['nameDe'] || matchData['nameEn'] || 'Unbekannt';
            const iconHtml = (matchData.icon || '').toString();
            const groupId = (matchData.groupId || '').toString();
            const unitDe = (matchData.unitDe || 'g.').toString();

            // Minimal-LocalizedData (nur Name, Rest als Defaults)
            const localizedData = {
                names: {},
                genus: {},
                groupId: groupId,
                isLiquid: 'false',
                isFat: 'false',
                isHard: 'false',
                isSoft: 'false',
                isPeelable: 'false',
                isCuttable: 'false',
                isGrateable: 'false',
                isFryable: 'false',
                isRoastable: 'false',
                isGrillable: 'false',
                isSteamable: 'false',
                isBoilable: 'false',
                isSearable: 'false',
                isPoachable: 'false',
                isBlendable: 'false'
            };

            // Namen für alle Sprachen setzen
            // Mapping: Frontend-Code → Server-Feldname-Suffix
            const langMap = {
                'de': 'De', 'en': 'En', 'esp': 'Es', 'prt': 'Pt',
                'id': 'Id', 'nl': 'Nl', 'sv': 'Sv', 'da': 'Da',
                'no': 'No', 'ms': 'Ms'
            };
            supportedLanguages.forEach(lang => {
                const suffix = langMap[lang] || lang.charAt(0).toUpperCase() + lang.slice(1);
                localizedData.names[lang] = matchData['name' + suffix] || displayName;
                localizedData.genus[lang] = matchData['genus' + suffix] || '';
            });

            // Unit-Objekt finden
            const selectedUnitObj = findUnitByDe(unitDe);
            const selectedUnitLabel = getUnitLabel(selectedUnitObj, currentLang) || unitDe;

            // Ingredient-Row HTML bauen
            const newIngredient = buildIngredientRowHtml({
                id,
                localizedData,
                selectedQuantity: '100',  // Default-Menge
                selectedUnit: unitDe,
                selectedUnitLabel: selectedUnitLabel,
                displayName: displayName,
                iconHtml: iconHtml
            });

            $('#selectedIngredients').append(newIngredient);

            // Cache invalidieren & UI aktualisieren
            invalidateSandboxCache();
            markDirty('derivedVisuals', 'masterTemplateBuilder', 'sourceVisibility', 'probabilityHints');
            window.MasterStepCreatorHelpers?.renderStepButtons?.();
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
            showCreatorToast('Chip auswÃƒÂ¤hlen Ã¢â€ â€™ dann Einsetzen tippen');
        }

        function applyChipToStep() {
            const row = creatorState.activeStepIngredientRow;
            if (!row) { cancelStepIngredientEdit(); return; }

            const selectedNames = getSelectedIngredientNames();
            if (!selectedNames.length) { showCreatorToast('Bitte zuerst eine Zutat auswÃƒÂ¤hlen'); return; }

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
            invalidateSandboxCache();
            markDirty('masterTemplateBuilder', 'sourceVisibility', 'probabilityHints');
            window.MasterStepCreatorHelpers?.renderStepButtons?.();
            scheduleCreatePostingDraftSave();
        }

        function adjustIngredientQuantity(btn, delta) {
            const row = $(btn).closest('.ingredient-row');
            const qtyInput = row.find('.ingredient-qty-hidden');
            const currentQty = parseFloat(qtyInput.val() || '0');
            let newQty = currentQty + delta;

            // ✅ Wenn Menge auf 0 oder negativ, Zutat entfernen
            if (newQty <= 0) {
                closeIngredientConfigPopup();
                row.remove();
                invalidateSandboxCache();
                markDirty('masterTemplateBuilder', 'sourceVisibility', 'probabilityHints');
                window.MasterStepCreatorHelpers?.renderStepButtons?.();
                scheduleCreatePostingDraftSave();
                return;
            }

            // Runde auf 2 Dezimalstellen
            newQty = Math.round(newQty * 100) / 100;

            // Update hidden input
            qtyInput.val(newQty);

            // Update visual display
            const unitLabel = row.find('.ingredient-unit-hidden').val() || '';
            const unitObj = findUnitByDe(unitLabel);
            const displayUnit = getUnitLabel(unitObj, currentLang) || unitLabel;
            row.find('.ingredient-stepper-value').text(newQty + ' ' + displayUnit);

            invalidateSandboxCache();
            markDirty('masterTemplateBuilder', 'sourceVisibility', 'probabilityHints');
            window.MasterStepCreatorHelpers?.renderStepButtons?.();
            scheduleCreatePostingDraftSave();
        }

        window.adjustIngredientQuantity = adjustIngredientQuantity;

        // ✅ Hold-to-Repeat für Plus/Minus Buttons
        let holdInterval = null;
        let holdTimeout = null;

        function startHoldRepeat(btn, delta) {
            // Stoppe existierende Intervals
            stopHoldRepeat();

            // Erste Aktion sofort
            adjustIngredientQuantity(btn, delta);

            // Nach 300ms starte kontinuierliches Wiederholen
            holdTimeout = setTimeout(function() {
                holdInterval = setInterval(function() {
                    adjustIngredientQuantity(btn, delta);
                }, 100); // Alle 100ms wiederholen
            }, 300);
        }

        function stopHoldRepeat() {
            if (holdTimeout) {
                clearTimeout(holdTimeout);
                holdTimeout = null;
            }
            if (holdInterval) {
                clearInterval(holdInterval);
                holdInterval = null;
            }
        }

        // Event-Delegation für dynamisch hinzugefügte Buttons
        $(document).on('mousedown touchstart', '.ingredient-stepper-btn', function(e) {
            e.preventDefault();
            e.stopPropagation();

            const btn = this;
            const delta = $(btn).hasClass('plus') ? 1 : -1;

            startHoldRepeat(btn, delta);
        });

        $(document).on('mouseup touchend mouseleave', '.ingredient-stepper-btn', function(e) {
            e.preventDefault();
            e.stopPropagation();
            stopHoldRepeat();
        });

        // Cleanup bei Dokumentverlassen
        $(document).on('mouseleave', function() {
            stopHoldRepeat();
        });

        // ✅ Lösch-Button für Zutaten
        $(document).on('click', '.ingredient-delete-btn', function(e) {
            e.preventDefault();
            e.stopPropagation();

            const row = $(this).closest('.ingredient-row');
            closeIngredientConfigPopup();
            row.remove();
            invalidateSandboxCache();
            markDirty('masterTemplateBuilder', 'sourceVisibility', 'probabilityHints');
            window.MasterStepCreatorHelpers?.renderStepButtons?.();
            scheduleCreatePostingDraftSave();
        });

        // Entfernt alle Steps die fÃƒÂ¼r dieselben Zutaten+Phase gebunden sind wie der neue Step
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

                // PrÃƒÂ¼fe ob der bestehende Step mindestens eine gemeinsame Zutat hat
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
            flushUIRefresh();
            prefillUneditedStepPlaceholders();
            updateStepIndices();
            // Remove derived/transformed rows before binding to avoid phantom entries
            $('#selectedIngredients .ingredient-row[data-derived-row="true"]').remove();
            $('#selectedIngredients .ingredient-transformed-display').remove();
            $('#selectedIngredients .ingredient-row').each(function (i) {
                $(this).find('input, select').each(function () {
                    if (this.name) {
                        // Robust reindexing: Replace first array index with new index
                        // Handles: IngredientMeasureQuantity[old].Property.Subproperty
                        this.name = this.name.replace(/\[(\d+)\]/, '[' + i + ']');
                    }
                });
            });
            $('.step-row').each(function (i) {
                $(this).find('input, select').each(function () {
                    if (this.name) {
                        // Robust reindexing for steps
                        // Handles: RecipePreperationSteps[old].Property or SmartStepReferences[old].Property
                        this.name = this.name.replace(/\[(\d+)\]/, '[' + i + ']');
                    }
                });
            });
            $('input[name$=".Quantity.Quantitys"], .js-decimal-input').each(function () {
                this.value = normalizeDecimalInputValue(this.value);
            });
            return true;
        }

        async function publishAsync() {
            if (!prepareBinding()) return;
            const form = document.getElementById('recipeForm');
            const formData = new FormData(form);
            const submitBtn = form.querySelector('[type="submit"]');
            if (submitBtn) submitBtn.disabled = true;

            // Generate idempotency key for this upload (prevents duplicates on retry)
            let idempotencyKey = sessionStorage.getItem('createPostingIdempotencyKey');
            if (!idempotencyKey) {
                idempotencyKey = crypto.randomUUID ? crypto.randomUUID() : Date.now() + '-' + Math.random().toString(36);
                sessionStorage.setItem('createPostingIdempotencyKey', idempotencyKey);
            }

            try {
                const resp = await fetch(form.action, {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'X-Idempotency-Key': idempotencyKey
                    }
                });
                const data = await resp.json();
                if (data.success) {
                    // Clear draft explicitly on client before redirect
                    clearCreatePostingDraft();
                    clearCreatePostingDraftResetCookie();
                    sessionStorage.removeItem('createPostingIdempotencyKey');
                    window.location.href = '/WorldMiniApp/Home/Index?toast=published';
                } else {
                    alert(data.error || 'Fehler beim Hochladen');
                    if (submitBtn) submitBtn.disabled = false;
                }
            } catch (e) {
                alert('Netzwerkfehler beim Hochladen');
                if (submitBtn) submitBtn.disabled = false;
            }
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
        // SC2 Ã¢â€ â€™ Eingebetteter Smart Step Creator im Steps-Bereich
        // Shares creatorState with the main creator; separate DOM
        // =========================================================

        function updateSc2PreviewText(cachedTemplateId, cachedTpl, cachedVars) {
            // Performance: Accept pre-computed values from updatePreviewText() to avoid double computation
            const templateId = cachedTemplateId || getEffectiveTemplateId();
            if (!templateId || !window.MasterStepRenderer) {
                $('#sc2MasterPreviewText').text('WÃƒÂ¤hle eine Zutat und ein Template.');
                return;
            }
            let tpl = cachedTpl;
            if (!tpl) {
                const template = MasterStepRenderer.findTemplate(templateId);
                const langKey = (currentLang || 'de').toLowerCase();
                tpl = template?.templates?.[langKey] || template?.templates?.de || '';
            }
            const vars = cachedVars || buildVariablesForTemplate(templateId);
            const previewHtml = window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate === 'function'
                ? window.MasterStepCreatorHelpers.renderAssignedPlaceholderTemplate(tpl, vars, creatorState.placeholderAssignments, {
                    activeTokenId: creatorState.activePlaceholderTokenId
                })
                : (tpl || '');
            $('#sc2MasterPreviewText').html(previewHtml || 'Keine Vorschau verfÃƒÂ¼gbar.');
            if (!creatorState.ingredientReplaceArmed) { $('#sc2MasterPreviewCard').removeClass('token-replace-active'); }
            if (!_suppressPreviewAnimation) {
                const sc2Card = $('#sc2MasterPreviewCard');
                sc2Card.addClass('preview-animate');
                setTimeout(() => sc2Card.removeClass('preview-animate'), 250);
            }
        }

        function renderSc2IngredientChips() {
            const wrap = $('#sc2MasterIngredientButtons');
            if (!wrap.length) return;
            const ingredients = getSelectedIngredientsForSandbox();
            if (!ingredients.length) {
                wrap.html('<div class="small text-white-50">WÃƒÂ¤hle zuerst Zutaten aus.</div>');
                return;
            }
            creatorState.selectedIngredientIds = (creatorState.selectedIngredientIds || []).filter(id => ingredients.some(x => x.id === id));
            // Performance: Build HTML string once, insert with single .html() call
            const htmlParts = [];
            ingredients.forEach(item => {
                const active = creatorState.selectedIngredientIds.includes(item.id) ? 'active' : '';
                const displayName = (item.name || '').toString();
                const iconHtml = (item.iconHtml || '').toString();
                const chipLabel = `${iconHtml ? `${iconHtml} ` : ''}${displayName}`;
                htmlParts.push(`<button type="button" class="ingredient-chip ${active}" data-id="${item.id}" data-name="${displayName}">${chipLabel}</button>`);
            });
            wrap.html(htmlParts.join(''));
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
                creatorState.temperatureUnit = currentText.includes('Ã‚Â°F') ? 'fahrenheit' : 'celsius';
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
            // pronoun:     { open: openSc2PronounEditorForToken,     toast: 'Pronomen wÃƒÂ¤hlen', keepTokenActive: true },
            temperature: { open: openSc2TemperatureEditorForToken, toast: 'Temperatur setzen' },
            heat:        { open: openSc2HeatEditorForToken,        toast: 'Hitze-Stufe wÃƒÂ¤hlen' },
            mode:        { open: openSc2ModeEditorForToken,        toast: 'Ofenmodus wÃƒÂ¤hlen' },
            equipment:   { open: openSc2EquipmentEditorForToken,   toast: 'Tool / GerÃƒÂ¤t wÃƒÂ¤hlen' },
            // state:       { open: openSc2StateEditorForToken,       toast: 'Zustand wÃƒÂ¤hlen' },
            tool:        { open: openSc2ToolEditorForToken,        toast: 'Tool / GerÃƒÂ¤t wÃƒÂ¤hlen' },
            grindSize:   { open: openSc2GrindSizeEditorForToken,   toast: 'SchnittgrÃƒÂ¶ÃƒÅ¸e wÃƒÂ¤hlen' },
            shape:       { open: openSc2ShapeEditorForToken,       toast: 'Schnittform wÃƒÂ¤hlen' },
            base:        { open: openSc2BaseEditorForToken,        toast: 'Basis wÃƒÂ¤hlen' },
            item:        { open: openSc2ItemEditorForToken,        toast: 'Item wÃƒÂ¤hlen' },
            balance:     { open: openSc2BalanceEditorForToken,     toast: 'Balance wÃƒÂ¤hlen' },
            seasonings:  { open: openSc2SeasoningsEditorForToken,  toast: 'Seasonings wÃƒÂ¤hlen' }
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
                //         showSc2CreatorToast('Zuerst Pronomen wÃƒÂ¤hlen');
                //         renderSc2TemplateCards();
                //     }
                // })) return;

                if (placeholderType === 'ingredient') {
                    creatorState.activePlaceholderTokenId = tokenId;
                    creatorState.ingredientReplaceArmed = true;
                    $('#sc2MasterPreviewCard').addClass('token-replace-active');
                    showSc2CreatorToast('Zutat auswÃƒÂ¤hlen');
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
            } catch (err) {
                console.error('SC2 openSc2EditorForPlaceholderToken error:', err);
            }
        }

        // =========================================================
        // Edit-Mode: pre-populate form with existing recipe data
        // =========================================================
        function initEditMode(data) {
            if (!data || !data.editData) return;
            var ed = data.editData;

            // 1. Basic fields
            if (ed.title) $('[name="Title"]').val(ed.title);
            if (ed.category) $('[name="Recipe.Category"]').val(ed.category);
            if (ed.preferences) $('[name="Recipe.Preferences"]').val(ed.preferences);
            if (ed.personCount) $('[name="Recipe.PersonCount"]').val(ed.personCount);
            if (ed.preparationTime) $('[name="Recipe.PreparationTime"]').val(ed.preparationTime);

            // 2. Current media preview
            if (ed.currentImageUrl) {
                var imgEl = document.getElementById('editCurrentImage');
                if (imgEl) imgEl.src = ed.currentImageUrl;
            }

            // 3. Ingredients: find catalog row, set qty/unit, call addIngredient
            if (data.ingredients && data.ingredients.length) {
                data.ingredients.forEach(function (ing) {
                    var row = $('#ingredientsCatalog .ingredient-db-row[data-ingredient-id="' + ing.id + '"]');
                    if (!row.length) return;

                    // Set quantity on the catalog row
                    if (ing.quantity) {
                        row.attr('data-selected-qty', ing.quantity);
                        row.find('.js-db-qty').val(ing.quantity);
                    }

                    // Set measure unit on the catalog row
                    if (ing.measureDe) {
                        row.attr('data-selected-unit', ing.measureDe);
                        row.find('.js-db-unit').val(ing.measureDe);
                    }

                    addIngredient(ing.id.toString(), row[0]);
                });
            }

            // 4. Keywords: find button, call toggleKeyword
            if (data.keywordIds && data.keywordIds.length) {
                data.keywordIds.forEach(function (kwId) {
                    var btn = $('.keyword-btn[data-keyword-id="' + kwId + '"]');
                    if (btn.length) {
                        toggleKeyword(kwId.toString(), btn[0]);
                    }
                });
            }

            // 5. Steps: add each step to selectedSteps
            if (data.steps && data.steps.length) {
                data.steps.forEach(function (step) {
                    var stepData = {
                        de: step.stepDe || '',
                        en: step.stepEn || '',
                        esp: step.stepEsp || '',
                        prt: step.stepPrt || '',
                        phase: step.phase || 0,
                        equipment: step.equipment || 0
                    };
                    var stepId = step.preparationStepId || ('edit_' + step.stepIndex);
                    addStep(stepId, null, null, { stepData: stepData });
                });
            }

            // 6. Smart steps: restore probability states + add step rows
            if (data.smartSteps && data.smartSteps.length) {
                data.smartSteps.forEach(function (ss) {
                    var masterId = ss.masterStepKey;
                    if (!masterId) return;

                    // Parse variables and restore into probability state
                    try {
                        var vars = typeof ss.variablesJson === 'string' ? JSON.parse(ss.variablesJson) : (ss.variablesJson || {});
                        var draft = ensureProbabilityDraftState(masterId);
                        if (draft && vars) {
                            if (!draft._explicitValues || typeof draft._explicitValues !== 'object') {
                                draft._explicitValues = {};
                            }
                            Object.keys(vars).forEach(function (k) {
                                draft.values[k] = vars[k];
                                draft._explicitValues[k] = vars[k];
                            });
                        }
                    } catch (e) {
                        console.warn('[initEditMode] Could not parse smart step variables for', masterId, e);
                    }
                });
            }
        }

        $(document).ready(function () {
            const token = sessionStorage.getItem('UserToken') || localStorage.getItem('UserToken');
            if (token) $('#hiddenUserTokenField').val(token);

            var isEditMode = !!window.CreatePostingEditData;

            window.setCreatePostingTheme(readStoredCreatePostingTheme(), { persist: false, refresh: false });
            if (!isEditMode) {
                showDraftRestoreBannerIfNeeded();
            }

            var _ingredientSearchTimer;
            $('#ingredientSearch').on('input', function () {
                clearTimeout(_ingredientSearchTimer);
                _ingredientSearchTimer = setTimeout(syncIngredientSourceVisibility, 200);
                $('#clearIngredientSearch').toggleClass('d-none', !($(this).val() || '').toString().trim());
                if (!($('#missingIngredientInput').val() || '').toString().trim()) {
                    $('#missingIngredientInput').val(($(this).val() || '').toString().trim());
                }
                scheduleCreatePostingDraftSave();
            });
            $('#clearIngredientSearch').toggleClass('d-none', !(($('#ingredientSearch').val() || '').toString().trim()));
            $('#clearIngredientSearch').on('click', function () {
                clearTimeout(_ingredientSearchTimer);
                $('#ingredientSearch').val('').trigger('focus');
                $('#clearIngredientSearch').addClass('d-none');
                syncIngredientSourceVisibility();
                scheduleCreatePostingDraftSave();
            });
            $('#btnToggleMissingIngredientPanel').on('click', function () {
                const panel = $('#missingIngredientPanel');
                panel.toggleClass('d-none');
                if (!panel.hasClass('d-none') && !($('#missingIngredientInput').val() || '').toString().trim()) {
                    $('#missingIngredientInput').val(($('#ingredientSearch').val() || '').toString().trim()).trigger('focus');
                }
            });
            $('#btnAnalyzeMissingIngredient').on('click', function () {
                requestMissingIngredientSuggestion();
            });
            $('#missingIngredientInput').on('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    requestMissingIngredientSuggestion();
                }
            });
            $('#missingIngredientInput').on('blur', function () {
                $(this).val(normalizeMissingIngredientInputValue($(this).val()));
            });
            $('#recipeForm').on('click', '.js-use-existing-ingredient', function () {
                const $btn = $(this);
                const ingId = ($btn.data('id') || '').toString();
                if (!ingId) return;

                // ✅ Erst im Katalog suchen (normale Funktionalität)
                const catalogRow = $(`#ingredientsCatalog .ingredient-db-row[data-ingredient-id="${ingId}"]`).first();

                if (catalogRow.length) {
                    // Zutat im Katalog gefunden - normal hinzufügen
                    addIngredient(ingId, catalogRow[0]);
                    showCreatorToast('Vorhandene Zutat hinzugefügt');
                } else {
                    // ✅ NEU: Zutat nicht im sichtbaren Katalog → Direkt mit Match-Daten hinzufügen
                    const matchData = {
                        id: ingId,
                        nameDe: $btn.data('name-de') || '',
                        nameEn: $btn.data('name-en') || '',
                        icon: $btn.data('icon') || '',
                        groupId: $btn.data('group-id') || '',
                        unitDe: $btn.data('unit-de') || 'g.'
                    };
                    addIngredientFromMatchData(matchData);
                    showCreatorToast('Zutat hinzugefügt');
                }
            });
            $('#recipeForm').on('click', '.js-save-ai-ingredient', function () {
                saveAiSuggestedIngredient();
            });
            $('#restoreDraftBtn').on('click', function () {
                restoreCreatePostingDraft();
                $('#draftRestoreBanner').addClass('d-none');
            });
            $('#discardDraftBtn').on('click', function () {
                clearCreatePostingDraft();
                $('#draftRestoreBanner').addClass('d-none');
            });
            if (!isEditMode) {
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

            /**
             * Renders a probability template from object state (analog zu renderMasterText)
             */
            function renderProbabilityTemplate(masterId) {
                console.log(`[renderProbabilityTemplate] â”â”â” RE-RENDERING "${masterId}" â”â”â”`);
                const prob = ensureProbabilityDraftState(masterId);
                if (!prob) {
                    console.warn('[renderProbabilityTemplate] âŒ No state found for:', masterId);
                    return;
                }

                console.log('[renderProbabilityTemplate] prob.values:', JSON.stringify(prob.values, null, 2));
                console.log('[renderProbabilityTemplate] prob.templateRaw:', prob.templateRaw?.substring(0, 100));

                const $anchor = $(`.probability-template-wrap[data-master-id="${CSS.escape(masterId)}"]`).first();
                if (!$anchor.length) {
                    console.warn('[renderProbabilityTemplate] âŒ No anchor found for:', masterId);
                    return;
                }

                const currentLang = $('html').attr('lang') || 'de';
                const rendererHelpers = window.MasterStepCreatorHelpers;
                if (!rendererHelpers || typeof rendererHelpers.renderEditableStepPreview !== 'function') {
                    console.warn('[renderProbabilityTemplate] âŒ renderEditableStepPreview not available');
                    return;
                }

                const defaultVars = buildVariablesForTemplate(masterId, creatorState?.activeRecipeType || '');
                const mergedVars = {
                    ...(defaultVars || {}),
                    ...(prob.values || {})
                };
                const explicitValues = prob._explicitValues || {};

                console.log('[renderProbabilityTemplate] Calling renderEditableStepPreview with:');
                console.log('  - masterId:', masterId);
                console.log('  - lang:', currentLang);
                console.log('  - vars (merged):', mergedVars);
                console.log('  - optionalValues (user-set):', explicitValues);
                console.log('  - draft:', prob);

                const rendered = rendererHelpers.renderEditableStepPreview({
                    masterId: masterId,
                    templateRaw: prob.templateRaw || '',
                    values: mergedVars
                }, {
                    mode: 'probability',
                    title: 'Erkannter Step',
                    bodyClasses: 'probability-template-text preview-step-text mt-2',
                    wrapperClass: 'current-step-wrap probability-preview-wrap',
                    varsOverride: mergedVars,
                    optionalValues: explicitValues,
                    actionHtml: `<div class="probability-template-actions d-flex gap-2 align-items-center">
          <button type="button" class="btn btn-sm creator-cta-primary js-probability-accept" data-master-id="${masterId}">Akzeptieren</button>
          <button type="button" class="btn btn-sm btn-outline-light js-probability-dismiss" data-master-id="${masterId}">Löschen</button>
        </div>`
                });

                console.log('[renderProbabilityTemplate] Rendered HTML (first 300 chars):', rendered?.substring(0, 300));

                // Update DOM
                const $card = $anchor.find('.probability-template-card').first();
                if ($card.length) {
                    console.log('[renderProbabilityTemplate] Updating DOM...');
                    $card.html(rendered);
                    console.log('[renderProbabilityTemplate] âœ… DOM updated');
                } else {
                    console.warn('[renderProbabilityTemplate] âŒ Card container not found!');
                }
            }

            // Export for use in CreatePostingSmartStepCreator.js
            window.renderProbabilityTemplate = renderProbabilityTemplate;

            function openProbabilityOverlayForVar(masterId, varKey, currentVal, tokenElement) {
                const safeMasterId = (masterId || '').toString().trim();
                const safeVarKey = (varKey || '').toString().trim();
                if (!safeMasterId || !safeVarKey) return;

                const prob = ensureProbabilityDraftState(safeMasterId);
                if (!prob) {
                    console.warn('[Probability Overlay] Could not initialize draft for:', safeMasterId);
                    return;
                }

                const resolvedValue = typeof currentVal === 'string'
                    ? currentVal
                    : ((prob.values || {})[safeVarKey] || '');

                const config = buildProbabilityEditorConfig(
                    safeMasterId,
                    safeVarKey,
                    resolvedValue,
                    tokenElement || null,
                    null,
                    {
                        onApply: function(newVal, extras) {
                            console.log('[Probability Overlay onApply]', { masterId: safeMasterId, varKey: safeVarKey, newVal, extras });
                        },
                        onClose: function() {
                            console.log('[Probability Overlay onClose]', { masterId: safeMasterId, varKey: safeVarKey });
                        }
                    }
                );

                if (!config) {
                    return;
                }

                config.helpers.openStepDraftEditor({
                    source: config.source,
                    varName: config.varName,
                    currentVal: config.currentVal,
                    masterId: config.masterId,
                    context: config.context,
                    onApply: config.onApply,
                    onClose: config.onClose
                });
            }

            // Click on Probability Variable Token to edit
            $('#recipeForm').on('click', '.probability-template-wrap .template-var, .probability-template-wrap .js-optional-var-add, .probability-template-wrap .js-probability-var', function (e) {
                e.stopImmediatePropagation();
                e.stopPropagation();
                e.preventDefault();

                const token = $(this);
                const varKey = (token.data('optional-var') || token.data('var-key') || token.data('var') || '').toString();
                const masterId = token.closest('.probability-template-wrap').data('master-id') || '';

                if (!varKey || !masterId) {
                    console.error('[Probability Var Click] Missing varKey or masterId:', { varKey, masterId });
                    return;
                }

                const prob = ensureProbabilityDraftState(masterId);
                const currentVal = prob && prob.values ? (prob.values[varKey] || '') : '';
                openProbabilityOverlayForVar(masterId, varKey, currentVal, token[0]);
            });

            // Reset Button in Probability Area
            $('#recipeForm').on('click', '.probability-template-wrap .placeholder-reset', function (e) {
                e.stopPropagation();
                e.preventDefault();

                const btn = this;
                const varName = btn.dataset.var;
                const masterId = $(btn).closest('.probability-template-wrap').data('master-id');

                if (!varName || !masterId) return;

                console.log("[Probability Reset Button]", { varName, masterId });

                const prob = ensureProbabilityDraftState(masterId);
                if (prob) {
                    if (draftEngine && typeof draftEngine.resetProbabilityValue === 'function') {
                        draftEngine.resetProbabilityValue(masterId, varName);
                    } else {
                        delete prob.values[varName];
                        if (prob._multiIngredients) {
                            prob._multiIngredients[varName] = [];
                        }
                    }

                    renderProbabilityTemplate(masterId);
                }
            });

            // Multi-Ingredient Plus-Button in Probability Area
            $('#recipeForm').on('click', '.probability-template-wrap .ingredient-plus-btn', function (e) {
                e.preventDefault();
                e.stopPropagation();
                const btn = this;
                const varName = btn.dataset.var;
                const masterId = btn.dataset.masterId;
                if (!varName || !masterId) return;

                const currentValue = ensureProbabilityDraftState(masterId)?.values?.[varName] || '';
                console.log("[Probability Plus Button]", { varName, masterId, currentValue });
                openProbabilityOverlayForVar(masterId, varName, currentValue, btn);
            });



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
                    //         showCreatorToast('Zuerst Pronomen wÃ¤hlen');
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
                    window.CreatePostingFeedback.reportError('Fehler beim Ãƒâ€“ffnen des Platzhalter-Editors', err, {
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

            // Vorgeschlagene Zutat hinzufügen: findet die Zeile im Katalog und ruft addIngredient auf
            $('#recipeForm').on('click', '.js-typical-ingredient-chip', function () {
                const ingId = ($(this).data('ingredient-id') || '').toString();
                if (!ingId) return;

                const catalogRow = $(`.ingredient-db-row[data-ingredient-id="${ingId}"]`).first();
                if (!catalogRow.length) {
                    showCreatorToast('Zutat nicht im Katalog gefunden');
                    return;
                }

                addIngredient(ingId, catalogRow[0]);
                showCreatorToast('Zutat hinzugefügt');
                $(this).prop('disabled', true).addClass('opacity-50');
                catalogRow[0]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            });

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
                        ingredientTransforms = transforms;
                        return transforms;
                    })
                    .catch(() => null);
            }

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
                    showCreatorToast('Drag & Drop nur fÃƒÂ¼r ingredient/ingredients');
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
                showCreatorToast('Platzhalter zurÃƒÂ¼ckgesetzt');
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
                toast: 'Tool / GerÃƒÆ’Ã‚Â¤t eingesetzt',
                composeDuringSelection: true,
                composeOnApply: false
            });

            // Equipment-Wert merken für Auto-Prefill in Folge-Steps
            $('#btnApplyEquipment').on('click', function () {
                creatorState.lastEquipmentValue = creatorState.equipmentValue;
                creatorState.lastEquipmentArticleValue = creatorState.equipmentArticleValue;
                window._lastEquipmentValue = creatorState.equipmentValue;
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
                toast: 'SchnittgrÃƒÂ¶ÃƒÅ¸e eingesetzt'
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

            // ✅ NEU (2026-05-04): Ingredient Add Dropdown statt Inline-Panel
            let currentIngredientRow = null;

            $('#recipeForm').on('click', '.ingredient-db-row', function (e) {
                if ($(e.target).closest('button, input, select, label').length) return;

                const $row = $(this);
                const ingId = $row.data('ingredient-id');
                const ingName = $row.data('name-' + currentLang) || $row.data('name-de') || 'Zutat';
                const ingIcon = $row.find('.ingredient-group-icon').html() || '';

                // Dropdown öffnen
                currentIngredientRow = $row[0];
                $('#ingredientAddIcon').html(ingIcon);
                $('#ingredientAddName').text(ingName);
                $('#ingredientAddQty').val('100').focus();
                $('#ingredientAddUnit').val('g.');

                // Position berechnen (unter der Pill)
                const rect = $row[0].getBoundingClientRect();
                const dropdown = $('#ingredientAddDropdown');
                const maxWidth = Math.min(320, window.innerWidth - 32);

                dropdown.css({
                    top: rect.bottom + window.scrollY + 8 + 'px',
                    left: Math.max(16, Math.min(rect.left + window.scrollX, window.innerWidth - maxWidth - 16)) + 'px',
                    display: 'block',
                    width: maxWidth + 'px'
                });

                $('#ingredientAddOverlay').css('display', 'block');
            });

            // Overlay schließen
            $('#ingredientAddOverlay').on('click', function () {
                $('#ingredientAddDropdown, #ingredientAddOverlay').css('display', 'none');
                currentIngredientRow = null;
            });

            // Quick-Unit Chips
            $(document).on('click', '.js-quick-unit-chip', function (e) {
                e.preventDefault();
                e.stopPropagation();
                const unit = $(this).data('unit');
                console.log('🔵 Quick-Unit clicked:', unit);

                const $select = $('#ingredientAddUnit');

                // Prüfe ob Option existiert
                const optionExists = $select.find(`option[value="${unit}"]`).length > 0;
                console.log('🔵 Option exists?', optionExists, 'Options:', $select.find('option').map(function() { return $(this).val(); }).get());

                if (!optionExists) {
                    // Füge Option hinzu wenn sie fehlt
                    $select.append(`<option value="${unit}">${unit}</option>`);
                }

                // Setze Wert und trigger change event
                $select.val(unit).trigger('change');

                // Visuelles Feedback - alle zurücksetzen
                $('.js-quick-unit-chip').removeClass('active').css({
                    'background': 'white',
                    'color': 'var(--cp-ink-deep, #14391f)',
                    'border-color': 'rgba(20, 57, 31, 0.12)'
                });
                // Aktiven Button hervorheben
                $(this).addClass('active').css({
                    'background': 'var(--cp-avocado)',
                    'color': 'white',
                    'border-color': 'var(--cp-avocado)'
                });

                showCreatorToast(`Einheit: ${unit}`);
            });

            // Add Button
            $('#btnAddIngredientFromDropdown').on('click', function () {
                if (!currentIngredientRow) return;

                const qty = $('#ingredientAddQty').val() || '100';
                const unit = $('#ingredientAddUnit').val() || 'g.';

                // Daten setzen
                $(currentIngredientRow).attr('data-selected-qty', qty);
                $(currentIngredientRow).attr('data-selected-unit', unit);

                // Zutat hinzufügen
                const ingId = $(currentIngredientRow).data('ingredient-id');
                addIngredient(ingId, currentIngredientRow);

                // Overlay schließen
                $('#ingredientAddDropdown, #ingredientAddOverlay').css('display', 'none');
                currentIngredientRow = null;

                showCreatorToast('Zutat hinzugefügt');
            });

            $('#btnCloseIngredientConfig, #btnCancelIngredientConfig').on('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
            });

            $('#btnApplyIngredientConfig').on('click', function () {
                applyIngredientConfigPopup();
            });

            /* ===== OLD PROBABILITY EDITOR HANDLERS - AUSKOMMENTIERT 2026-03-27 =====
             * Diese Handler sind fÃ¼r das alte #probVarEditorDock System.
             * Jetzt wird das Universal Overlay System verwendet.
             * Kann gelÃ¶scht werden, wenn alles funktioniert.
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
            //     $('#probVarIngredientChips').html(chips || '<span class="small text-white-50">Keine Zutaten ausgewÃƒÂ¤hlt</span>');
            // });

            // // Article chip for ingredient variable in prob editor
            // $('#probVarEditorDock').on('click', '.js-prob-ingredient-article-chip', function () {
            //     const val = ($(this).data('value') || '').toString();
            //     probVarIngredientArticleValue = val;
            //     $('.js-prob-ingredient-article-chip').removeClass('active btn-light text-dark').addClass('btn-outline-light');
            //     $(this).addClass('active btn-light text-dark').removeClass('btn-outline-light');
            // });

            // // Option chip â†’ auto-apply and close
            // $('#probVarEditorDock').on('click', '.js-prob-option-chip', function () {
            //     const value = ($(this).data('value') || $(this).text()).toString();
            //     if (probVarEditorCallback) probVarEditorCallback(value);
            //     closeProbVarEditor();
            // });

            // // Pronoun chip â†’ select and apply
            // $('#probVarEditorDock').on('click', '.js-prob-pronoun-chip', function () {
            //     const value = ($(this).data('value') || $(this).text()).toString();
            //     if (probVarEditorCallback) probVarEditorCallback(value);
            //     closeProbVarEditor();
            // });

            // // State chip â†’ select and apply
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
                showSc2CreatorToast('Template gewÃƒÂ¤hlt');

                // Equipment Auto-Prefill aus letzter Auswahl
                setTimeout(function () {
                    if (!creatorState.lastEquipmentValue) return;
                    var eqToken = document.querySelector('#sc2MasterPreviewText .placeholder-token[data-placeholder-key="equipment"]');
                    if (!eqToken) return;
                    var tokenId = eqToken.getAttribute('data-placeholder-token-id');
                    if (!tokenId) return;
                    creatorState.placeholderAssignments[tokenId] = creatorState.lastEquipmentValue;
                    creatorState.equipmentValue = creatorState.lastEquipmentValue;
                    creatorState.equipmentArticleValue = creatorState.lastEquipmentArticleValue;
                    updateSc2PreviewText();
                }, 0);
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
                showSc2CreatorToast('Platzhalter zurÃƒÂ¼ckgesetzt');
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

            // SC2 Equipment-Wert merken für Auto-Prefill in Folge-Steps
            $('#sc2BtnApplyEquipment').on('click', function () {
                creatorState.lastEquipmentValue = creatorState.equipmentValue;
                creatorState.lastEquipmentArticleValue = creatorState.equipmentArticleValue;
                window._lastEquipmentValue = creatorState.equipmentValue;
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
                showSc2CreatorToast('SchnittgrÃƒÂ¶ÃƒÅ¸e eingesetzt');
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

            // Initialize Smart Step Creator filter UI
            if (window.CreatePostingStepFilterUI && typeof window.CreatePostingStepFilterUI.init === 'function') {
                window.CreatePostingStepFilterUI.init();
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
                    refreshIngredientProbabilityHints();
                }).catch((error) => {
                    const msg = error && error.message ? error.message : 'Unbekannter Fehler beim Laden.';
                    setMasterTemplateError(`Template-Fehler: ${msg}`);
                });
            });

            // Page fully ready Ã¢â€ â€™ hide overlay, show content
            // Edit-mode: pre-populate form with existing data
            if (window.CreatePostingEditData) {
                initEditMode(window.CreatePostingEditData);
            }

            $('#datatableLoadingOverlay').addClass('d-none');
            $('.creator-topbar, .feed-shell').css('visibility', 'visible');

            // ✅ Initial validation
            validateRecipeForm();
        });

        // ========================================
        // ECHTZEIT-VALIDIERUNG
        // ========================================

        const VALIDATION_RULES = {
            title: { min: 5, weight: 1, message: 'Titel zu kurz (min. 5 Zeichen)' },
            category: { required: true, weight: 1, message: 'Kategorie auswählen' },
            media: { required: true, weight: 1, message: 'Video oder Bild hochladen' },
            ingredients: { min: 2, weight: 1, message: 'Mind. 2 Zutaten hinzufügen' },
            steps: { min: 1, weight: 1, message: 'Mind. 1 Step hinzufügen' },
            keywords: { min: 1, weight: 1, message: 'Mind. 1 Keyword hinzufügen' }
        };

        function validateRecipeForm() {
            const checks = {
                title: false,
                category: false,
                media: false,
                ingredients: false,
                steps: false,
                keywords: false
            };

            const errors = [];

            // 1. Titel
            const title = ($('#recipeTitle').val() || '').trim();
            if (title.length >= VALIDATION_RULES.title.min) {
                checks.title = true;
            } else {
                errors.push({ field: 'title', message: VALIDATION_RULES.title.message });
            }

            // 2. Kategorie
            const category = $('#selectedCategory').val();
            if (category && category !== '' && category !== 'none') {
                checks.category = true;
            } else {
                errors.push({ field: 'category', message: VALIDATION_RULES.category.message });
            }

            // 3. Media (Video oder Bild)
            const hasVideo = $('#video_storage_url').val() || '';
            const hasImage = $('#image_storage_url').val() || '';
            if (hasVideo || hasImage) {
                checks.media = true;
            } else {
                errors.push({ field: 'media', message: VALIDATION_RULES.media.message });
            }

            // 4. Zutaten (min. 2)
            const ingredientCount = $('#selectedIngredients .ingredient-row').length;
            if (ingredientCount >= VALIDATION_RULES.ingredients.min) {
                checks.ingredients = true;
            } else {
                errors.push({ field: 'ingredients', message: VALIDATION_RULES.ingredients.message + ` (${ingredientCount}/2)` });
            }

            // 5. Steps (min. 1)
            const stepCount = getAllStepRows().length;
            if (stepCount >= VALIDATION_RULES.steps.min) {
                checks.steps = true;
            } else {
                errors.push({ field: 'steps', message: VALIDATION_RULES.steps.message });
            }

            // 6. Keywords (min. 1)
            const keywordCount = $('#selectedKeywords .keyword-badge').length;
            if (keywordCount >= VALIDATION_RULES.keywords.min) {
                checks.keywords = true;
            } else {
                errors.push({ field: 'keywords', message: VALIDATION_RULES.keywords.message });
            }

            // Berechne Fortschritt
            const totalChecks = Object.keys(checks).length;
            const passedChecks = Object.values(checks).filter(Boolean).length;
            const progressPercent = Math.round((passedChecks / totalChecks) * 100);

            // Update UI
            updateValidationUI(passedChecks, totalChecks, progressPercent, errors, checks);

            return passedChecks === totalChecks;
        }

        function updateValidationUI(passedChecks, totalChecks, progressPercent, errors, checks) {
            // Update Fortschrittsbalken
            $('#heroProgressNum').text(passedChecks);
            $('#heroProgressBar').css('width', progressPercent + '%');
            $('#publishProgressBar').css('width', progressPercent + '%');

            // Update Publish-Button
            const $publishBtn = $('#publishBtn');
            const isValid = passedChecks === totalChecks;

            if (isValid) {
                $publishBtn.prop('disabled', false).css({
                    'opacity': '1',
                    'cursor': 'pointer'
                });
            } else {
                $publishBtn.prop('disabled', true).css({
                    'opacity': '0.5',
                    'cursor': 'not-allowed'
                });
            }

            // Update Tab-Badges (zeige ⚠️ bei fehlenden Feldern)
            updateTabBadges(checks);

            // Zeige Fehler im Hero-Bereich wenn nicht komplett
            updateHeroValidationMessage(errors, isValid);
        }

        function updateTabBadges(checks) {
            const tabMapping = {
                'card-basics': checks.title && checks.category,
                'card-media': checks.media,
                'card-ingredients': checks.ingredients,
                'card-steps': checks.steps,
                'card-keywords': checks.keywords
            };

            $('.cp-tab').each(function() {
                const section = $(this).data('section');
                const isValid = tabMapping[section];

                // Entferne existierende Badges
                $(this).find('.validation-badge').remove();

                // Füge Badge hinzu wenn nicht valid
                if (isValid === false) {
                    $(this).append('<span class="validation-badge">⚠️</span>');
                } else if (isValid === true) {
                    $(this).append('<span class="validation-badge validation-badge-success">✓</span>');
                }
            });
        }

        function updateHeroValidationMessage(errors, isValid) {
            const $publishBar = $('.cp-publishbar-label');

            if (isValid) {
                $publishBar.text('Bereit zum Posten!').css('color', 'var(--cp-avocado, #5fa052)');
            } else if (errors.length > 0) {
                // Zeige ersten Fehler
                $publishBar.text(errors[0].message).css('color', '#dc3545');
            }
        }

        // Trigger Validierung bei relevanten Änderungen
        $(document).on('input', '#recipeTitle', function() {
            validateRecipeForm();
        });

        $(document).on('change', '#selectedCategory', function() {
            validateRecipeForm();
        });

        // Trigger bei Media-Upload
        $(document).on('change', '#video_storage_url, #image_storage_url', function() {
            validateRecipeForm();
        });

        // Hook in markDirty um Validierung bei Zutaten/Steps/Keywords zu triggern
        const originalMarkDirty = window.markDirty || function() {};
        window.markDirty = function(...args) {
            originalMarkDirty(...args);
            // Debounce validation
            clearTimeout(window._validationDebounce);
            window._validationDebounce = setTimeout(validateRecipeForm, 150);
        };

        window.validateRecipeForm = validateRecipeForm;

