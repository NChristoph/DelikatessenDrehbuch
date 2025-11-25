const { data } = require("jquery");

// DOM Ready
document.addEventListener('DOMContentLoaded', () => {
    initMealPlan();
    ReloadIngredientList();
    LoadOverView();
});

async function ChangeOrAddRecipe(button) {
    const elements= document.getElementsByName("RecipeId");
    const ids = Array.from(elements).map(el => parseInt(el.value));
    const name = button.getAttribute("data-name");
    const index = button.getAttribute("data-index");
    const category=button.getAttribute("data-category")
    const slotId = name + index;
    const slot = document.getElementById(slotId);

    button.innerHTML = "Ändern";

    if (!slot) {
        console.error(`Slot mit ID "${slotId}" nicht gefunden`);
        return;
    }

    try {
        // Loading-Indikator anzeigen
        slot.innerHTML = `
            <div class="d-flex justify-content-center align-items-center" style="min-height: 200px;">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Lädt...</span>
                </div>
            </div>
        `;
        const params = new URLSearchParams();

        // Füge jede ID als separates usedIds=X hinzu
        ids.forEach(id => {
            params.append('usedIds', id.toString());
        });

        // Füge die Kategorie hinzu
        params.append('category', category);

        // Der resultierende Query String ist nun: usedIds=100&usedIds=200&usedIds=300&category=Hauptgericht
        const queryString = params.toString();
        // Rezept laden
        const response = await fetch(`/CreateNewMealPlan/LoadRecipeInMealPlaner?${queryString}`);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const recipe = await response.json();

        // HTML erstellen und einfügen
        const html = createMealCard(recipe);
        slot.innerHTML = html;

        // Bootstrap Dropdown initialisieren
        const dropdown = slot.querySelector('[data-bs-toggle="dropdown"]');
        if (dropdown && typeof bootstrap !== 'undefined') {
            new bootstrap.Dropdown(dropdown);
        }

    } catch (error) {
        console.error('Fehler beim Laden des Rezepts:', error);

        // Benutzerfreundliche Fehlermeldung
        slot.innerHTML = `
            <div class="alert alert-danger d-flex justify-content-between align-items-center">
                <span>Rezept konnte nicht geladen werden</span>
                <button class="btn btn-sm btn-outline-danger" 
                        onclick="ChangeOrAddRecipe(this)"
                        data-id="${id}"
                        data-name="${name}"
                        data-index="${index}">
                    Erneut versuchen
                </button>
            </div>
        `;
    }
}




function createMealCard(recipes) {
    // Bild-HTML vorbereiten
    let imageHtml;
    if (recipes.imagePath) {
        imageHtml = `
            <div class="dropdown">
                <picture data-bs-toggle="dropdown">
                    <img src="${recipes.imagePath}" alt="${recipes.name}" />
                </picture>
                <ul class="dropdown-menu w-100">
                    <li>${recipes.description || ''}</li>
                </ul>
            </div>
        `;
    } else {
        imageHtml = `<div class="ph" style="aspect-ratio:3/2">Kein Bild</div>`;
    }

    // Chips sammeln
    let chipsHtml = '';

    if (recipes.preparationTime) {
        chipsHtml += `<span class="chip">${recipes.preparationTime}&nbsp;min</span>`;
    }

    if (recipes.likeCount > 0) {
        chipsHtml += `<span class="chip">❤ ${recipes.likeCount}</span>`;
    }

    chipsHtml += `<span class="chip">Beschreibung öffnen</span>`;

    // Komplettes HTML zusammenbauen
    return `
        <div class="recipe-media">
            ${imageHtml}
            <div class="recipe-chip">
                ${chipsHtml}
            </div>
        </div>
        <div class="recipe-body">
            <h3 class="recipe-title">${recipes.name}</h3>
        </div>
    `;
}

function LoadOverView() {
    const overview = document.getElementById('overView');
    var dic = getRecipeDictionary();
    const query = encodeURIComponent(JSON.stringify(dic));
    if (overview) {
        $.get('/CreateNewMealPlan/LoadMealPlanOverviewPartialView?IndexAndIds=' + query)
            .done(function (html) {
                overview.innerHTML = html;

            })
    }
};

function ChangeRecipeDay(button) {

    var currentIndex = button.getAttribute("data-day");
    var indexToMove = button.value;

    var otherButton = document.getElementById("changeIndex_" + indexToMove)
    otherButton.dataset.day = currentIndex;
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
    btn.hidden = true;

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
    fetch(`/CreateNewMealPlan/UpdateRecipeDictInDB?recipeId=${encodeURIComponent(recipeId)}&dayIndex=${encodeURIComponent(dayIndex)}`, {
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