(function (window) {
    'use strict';

    function initStepFilterUI() {
        // Füge Filter-UI vor beiden Template-Containern ein
        injectFilterUI('#masterTemplateCards');
        injectFilterUI('#sc2MasterTemplateCards');

        // Event-Listener für Such-Inputs
        $(document).on('input', '.step-search-input', function () {
            const query = $(this).val();
            window.CreatePostingTemplateBuilder.setSearchQuery(query);
            refreshTemplates();
        });

        // Event-Listener für Phase-Tabs
        $(document).on('click', '.phase-tab', function () {
            $('.phase-tab').removeClass('active');
            $(this).addClass('active');
            const phase = $(this).data('phase');
            window.CreatePostingTemplateBuilder.setPhaseFilter(phase);
            refreshTemplates();
        });
    }

    function injectFilterUI(containerSelector) {
        const container = $(containerSelector);
        if (!container.length || container.prev('.step-filter-controls').length) return;

        const theme = window.getCreatePostingTheme && window.getCreatePostingTheme() || 'dark';
        const filterHTML = `
            <div class="step-filter-controls" data-theme="${theme}">
                <div class="step-search-wrapper">
                    <input type="text"
                           class="step-search-input"
                           placeholder="🔍 Step suchen (z.B. 'anbraten', 'schneiden')..."
                           autocomplete="off">
                </div>
                <div class="phase-tabs">
                    <button type="button" class="phase-tab active" data-phase="all">Alle</button>
                    <button type="button" class="phase-tab" data-phase="1">🔪 Vorbereitung</button>
                    <button type="button" class="phase-tab" data-phase="2">🔥 Kochen</button>
                    <button type="button" class="phase-tab" data-phase="3">✨ Finishing</button>
                    <button type="button" class="phase-tab" data-phase="4">🍽️ Servieren</button>
                </div>
            </div>
        `;

        container.before(filterHTML);
    }

    function refreshTemplates() {
        if (window.CreatePostingTemplateBuilder && window.CreatePostingTemplateBuilder.refreshMasterTemplateBuilder) {
            // Trigger refresh über globale Abhängigkeiten
            if (window.refreshMasterTemplateBuilder) {
                window.refreshMasterTemplateBuilder();
            } else if (window.CreatePostingDeps) {
                window.CreatePostingTemplateBuilder.refreshMasterTemplateBuilder(window.CreatePostingDeps);
            }
        }
    }

    // Init beim Laden
    $(document).ready(function () {
        // Warte kurz, bis master templates geladen sind
        setTimeout(initStepFilterUI, 500);
    });

    window.CreatePostingStepFilterUI = {
        init: initStepFilterUI
    };
})(window);
