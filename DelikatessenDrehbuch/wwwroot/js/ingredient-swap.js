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

    const batchStorageKeyForRecipe = (recipeId) => `swap_batch_v1_${recipeId}`;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        // Prefer our app-specific language cookie first (used across the WorldMiniApp).
        // Fallback to ASP.NET culture cookie if present.
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
        renderBatchBarIfPresent();
    }

    function attachSwapButtonListeners() {
        document.querySelectorAll('[data-swap-ingredient]').forEach(btn => {
            btn.addEventListener('click', handleSwapClick);
        });
    }

    function getRecipeIdFromPage() {
        const btn = document.querySelector('[data-swap-ingredient][data-recipe-id]');
        if (!btn) return null;
        const id = parseInt(btn.dataset.recipeId, 10);
        return Number.isFinite(id) ? id : null;
    }

    function loadBatch(recipeId) {
        try {
            const raw = sessionStorage.getItem(batchStorageKeyForRecipe(recipeId));
            const parsed = raw ? JSON.parse(raw) : [];
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    }

    function saveBatch(recipeId, swaps) {
        try {
            sessionStorage.setItem(batchStorageKeyForRecipe(recipeId), JSON.stringify(swaps || []));
        } catch {
        }
    }

    function upsertBatchSwap(recipeId, swap) {
        const swaps = loadBatch(recipeId);
        const idx = swaps.findIndex(s => s && s.fromIngredientId === swap.fromIngredientId);
        if (idx >= 0) {
            swaps[idx] = swap;
        } else {
            swaps.push(swap);
        }
        saveBatch(recipeId, swaps);
        return swaps;
    }

    function clearBatch(recipeId) {
        try {
            sessionStorage.removeItem(batchStorageKeyForRecipe(recipeId));
        } catch {
        }
    }

    function renderBatchBarIfPresent() {
        const recipeId = getRecipeIdFromPage();
        const bar = document.getElementById('swapBatchBar');
        if (!bar || !recipeId) {
            console.debug('[swap-batch] skip render', { hasBar: !!bar, recipeId });
            return;
        }

        const swaps = loadBatch(recipeId);
        if (!swaps || swaps.length === 0) {
            bar.style.display = 'none';
            bar.innerHTML = '';
            return;
        }

        console.debug('[swap-batch] render', { recipeId, count: swaps.length });
        bar.style.display = 'block';
        bar.innerHTML = `
            <div class="d-flex align-items-center justify-content-between gap-2 rounded border bg-white px-3 py-2 shadow-sm">
                <div class="small text-muted">
                    <strong>Batch:</strong> ${swaps.length} Änderung(en) vorgemerkt
                </div>
                <div class="d-flex gap-2">
                    <button type="button" class="btn btn-sm btn-outline-secondary" id="swapBatchClearBtn">Leeren</button>
                    <button type="button" class="btn btn-sm btn-success" id="swapBatchApplyBtn">Batch anwenden</button>
                </div>
            </div>
        `;

        const clearBtn = document.getElementById('swapBatchClearBtn');
        const applyBtn = document.getElementById('swapBatchApplyBtn');

        if (clearBtn) {
            clearBtn.addEventListener('click', () => {
                clearBatch(recipeId);
                renderBatchBarIfPresent();
                if (typeof showToast === 'function') showToast('Batch geleert.', 'info', 1500);
            });
        }

        if (applyBtn) {
            applyBtn.addEventListener('click', () => applyBatch(recipeId));
        }
    }

    async function applyBatch(recipeId) {
        const swaps = loadBatch(recipeId);
        if (!swaps || swaps.length === 0) return;

        if (!confirm(`Batch wirklich anwenden?\n\nÄnderungen: ${swaps.length}`)) {
            return;
        }

        try {
            const u = new URL(window.location.href);
            const raw = u.searchParams.get('swapVariantId');
            const swapVariantId = raw ? parseInt(raw, 10) : null;

            const response = await fetch('/api/recipe-swap/apply-batch', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    recipeId,
                    swapVariantId: Number.isFinite(swapVariantId) && swapVariantId > 0 ? swapVariantId : null,
                    swaps: swaps.map(s => ({
                        recipeId,
                        swapVariantId: Number.isFinite(swapVariantId) && swapVariantId > 0 ? swapVariantId : null,
                        fromIngredientId: s.fromIngredientId,
                        fromName: s.fromName,
                        toIngredientId: s.toIngredientId,
                        toName: s.toName,
                        newQuantity: s.newQuantity,
                        unit: s.unit
                    }))
                })
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || `HTTP ${response.status}`);
            }

            const data = await response.json();
            const variantId = data.variant?.id;

            clearBatch(recipeId);
            renderBatchBarIfPresent();

            if (typeof showToast === 'function') {
                showToast('Batch gespeichert! Wird geladen...', 'success', 1800);
            }

            if (variantId) {
                const baseUrl = window.location.pathname.split('?')[0];
                const newUrl = `${baseUrl}?id=${recipeId}&swapVariantId=${variantId}`;
                setTimeout(() => window.location.href = newUrl, 700);
            } else {
                setTimeout(() => window.location.reload(), 700);
            }
        } catch (err) {
            console.error('Apply batch error:', err);
            alert('Fehler beim Batch-Speichern: ' + getErrorMessage(err));
        }
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

        const u = new URL(window.location.href);
        const rawVariant = u.searchParams.get('swapVariantId');
        const swapVariantId = rawVariant ? parseInt(rawVariant, 10) : null;

        const batch = currentRecipeId ? loadBatch(currentRecipeId) : [];
        const contextSwaps = Array.isArray(batch)
            ? batch
                .filter(s => s && Number.isFinite(s.fromIngredientId) && Number.isFinite(s.toIngredientId))
                .map(s => ({
                    fromIngredientId: s.fromIngredientId,
                    fromName: s.fromName || '',
                    toIngredientId: s.toIngredientId,
                    toName: s.toName || '',
                    newQuantity: s.newQuantity || 0,
                    unit: s.unit || 'g'
                }))
            : [];

        const response = await fetch('/api/recipe-swap/suggest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                originalIngredientId: currentIngredientId,
                recipeId: currentRecipeId,
                swapVariantId: Number.isFinite(swapVariantId) && swapVariantId > 0 ? swapVariantId : null,
                goal: currentGoal,
                language: currentLanguage,
                aiProvider: 'openai',
                contextSwaps
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
                            <p class="text-muted mb-0">Fuer: <strong>${escapeHtml(ingredientName)}</strong></p>
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
                                <h5 class="modal-title mb-1">Alternativen fuer ${escapeHtml(ingredientName)}</h5>
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

        currentModal.querySelectorAll('[data-add-to-batch]').forEach(btn => {
            btn.addEventListener('click', handleAddToBatch);
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
                    <button class="btn btn-outline-success btn-sm w-100 mt-2"
                            data-add-to-batch
                            data-suggestion-index="${index}">
                        Zum Batch hinzufügen
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

    function handleAddToBatch(event) {
        const button = event.currentTarget;
        const suggestionIndex = parseInt(button.dataset.suggestionIndex, 10);
        const recipeId = currentRecipeId;

        if (!recipeId || !currentSuggestions || !currentSuggestions[suggestionIndex]) {
            alert('Fehler: Vorschlagsdaten nicht verfügbar');
            return;
        }

        const suggestion = currentSuggestions[suggestionIndex];
        const swap = {
            fromIngredientId: currentIngredientId,
            fromName: currentOriginalName,
            toIngredientId: suggestion.ingredientId,
            toName: suggestion.name,
            newQuantity: suggestion.quantity,
            unit: suggestion.unit
        };

        const next = upsertBatchSwap(recipeId, swap);
        renderBatchBarIfPresent();

        if (typeof showToast === 'function') {
            showToast(`Zum Batch hinzugefügt (${next.length})`, 'success', 1500);
        }

        closeModal();
    }

    function injectSwapUiStylesOnce() {
        if (document.getElementById('swapModalStylesV1')) return;
        const style = document.createElement('style');
        style.id = 'swapModalStylesV1';
        style.textContent = `
            .swap-modal { border-radius: 18px; overflow: hidden; border: 1px solid rgba(0,0,0,0.06); box-shadow: 0 28px 80px rgba(0,0,0,0.22); }
            .swap-modal-header { background: radial-gradient(1200px 400px at 0% 0%, rgba(255,227,179,0.55) 0%, rgba(255,255,255,0.0) 55%), linear-gradient(180deg, #fff 0%, #fbfbfc 100%); border-bottom: 1px solid rgba(0,0,0,0.06); }
            .swap-modal-kicker { font-size: 0.72rem; letter-spacing: 0.08em; text-transform: uppercase; color: rgba(0,0,0,0.55); font-weight: 800; }
            .swap-modal-meta { font-size: 0.82rem; color: rgba(0,0,0,0.55); margin-top: 2px; }
            .swap-modal-body { max-height: 60vh; overflow-y: auto; background: #fcfcfd; }
            .swap-original { background: #fff; border-radius: 14px; padding: 14px; box-shadow: 0 10px 26px rgba(0,0,0,0.06); margin-bottom: 14px; border: 1px solid rgba(0,0,0,0.06); }
            .swap-original-label { font-size: 0.82rem; color: rgba(0,0,0,0.55); font-weight: 800; }
            .swap-original-title { font-size: 1.08rem; font-weight: 900; margin-top: 2px; }
            .swap-original-sub { font-size: 0.9rem; color: rgba(0,0,0,0.55); }
            .swap-chips { display: flex; gap: 8px; flex-wrap: wrap; justify-content: flex-end; }
            .chip { display: inline-flex; align-items: center; gap: 6px; padding: 6px 10px; border-radius: 999px; font-weight: 900; font-size: 0.82rem; border: 1px solid rgba(0,0,0,0.06); background: rgba(255,255,255,0.96); }
            .chip-muted { color: rgba(0,0,0,0.75); }
            .chip-good { background: rgba(46, 204, 113, 0.12); border-color: rgba(46, 204, 113, 0.25); color: #1b7f45; }
            .chip-info { background: rgba(52, 152, 219, 0.12); border-color: rgba(52, 152, 219, 0.25); color: #1b5e8a; }
            .chip-warn { background: rgba(241, 196, 15, 0.18); border-color: rgba(241, 196, 15, 0.35); color: #7a5b00; }
            .chip-bad { background: rgba(231, 76, 60, 0.12); border-color: rgba(231, 76, 60, 0.25); color: #a43126; }
            .swap-suggestion { border-radius: 14px; border: 1px solid rgba(0,0,0,0.06); box-shadow: 0 10px 26px rgba(0,0,0,0.06); overflow: hidden; background: #fff; }
            .swap-reason { color: rgba(0,0,0,0.72); line-height: 1.35; }
            @media (max-width: 576px) { .swap-modal-body { max-height: 66vh; } }
        `;
        document.head.appendChild(style);
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
                            <p>Konnte keine Alternativen fuer <strong>${escapeHtml(ingredientName)}</strong> finden.</p>
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
