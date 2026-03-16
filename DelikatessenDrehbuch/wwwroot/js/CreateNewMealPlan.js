/* ===========================================================
   CreateNewMealPlan.js
   Logik für das Laden, Ändern und Speichern von Rezepten
   =========================================================== */
function resolveMealPlanThemeColors(theme) {
    const themeKey = (theme || '').toLowerCase();
    const themeMap = {
        color: { accent: '#2D4F1E', contrast: '#ffffff' },
        black: { accent: '#ffffff', contrast: '#1A1A1A' },
        white: { accent: '#1A1A1A', contrast: '#ffffff' },
        rose: { accent: '#FFB6C1', contrast: '#ffffff' },
        lavender: { accent: '#A78BFA', contrast: '#ffffff' }
    };

    return themeMap[themeKey] || { accent: '#770f0f', contrast: '#ffffff' };
}

function applyMealPlanThemeFromProfile() {
    const theme = localStorage.getItem('profile_theme');
    const colors = resolveMealPlanThemeColors(theme);
    const root = document.documentElement;
    root.style.setProperty('--mealplan-accent', colors.accent);
    root.style.setProperty('--mealplan-accent-soft', colors.accent);
    root.style.setProperty('--mealplan-accent-contrast', colors.contrast);
}

document.addEventListener('DOMContentLoaded', applyMealPlanThemeFromProfile);
function AddRecipe(button, index) {

    var jsonString = button.getAttribute("data-json");
    var recipe = JSON.parse(jsonString);
    var html = createMealCard(recipe, index);
    let namePrefix = "main_";

    if (recipe.category === "Vorspeise") namePrefix = "apperetizer_";
    else if (recipe.category === "Dessert") namePrefix = "dessert_";

    updateMiniCard(index, recipe.category, recipe);

    var slot = document.getElementById(namePrefix + index);
    slot.innerHTML = html;

    SaveMealPlanToDb();
}

function createEmptyMealCard(category, index, namePrefix) {
    return `
        <div class="empty-state">
            <i class="bi bi-plus-circle display-1 mb-3 text-secondary opacity-25"></i>
            <h5 class="text-muted">Kein Gericht</h5>
            <button class="btn btn-sm mealplan-accent-bg mt-2"
                    data-name="${namePrefix}"
                    data-index="${index}"
                    data-id="0"
                    data-category="${category}"
                    onclick="ChangeOrAddRecipe(this)">
                + Hinzufügen
            </button>
        </div>
    `;
}

function resetMiniCard(dayIndex, category) {
    const desktopId = `mini_${dayIndex}_${category}`;
    const mobileId = `mini_mobile_${dayIndex}_${category}`;
    const targetIds = [desktopId, mobileId];

    targetIds.forEach(id => {
        const item = document.getElementById(id);
        if (!item) return;

        const imgEl = item.querySelector('.ov-img');
        const placeholderHtml = '<div class="ov-img d-flex align-items-center justify-content-center small text-muted bg-light"><i class="bi bi-egg"></i></div>';
        if (imgEl) {
            if (imgEl.tagName === 'IMG') {
                imgEl.outerHTML = placeholderHtml;
            } else {
                imgEl.outerHTML = placeholderHtml;
            }
        }

        const titleEl = item.querySelector('.ov-title');
        if (titleEl) {
            titleEl.innerText = '- Leer -';
            titleEl.classList.remove('text-truncate', 'd-block');
            titleEl.classList.add('text-muted', 'fst-italic', 'small');
        }
    });
}

function ClearMealSlot(button) {
    const name = button.getAttribute("data-name");
    const index = button.getAttribute("data-index");
    const category = button.getAttribute("data-category");
    const slotId = `${name}${index}`;
    const slot = document.getElementById(slotId);

    if (!slot) return;

    slot.innerHTML = createEmptyMealCard(category, index, name);
    resetMiniCard(index, category);
    SaveMealPlanToDb();
}

async function triggerSearch(category) {
    var content = document.getElementById("SearchOutPut_" + category);
    var query = document.getElementById(category).value;
    var dayIndex = document.getElementById("dayIndex_" + category).value;
    var categoryInput = document.getElementById("category_" + category).value;
    let url = `/CreateNewMealPlan/GetRecipesByQuery?query=${encodeURIComponent(query)}&dayIndex=${dayIndex}&category=${categoryInput}`

    const response = await fetch(url);

    if (response.ok) {
        const html = await response.text();
        content.innerHTML = html;
    }
    else {
        content.innerHTML = '<div class="text-danger text-center mt-3">Fehler beim Laden.</div>';
    }
}

