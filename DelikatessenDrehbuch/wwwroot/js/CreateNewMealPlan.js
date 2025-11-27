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
        const html = createMealCard(recipe,index);
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

function createMealCard(recipes, index) {
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
        <input type="hidden" 
               class="meal-plan-input" 
               name="RecipeId" 
               value="${recipes.id}" 
               data-day-index="${index}" 
               data-category="${recipes.category || ''}" />

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

function createMealPlan() {
    // 1. Alle versteckten Inputs sammeln
    const inputs = document.querySelectorAll('.meal-plan-input');
    const planData = [];

    // 2. Daten auslesen
    inputs.forEach(input => {
        // Nur hinzufügen, wenn auch wirklich eine ID da ist (Validierung)
        if (input.value && input.value !== "0") {
            planData.push({
                Index: parseInt(input.getAttribute('data-day-index')),
                RecipeId: parseInt(input.value),
                
            });
        }
    });

    // 3. Prüfen ob Daten da sind
    if (planData.length === 0) return alert("Bitte auswählen.");

    // 2. WICHTIG: In String umwandeln und encodieren!
    // JSON macht daraus: '[{"Index":1,"RecipeId":5},...]'
    const jsonString = JSON.stringify(planData);

    // Encode macht daraus: '%5B%7B%22Index%22%3A1...' (Sicher für URL)
    const encodedData = encodeURIComponent(jsonString);

    // 3. Aufrufen (GET Request)
    // Hier schickst du alles in einem Parameter namens "data"
    const personCount = document.getElementById("personCount").value;
    window.location.href = `/CreateNewMealPlan/CreateMealPlan?data=${encodedData}&personCount=${personCount}`;

   
}