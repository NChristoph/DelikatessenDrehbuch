function ReloadIngredientList() {

    $("#ingredientPartialView").load("/CreateNewMealPlan/LoadIngredientPartialView"); 

}

function LoadIngredientPerDay(id,index) {
    $("#" + id).load("/CreateNewMealPlan/GetIngredientPerDayPartialView?index="+index);
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

function ChangeRecipesQuantity(input) {

    //var multipler = input.value;
    //var quantity = document.getElementsByName("quantity").forEach(x => {
    //    var base = parseNumberLocale(x.dataset.original);
    //    var value = base * multipler;
    //    let formatted;

    //    if (value % 1 === 0) {
    //        // Ganze Zahl → ohne Nachkommastellen
    //        formatted = value.toString();
    //    } else {
    //        // Hat Nachkommastellen → auf 2 begrenzen
    //        formatted = value.toFixed(2).replace('.', ',');
    //    }

    //    x.textContent = formatted;
    //});
 

   // ChangeNutrientQuantity(multiplier);
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

