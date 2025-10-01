// DOM Ready
document.addEventListener('DOMContentLoaded', () => {
    initMealPlan();
    ReloadIngredientList();
});

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

        // Zutatenliste refresh
        if (typeof ReloadIngredientList === 'function') {
            ReloadIngredientList(null, dataId);
        }
    });
}


// Rezept entfernen
function handleRemoveRecipe(btn) {
    const slot = btn.closest('.slot');
    const target = slot.querySelector('.slot-target');
    const addBtn = slot.querySelector('.add-recipe');
    const recipeId = addBtn.dataset.recipeId || 0;

    // Slot leeren
    target.innerHTML = '';
   
    // Button state zurücksetzen
    if (addBtn) {
        delete addBtn.dataset.recipeId;
        addBtn.innerHTML = '<i class="bi bi-plus-lg"></i> Hinzufügen';
    }
    btn.hidden = true;
    ReloadIngredientList(null,recipeId);
   
}

// Tag nach oben verschieben
function moveDayUp(btn) {
    const currentDay = btn.closest('.day-item');
    const prevDay = currentDay.previousElementSibling;

    if (prevDay && prevDay.classList.contains('day-item')) {
        swapDayContents(currentDay, prevDay);
        scrollToDay(prevDay);
    }
}

// Tag nach unten verschieben  
function moveDayDown(btn) {
    const currentDay = btn.closest('.day-item');
    const nextDay = currentDay.nextElementSibling;

    if (nextDay && nextDay.classList.contains('day-item')) {
        swapDayContents(currentDay, nextDay);
        scrollToDay(nextDay);
    }
}

// Tage-Inhalte tauschen (nur day-content, Header bleiben)
function swapDayContents(dayA, dayB) {
    const contentA = dayA.querySelector('.day-content');
    const contentB = dayB.querySelector('.day-content');
    const indexA = dayA.dataset.day;
    const indexB = dayB.dataset.day;

    // Inhalte tauschen
    const tempDiv = document.createElement('div');
    tempDiv.appendChild(contentA.cloneNode(true));
    contentA.innerHTML = contentB.innerHTML;
    contentB.innerHTML = tempDiv.firstChild.innerHTML;

    // data-day-index in den slot-grids korrigieren
    contentA.querySelector('.slot-grid').dataset.dayIndex = indexA;
    contentB.querySelector('.slot-grid').dataset.dayIndex = indexB;
}

// Zu einem Tag scrollen
function scrollToDay(dayEl) {
    const header = dayEl.querySelector('.day-header');
    header.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
        inline: 'nearest'
    });
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

// Finaler Aufruf
function createMealPlan() {
    const dict = getRecipeDictionary();
    const query = encodeURIComponent(JSON.stringify(dict));
    const personCount = document.getElementById('personCount')?.value || 1;

    window.location.href = `/CreateNewMealPlan/CreatedMealPlan?indexAndIds=${query}&personCount=${personCount}`;
}