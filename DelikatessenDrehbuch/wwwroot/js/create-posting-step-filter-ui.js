(function (window) {
    'use strict';

    const $ = window.jQuery;

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

    function attachFilterEventHandlers() {
        // Search input handler
        $(document).on('input', '.step-search-input', function () {
            const query = $(this).val().trim();
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.setSearchQuery === 'function') {
                window.MasterStepCreatorHelpers.setSearchQuery(query);
            }
        });

        // Phase tab click handler
        $(document).on('click', '.phase-tab', function () {
            const phase = $(this).data('phase').toString();

            // Update active state
            $('.phase-tab').removeClass('active');
            $(this).addClass('active');

            // Apply filter
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.setPhaseFilter === 'function') {
                window.MasterStepCreatorHelpers.setPhaseFilter(phase);
        }
        });
    }

    function initStepFilterUI() {
        // Inject UI before #sc2MasterTemplateCards
        injectFilterUI('#sc2MasterTemplateCards');

        // Attach event handlers
        attachFilterEventHandlers();

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
