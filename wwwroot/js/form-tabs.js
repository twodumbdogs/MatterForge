(() => {
    const normalize = (value) => {
        const trimmed = (value ?? '').trim();
        if (['yes', 'on', 'true'].includes(trimmed.toLowerCase())) {
            return 'true';
        }

        if (trimmed.toLowerCase() === 'false') {
            return 'false';
        }

        return trimmed.toLowerCase();
    };

    const fieldValue = (root, key) => {
        const fields = Array.from(root.querySelectorAll(`[name="Fields[${CSS.escape(key)}]"], [name^="Fields[${CSS.escape(key)}]."]`));
        if (fields.length === 0) {
            return '';
        }

        const checkbox = fields.find((field) => field.type === 'checkbox');
        if (checkbox) {
            return checkbox.checked ? 'true' : 'false';
        }

        return fields[0].value ?? '';
    };

    const updateConditions = (root) => {
        root.querySelectorAll('[data-form-field]').forEach((field) => {
            const conditionField = field.dataset.visibleField?.trim() ?? '';
            const conditionValue = field.dataset.visibleValue?.trim() ?? '';
            const shouldShow = conditionField.length === 0 ||
                normalize(fieldValue(root, conditionField)) === normalize(conditionValue);

            field.hidden = !shouldShow;
            field.querySelectorAll('[data-field-input]').forEach((input) => {
                input.disabled = !shouldShow;
            });
        });
    };

    document.querySelectorAll('[data-form-tabs]').forEach((shell) => {
        const tabs = Array.from(shell.querySelectorAll('[data-section-tab]'));
        const panels = Array.from(shell.querySelectorAll('[data-section-panel]'));

        const activate = (key) => {
            tabs.forEach((tab) => {
                const active = tab.dataset.sectionTab === key;
                tab.classList.toggle('active', active);
                tab.setAttribute('aria-selected', active.toString());
            });

            panels.forEach((panel) => {
                panel.hidden = panel.dataset.sectionPanel !== key;
            });
        };

        tabs.forEach((tab) => {
            tab.addEventListener('click', () => activate(tab.dataset.sectionTab));
        });

        if (tabs.length > 0) {
            activate(tabs[0].dataset.sectionTab);
        }

        shell.addEventListener('input', () => updateConditions(shell));
        shell.addEventListener('change', () => updateConditions(shell));
        updateConditions(shell);
    });
})();
