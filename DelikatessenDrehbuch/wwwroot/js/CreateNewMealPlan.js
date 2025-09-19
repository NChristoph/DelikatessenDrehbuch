
window.onload = function () {
    ReloadIngredientList();
};

function ChangeMealPlanRecipes(button, spawnId) {
    const ingredientList = document.getElementById("ingredientPartialView");
    ingredientList.innerHTML = "";

    var category = button.getAttribute("data-category");
    var recipeId = button.value || 0;
    var index = button.getAttribute("data-index");
 

    $.get("/CreateNewMealPlan/LoadRecipesPartialView", {
        recipeId: recipeId,
        category: category,
        index: index
    }, function (html) {
        var dataId = $(html).attr("data-id");
        button.value = dataId;
        $("#" + spawnId + index).html(html)
        ReloadIngredientList();
    });

}

function GetRecipeDictionaryByIndex() {
    const elements = document.querySelectorAll('[name="Recipes"]');
    const dict = {};
    elements.forEach(el => {
        const index = parseInt(el.getAttribute('data-index'));
        const id = parseInt(el.getAttribute('data-id'));
        if (!dict[index]) { dict[index] = []; }
        dict[index].push(id);
    });
    return dict;
}

function CreatedMealPlan() {
    var ids = GetRecipeDictionaryByIndex();
    let query = encodeURIComponent(JSON.stringify(ids));
    var personcount = document.getElementById("personCount").value;

    window.location.href = "/CreateNewMealPlan/CreatedMealPlan?indexAndIds=" + query + '&personCount=' + personcount;
};






// --- Verschiebe-Funktion ---
function moveDayUp(btn) {
    const day = btn.closest('.day-item');
    const prev = day?.previousElementSibling;
    if (prev && prev.classList.contains('day-item')) {
        day.parentNode.insertBefore(day, prev);
        renumberDays();
    }
}

function moveDayDown(btn) {
    const day = btn.closest('.day-item');
    const next = day?.nextElementSibling;
    if (next && next.classList.contains('day-item')) {
        day.parentNode.insertBefore(next, day);
        renumberDays();
    }
}

function renumberDays() {
    const days = document.querySelectorAll('.day-item');
    days.forEach((day, idx) => {
        const newIndex = idx + 1;
        const oldIndex = parseInt(day.getAttribute('data-day')) || newIndex;

        const label = day.querySelector('.day-label');
        if (label) label.textContent = `Tag ${newIndex}`;

        day.setAttribute('data-day', newIndex);
        day.querySelectorAll('[data-index]').forEach(el => {
            el.setAttribute('data-index', newIndex);
        });

        const prefixes = [
            'appetizer_', 'appetizerName_', 'appetizerPreperationTime_', 'appetizerDiscription_',
            'dessert_', 'dessertName_', 'dessertPreperationTime_', 'dessertDiscription_',
            'recipesContainer_', 'recipesContainerName_', 'recipesContainerPreperationTime_', 'recipesContainerDiscription_',
            'appetizerDeleteButton_', 'dessertDeleteButton_'
        ];

        const idSuffixOld = `_${oldIndex}`;
        const idSuffixNew = `_${newIndex}`;

        day.querySelectorAll('[id]').forEach(el => {
            let id = el.id;
            prefixes.forEach(p => {
                if (id.startsWith(p) && id.endsWith(idSuffixOld)) {
                    id = p + newIndex;
                }
            });
            if (id.endsWith(idSuffixOld)) {
                id = id.slice(0, -idSuffixOld.length) + idSuffixNew;
            }
            el.id = id;
        });

        day.querySelectorAll('label[for]').forEach(el => {
            let f = el.htmlFor;
            if (f && f.endsWith(idSuffixOld)) {
                el.htmlFor = f.slice(0, -idSuffixOld.length) + idSuffixNew;
            }
        });

        day.querySelectorAll('a[onclick], button[onclick]').forEach(el => {
            let h = el.getAttribute('onclick');
            if (!h) return;
            prefixes.forEach(p => {
                const find = new RegExp(p + oldIndex + '(?=[^0-9]|$)', 'g');
                h = h.replace(find, p + newIndex);
            });
            h = h.replace(new RegExp('_' + oldIndex + '(?=[^0-9]|$)', 'g'), '_' + newIndex);
            el.setAttribute('onclick', h);
        });
    });
}

