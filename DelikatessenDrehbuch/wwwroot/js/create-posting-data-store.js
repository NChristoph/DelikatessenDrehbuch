(function (window) {
    'use strict';

    const cache = new Map();
    const loadingPromises = new Map();

    function validateShape(key, data) {
        if (data == null) {
            throw new Error('Missing data for ' + key);
        }

        // Validation removed - old step system deleted
        return data;
    }

    function load(key, options) {
        const forceReload = !!(options && options.forceReload);

        const cacheKey = key;

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
