/* ===========================================================
   CreateNewMealPlan.js
   Logik für das Laden, Ändern und Speichern von Rezepten
   =========================================================== */

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

    // WICHTIG: KEIN äusseres <div id="..."> mehr! Nur der Inhalt.
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


