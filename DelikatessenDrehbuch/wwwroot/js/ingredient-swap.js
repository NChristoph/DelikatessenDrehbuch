// Ingredient Swap Modal - Design Handoff Implementation with ChatGPT API
// Shows ingredient alternatives popup with ChatGPT-powered suggestions

(function () {
    'use strict';

    let currentLanguage = 'de';
    let currentIngredientId = null;
    let currentRecipeId = null;
    let currentIngredientName = null;
    let currentOriginalData = null;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        // Get language from cookie
        const cookies = document.cookie.split('; ').filter(Boolean);
        const deliLangCookie = cookies.find(row => row.startsWith('deli-lang='));
        if (deliLangCookie) {
            const raw = (deliLangCookie.split('=')[1] || '').trim().toLowerCase();
            if (raw) currentLanguage = raw;
        } else {
            const cultureCookie = cookies.find(row => row.startsWith('.AspNetCore.Culture='));
            if (cultureCookie) {
                const match = cultureCookie.match(/c=([a-z]{2})/);
                if (match) currentLanguage = match[1];
            }
        }

        attachSwapButtonListeners();
        setupModalHandlers();
    }

    function attachSwapButtonListeners() {
        document.addEventListener('click', function (e) {
            const swapBtn = e.target.closest('[data-swap-ingredient]');
            if (!swapBtn) return;

            e.preventDefault();
            e.stopPropagation();

            handleSwapClick(swapBtn);
        });
    }

    function setupModalHandlers() {
        const modal = document.getElementById('ingredientAlternativesModal');
        if (!modal) return;

        const closeBtn = document.getElementById('closeAltModal');
        const cancelBtn = document.getElementById('cancelAltModal');
        const backdrop = modal.querySelector('.alt-backdrop');

        if (closeBtn) closeBtn.addEventListener('click', () => closeModal(modal));
        if (cancelBtn) cancelBtn.addEventListener('click', () => closeModal(modal));
        if (backdrop) backdrop.addEventListener('click', () => closeModal(modal));
    }

    function closeModal(modal) {
        if (modal) modal.style.display = 'none';
    }

    function collectExistingIngredients() {
        // Collect all ingredient names from the ingredient list on the page
        const ingredients = [];
        const ingredientElements = document.querySelectorAll('[data-swap-ingredient]');

        ingredientElements.forEach(btn => {
            const name = btn.getAttribute('data-ingredient-name');
            if (name && name.trim()) {
                ingredients.push(name.trim());
            }
        });

        return ingredients;
    }

    async function handleSwapClick(swapBtn) {
        currentIngredientId = parseInt(swapBtn.dataset.swapIngredient, 10);
        currentRecipeId = parseInt(swapBtn.dataset.recipeId, 10);
        currentIngredientName = swapBtn.dataset.ingredientName || 'Unknown Ingredient';

        const modal = document.getElementById('ingredientAlternativesModal');
        if (!modal) return;

        // Show loading state
        showLoadingState(modal, currentIngredientName);

        try {
            // Get swap variant ID from URL
            const u = new URL(window.location.href);
            const rawVariant = u.searchParams.get('swapVariantId');
            const swapVariantId = rawVariant ? parseInt(rawVariant, 10) : null;

            // Collect all existing ingredients from the recipe to avoid suggesting them
            const existingIngredients = collectExistingIngredients();

            // Call ChatGPT API
            const response = await fetch('/api/recipe-swap/suggest', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    originalIngredientId: currentIngredientId,
                    recipeId: currentRecipeId,
                    swapVariantId: Number.isFinite(swapVariantId) && swapVariantId > 0 ? swapVariantId : null,
                    goal: null,
                    language: currentLanguage,
                    aiProvider: 'openai',
                    contextSwaps: [],
                    existingIngredients: existingIngredients
                })
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || `HTTP ${response.status}`);
            }

            const data = await response.json();
            showAlternatives(modal, data);
        } catch (error) {
            console.error('Swap suggestion error:', error);
            showErrorState(modal, currentIngredientName, getErrorMessage(error));
        }
    }

    function showLoadingState(modal, ingredientName) {
        const titleEl = document.getElementById('altIngredientName');
        const originalNameEl = document.getElementById('altOriginalName');
        const optionsListEl = document.getElementById('altOptionsList');
        const originalMacrosEl = document.getElementById('altOriginalMacros');
        const originalAmountEl = document.getElementById('altOriginalAmount');

        if (titleEl) titleEl.textContent = ingredientName;
        if (originalNameEl) originalNameEl.textContent = ingredientName;
        if (originalAmountEl) originalAmountEl.textContent = '...';
        if (originalMacrosEl) originalMacrosEl.innerHTML = '';

        if (optionsListEl) {
            optionsListEl.innerHTML = `
                <div style="text-align: center; padding: 60px 20px; color: #6b7868;">
                    <div class="spinner-border" role="status" style="width: 3rem; height: 3rem; border-color: #5fa052; border-right-color: transparent;">
                        <span class="visually-hidden">Laden...</span>
                    </div>
                    <p style="margin-top: 20px; font-size: 14px;">ChatGPT generiert Alternativen...</p>
                </div>
            `;
        }

        modal.style.display = 'flex';
    }

    function showAlternatives(modal, data) {
        const { suggestions, original, processingTimeMs } = data;
        const alternatives = suggestions || [];
        currentOriginalData = original;

        // Update header with processing time
        const metaTime = modal.querySelector('.alt-meta-time span');
        if (metaTime && processingTimeMs) {
            metaTime.textContent = `${(processingTimeMs / 1000).toFixed(1)}s`;
        }

        // Update original card
        const originalNameEl = document.getElementById('altOriginalName');
        const originalAmountEl = document.getElementById('altOriginalAmount');
        const originalMacrosEl = document.getElementById('altOriginalMacros');

        if (originalNameEl) originalNameEl.textContent = original.name;
        if (originalAmountEl) originalAmountEl.textContent = `${original.quantity}${original.unit}`;

        if (originalMacrosEl) {
            originalMacrosEl.innerHTML = `
                <span class="alt-macro-pill alt-macro-kcal">
                    <span class="alt-macro-val">${Math.round(original.nutrition.calories)}</span>
                    <span class="alt-macro-unit">kcal</span>
                </span>
                <span class="alt-macro-pill alt-macro-protein">
                    <span class="alt-macro-val">${original.nutrition.protein.toFixed(1)}</span>
                    <span class="alt-macro-unit">P</span>
                </span>
                <span class="alt-macro-pill alt-macro-carbs">
                    <span class="alt-macro-val">${original.nutrition.carbs.toFixed(1)}</span>
                    <span class="alt-macro-unit">C</span>
                </span>
                <span class="alt-macro-pill alt-macro-fat">
                    <span class="alt-macro-val">${original.nutrition.fat.toFixed(1)}</span>
                    <span class="alt-macro-unit">F</span>
                </span>
            `;
        }

        // Generate alternative cards
        const optionsListEl = document.getElementById('altOptionsList');
        if (optionsListEl) {
            if (alternatives.length === 0) {
                optionsListEl.innerHTML = `
                    <div style="text-align: center; padding: 40px 20px; color: #6b7868;">
                        <p>Keine passenden Alternativen gefunden.</p>
                    </div>
                `;
            } else {
                optionsListEl.innerHTML = alternatives.map(alt => createOptionCard(alt)).join('');

                // Attach event listeners to buttons
                optionsListEl.querySelectorAll('.alt-btn-use').forEach((btn, idx) => {
                    btn.addEventListener('click', () => handleUseAlternative(alternatives[idx]));
                });
            }
        }
    }

    function showErrorState(modal, ingredientName, errorMessage) {
        const optionsListEl = document.getElementById('altOptionsList');
        if (optionsListEl) {
            optionsListEl.innerHTML = `
                <div style="text-align: center; padding: 40px 20px;">
                    <div style="color: #ff7849; font-size: 48px; margin-bottom: 16px;">⚠️</div>
                    <h5 style="color: #14391f; margin-bottom: 8px;">Fehler beim Laden</h5>
                    <p style="color: #6b7868; font-size: 14px;">Konnte keine Alternativen für <strong>${escapeHtml(ingredientName)}</strong> laden.</p>
                    <p style="color: #9aa295; font-size: 12px; margin-top: 8px;">${escapeHtml(errorMessage)}</p>
                </div>
            `;
        }
    }

    function createDeltaPill(tone, value, suffix) {
        const tones = {
            kcal: 'alt-macro-kcal',
            protein: 'alt-macro-protein',
            carbs: 'alt-macro-carbs',
            fat: 'alt-macro-fat'
        };

        const numValue = Number(value) || 0;
        const sign = numValue > 0 ? '+' : '';
        const displayValue = suffix === 'kcal' ? Math.round(numValue) : numValue.toFixed(1);

        return `<span class="alt-delta-pill ${tones[tone]}">
            <span class="alt-delta-val">${sign}${displayValue}</span>
            <span class="alt-delta-unit">${escapeHtml(suffix)}</span>
        </span>`;
    }

    function createOptionCard(alt) {
        const scorePercent = Math.round((alt.compatibilityScore || 0) * 100);

        return `
            <div class="alt-option-card">
                <div class="alt-option-accent"></div>

                <div class="alt-option-header">
                    <div class="alt-option-left">
                        <div class="alt-option-name">${escapeHtml(alt.name)}</div>
                        <div class="alt-option-amount">${escapeHtml(alt.quantity)}${escapeHtml(alt.unit)}</div>
                    </div>
                    <div class="alt-option-right">
                        <span class="alt-match-badge">
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
                            ${scorePercent}% Match
                        </span>
                        <div class="alt-option-deltas">
                            ${createDeltaPill('kcal', alt.delta.caloriesDelta, 'kcal')}
                            ${createDeltaPill('protein', alt.delta.proteinDelta, 'P')}
                            ${createDeltaPill('carbs', alt.delta.carbsDelta, 'C')}
                            ${createDeltaPill('fat', alt.delta.fatDelta, 'F')}
                        </div>
                    </div>
                </div>

                <p class="alt-option-desc">${escapeHtml(alt.reason || '')}</p>

                ${alt.preparationChange ? `
                    <div class="alt-option-prep">
                        <span class="alt-option-prep-label">Zubereitung:</span> ${escapeHtml(alt.preparationChange)}
                    </div>
                ` : ''}

                <div class="alt-option-procon">
                    <div class="alt-procon-col">
                        <div class="alt-procon-title pros">Vorteile</div>
                        <ul class="alt-procon-list">
                            ${(alt.pros || []).map(p => `
                                <li class="alt-procon-item">
                                    <span class="alt-procon-dot pros"></span>
                                    <span>${escapeHtml(p)}</span>
                                </li>
                            `).join('')}
                        </ul>
                    </div>
                    <div class="alt-procon-col">
                        <div class="alt-procon-title cons">Nachteile</div>
                        <ul class="alt-procon-list">
                            ${(alt.cons || []).map(c => `
                                <li class="alt-procon-item">
                                    <span class="alt-procon-dot cons"></span>
                                    <span>${escapeHtml(c)}</span>
                                </li>
                            `).join('')}
                        </ul>
                    </div>
                </div>

                ${alt.tasteImpact ? `
                    <div class="alt-option-taste">
                        <span class="alt-option-taste-label">Geschmack:</span> ${escapeHtml(alt.tasteImpact)}
                    </div>
                ` : ''}

                <div class="alt-option-actions">
                    <button class="alt-btn-use">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
                        Diese Alternative verwenden
                    </button>
                </div>
            </div>
        `;
    }

    async function handleUseAlternative(alternative) {
        if (!alternative || !currentOriginalData) {
            alert('Fehler: Alternativdaten nicht verfügbar');
            return;
        }

        if (!confirm(`Diese Zutat wirklich ersetzen?\n\nAlt: ${currentOriginalData.name}\nNeu: ${alternative.name} (${alternative.quantity}${alternative.unit})`)) {
            return;
        }

        try {
            const u = new URL(window.location.href);
            const raw = u.searchParams.get('swapVariantId');
            const swapVariantId = raw ? parseInt(raw, 10) : null;

            const response = await fetch('/api/recipe-swap/apply', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    recipeId: currentRecipeId,
                    swapVariantId: Number.isFinite(swapVariantId) && swapVariantId > 0 ? swapVariantId : null,
                    fromIngredientId: currentIngredientId,
                    fromName: currentOriginalData.name,
                    toIngredientId: alternative.ingredientId,
                    toName: alternative.name,
                    newQuantity: alternative.quantity,
                    unit: alternative.unit
                })
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();
            if (typeof showToast === 'function') {
                showToast('Alternative gespeichert! Wird geladen...', 'success', 2000);
            } else {
                alert('Alternative gespeichert!');
            }

            const variantId = data.variant?.id;
            if (variantId) {
                const baseUrl = window.location.pathname.split('?')[0];
                const newUrl = `${baseUrl}?id=${currentRecipeId}&swapVariantId=${variantId}`;
                setTimeout(() => window.location.href = newUrl, 1000);
            } else {
                setTimeout(() => window.location.reload(), 1000);
            }
        } catch (error) {
            console.error('Apply swap error:', error);
            alert('Fehler beim Speichern: ' + getErrorMessage(error));
        }
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text == null ? '' : String(text);
        return div.innerHTML;
    }

    function getErrorMessage(error) {
        if (!error) return 'Unbekannter Fehler';
        return error.message || String(error);
    }

    // Export for external use
    window.IngredientSwap = {
        openModal: handleSwapClick,
        closeModal: function() {
            const modal = document.getElementById('ingredientAlternativesModal');
            if (modal) modal.style.display = 'none';
        }
    };
})();
