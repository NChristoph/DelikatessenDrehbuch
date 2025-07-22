function ReloadIngredientList(id, recipesId) {
   
   
    if (!id) {
        var recipesIdsList = GetRecipesIdsList();
        $("#ingredientPartialView").load("/CreateNewMealPlan/LoadIngredientPartialView?recipesIds=" + recipesIdsList.join(";"));
    } else {
        
        $("#" + id).load("/CreateNewMealPlan/LoadIngredientPartialView?recipesIds=" + recipesId);
    }
}

function GetRecipesIdsList() {
    var recipesElements = document.getElementsByName("Recipes");
    
    return Array.from(recipesElements).map(function (el) {
        return el.title;
    });
}