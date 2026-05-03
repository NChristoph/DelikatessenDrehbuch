(function (window) {
    'use strict';

    const $ = window.jQuery;

 

    function attachFilterEventHandlers() {
        // Search input handler
        $(document).on('input', '.step-search-input', function () {
            const query = $(this).val().trim();
            $(this).siblings('.step-search-clear').toggleClass('d-none', !query);
            if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.setSearchQuery === 'function') {
                window.MasterStepCreatorHelpers.setSearchQuery(query);
            }
        });

        // Clear search button
        $(document).on('click', '.step-search-clear', function () {
            const input = $(this).siblings('.step-search-input');
            input.val('').trigger('input').focus();
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
        attachFilterEventHandlers: attachFilterEventHandlers
    };

})(window);
