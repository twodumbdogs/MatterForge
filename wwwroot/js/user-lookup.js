(() => {
    const lookups = Array.from(document.querySelectorAll('[data-user-lookup]'));
    if (lookups.length === 0) {
        return;
    }

    const escapeHtml = (value) => {
        const node = document.createElement('div');
        node.textContent = value || '';
        return node.innerHTML;
    };

    lookups.forEach((lookup) => {
        const input = lookup.querySelector('[data-user-lookup-input]');
        const value = lookup.querySelector('[data-user-lookup-value]');
        const list = lookup.querySelector('[data-user-lookup-list]');
        const url = lookup.dataset.userLookupUrl;
        let timer;
        let requestId = 0;
        let suppressLookupRefresh = false;

        if (!input || !value || !list || !url) {
            return;
        }

        const close = () => {
            list.hidden = true;
            list.innerHTML = '';
            lookup.classList.remove('is-open');
            input.setAttribute('aria-expanded', 'false');
        };

        const open = () => {
            list.hidden = false;
            lookup.classList.add('is-open');
            input.setAttribute('aria-expanded', 'true');
        };

        const render = (suggestions) => {
            if (!suggestions || suggestions.length === 0) {
                list.innerHTML = '<div class="mf-user-lookup-empty">No matching users found.</div>';
                open();
                return;
            }

            list.innerHTML = suggestions.map((suggestion) => {
                const id = suggestion.id ?? suggestion.Id ?? '';
                const name = suggestion.name ?? suggestion.Name ?? '';
                const displayLabel = suggestion.displayLabel ?? suggestion.DisplayLabel ?? name;
                return `
                <button class="mf-user-lookup-item" type="button" data-user-id="${escapeHtml(id)}" data-user-name="${escapeHtml(name)}">
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
                    throw new Error(`User lookup failed: ${response.status}`);
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

            value.value = '';
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
            const button = event.target.closest('[data-user-id]');
            if (!button) {
                return;
            }

            value.value = button.dataset.userId || '';
            input.value = button.dataset.userName || '';
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