async function GetOptionalRecipe(button) {

    const index = button.getAttribute("data-index");   // z.B. "1" (Tag)
    const category = button.getAttribute("data-category"); // z.B. "Hauptspeise"

    const id = "offcanvasDay_" + category;
    var canvas = document.getElementById(id);
    var body = canvas.querySelector('.offcanvas-body');

    try {


        let url = `/CreateNewMealPlan/LoadSearchImput?category=${encodeURIComponent(category)}&dayIndex=${index}`;

        const response = await fetch(url);

        if (response.ok) {
            // Wir bekommen jetzt HTML text zurück, kein JSON!
            const html = await response.text();
            body.innerHTML = html;
            var indexToChange = body.querySelector("#dayIndex_"+category);
            indexToChange.value = index;
        } else {
            body.innerHTML = '<div class="text-danger text-center mt-3">Fehler beim Laden.</div>';
        }

        var second = body.querySelector("#SearchOutPut_" + category);
        let url2 = `/CreateNewMealPlan/LoadRecipesSearch?category=${encodeURIComponent(category)}&dayIndex=${index}`;
        const res = await fetch(url2);

        if (res.ok) {
            // Wir bekommen jetzt HTML text zurück, kein JSON!
            const html2 = await res.text();
            second.innerHTML = html2;
        } else {
            second.innerHTML = '<div class="text-danger text-center mt-3">Fehler beim Laden.</div>';
        }

    } catch (error) {
        console.error(error);
        body.innerHTML = '<div class="text-danger text-center mt-3">Netzwerkfehler.</div>';
    }

}

async function ChangeOrAddRecipe(button) {
    const elements = document.getElementsByName("RecipeId");
    const ids = Array.from(elements).map(el => parseInt(el.value));

    const name = button.getAttribute("data-name");     // z.B. "main_"
    const index = button.getAttribute("data-index");   // z.B. "1" (Tag)
    const category = button.getAttribute("data-category"); // z.B. "Hauptspeise"

    const slotId = name + index;
    const slot = document.getElementById(slotId);

    // Loading State im Button
    const originalText = button.innerHTML;
    button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';
    button.disabled = true;

    if (!slot) {
        console.error(`Slot mit ID "${slotId}" nicht gefunden`);
        return;
    }

    try {
        const params = new URLSearchParams();
        ids.forEach(id => params.append('usedIds', id.toString()));
        params.append('category', category);

        const response = await fetch(`/CreateNewMealPlan/LoadRecipeInMealPlaner?${params.toString()}`);

        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const recipe = await response.json();

        // 1. GROSSE KARTE AKTUALISIEREN
        // Wir nutzen jetzt exakt das gleiche Layout wie im C# Helper
        const html = createMealCard(recipe, index);
        slot.innerHTML = html;

        // 2. MINI KARTE (SIDEBAR) AKTUALISIEREN
        updateMiniCard(index, category, recipe);

    } catch (error) {
        console.error('Fehler:', error);
        alert("Fehler beim Laden des Rezepts.");
        button.innerHTML = originalText; // Reset bei Fehler
        button.disabled = false;
    } finally {
        // Auto-Save
        SaveMealPlanToDb();
    }
}

