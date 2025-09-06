function ReloadIngredientList(id, recipesId) {

    var personcount = document.getElementById("personCount").value;

    
    if (!id) {
        var recipesIdsList = GetRecipesIdsList();
        $("#ingredientPartialView").load("/CreateNewMealPlan/LoadIngredientPartialView?recipesIds=" + recipesIdsList.join(";")
                                                                                        + '&personCount=' + personcount);
    } else {
        $("#" + id).load("/CreateNewMealPlan/LoadIngredientPartialView?recipesIds=" + recipesId
                                                                                    + '&personCount=' + personcount);
    }
}

function GetRecipesIdsList() {
    var recipesElements = document.getElementsByName("Recipes");
    
    return Array.from(recipesElements).map(function (el) {
        return el.title;
    });
}

function parseNumberLocale(str) {
    if (str == null) return NaN;
    const m = String(str).match(/-?\d+(?:[.,]\d+)?/);
    if (!m) return NaN;
    return Number(m[0].replace(',', '.'));
}

function ChangeRecipesQuantity() {
    const count = parseNumberLocale(document.getElementById("personCount").value) || 1;

    document.querySelectorAll('[name="IngredientContainer_SelectetRecipe"]').forEach(li => {
        const quantitySpan = li.querySelector('[name="quantity"]');
        const unitSpan = li.querySelector('span:nth-of-type(2)'); // etwas robuster

        // Fallback: falls data-original fehlt, nimm Text
        let base = parseNumberLocale(quantitySpan.dataset.original ?? quantitySpan.textContent);
        if (Number.isNaN(base)) return;

        // Sonderregel Bund
        if (unitSpan && unitSpan.textContent.trim().toLowerCase() === "bund") {
            base = base / 3;
        }

        const value = base * count;

        // Ganzzahlig → 0 Stellen, sonst 2
        const formatted = Number.isInteger(value) ? value.toFixed(0) : value.toFixed(2);

        // Wenn du Anzeige mit Komma willst:
        quantitySpan.textContent = formatted.replace('.', ',');
    });

    ChangeNutrientQuantity(count);
}

function ChangeNutrientQuantity(count) {
    var quantitysToChange = document.getElementsByName("nutrientQuantity").forEach(li => {

        let base = parseNumberLocale(li.dataset.original ?? li.textContent);
        if (Number.isNaN(base)) return;

        const value = base * count;
        
        const formatted = Number.isInteger(value) ? value.toFixed(0) : value.toFixed(2);

        li.textContent = formatted.replace('.', ',');

    });

 
}

