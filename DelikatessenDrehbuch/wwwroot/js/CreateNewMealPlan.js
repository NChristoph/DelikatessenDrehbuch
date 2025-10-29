// DOM Ready
document.addEventListener('DOMContentLoaded', () => {
    initMealPlan();
    ReloadIngredientList();
    LoadOverView();
});

function LoadOverView() {
    const overview = document.getElementById('overView');
    var dic = getRecipeDictionary();
    const query = encodeURIComponent(JSON.stringify(dic));
    if (overview) {
        $.get('/CreateNewMealPlan/LoadMealPlanOverviewPartialView?IndexAndIds='+query)
            .done(function (html) {
                overview.innerHTML = html;

            })
    }
};

function ChangeRecipeDay(button) {
    
    var currentIndex = button.getAttribute("data-day");
    var indexToMove = button.value;

    var otherButton = document.getElementById("changeIndex_" + indexToMove)
    otherButton.dataset.day= currentIndex;
    button.dataset.day = indexToMove;

    var currentPlace = document.getElementById("RecipeSlot_" + currentIndex);
    var placeToMove = document.getElementById("RecipeSlot_" + indexToMove);

    var currentPlace2 = document.getElementById("day_" + currentIndex);
    var placeToMove2 = document.getElementById("day_" + indexToMove);

    const current = currentPlace2.querySelector('.slot-grid');
    const toMove = placeToMove2.querySelector('.slot-grid');

    current.dataset.dayIndex = indexToMove;
    toMove.dataset.dayIndex = currentIndex;


    var htmlCurrent = document.getElementById("RecipeSlot_" + currentIndex).innerHTML;
    var htmlToMove = document.getElementById("RecipeSlot_" + indexToMove).innerHTML;
   
    var htmlCurrent2 = document.getElementById("day_" + currentIndex).innerHTML;
    var htmlToMove2 = document.getElementById("day_" + indexToMove).innerHTML;

    currentPlace.innerHTML = htmlToMove;
    placeToMove.innerHTML = htmlCurrent;
    currentPlace2.innerHTML = htmlToMove2;
    placeToMove2.innerHTML = htmlCurrent2;

    const dict = getRecipeDictionaryFinaly();
    const query = encodeURIComponent(JSON.stringify(dict));
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    fetch(`/CreateNewMealPlan/SaveDayInSession?indexAndIds=${query}`, {
        method: 'POST',
        headers: {
            ...(token ? { 'RequestVerificationToken': token } : {})
        }

    });

}

function initMealPlan() {
    // Event Delegation für alle Buttons
    document.addEventListener('click', (e) => {
        if (e.target.closest('.add-recipe')) {
            handleAddRecipe(e.target.closest('.add-recipe'));
        }
        if (e.target.closest('.remove-recipe')) {
            handleRemoveRecipe(e.target.closest('.remove-recipe'));
        }
    });
}

// Rezept hinzufügen/ändern
function handleAddRecipe(btn) {
    const slot = btn.closest('.slot');
    const grid = slot.closest('.slot-grid');
    const dayIndex = grid.dataset.dayIndex;
    const category = btn.dataset.category;
    const recipeId = btn.dataset.recipeId || 0;
    btn.hidden=true;

    $.get("/CreateNewMealPlan/LoadRecipesPartialView", {
        recipeId: recipeId,
        category: category,
        index: dayIndex
    }).done(function (html) {

        const target = slot.querySelector('.slot-target');
        target.innerHTML = html;

        // data-id aus dem zurückgegebenen HTML holen (funktioniert auch bei mehreren Root-Knoten)
        const doc = new DOMParser().parseFromString(html, "text/html");
        const dataId =
            doc.body.firstElementChild?.getAttribute("data-id") ||
            doc.body.querySelector('[data-id]')?.getAttribute("data-id") ||
            recipeId;

        

        // Button-Status aktualisieren
        const addBtn = slot.querySelector('.add-recipe');
        if (addBtn) {
            addBtn.dataset.recipeId = dataId || 0;
            addBtn.innerHTML = '<i class="bi bi-arrow-repeat"></i> Ändern';
        }
        const removeBtn = slot.querySelector('.remove-recipe');
        if (removeBtn) removeBtn.hidden = false;

        ReloadIngredientList();
        LoadOverView();
        btn.hidden = false;
    });
}


// Rezept entfernen
function handleRemoveRecipe(btn) {
    const slot = btn.closest('.slot');
    const target = slot.querySelector('.slot-target');
    const grid = slot.closest('.slot-grid');
    const dayIndex = grid.dataset.dayIndex;
    const addBtn = slot.querySelector('.add-recipe');
    const recipeId = addBtn.dataset.recipeId || 0;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    fetch(`/CreateNewMealPlan/EditeSession?recipeId=${encodeURIComponent(recipeId)}&dayIndex=${encodeURIComponent(dayIndex)}&idToSave=0`, {
        method: 'POST',
        headers: {
            ...(token ? { 'RequestVerificationToken': token } : {})
        }

    });
    // Slot leeren
    target.innerHTML = '';

    // Button state zurücksetzen
    if (addBtn) {
        delete addBtn.dataset.recipeId;
        addBtn.innerHTML = '<i class="bi bi-plus-lg"></i> Hinzufügen';
    }
    btn.hidden = true;
    ReloadIngredientList();
    LoadOverView();
}



// Rezept-Dictionary für Finalaufruf erstellen
function getRecipeDictionary() {
    const dict = {};

    document.querySelectorAll('.day-item').forEach(day => {
        const dayIndex = parseInt(day.dataset.day);
        const recipes = [];

        // Alle Rezepte des Tages sammeln
        day.querySelectorAll('[name="Recipes"]').forEach(recipe => {
            const id = parseInt(recipe.dataset.id);
            if (!isNaN(id)) recipes.push(id);
        });

        dict[dayIndex] = recipes;
    });

    return dict;
}
function getRecipeDictionaryFinaly() {

    // alle Tages-Container durchgehen
    const dict = {};
   
    document.querySelectorAll('[name="day-item"]').forEach(day => {
        const dayIndex = parseInt(day.dataset.day);
        const recipes = [];

        // Alle Rezepte des Tages sammeln
        day.querySelectorAll('input[name="Recipe"]').forEach(input => {
            const id = parseInt(input.value, 10);
            if (!isNaN(id)) recipes.push(id);
        });

        dict[dayIndex] = recipes;
    });

    return dict;


}

// Finaler Aufruf
function createMealPlan() {

    const dict = getRecipeDictionaryFinaly();
    const query = encodeURIComponent(JSON.stringify(dict));
    const personCount = document.getElementById('personCount')?.value || 1;

    window.location.href = `/CreateNewMealPlan/CreatedMealPlan?indexAndIds=${query}&personCount=${personCount}`;
} 