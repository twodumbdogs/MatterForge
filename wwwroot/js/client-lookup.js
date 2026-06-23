(() => {
    const lookups = Array.from(document.querySelectorAll('[data-client-lookup]'));
    if (lookups.length === 0) {
        return;
    }

    const escapeHtml = (value) => {
        const node = document.createElement('div');
        node.textContent = value || '';
        return node.innerHTML;
    };

    lookups.forEach((lookup) => {
        const input = lookup.querySelector('[data-client-lookup-input]');
        const list = lookup.querySelector('[data-client-lookup-list]');
        const url = lookup.dataset.clientLookupUrl;
        let timer;
        let requestId = 0;
        let suppressLookupRefresh = false;

        if (!input || !list || !url) {
            return;
        }

        const close = () => {
            list.hidden = true;
            list.innerHTML = '';
            lookup.classList.remove('is-open');
            input.setAttribute('aria-expanded', 'false');
            window.dispatchEvent(new CustomEvent('cmiforge:client-lookup-closed', { detail: { input } }));
        };

        const open = () => {
            list.hidden = false;
            lookup.classList.add('is-open');
            input.setAttribute('aria-expanded', 'true');
            window.dispatchEvent(new CustomEvent('cmiforge:client-lookup-opened', { detail: { input } }));
        };

        const render = (suggestions) => {
            if (!suggestions || suggestions.length === 0) {
                list.innerHTML = '<div class="mf-client-lookup-empty">No existing clients match. A new client can still be created from this value.</div>';
                open();
                return;
            }

            list.innerHTML = suggestions.map((suggestion) => {
                const name = suggestion.name ?? suggestion.Name ?? '';
                const displayLabel = suggestion.displayLabel ?? suggestion.DisplayLabel ?? name;
                return `
                <button class="mf-client-lookup-item" type="button" data-client-name="${escapeHtml(name)}">
                    <strong>${escapeHtml(name)}</strong>
                    <small>${escapeHtml(displayLabel)}</small>
                </button>`;
            }).join('');
            open();
        };

        const refresh = async () => {
            const term = input.value.trim();
            const currentRequest = ++requestId;

            if (term.length < 2) {
                close();
                return;
            }

            const separator = url.includes('?') ? '&' : '?';
            try {
                const response = await fetch(`${url}${separator}term=${encodeURIComponent(term)}`, {
                    headers: { Accept: 'application/json' }
                });
                if (!response.ok) {
                    throw new Error(`Client lookup failed: ${response.status}`);
                }

                const suggestions = await response.json();
                if (currentRequest !== requestId) {
                    return;
                }

                render(suggestions);
            } catch (error) {
                console.error(error);
                if (currentRequest === requestId) {
                    close();
                }
            }
        };

        const scheduleRefresh = () => {
            window.clearTimeout(timer);
            timer = window.setTimeout(refresh, 180);
        };

        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-autocomplete', 'list');
        input.setAttribute('aria-expanded', 'false');

        input.addEventListener('focus', scheduleRefresh);
        input.addEventListener('input', () => {
            if (suppressLookupRefresh) {
                suppressLookupRefresh = false;
                return;
            }

            scheduleRefresh();
        });
        input.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') {
                close();
            }
        });

        lookup.addEventListener('focusout', () => {
            window.setTimeout(() => {
                if (!lookup.contains(document.activeElement)) {
                    close();
                }
            }, 120);
        });

        list.addEventListener('click', (event) => {
            const button = event.target.closest('[data-client-name]');
            if (!button) {
                return;
            }

            input.value = button.dataset.clientName || '';
            suppressLookupRefresh = true;
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            close();
            input.focus();
        });

        document.addEventListener('pointerdown', (event) => {
            if (!lookup.contains(event.target)) {
                close();
            }
        });
    });
})();
