(function (window) {
    'use strict';

    function showToast(message, options) {
        const settings = options || {};
        const selector = settings.selector || '#creatorToast';
        const duration = typeof settings.duration === 'number' ? settings.duration : 1200;
        const state = settings.state || null;
        const stateKey = settings.stateKey || 'toastVisible';

        if (state && stateKey) {
            state[stateKey] = true;
        }

        window.CreatePostingUtils.showTransientMessage(message, {
            selector: selector,
            duration: duration
        });

        if (state && stateKey) {
            window.setTimeout(function () {
                state[stateKey] = false;
            }, duration);
        }
    }

    function reportError(context, error, options) {
        const settings = options || {};
        window.CreatePostingUtils.reportError(context, error, {
            prefix: settings.prefix || 'CreatePostingPage',
            selector: settings.selector || '#creatorToast',
            duration: settings.duration || 3600
        });
    }

    window.CreatePostingFeedback = {
        showToast: showToast,
        reportError: reportError
    };
})(window);