// --- 1. HTML Generator für die große Karte (Muss 1:1 wie C# RenderRecipeCard aussehen) ---
// --- 1. HTML Generator für die große Karte ---
function createMealCard(recipes, index) {
    let namePrefix = "main_";
    if (recipes.category === "Vorspeise") namePrefix = "apperetizer_";
    else if (recipes.category === "Dessert") namePrefix = "dessert_";

    // Bild Logik
    let imageHtml;
    if (recipes.imagePath) {
        imageHtml = `<img src="${recipes.imagePath}" alt="${recipes.name}" loading="lazy" style="width:100%; height:100%; object-fit:cover;" />`;
    } else {
        imageHtml = `<div class="empty-state"><i class="bi bi-image fs-1 mb-2"></i> Kein Bild</div>`;
    }

    // Chips
    let chipsHtml = '';
    if (recipes.preparationTime) {
        chipsHtml += `<span class="meal-badge"><i class="bi bi-clock"></i> ${recipes.preparationTime} min</span>`;
    }
    if (recipes.likeCount > 0) {
        chipsHtml += `<span class="meal-badge ms-1"><i class="bi bi-heart-fill text-danger"></i> ${recipes.likeCount}</span>`;
    }

    let descHtml = '';
    if (recipes.description) {
        descHtml = `<p class="text-muted small text-truncate" style="max-width:300px; margin:0 auto;">${recipes.description}</p>`;
    }

    // Eindeutige ID für den Offcanvas-Trigger
    // Wichtig: Diese ID muss mit dem existierenden Offcanvas übereinstimmen!
    const offcanvasId = `offcanvasDay_${recipes.category}`;

    return `
        <input type="hidden" class="meal-plan-input" name="RecipeId" value="${recipes.id}" data-day-index="${index}" data-category="${recipes.category || ''}" />
        
        <div class="meal-img-wrapper">
            ${imageHtml}
            <div class="meal-chips">
                ${chipsHtml}
            </div>
        </div>

        <div class="meal-body">
            <h5 class="meal-title text-truncate">${recipes.name}</h5>
            ${descHtml}

            <div class="meal-actions d-flex justify-content-center gap-2">
                
                <button class="btn btn-change"
                        type="button"
                        data-name="${namePrefix}"
                        data-index="${index}"
                        data-id="${recipes.id}"
                        data-category="${recipes.category || ''}"
                        onclick="ChangeOrAddRecipe(this)"
                        title="Zufälliges anderes Gericht">
                    <i class="bi bi-arrow-repeat"></i> Ändern
                </button>

              

                <button class="btn btn-change"
                        type="button"
                        data-bs-toggle="offcanvas"
                        data-bs-target="#${offcanvasId}"
                        data-name="${namePrefix}"
                        data-index="${index}"
                        data-id="${recipes.id}"
                        data-category="${recipes.category || ''}"
                        aria-controls="${offcanvasId}"
                        title="Gezielt suchen"
                        onclick="GetOptionalRecipe(this)">
                    <i class="bi bi-search"></i> Ersetzen durch...
                </button>

                <button class="btn btn-change"
                        type="button"
                        data-name="${namePrefix}"
                        data-index="${index}"
                        data-category="${recipes.category || ''}"
                        onclick="ClearMealSlot(this)"
                        title="Slot leeren">
                    <i class="bi bi-x-circle"></i> Leeren
                </button>

            </div>
        </div>
    `;
}

// --- 2. Update Logik für die Sidebar (Mini Items) ---
// --- 2. Update Logik für BEIDE Sidebars (Desktop & Mobile) ---
function updateMiniCard(dayIndex, category, recipe) {
    // Wir definieren die IDs für beide Orte
    // (Stelle sicher, dass du im HTML unterschiedliche IDs vergibst, siehe Schritt 2 unten)
    const desktopId = `mini_${dayIndex}_${category}`;       // z.B. mini_1_Hauptspeise
    const mobileId = `mini_mobile_${dayIndex}_${category}`; // z.B. mini_mobile_1_Hauptspeise

    // Array mit beiden IDs, um durchzuloopen
    const targetIds = [desktopId, mobileId];

    targetIds.forEach(id => {
        const item = document.getElementById(id);

        if (item) {
            // Bild aktualisieren
            const imgEl = item.querySelector('.ov-img');
            if (imgEl) {
                if (imgEl.tagName === 'IMG') {
                    // Wenn schon ein Bild da war -> src tauschen
                    imgEl.src = recipe.imagePath || '';
                } else {
                    // Wenn vorher ein Platzhalter-Div da war -> wir müssen es durch ein IMG ersetzen
                    if (recipe.imagePath) {
                        const newImg = document.createElement('img');
                        newImg.src = recipe.imagePath;
                        newImg.className = 'ov-img';
                        imgEl.replaceWith(newImg);
                    }
                }
            }

            // Titel aktualisieren
            const titleEl = item.querySelector('.ov-title');
            if (titleEl) {
                titleEl.innerText = recipe.name;
                // Styles anpassen (von "Leer" zu "Gefüllt")
                titleEl.classList.remove('text-muted', 'fst-italic', 'small');
                titleEl.classList.add('text-truncate', 'd-block');
            }
        }
    });
}

