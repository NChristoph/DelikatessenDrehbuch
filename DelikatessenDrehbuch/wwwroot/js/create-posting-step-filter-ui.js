(function (window) {
    'use strict';

    const $ = window.jQuery;

    function injectFilterUI(containerSelector) {
        const container = $(containerSelector);
        if (!container.length || container.prev('.step-filter-controls').length) {
            return; // Already injected or container not found
        }

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

    function attachFilterEventHandlers(deps) {
        if (!deps || typeof deps.renderSc2TemplateCards !== 'function') {
            console.warn('attachFilterEventHandlers: missing deps or renderSc2TemplateCards');
            return;
        }

        // Search input handler
        $(document).on('input', '.step-search-input', function () {
            const query = $(this).val().trim();
            if (window.CreatePostingTemplateBuilder && typeof window.CreatePostingTemplateBuilder.setSearchQuery === 'function') {
                window.CreatePostingTemplateBuilder.setSearchQuery(query);
                deps.renderSc2TemplateCards();
            }
        });

        // Phase tab click handler
        $(document).on('click', '.phase-tab', function () {
            const phase = $(this).data('phase').toString();

            // Update active state
            $('.phase-tab').removeClass('active');
            $(this).addClass('active');

            // Apply filter
            if (window.CreatePostingTemplateBuilder && typeof window.CreatePostingTemplateBuilder.setPhaseFilter === 'function') {
                window.CreatePostingTemplateBuilder.setPhaseFilter(phase);
                deps.renderSc2TemplateCards();
            }
        });
    }

    function initStepFilterUI(deps) {
        // Inject UI before #sc2MasterTemplateCards
        injectFilterUI('#sc2MasterTemplateCards');

        // Attach event handlers
        attachFilterEventHandlers(deps);

        // Update theme when theme changes
        if (window.getCreatePostingTheme) {
            const updateTheme = function () {
                const theme = window.getCreatePostingTheme();
                $('.step-filter-controls').attr('data-theme', theme);
            };
            // Call once and set up observer if needed
            updateTheme();
        }
    }

    window.CreatePostingStepFilterUI = {
        init: initStepFilterUI,
        injectFilterUI: injectFilterUI,
        attachFilterEventHandlers: attachFilterEventHandlers
    };

})(window);
