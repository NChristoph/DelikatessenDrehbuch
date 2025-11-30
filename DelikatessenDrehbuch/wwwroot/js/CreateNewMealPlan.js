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
                        data-id="${recipe.id}"
                        data-name="${name}"
                        data-index="${index}">
                    Erneut versuchen
                </button>
            </div>
        `;
    }

    SaveMealPlanToDb();
}

async function SaveMealPlanToDb() {
    // 1. Alle Inputs mit der Klasse 'meal-plan-input' sammeln
    const inputs = document.querySelectorAll('.meal-plan-input');

    // Das Objekt, das später zum Dictionary<int, List<int>> wird
    // Struktur: { "1": [10, 12, 15], "2": [99, 100] }
    const groupedData = {};

    // 2. Durch die Inputs iterieren
    inputs.forEach(input => {
        const recipeId = parseInt(input.value);
        const dayIndex = parseInt(input.getAttribute('data-day-index'));

        // Nur echte Rezept-IDs aufnehmen (ID > 0)
        if (recipeId && recipeId > 0) {

            // Falls der Key (Tag) noch nicht existiert, erstelle leeres Array
            if (!groupedData[dayIndex]) {
                groupedData[dayIndex] = [];
            }

            // Rezept-ID zum Array des jeweiligen Tages hinzufügen
            groupedData[dayIndex].push(recipeId);
        }
    });

    // 3. Prüfen, ob Daten vorhanden sind
    if (Object.keys(groupedData).length === 0) {
        alert("Der Plan ist leer. Bitte wähle Gerichte aus.");
        return;
    }

    try {
        // 4. Senden an den Controller
        const response = await fetch('/CreateNewMealPlan/SaveMealPlanInDb', { // Passe den Controller-Namen an
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                // Falls du Anti-Forgery nutzt (empfohlen):
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
            },
            body: JSON.stringify(groupedData)
        });

      
    } catch (error) {
        console.error('Error:', error);
        alert("Ein Netzwerkfehler ist aufgetreten.");
    }
}

function createMealCard(recipes, index) {
    // 1. Prefix ermitteln (damit der "Ändern" Button weiß, wer er ist)
    // Das Mapping muss zu deinen IDs im HTML passen (apperetizer_, main_, dessert_)
    let namePrefix = "main_";
    if (recipes.category === "Vorspeise") namePrefix = "apperetizer_";
    else if (recipes.category === "Dessert") namePrefix = "dessert_";

    // 2. Bild-HTML vorbereiten (Mit Fallback, falls kein Bild da ist)
    let imageHtml;
    if (recipes.imagePath) {
        imageHtml = `<img src="${recipes.imagePath}" alt="${recipes.name}" loading="lazy" />`;
    } else {
        // Leerer Zustand Icon
        imageHtml = `
            <div class="empty-state" style="height:100%; display:flex; align-items:center; justify-content:center; background:#f9f9f9; color:#999;">
                <i class="bi bi-image fs-1"></i>
            </div>`;
    }

    // 3. Chips sammeln (Zeit & Likes)
    let chipsHtml = '';
    if (recipes.preparationTime) {
        chipsHtml += `<span class="meal-badge"><i class="bi bi-clock"></i> ${recipes.preparationTime} min</span>`;
    }
    if (recipes.likeCount > 0) {
        chipsHtml += `<span class="meal-badge ms-1"><i class="bi bi-heart-fill text-danger"></i> ${recipes.likeCount}</span>`;
    }

    // 4. Beschreibung kürzen (optional, falls vorhanden)
    let descHtml = '';
    if (recipes.description) {
        // Einfache Methode um HTML Tags zu entfernen für Vorschau, oder roh lassen
        descHtml = `<p class="text-muted small text-truncate" style="max-width:300px; margin:0 auto;">${recipes.description}</p>`;
    }

    // 5. Das neue HTML zusammenbauen (Passend zum Tab-Design!)
    // Wir bauen hier das Innere von <div id="main_1"> nach
    return `
        <input type="hidden" 
               class="meal-plan-input" 
               name="RecipeId" 
               value="${recipes.id}" 
               data-day-index="${index}" 
               data-category="${recipes.category || ''}" />

        <div class="meal-img-wrapper">
            ${imageHtml}
            
            <div class="meal-chips">
                ${chipsHtml}
            </div>
        </div>

        <div class="meal-body">
            <h5 class="meal-title">${recipes.name}</h5>
            ${descHtml}

            <div class="meal-actions">
                <button class="btn btn-change"
                        data-name="${namePrefix}"
                        data-index="${index}"
                        data-id="${recipes.id}"
                        data-category="${recipes.category || ''}"
                        onclick="ChangeOrAddRecipe(this)">
                    <i class="bi bi-arrow-repeat me-1"></i> Ändern
                </button>
            </div>
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