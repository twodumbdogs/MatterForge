(() => {
    const stages = [
        { percent: 12, message: "Preparing search terms..." },
        { percent: 32, message: "Checking clients, matters, parties, and aliases..." },
        { percent: 56, message: "Comparing relationship history and prior searches..." },
        { percent: 78, message: "Scoring candidate matches..." },
        { percent: 92, message: "Building the review results..." }
    ];

    const startProgress = (form) => {
        const panel = form.querySelector("[data-conflict-search-progress]");
        const bar = form.querySelector("[data-conflict-search-progress-bar]");
        const message = form.querySelector("[data-conflict-search-progress-message]");
        const percent = form.querySelector("[data-conflict-search-progress-percent]");
        const title = form.querySelector("[data-conflict-search-progress-title]");
        const submitButtons = form.querySelectorAll("[data-conflict-search-submit], button[type='submit']");

        if (!panel || !bar || !message || !percent) {
            return;
        }

        if (title && form.dataset.conflictSearchProgressTitle) {
            title.textContent = form.dataset.conflictSearchProgressTitle;
        }

        panel.hidden = false;
        panel.classList.add("is-active");
        submitButtons.forEach((button) => {
            button.disabled = true;
            button.dataset.originalText = button.textContent.trim();
            button.textContent = "Searching...";
        });

        let stageIndex = 0;
        const applyStage = () => {
            const stage = stages[Math.min(stageIndex, stages.length - 1)];
            bar.style.width = `${stage.percent}%`;
            bar.setAttribute("aria-valuenow", String(stage.percent));
            message.textContent = stage.message;
            percent.textContent = `${stage.percent}%`;
            stageIndex += 1;
        };

        applyStage();
        window.setInterval(applyStage, 1200);
    };

    document.querySelectorAll("[data-conflict-search-form]").forEach((form) => {
        form.addEventListener("submit", (event) => {
            if (form.dataset.conflictSearchSubmitting === "true") {
                return;
            }

            if (typeof form.checkValidity === "function" && !form.checkValidity()) {
                return;
            }

            form.dataset.conflictSearchSubmitting = "true";
            startProgress(form);
        });
    });
})();
