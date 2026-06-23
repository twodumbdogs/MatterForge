// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const storageKey = 'cmiforge.theme';
    const root = document.documentElement;
    const toggle = document.querySelector('[data-theme-toggle]');
    const label = document.querySelector('[data-theme-toggle-label]');

    function normalizeTheme(value) {
        return value === 'dark' ? 'dark' : 'light';
    }

    function setTheme(theme, persist) {
        const normalized = normalizeTheme(theme);
        root.dataset.bsTheme = normalized;
        root.dataset.theme = normalized;

        if (persist) {
            localStorage.setItem(storageKey, normalized);
        }

        if (toggle) {
            const isDark = normalized === 'dark';
            if (label) {
                label.textContent = 'Dark mode';
            }
            if (toggle.matches('input[type="checkbox"]')) {
                toggle.checked = isDark;
                toggle.setAttribute('aria-checked', String(isDark));
            }

            toggle.setAttribute('aria-label', 'Dark mode');
            toggle.title = isDark ? 'Dark mode on' : 'Dark mode off';
            toggle.dataset.currentTheme = normalized;
        }
    }

    setTheme(root.dataset.theme, false);

    toggle?.addEventListener('change', () => {
        const nextTheme = toggle.checked ? 'dark' : 'light';
        setTheme(nextTheme, true);
    });
})();

(() => {
    const cookieName = 'cmiforge.timezone';
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!timeZone) {
        return;
    }

    const existing = document.cookie
        .split(';')
        .map((part) => part.trim())
        .find((part) => part.startsWith(`${cookieName}=`));
    const existingValue = existing ? decodeURIComponent(existing.substring(cookieName.length + 1)) : '';

    if (existingValue !== timeZone) {
        const maxAge = 60 * 60 * 24 * 365;
        document.cookie = `${cookieName}=${encodeURIComponent(timeZone)}; path=/; max-age=${maxAge}; samesite=lax`;
        if (!sessionStorage.getItem('cmiforge.timezoneReloaded')) {
            sessionStorage.setItem('cmiforge.timezoneReloaded', 'true');
            window.location.reload();
        }
    }
})();

(() => {
    function syncWorkflowStep(card) {
        const type = card.querySelector('[data-workflow-step-type]')?.value?.toLowerCase();
        const isNotification = type === 'notification';
        card.querySelectorAll('[data-workflow-notification-section]').forEach((section) => {
            section.hidden = !isNotification;
        });
        card.querySelectorAll('[data-workflow-approval-section], [data-workflow-routing-section]').forEach((section) => {
            section.classList.toggle('mf-muted-section', isNotification);
        });
    }

    function initWorkflowCards(root = document) {
        root.querySelectorAll('[data-workflow-step-card]').forEach((card) => {
            const type = card.querySelector('[data-workflow-step-type]');
            if (!type) {
                return;
            }

            syncWorkflowStep(card);
            type.addEventListener('change', () => syncWorkflowStep(card));
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initWorkflowCards());
    } else {
        initWorkflowCards();
    }
})();

(() => {
    const sortableTableSelector = 'table.table:not([data-no-sort])';
    const skipContainerSelector = '[data-no-sort], [data-form-designer], .mf-designer-table';

    function initSortableTables(root = document) {
        root.querySelectorAll(sortableTableSelector).forEach((table) => {
            if (table.dataset.sortInitialized === 'true' || table.closest(skipContainerSelector)) {
                return;
            }

            const headerRow = table.tHead?.rows?.[0];
            const body = table.tBodies?.[0];
            if (!headerRow || !body || body.rows.length < 2) {
                return;
            }

            const headers = Array.from(headerRow.cells);
            headers.forEach((header, index) => {
                if (!isSortableHeader(header, index, body)) {
                    return;
                }

                header.classList.add('mf-sortable-heading');
                header.tabIndex = 0;
                header.role = 'button';
                header.setAttribute('aria-sort', 'none');
                header.title = `Sort by ${header.textContent.trim()}`;

                const sort = () => sortTable(table, index, header);
                header.addEventListener('click', sort);
                header.addEventListener('keydown', (event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        sort();
                    }
                });
            });

            table.dataset.sortInitialized = 'true';
        });
    }

    function isSortableHeader(header, index, body) {
        if (header.matches('[data-no-sort]') || header.colSpan > 1 || header.rowSpan > 1) {
            return false;
        }

        if (header.querySelector('input, select, textarea, button')) {
            return false;
        }

        return Array.from(body.rows).some((row) => getCellSortText(row.cells[index]).length > 0);
    }

    function sortTable(table, columnIndex, activeHeader) {
        const body = table.tBodies[0];
        const currentDirection = activeHeader.dataset.sortDirection === 'asc' ? 'desc' : 'asc';
        const directionFactor = currentDirection === 'asc' ? 1 : -1;
        const rows = Array.from(body.rows).map((row, originalIndex) => ({
            row,
            originalIndex,
            value: getCellSortValue(row.cells[columnIndex])
        }));

        rows.sort((left, right) => {
            const compared = compareValues(left.value, right.value);
            return compared === 0
                ? left.originalIndex - right.originalIndex
                : compared * directionFactor;
        });

        rows.forEach(({ row }) => body.appendChild(row));
        updateHeaderState(table, activeHeader, currentDirection);
    }

    function updateHeaderState(table, activeHeader, direction) {
        table.querySelectorAll('th.mf-sortable-heading').forEach((header) => {
            header.dataset.sortDirection = '';
            header.setAttribute('aria-sort', 'none');
        });

        activeHeader.dataset.sortDirection = direction;
        activeHeader.setAttribute('aria-sort', direction === 'asc' ? 'ascending' : 'descending');
    }

    function getCellSortValue(cell) {
        const text = getCellSortText(cell);
        return {
            text,
            number: parseNumber(text),
            date: parseDate(text)
        };
    }

    function getCellSortText(cell) {
        if (!cell) {
            return '';
        }

        if (cell.dataset.sortValue) {
            return normalizeText(cell.dataset.sortValue);
        }

        const field = cell.querySelector('input:not([type="hidden"]), select, textarea');
        if (field) {
            if (field.type === 'checkbox') {
                return field.checked ? 'true' : 'false';
            }

            return normalizeText(field.value);
        }

        return normalizeText(cell.textContent);
    }

    function normalizeText(value) {
        return (value ?? '').replace(/\s+/g, ' ').trim();
    }

    function parseNumber(value) {
        const recordMatch = value.match(/^[A-Za-z]+-(\d+)$/);
        if (recordMatch) {
            return Number(recordMatch[1]);
        }

        const leadingNumber = value.match(/^-?\$?\d[\d,]*(?:\.\d+)?%?/);
        if (!leadingNumber) {
            return null;
        }

        const number = Number(leadingNumber[0].replace(/[$,%]/g, ''));
        return Number.isFinite(number) ? number : null;
    }

    function parseDate(value) {
        if (!/[/-]|\b(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\b/i.test(value)) {
            return null;
        }

        const parsed = Date.parse(value);
        return Number.isNaN(parsed) ? null : parsed;
    }

    function compareValues(left, right) {
        if (left.number !== null && right.number !== null) {
            return left.number - right.number;
        }

        if (left.date !== null && right.date !== null) {
            return left.date - right.date;
        }

        return left.text.localeCompare(right.text, undefined, {
            numeric: true,
            sensitivity: 'base'
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initSortableTables());
    } else {
        initSortableTables();
    }
})();
