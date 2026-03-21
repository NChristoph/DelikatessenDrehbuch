(function (window) {
    'use strict';

    async function loadIngredientArticleRules() {
        return await window.CreatePostingDataStore.load('ingredientArticleRules');
    }

    window.CreatePostingPageData = {
        loadIngredientArticleRules: loadIngredientArticleRules
    };
})(window);
