// Lightweight Smart Step integration for EditRecipe page.
// Provides addStep (for SmartStepCreator) and addStepRowFromData (for restoring existing steps).
(function () {
    "use strict";

    var stepCounter = 0;

    function escapeHtml(str) {
        var div = document.createElement("div");
        div.textContent = str;
        return div.innerHTML;
    }

    function buildStepRowHtml(id, text, masterTemplateId, referenceJson, ingredientName) {
        var encodedRef = escapeHtml(referenceJson || "{}");
        var encodedMaster = escapeHtml(masterTemplateId || "");
        var encodedIngredient = escapeHtml(ingredientName || "");

        return '<div class="dynamic-item d-flex align-items-center step-row" draggable="true"'
            + ' data-step-id="' + escapeHtml(String(id)) + '"'
            + ' data-master-template-id="' + encodedMaster + '"'
            + ' data-step-reference-json="' + encodedRef + '"'
            + ' data-ingredient-name="' + encodedIngredient + '">'
            + '<div class="badge candy-purple rounded-pill me-3 step-badge">0</div>'
            + '<div class="small flex-grow-1 display-step-selected">'
            + '<span class="step-text-content">' + escapeHtml(text || "Schritt") + '</span>'
            + '<div class="step-row-actions">'
            + '<button type="button" class="btn btn-sm btn-outline-secondary" onclick="window._editMoveStepRow(this,-1)"><i class="bi bi-arrow-up"></i></button>'
            + '<button type="button" class="btn btn-sm btn-outline-secondary" onclick="window._editMoveStepRow(this,1)"><i class="bi bi-arrow-down"></i></button>'
            + '<button type="button" class="btn btn-sm text-danger opacity-50" onclick="window._editRemoveStep(this)"><i class="bi bi-trash3"></i></button>'
            + '</div>'
            + '</div>'
            + '</div>';
    }

    function updateStepIndices() {
        var rows = document.querySelectorAll("#selectedSteps .step-row");
        rows.forEach(function (row, i) {
            var badge = row.querySelector(".step-badge");
            if (badge) badge.textContent = String(i + 1);
        });
        var countEl = document.getElementById("sc2StoryStepCount");
        if (countEl) countEl.textContent = "Schritte: " + rows.length;
    }

    // Called by SmartStepCreator's acceptActiveStep when window.addStep exists
    window.addStep = function (id, btn, manualText, options) {
        options = options || {};
        var stepData = options.stepData || {};
        var masterTemplateId = (options.masterTemplateId || stepData.masterTemplateId || "").toString();
        var stableReference = stepData.stableReference || {};
        var referenceJson = JSON.stringify(stableReference);
        var ingredientName = (options.ingredientName || "").toString().trim();
        var text = manualText || stepData.de || "Schritt";

        var selected = document.getElementById("selectedSteps");
        if (!selected) return;

        selected.insertAdjacentHTML("beforeend",
            buildStepRowHtml(id, text, masterTemplateId, referenceJson, ingredientName));
        updateStepIndices();
    };

    window.updateStepIndices = updateStepIndices;

    // Restore an existing smart step from DB data
    window.addStepRowFromData = function (masterStepKey, variablesJson, stepIndex) {
        var selected = document.getElementById("selectedSteps");
        if (!selected) return;

        stepCounter++;
        var id = "restored_" + stepCounter;
        var displayText = masterStepKey || "Schritt " + stepIndex;

        // Build a minimal reference JSON for form submission
        var reference = {
            master_step_key: masterStepKey || "",
            variables: {}
        };
        try {
            if (variablesJson && variablesJson !== "{}") {
                reference.variables = JSON.parse(variablesJson);
            }
        } catch (e) { /* ignore */ }

        var referenceJson = JSON.stringify(reference);

        selected.insertAdjacentHTML("beforeend",
            buildStepRowHtml(id, displayText, masterStepKey, referenceJson, ""));
        updateStepIndices();
    };

    window._editRemoveStep = function (btn) {
        var row = btn.closest(".step-row");
        if (row) row.remove();
        updateStepIndices();
    };

    window._editMoveStepRow = function (btn, direction) {
        var row = btn.closest(".step-row");
        if (!row) return;
        if (direction === -1 && row.previousElementSibling) {
            row.parentNode.insertBefore(row, row.previousElementSibling);
        } else if (direction === 1 && row.nextElementSibling) {
            row.parentNode.insertBefore(row.nextElementSibling, row);
        }
        updateStepIndices();
    };
})();
