(function (window) {
    'use strict';

    async function loadIngredientArticleRules() {
        return await window.CreatePostingDataStore.load('ingredientArticleRules');
    }

    async function loadIngredientTransforms() {
        return await window.CreatePostingDataStore.load('ingredientTransforms');
    }

    window.CreatePostingPageData = {
        loadIngredientArticleRules: loadIngredientArticleRules,
        loadIngredientTransforms: loadIngredientTransforms
    };
})(window);
