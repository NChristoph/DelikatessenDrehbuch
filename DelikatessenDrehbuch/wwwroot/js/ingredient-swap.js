// Ingredient Swap Modal - AI-powered ingredient substitution
// OpenAI-only implementation.

(function () {
    'use strict';

    let currentModal = null;
    let currentIngredientId = null;
    let currentRecipeId = null;
    let currentLanguage = 'de';
    let currentSuggestions = null;
    let currentOriginalName = null;
    let currentGoal = null;
    let currentIngredientName = null;
    let currentProvider = 'openai';

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        const cultureCookie = document.cookie
            .split('; ')
            .find(row => row.startsWith('.AspNetCore.Culture='));

        if (cultureCookie) {
            const match = cultureCookie.match(/c=([a-z]{2})/);
            if (match) {
                currentLanguage = match[1];
            }
        }

        attachSwapButtonListeners();
    }

    function attachSwapButtonListeners() {
        document.querySelectorAll('[data-swap-ingredient]').forEach(btn => {
            btn.addEventListener('click', handleSwapClick);
        });
    }

    async function handleSwapClick(event) {
        event.preventDefault();
        event.stopPropagation();

        const button = event.currentTarget;
        currentIngredientId = parseInt(button.dataset.swapIngredient, 10);
        currentRecipeId = parseInt(button.dataset.recipeId, 10);
        currentIngredientName = button.dataset.ingredientName;
        currentGoal = button.dataset.goal || null;
        currentProvider = 'openai';

        try {
            await loadSuggestions(currentProvider);
        } catch (error) {
            console.error('Swap suggestion error:', error);
            showErrorModal(currentIngredientName, getErrorMessage(error));
        }
    }

    async function loadSuggestions(provider) {
        currentProvider = 'openai';
        showLoadingModal(currentIngredientName, currentProvider);

        const response = await fetch('/api/recipe-swap/suggest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                originalIngredientId: currentIngredientId,
                recipeId: currentRecipeId,
                goal: currentGoal,
                language: currentLanguage,
                aiProvider: 'openai'
            })
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || `HTTP ${response.status}`);
        }

        const data = await response.json();
        showSuggestionsModal(currentIngredientName, data);
    }

    function showLoadingModal(ingredientName, provider) {
        const html = `
            <div class="modal fade show" style="display: block; background: rgba(0,0,0,0.5);">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content" style="border-radius: 16px;">
                        <div class="modal-body text-center py-5">
                            <div class="spinner-border text-primary mb-3" style="width: 3rem; height: 3rem;"></div>
                            <h5 class="mb-2">Alternativen werden gesucht...</h5>
                            <p class="text-muted mb-0">Fur: <strong>${escapeHtml(ingredientName)}</strong></p>
                            <small class="text-muted d-block mt-2">${getProviderLabel(provider)} wird abgefragt</small>
                        </div>
                    </div>
                </div>
            </div>
        `;

        if (currentModal) {
            currentModal.remove();
        }

        currentModal = createElementFromHTML(html);
        document.body.appendChild(currentModal);
    }

    function showSuggestionsModal(ingredientName, data) {
        const { suggestions, original, aiProvider, processingTimeMs } = data;
        currentSuggestions = suggestions || [];
        currentOriginalName = original?.name || ingredientName;
        currentProvider = 'openai';

        const html = `
            <div class="modal fade show" style="display: block; background: rgba(0,0,0,0.5);">
                <div class="modal-dialog modal-dialog-centered modal-lg">
                    <div class="modal-content" style="border-radius: 16px;">
                        <div class="modal-header" style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; border-radius: 16px 16px 0 0;">
                            <div>
                                <h5 class="modal-title mb-1">Alternativen fur ${escapeHtml(ingredientName)}</h5>
                                <small style="opacity: 0.9;">
                                    ${getProviderLabel(currentProvider)} • ${processingTimeMs}ms
                                </small>
                            </div>
                            <button type="button" class="btn-close btn-close-white" data-dismiss="modal"></button>
                        </div>
                        <div class="modal-body" style="max-height: 60vh; overflow-y: auto;">
                            <div class="card mb-3" style="background: #f8f9fa; border: none;">
                                <div class="card-body">
                                    <h6 class="mb-2">Original:</h6>
                                    <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                                        <span><strong>${escapeHtml(original.name)}</strong> (${original.quantity}${original.unit})</span>
                                        <div class="d-flex gap-2 small flex-wrap">
                                            <span class="badge bg-secondary">${Math.round(original.nutrition.calories)} kcal</span>
                                            <span class="badge bg-success">${original.nutrition.protein.toFixed(1)}g P</span>
                                            <span class="badge bg-primary">${original.nutrition.carbs.toFixed(1)}g C</span>
                                            <span class="badge bg-warning text-dark">${original.nutrition.fat.toFixed(1)}g F</span>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            ${currentSuggestions.length === 0
                                ? '<p class="text-center text-muted py-4">Keine passenden Alternativen gefunden.</p>'
                                : currentSuggestions.map((s, idx) => renderSuggestionCard(s, idx)).join('')}
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-dismiss="modal">Abbrechen</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        if (currentModal) {
            currentModal.remove();
        }

        currentModal = createElementFromHTML(html);
        document.body.appendChild(currentModal);

        currentModal.querySelectorAll('[data-dismiss="modal"], .btn-close').forEach(btn => {
            btn.addEventListener('click', closeModal);
        });

        currentModal.querySelectorAll('[data-apply-swap]').forEach(btn => {
            btn.addEventListener('click', handleApplySwap);
        });

    }

    function renderSuggestionCard(suggestion, index) {
        const scorePercent = Math.round((suggestion.compatibilityScore || 0) * 100);
        const scoreColor = scorePercent >= 80 ? 'success' : scorePercent >= 60 ? 'warning' : 'danger';

        return `
            <div class="card mb-3 shadow-sm" style="border-left: 4px solid var(--bs-${scoreColor});">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-start mb-2 gap-2">
                        <div>
                            <h6 class="mb-1">${escapeHtml(suggestion.name)}</h6>
                            <small class="text-muted">${suggestion.quantity}${suggestion.unit}</small>
                        </div>
                        <div class="text-end">
                            <div class="badge bg-${scoreColor} mb-1">${scorePercent}% Match</div>
                            <div class="d-flex gap-1 small flex-wrap justify-content-end">
                                ${formatDelta(suggestion.delta.caloriesDelta, 'kcal')}
                                ${formatDelta(suggestion.delta.proteinDelta, 'P', 'success')}
                                ${formatDelta(suggestion.delta.carbsDelta, 'C', 'primary')}
                                ${formatDelta(suggestion.delta.fatDelta, 'F', 'warning')}
                            </div>
                        </div>
                    </div>

                    <p class="mb-2 small">${escapeHtml(suggestion.reason || '')}</p>

                    ${suggestion.preparationChange
                        ? `<div class="alert alert-info py-2 px-3 mb-2 small">
                             <strong>Zubereitung:</strong> ${escapeHtml(suggestion.preparationChange)}
                           </div>`
                        : ''}

                    <div class="row mb-2">
                        <div class="col-6">
                            <strong class="small text-success">Vorteile:</strong>
                            <ul class="small mb-0 ps-3">
                                ${(suggestion.pros || []).map(p => `<li>${escapeHtml(p)}</li>`).join('')}
                            </ul>
                        </div>
                        <div class="col-6">
                            <strong class="small text-danger">Nachteile:</strong>
                            <ul class="small mb-0 ps-3">
                                ${(suggestion.cons || []).map(c => `<li>${escapeHtml(c)}</li>`).join('')}
                            </ul>
                        </div>
                    </div>

                    <div class="alert alert-light py-2 px-3 mb-2 small">
                        <strong>Geschmack:</strong> ${escapeHtml(suggestion.tasteImpact || '')}
                    </div>

                    <button class="btn btn-primary btn-sm w-100"
                            data-apply-swap
                            data-suggestion-index="${index}">
                        Diese Alternative verwenden
                    </button>
                </div>
            </div>
        `;
    }

    function formatDelta(delta, label, color = 'secondary') {
        const numericDelta = Number(delta || 0);
        if (Math.abs(numericDelta) < 0.1) {
            return '';
        }

        const sign = numericDelta > 0 ? '+' : '';
        const rounded = label === 'kcal' ? Math.round(numericDelta) : numericDelta.toFixed(1);
        return `<span class="badge bg-${color}">${sign}${rounded}${label}</span>`;
    }

    async function handleApplySwap(event) {
        const button = event.currentTarget;
        const suggestionIndex = parseInt(button.dataset.suggestionIndex, 10);

        if (!currentSuggestions || !currentSuggestions[suggestionIndex]) {
            alert('Fehler: Vorschlagsdaten nicht verfugbar');
            return;
        }

        const suggestion = currentSuggestions[suggestionIndex];
        if (!confirm(`Diese Zutat wirklich ersetzen?\n\nAlt: ${currentOriginalName}\nNeu: ${suggestion.name} (${suggestion.quantity}${suggestion.unit})`)) {
            return;
        }

        try {
            const response = await fetch('/api/recipe-swap/apply', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    recipeId: currentRecipeId,
                    fromIngredientId: currentIngredientId,
                    fromName: currentOriginalName,
                    toIngredientId: suggestion.ingredientId,
                    toName: suggestion.name,
                    newQuantity: suggestion.quantity,
                    unit: suggestion.unit
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

    function showErrorModal(ingredientName, errorMessage) {
        const html = `
            <div class="modal fade show" style="display: block; background: rgba(0,0,0,0.5);">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content" style="border-radius: 16px;">
                        <div class="modal-header bg-danger text-white" style="border-radius: 16px 16px 0 0;">
                            <h5 class="modal-title">Fehler</h5>
                            <button type="button" class="btn-close btn-close-white" data-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <p>Konnte keine Alternativen fur <strong>${escapeHtml(ingredientName)}</strong> finden.</p>
                            <p class="text-muted small mb-0">Fehler: ${escapeHtml(errorMessage)}</p>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-dismiss="modal">Schliessen</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        if (currentModal) {
            currentModal.remove();
        }

        currentModal = createElementFromHTML(html);
        document.body.appendChild(currentModal);
        currentModal.querySelectorAll('[data-dismiss="modal"], .btn-close').forEach(btn => {
            btn.addEventListener('click', closeModal);
        });
    }

    function closeModal() {
        if (currentModal) {
            currentModal.remove();
            currentModal = null;
        }
    }

    function createElementFromHTML(htmlString) {
        const div = document.createElement('div');
        div.innerHTML = htmlString.trim();
        return div.firstChild;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text == null ? '' : String(text);
        return div.innerHTML;
    }

    function getProviderLabel(provider) {
        return 'ChatGPT (GPT-4o-mini)';
    }

    function getErrorMessage(error) {
        if (!error) {
            return 'Unbekannter Fehler';
        }

        return error.message || String(error);
    }

    window.IngredientSwap = {
        openModal: handleSwapClick,
        closeModal: closeModal
    };
})();
