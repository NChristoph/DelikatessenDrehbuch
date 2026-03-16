(function () {
    const STYLE_ID = "creator-publish-checklist-style";

    function injectStyles() {
        if (document.getElementById(STYLE_ID)) {
            return;
        }

        const style = document.createElement("style");
        style.id = STYLE_ID;
        style.textContent = `
            .creator-checklist-toolbar {
                color: #172033;
                position: sticky;
                top: 74px;
                z-index: 1450;
                max-width: 720px;
                margin: 0 auto 10px;
                padding: 8px 12px 10px;
                background: linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(255, 255, 255, 0.84));
                backdrop-filter: blur(10px);
            }

            .creator-checklist {
                display: flex;
                flex-wrap: nowrap;
                gap: 8px;
                align-items: center;
                overflow-x: auto;
                scrollbar-width: none;
            }

            .creator-checklist::-webkit-scrollbar {
                display: none;
            }

            .creator-check-item {
                flex: 0 0 auto;
                border-radius: 999px;
                padding: 7px 11px;
                font-size: 0.78rem;
                font-weight: 800;
                line-height: 1;
                cursor: pointer;
                border: 1px solid rgba(15, 23, 42, 0.09);
                background: rgba(255, 255, 255, 0.88);
                color: #405062;
                transition: background .2s ease, color .2s ease, border-color .2s ease, transform .2s ease, box-shadow .2s ease;
            }

            .creator-check-item.is-active {
                color: #fff;
                background: linear-gradient(135deg, #8d63ff 0%, #5f85ff 100%);
                border-color: rgba(95, 133, 255, 0.45);
                box-shadow: 0 10px 20px rgba(95, 133, 255, 0.20);
                transform: translateY(-1px);
            }

            .creator-check-item.is-done {
                color: #fff;
                background: linear-gradient(135deg, #FF9966 0%, #FF5E62 100%);
                border-color: rgba(255, 94, 98, 0.45);
                box-shadow: 0 10px 20px rgba(255, 94, 98, 0.18);
                transform: translateY(-1px);
            }
        `;

        document.head.appendChild(style);
    }

    function hasMeaningfulChildren(container) {
        return !!container && Array.from(container.children).some(child => !child.classList.contains("d-none"));
    }

    function hasMediaSelected(fileInput, imagePreview, videoPreview) {
        if (fileInput && fileInput.files && fileInput.files.length > 0) {
            return true;
        }

        if (imagePreview && !imagePreview.classList.contains("d-none") && imagePreview.getAttribute("src")) {
            return true;
        }

        if (videoPreview && videoPreview.getAttribute("src")) {
            return true;
        }

        return false;
    }

    function initChecklist() {
        const form = document.getElementById("recipeForm");
        const topbar = document.querySelector(".creator-topbar");
        const shell = document.querySelector(".feed-shell");
        const topbarProgress = document.getElementById("creatorTopbarProgress");
        const publishButtons = Array.from(document.querySelectorAll('button[type="submit"][form="recipeForm"], #recipeForm button[type="submit"]'));
        const requiredCompletedSteps = 4;

        if (!form || !topbar || !shell || !topbarProgress || document.querySelector(".creator-checklist-toolbar")) {
            return;
        }

        injectStyles();

        const toolbar = document.createElement("div");
        toolbar.className = "creator-checklist-toolbar d-lg-none";

        const checklist = document.createElement("div");
        checklist.className = "creator-checklist";
        checklist.setAttribute("aria-live", "polite");
        checklist.innerHTML = [
            '<span class="creator-check-item" data-check-key="basics">Basis</span>',
            '<span class="creator-check-item" data-check-key="media">Media</span>',
            '<span class="creator-check-item" data-check-key="ingredients">Zutaten</span>',
            '<span class="creator-check-item" data-check-key="steps">Steps</span>',
            '<span class="creator-check-item" data-check-key="keywords">Keywords</span>'
        ].join("");

        toolbar.appendChild(checklist);
        shell.parentNode.insertBefore(toolbar, shell);

        const titleInput = form.querySelector('[name="Title"]');
        const categorySelect = form.querySelector('[name="Recipe.Category"]');
        const fileInput = document.getElementById("videoInput");
        const imagePreview = document.getElementById("imagePreview");
        const videoPreview = document.getElementById("videoPreview");
        const selectedIngredients = document.getElementById("selectedIngredients");
        const selectedSteps = document.getElementById("selectedSteps");
        const selectedKeywords = document.getElementById("selectedKeywords");
        const sectionMap = {
            basics: "card-basics",
            media: "card-media",
            ingredients: "card-ingredients",
            steps: "card-steps",
            keywords: "card-keywords"
        };
        const sections = Object.entries(sectionMap)
            .map(([key, id]) => ({ key, element: document.getElementById(id) }))
            .filter(section => section.element);
        const checklistItems = Array.from(checklist.querySelectorAll(".creator-check-item[data-check-key]"));

        function buildChecklistState() {
            return {
                basics: !!(titleInput && titleInput.value.trim()) && !!(categorySelect && categorySelect.value.trim()),
                media: hasMediaSelected(fileInput, imagePreview, videoPreview),
                ingredients: hasMeaningfulChildren(selectedIngredients),
                steps: hasMeaningfulChildren(selectedSteps),
                keywords: hasMeaningfulChildren(selectedKeywords)
            };
        }

        function scrollToSection(key) {
            const targetId = sectionMap[key];
            const target = targetId ? document.getElementById(targetId) : null;
            if (!target) {
                return;
            }

            const topbarHeight = topbar ? topbar.offsetHeight : 0;
            const toolbarHeight = toolbar ? toolbar.offsetHeight : 0;
            const top = target.getBoundingClientRect().top + window.scrollY - topbarHeight - toolbarHeight - 16;
            window.scrollTo({ top, behavior: "auto" });
        }

        function centerChecklistItem(item) {
            if (!item || typeof item.scrollIntoView !== "function") {
                return;
            }

            item.scrollIntoView({
                behavior: "auto",
                inline: "center",
                block: "nearest"
            });
        }

        function setActiveSection(key) {
            checklistItems.forEach(item => {
                const isActive = item.dataset.checkKey === key;
                item.classList.toggle("is-active", isActive);

                if (isActive) {
                    centerChecklistItem(item);
                }
            });
        }

        function updateActiveSection() {
            if (!sections.length) {
                return;
            }

            const topbarHeight = topbar ? topbar.offsetHeight : 0;
            const toolbarHeight = toolbar ? toolbar.offsetHeight : 0;
            const marker = window.scrollY + topbarHeight + toolbarHeight + 28;
            let activeSection = sections[0];

            sections.forEach(section => {
                if (section.element.offsetTop <= marker) {
                    activeSection = section;
                }
            });

            if (activeSection) {
                setActiveSection(activeSection.key);
            }
        }

        function updateChecklist() {
            const state = buildChecklistState();
            const completedCount = Object.values(state).filter(Boolean).length;
            const isReady = completedCount >= requiredCompletedSteps;

            checklistItems.forEach(item => {
                const key = item.dataset.checkKey;
                item.classList.toggle("is-done", !!state[key]);
            });

            publishButtons.forEach(button => {
                button.disabled = !isReady;
                button.setAttribute("aria-disabled", isReady ? "false" : "true");
                button.title = isReady ? "" : `Mindestens ${requiredCompletedSteps} von 5 Schritten abschliessen`;
            });

            topbarProgress.innerHTML = isReady ? "Ready to publish" : `${completedCount}/5 bereit`;
        }

        checklistItems.forEach(item => {
            item.addEventListener("click", function () {
                scrollToSection(item.dataset.checkKey);
                setActiveSection(item.dataset.checkKey);
            });
        });

        [titleInput, categorySelect, fileInput].forEach(element => {
            if (!element) {
                return;
            }

            element.addEventListener("input", updateChecklist);
            element.addEventListener("change", updateChecklist);
        });

        [selectedIngredients, selectedSteps, selectedKeywords].forEach(container => {
            if (!container) {
                return;
            }

            new MutationObserver(updateChecklist).observe(container, { childList: true, subtree: true });
        });

        window.addEventListener("scroll", updateActiveSection, { passive: true });
        window.addEventListener("resize", updateActiveSection);
        form.addEventListener("submit", function (event) {
            const completedCount = Object.values(buildChecklistState()).filter(Boolean).length;
            if (completedCount >= requiredCompletedSteps) {
                return;
            }

            event.preventDefault();
            updateChecklist();
        });

        updateActiveSection();
        updateChecklist();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initChecklist);
    } else {
        initChecklist();
    }
})();
