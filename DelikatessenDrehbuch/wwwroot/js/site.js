function ReloadIngredientList(id, recipesId,ids) {

    var personcount = document.getElementById("personCount").value;


    if (!id) {
      
        var recipesIdsList = GetRecipesIdsList();
        $("#ingredientPartialView").load("/CreateNewMealPlan/LoadIngredientPartialView?recipesIds=" + recipesIdsList.join(";")
            + '&personCount=' + personcount);
    } else {
        if (ids)
            recipesId = ids;
            
        $("#" + id).load("/CreateNewMealPlan/LoadIngredientPartialView?recipesIds=" + recipesId
            + '&personCount=' + personcount);
    }
}

document.addEventListener('DOMContentLoaded', function () {
    var bottom = document.getElementById('bottomActions');
    var arrow = document.getElementById('collapseArrow');

    // 1️⃣  Zustand aus dem localStorage holen
    var isExpanded = localStorage.getItem('bottomActionsExpanded') === 'true';

    // 2️⃣  Collapse initial setzen
    var collapse = new bootstrap.Collapse(bottom, {
        toggle: false
    });

    if (isExpanded) {
        collapse.show();
        arrow.classList.remove('bi-chevron-down');
        arrow.classList.add('bi-chevron-up');
    } else {
        collapse.hide();
        arrow.classList.remove('bi-chevron-up');
        arrow.classList.add('bi-chevron-down');
    }

    // 3️⃣  Beim Einklappen Zustand speichern
    bottom.addEventListener('hidden.bs.collapse', function () {
        arrow.classList.remove('bi-chevron-up');
        arrow.classList.add('bi-chevron-down');
        localStorage.setItem('bottomActionsExpanded', 'false');
    });

    // 4️⃣  Beim Ausklappen Zustand speichern
    bottom.addEventListener('shown.bs.collapse', function () {
        arrow.classList.remove('bi-chevron-down');
        arrow.classList.add('bi-chevron-up');
        localStorage.setItem('bottomActionsExpanded', 'true');
    });
});

function GetRecipesIdsList() {
    var recipesElements = document.getElementsByName("Recipes");

    return Array.from(recipesElements).map(function (el) {
        return el.getAttribute('data-id');
    });
}

function parseNumberLocale(str) {
    if (str == null) return NaN;
    const m = String(str).match(/-?\d+(?:[.,]\d+)?/);
    if (!m) return NaN;
    return Number(m[0].replace(',', '.'));
}

function UnCheckAllCheckboxes(checkboxes) {
    checkboxes.forEach(cb => {
        var checkbox = document.getElementById(cb);
        if (checkbox.checked)
            checkbox.checked = false;
    });
};

function ChangeRecipesQuantity(person) {
    // Wenn kein Parameter übergeben → Wert aus Input nehmen
    const count = parseNumberLocale(document.getElementById("personCount").value) || 1;
    const multiplier = (typeof person !== "undefined") ? person : count;

    document.querySelectorAll('[name="IngredientContainer_SelectetRecipe"]').forEach(li => {
        const quantitySpan = li.querySelector('[name="quantity"]');
        const unitSpan = li.querySelector('span:nth-of-type(2)');
        

        let base = parseNumberLocale(quantitySpan.dataset.original ?? quantitySpan.textContent);
        if (Number.isNaN(base)) return;

        // Sonderregel Bund
        if (unitSpan && unitSpan.textContent.trim().toLowerCase() === "bund") {
            base = base / 3;
        }

        const value = base * multiplier;

        const formatted = Number.isInteger(value) ? value.toFixed(0) : value.toFixed(2);

        // Anzeige mit Komma statt Punkt
        quantitySpan.textContent = formatted.replace('.', ',');
    });

    // Weitergabe des Multiplikators (Person oder Count) an die nächste Funktion
    ChangeNutrientQuantity(multiplier);
}


function ChangeNutrientQuantity(count) {
    var quantitysToChange = document.getElementsByName("nutrientQuantity").forEach(li => {

        let base = parseNumberLocale(li.dataset.original ?? li.textContent);
        if (Number.isNaN(base)) return;

        const value = base * count;

        const formatted = Number.isInteger(value) ? value.toFixed(0) : value.toFixed(2);

        li.textContent = formatted.replace('.', ',');

    });

    changeKalorien(count);
}
function changeKalorien(count) {
    var kalorien = document.getElementById("calories");
    let base = parseNumberLocale(kalorien.dataset.original ?? kalorien.textContent);
    if (Number.isNaN(base)) return;
    const value = base * count;
    const formatted = value.toFixed(0);
    kalorien.textContent = "Kalorien gesamt : " + formatted;
}

