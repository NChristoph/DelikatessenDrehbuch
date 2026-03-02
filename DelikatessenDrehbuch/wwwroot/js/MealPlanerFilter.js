


$(document).ready(function () {
    // Favoriten initial laden
    $("#Favoriten").load("/MyRecipes/LoadRecipesILike");

    // DataTable init (responsive + no horizontal overflow)
    new DataTable('#IngredientTable', {
        responsive: true,
        pageLength: 14,
        lengthChange: false,
        ordering: false
    });
});

function CreateIngredientChangeValue(html, tag, spawnId, bindingName) {

    var checkbox = document.getElementById(tag + html.value);
    var name = html.getAttribute("data-name");
    if (checkbox && checkbox.checked) {
        checkbox.checked = false;

        document.getElementById("span" + name + html.value)?.remove();
        document.getElementById("hidden" + name + html.value)?.remove();
    }

    const existingSpan = document.getElementById('span' + name + html.value);
    const existingHidden = document.getElementById('hidden' + name + html.value);

    if (existingSpan && existingHidden) {
        existingSpan.remove();
        existingHidden.remove();
        return;
    }

    var span = CreateSpan(name, html);
    var hidden = CreateHidden(name, html, bindingName);

    $("#" + spawnId).append(span);
    $("#" + spawnId).append(hidden);
}

function CreateSpan(name, html) {

    const element = document.createElement("span");
    element.id = 'span' + name + html.value;
    element.innerText = html.getAttribute('data-name');

    return element;
}

function CreateHidden(name, html, bindingName) {

    const hidden = document.createElement("input");
    hidden.type = "hidden";
    hidden.name = bindingName;
    hidden.value = html.value;
    hidden.id = 'hidden' + name + html.value;

    return hidden;
}

function loadMealPlanView() {
    $('#Menue').load("MyRecipes/MealPlanView");
}

function loadRecipesILike() {
    $('#Favoriten').load("MyRecipes/LoadRecipesILike");
}

function loadShoppingList() {
    window.location.href ="/CreateNewMealPlan/LoadShoppingList"
  
}