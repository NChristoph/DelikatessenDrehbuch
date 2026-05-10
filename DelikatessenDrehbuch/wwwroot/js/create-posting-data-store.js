(function (window) {
    'use strict';

    const cache = new Map();
    const loadingPromises = new Map();

    function validateShape(key, data) {
        if (data == null) {
            throw new Error('Missing data for ' + key);
        }

        if (key === 'masterSteps' && !Array.isArray(data.master_steps)) {
            throw new Error('master_steps.json has an invalid format.');
        }

        if (key === 'recipeCategoryScoring' && !Array.isArray(data.categories)) {
            throw new Error('recipe_category_scoring.json has an invalid format.');
        }

        return data;
    }

    function load(key, options) {
        const forceReload = !!(options && options.forceReload);

        // For masterSteps, include language in the cache key
        let cacheKey = key;
        if (key === 'masterSteps' && options && options.language) {
            const lang = window.CreatePostingUtils.resolveLangKey(options.language);
            cacheKey = key + '.' + lang;
        }

        if (!forceReload && cache.has(cacheKey)) {
            return Promise.resolve(cache.get(cacheKey));
        }

        if (!forceReload && loadingPromises.has(cacheKey)) {
            return loadingPromises.get(cacheKey);
        }

        const promise = window.CreatePostingUtils.fetchJson(key, options)
            .then(function (data) {
                const validated = validateShape(key, data);
                cache.set(cacheKey, validated);
                loadingPromises.delete(cacheKey);
                return validated;
            })
            .catch(function (error) {
                loadingPromises.delete(cacheKey);
                throw error;
            });

        loadingPromises.set(cacheKey, promise);
        return promise;
    }

    function loadMany(keys, options) {
        const uniqueKeys = Array.from(new Set(keys || []));
        return Promise.all(uniqueKeys.map(function (key) {
            return load(key, options);
        })).then(function (values) {
            const result = {};
            uniqueKeys.forEach(function (key, index) {
                result[key] = values[index];
            });
            return result;
        });
    }

    function get(key) {
        if (!cache.has(key)) {
            throw new Error('CreatePostingDataStore: data not loaded for key ' + key);
        }
        return cache.get(key);
    }

    function peek(key) {
        return cache.has(key) ? cache.get(key) : null;
    }

    function has(key) {
        return cache.has(key);
    }

    function clear(key) {
        if (typeof key === 'string' && key) {
            cache.delete(key);
            loadingPromises.delete(key);
            return;
        }

        cache.clear();
        loadingPromises.clear();
    }

    window.CreatePostingDataStore = {
        load: load,
        loadMany: loadMany,
        get: get,
        peek: peek,
        has: has,
        clear: clear
    };
})(window);