// --- Speichern ---
async function SaveMealPlanToDb() {
    const inputs = document.querySelectorAll('.meal-plan-input');
    const groupedData = {};

    inputs.forEach(input => {
        const recipeId = parseInt(input.value);
        const dayIndex = parseInt(input.getAttribute('data-day-index'));
        if (recipeId && recipeId > 0) {
            if (!groupedData[dayIndex]) groupedData[dayIndex] = [];
            groupedData[dayIndex].push(recipeId);
        }
    });

    if (Object.keys(groupedData).length === 0) return;

    try {
        await fetch('/CreateNewMealPlan/SaveMealPlanInDb', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
            },
            body: JSON.stringify(groupedData)
        });
    } catch (error) {
        console.error('Auto-Save Error:', error);
    }
}

// --- Submit Button am Ende der Seite ---
function createMealPlan() {
    const inputs = document.querySelectorAll('.meal-plan-input');
    const planData = [];

    inputs.forEach(input => {
        if (input.value && input.value !== "0") {
            planData.push({
                Index: parseInt(input.getAttribute('data-day-index')),
                RecipeId: parseInt(input.value)
            });
        }
    });

    if (planData.length === 0) return alert("Bitte wähle mindestens ein Gericht aus.");

    const jsonString = JSON.stringify(planData);
    const encodedData = encodeURIComponent(jsonString);
    const personCount = document.getElementById("personCount").value;

    window.location.href = `/CreateNewMealPlan/CreateMealPlan?data=${encodedData}&personCount=${personCount}`;
}


/**
* Tauscht den Inhalt von zwei Tagen komplett Client-seitig (ohne Reload)
*/
function swapDay(currentDayIndex, direction) {
    const targetDayIndex = currentDayIndex + direction;

    const categories = ["apperetizer_", "main_", "dessert_"];

    // Auch die Sidebar-Elemente müssen getauscht werden
    const sidebarPrefixes = ["mini_", "mini_mobile_"];
    const sidebarCats = ["Vorspeise", "Hauptspeise", "Dessert"];

    // 1. Haupt-Inhalt tauschen (Die großen Karten)
    categories.forEach(prefix => {
        let idA = prefix + currentDayIndex;
        let idB = prefix + targetDayIndex;
        swapHtmlContent(idA, idB);
    });

    // 2. Sidebar / Offcanvas tauschen (Die kleinen Listen)
    sidebarPrefixes.forEach(prefix => {
        sidebarCats.forEach(cat => {
            let idA = `${prefix}${currentDayIndex}_${cat}`;
            let idB = `${prefix}${targetDayIndex}_${cat}`;
            swapHtmlContent(idA, idB);
        });
    });

    // 3. WICHTIG: Attribute aktualisieren!
    // Da wir das HTML verschoben haben, steht im Input von Tag 1 jetzt "data-day-index=2" (vom alten Ort).
    // Das müssen wir korrigieren.
    updateDayAttributes(currentDayIndex);
    updateDayAttributes(targetDayIndex);

    // 4. Speichern im Hintergrund (damit es beim Reload bleibt)
    SaveMealPlanToDb();
}

/**
 * Tauscht das innerHTML von zwei Elementen anhand ihrer IDs
 */
function swapHtmlContent(idA, idB) {
    const elA = document.getElementById(idA);
    const elB = document.getElementById(idB);

    if (elA && elB) {
        const temp = elA.innerHTML;
        elA.innerHTML = elB.innerHTML;
        elB.innerHTML = temp;
    }
}

/**
 * Repariert die IDs und Data-Attribute nach dem Tausch
 */
function updateDayAttributes(dayIndex) {
    // Wir suchen in allen 3 Slots des betroffenen Tages
    const prefixes = ["apperetizer_", "main_", "dessert_"];

    prefixes.forEach(prefix => {
        const containerId = prefix + dayIndex;
        const container = document.getElementById(containerId);

        if (container) {
            // A) Hidden Inputs korrigieren
            const inputs = container.querySelectorAll('.meal-plan-input');
            inputs.forEach(input => {
                input.setAttribute('data-day-index', dayIndex);
            });

            // B) Buttons korrigieren ("Ändern" oder "Hinzufügen")
            // Diese haben data-index="..." Attribute, die für das Modal wichtig sind
            const buttons = container.querySelectorAll('button[data-index]');
            buttons.forEach(btn => {
                btn.setAttribute('data-index', dayIndex);
            });
        }
    });
}
