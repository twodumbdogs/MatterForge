(() => {
    const getCards = (list) => Array.from(list.querySelectorAll('[data-workflow-step-card]'));

    const renumberSteps = (list) => {
        getCards(list).forEach((card, index) => {
            card.dataset.workflowStepIndex = index.toString();
            const orderInput = card.querySelector('input[name$=".StepNumber"]');
            if (!orderInput) {
                return;
            }

            orderInput.value = (index + 1).toString();
        });

        updateButtons(list);
    };

    const updateButtons = (list) => {
        const cards = getCards(list);
        cards.forEach((card, index) => {
            const up = card.querySelector('[data-move-workflow-step="up"]');
            const down = card.querySelector('[data-move-workflow-step="down"]');
            if (up) {
                up.disabled = index === 0;
            }
            if (down) {
                down.disabled = index === cards.length - 1;
            }
        });
    };

    document.querySelectorAll('[data-workflow-step-list]').forEach((list) => {
        list.addEventListener('click', (event) => {
            const button = event.target.closest('[data-move-workflow-step]');
            if (!button) {
                return;
            }

            const card = button.closest('[data-workflow-step-card]');
            if (!card) {
                return;
            }

            const direction = button.dataset.moveWorkflowStep;
            if (direction === 'up' && card.previousElementSibling) {
                list.insertBefore(card, card.previousElementSibling);
            }

            if (direction === 'down' && card.nextElementSibling) {
                list.insertBefore(card.nextElementSibling, card);
            }

            renumberSteps(list);
            card.querySelector('input, select, textarea')?.focus();
        });

        list.addEventListener('input', () => updateButtons(list));
        renumberSteps(list);
    });
})();
