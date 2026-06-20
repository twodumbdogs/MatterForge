(() => {
    const minimumLength = 3;
    const debounceMs = 550;

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');

    const findField = (scope, name) => scope.querySelector(`[data-address-field="${name}"]`);

    const setFieldValue = (scope, name, value) => {
        const field = findField(scope, name);
        if (!field || value === undefined || value === null) {
            return;
        }

        field.value = value;
        field.dispatchEvent(new Event('input', { bubbles: true }));
        field.dispatchEvent(new Event('change', { bubbles: true }));
    };

    const renderSuggestions = (list, suggestions) => {
        if (!suggestions || suggestions.length === 0) {
            list.hidden = true;
            list.innerHTML = '';
            return;
        }

        list.innerHTML = suggestions.map((suggestion, index) => `
            <button class="mf-address-lookup-item" type="button" data-address-index="${index}">
                <span>${escapeHtml(suggestion.label)}</span>
                <small>${escapeHtml([suggestion.city, suggestion.state, suggestion.postalCode].filter(Boolean).join(', '))}</small>
            </button>`).join('');
        list.hidden = false;
    };

    document.querySelectorAll('[data-address-lookup-input]').forEach((input) => {
        const form = input.closest('form');
        if (!form) {
            return;
        }

        const scope = input.closest('[data-address-lookup-group]') ?? form;
        const list = document.createElement('div');
        list.className = 'mf-address-lookup-list';
        list.hidden = true;
        input.insertAdjacentElement('afterend', list);

        let timer = 0;
        let requestId = 0;
        let suggestions = [];

        const close = () => {
            list.hidden = true;
        };

        const search = async () => {
            const text = input.value.trim();
            const currentRequest = ++requestId;
            if (text.length < minimumLength) {
                suggestions = [];
                close();
                return;
            }

            try {
                const response = await fetch(`/api/address-lookup?text=${encodeURIComponent(text)}`, {
                    headers: { Accept: 'application/json' }
                });
                if (!response.ok || currentRequest !== requestId) {
                    return;
                }

                const data = await response.json();
                suggestions = data.results ?? [];
                renderSuggestions(list, suggestions);
            } catch {
                suggestions = [];
                close();
            }
        };

        input.addEventListener('input', () => {
            window.clearTimeout(timer);
            timer = window.setTimeout(search, debounceMs);
        });

        input.addEventListener('focus', () => {
            if (suggestions.length > 0) {
                list.hidden = false;
            }
        });

        input.addEventListener('blur', () => {
            window.setTimeout(close, 150);
        });

        list.addEventListener('mousedown', (event) => {
            event.preventDefault();
        });

        list.addEventListener('click', (event) => {
            const button = event.target.closest('[data-address-index]');
            if (!button) {
                return;
            }

            const suggestion = suggestions[Number(button.dataset.addressIndex)];
            if (!suggestion) {
                return;
            }

            setFieldValue(scope, 'line1', suggestion.addressLine1);
            setFieldValue(scope, 'line2', suggestion.addressLine2);
            setFieldValue(scope, 'city', suggestion.city);
            setFieldValue(scope, 'state', suggestion.state);
            setFieldValue(scope, 'postalCode', suggestion.postalCode);
            setFieldValue(scope, 'country', suggestion.country);
            close();
            findField(scope, 'line2')?.focus();
        });
    });
})();
