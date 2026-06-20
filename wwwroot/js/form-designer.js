(() => {
    const fieldNamePattern = /Input\.Fields\[(?:\d+|__index__)\]/g;

    const idFromName = (name) => name
        .replace(/\[/g, '_')
        .replace(/\]\./g, '__')
        .replace(/\./g, '_')
        .replace(/\]/g, '_');

    const updateIndexedAttributes = (element, index) => {
        ['name', 'id', 'for', 'data-valmsg-for'].forEach((attribute) => {
            const value = element.getAttribute(attribute);
            if (!value || !fieldNamePattern.test(value)) {
                fieldNamePattern.lastIndex = 0;
                return;
            }

            fieldNamePattern.lastIndex = 0;
            element.setAttribute(attribute, value.replace(fieldNamePattern, `Input.Fields[${index}]`));
        });

        if ((element.matches('input, select, textarea')) && element.name) {
            element.id = idFromName(element.name);
        }
    };

    const getRows = (list) => Array.from(list.querySelectorAll('.mf-field-row'));

    const hasFieldValue = (row) => {
        const label = row.querySelector('[data-field-label]')?.value.trim() ?? '';
        const key = row.querySelector('[data-field-key]')?.value.trim() ?? '';
        return label.length > 0 || key.length > 0;
    };

    const dragAfterRow = (list, y, draggedRow) => getRows(list)
        .filter((row) => row !== draggedRow)
        .reduce((closest, row) => {
            const box = row.getBoundingClientRect();
            const offset = y - box.top - box.height / 2;
            if (offset < 0 && offset > closest.offset) {
                return { offset, row };
            }

            return closest;
        }, { offset: Number.NEGATIVE_INFINITY, row: null }).row;

    document.querySelectorAll('[data-form-designer]').forEach((builder) => {
        const list = builder.querySelector('[data-field-list]');
        const template = builder.querySelector('[data-field-template]');
        const addButton = builder.querySelector('[data-add-field]');
        const count = builder.querySelector('[data-field-count]');
        const maxFields = Number(builder.dataset.maxFields || 50);
        let draggedRow = null;

        const updateState = () => {
            const rows = getRows(list);
            rows.forEach((row, index) => {
                row.dataset.index = index.toString();
                row.querySelectorAll('input, select, textarea, label, span').forEach((element) => {
                    updateIndexedAttributes(element, index);
                });
            });

            const usedFields = rows.filter(hasFieldValue).length;
            if (count) {
                count.textContent = `${usedFields} of ${maxFields} fields`;
            }
            if (addButton) {
                addButton.disabled = rows.length >= maxFields;
                addButton.textContent = rows.length >= maxFields ? '50 field limit reached' : 'Add field';
            }
        };

        const addRow = () => {
            if (!template || getRows(list).length >= maxFields) {
                updateState();
                return;
            }

            const row = template.content.firstElementChild.cloneNode(true);
            list.appendChild(row);
            updateState();
            row.querySelector('input, select, textarea')?.focus();
        };

        addButton?.addEventListener('click', addRow);

        list.addEventListener('click', (event) => {
            const button = event.target.closest('button');
            if (!button) {
                return;
            }

            const row = button.closest('.mf-field-row');
            if (!row) {
                return;
            }

            if (button.matches('[data-remove-field]')) {
                const rows = getRows(list);
                if (rows.length === 1) {
                    row.querySelectorAll('input[type="text"], input:not([type]), textarea').forEach((input) => {
                        input.value = '';
                    });
                    row.querySelectorAll('input[type="checkbox"]').forEach((input) => {
                        input.checked = false;
                    });
                } else {
                    row.remove();
                }
                updateState();
            }
        });

        list.addEventListener('input', updateState);
        list.addEventListener('change', updateState);

        list.addEventListener('dragstart', (event) => {
            const handle = event.target.closest('[data-drag-handle]');
            if (!handle) {
                event.preventDefault();
                return;
            }

            draggedRow = handle.closest('.mf-field-row');
            draggedRow?.classList.add('is-dragging');
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', draggedRow?.dataset.index ?? '');
        });

        list.addEventListener('dragover', (event) => {
            if (!draggedRow) {
                return;
            }

            event.preventDefault();
            const afterRow = dragAfterRow(list, event.clientY, draggedRow);
            if (afterRow) {
                list.insertBefore(draggedRow, afterRow);
            } else {
                list.appendChild(draggedRow);
            }
        });

        list.addEventListener('dragend', () => {
            draggedRow?.classList.remove('is-dragging');
            draggedRow = null;
            updateState();
        });

        updateState();
    });
})();
