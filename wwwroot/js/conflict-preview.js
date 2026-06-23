(() => {
    const preview = document.querySelector('[data-conflict-preview]');
    if (!preview) {
        return;
    }

    const sources = Array.from(document.querySelectorAll('[data-conflict-source]'));
    const status = preview.querySelector('[data-conflict-status]');
    const summary = preview.querySelector('[data-conflict-summary]');
    const stats = preview.querySelector('[data-conflict-stats]');
    const results = preview.querySelector('[data-conflict-results]');
    const isPopover = preview.hasAttribute('data-conflict-popover');
    let requestId = 0;
    let timer;
    let activeSource = sources[0] || null;
    let hideTimer;

    const riskClass = (risk) => {
        switch (risk) {
            case 'Critical':
                return 'mf-risk-critical';
            case 'High':
                return 'mf-risk-high';
            case 'Medium':
                return 'mf-risk-medium';
            default:
                return 'mf-risk-low';
        }
    };

    const escapeHtml = (value) => {
        const node = document.createElement('div');
        node.textContent = value || '';
        return node.innerHTML;
    };

    const collectTerms = () => {
        const termSources = isPopover && activeSource ? [activeSource] : sources;
        const terms = termSources
            .map((source) => source.value.trim())
            .filter(Boolean)
            .join('\n')
            .split(/[\n,;]+/)
            .map((term) => term.trim())
            .filter((term, index, all) => term.length > 1 && all.findIndex(x => x.toLowerCase() === term.toLowerCase()) === index);

        return terms.join('\n');
    };

    const openPopover = (source) => {
        if (!isPopover) {
            return;
        }

        window.clearTimeout(hideTimer);
        activeSource = source;
        preview.classList.add('is-open');
        positionPopover();
    };

    const scheduleClosePopover = () => {
        if (!isPopover) {
            return;
        }

        window.clearTimeout(hideTimer);
        hideTimer = window.setTimeout(() => {
            if (document.activeElement && preview.contains(document.activeElement)) {
                return;
            }

            preview.classList.remove('is-open');
        }, 180);
    };

    const positionPopover = () => {
        if (!isPopover || !activeSource || !preview.classList.contains('is-open')) {
            return;
        }

        const narrowViewport = window.matchMedia('(max-width: 760px)').matches;
        if (narrowViewport) {
            const fieldGroup = activeSource.closest('.mb-3') || activeSource.parentElement;
            if (fieldGroup && preview.parentElement !== fieldGroup) {
                fieldGroup.appendChild(preview);
            }

            preview.classList.add('is-mobile-inline');
            preview.style.left = '';
            preview.style.top = '';
            preview.style.width = '';
            preview.style.maxHeight = '';
            return;
        }

        if (preview.classList.contains('is-mobile-inline')) {
            document.body.appendChild(preview);
            preview.classList.remove('is-mobile-inline');
        }

        const margin = 16;
        const rect = activeSource.getBoundingClientRect();
        const hasNativePicker = activeSource.hasAttribute('list');
        const clientLookupList = activeSource
            .closest('[data-client-lookup]')
            ?.querySelector('[data-client-lookup-list]:not([hidden])');
        const clientLookupRect = clientLookupList?.getBoundingClientRect();
        const hasClientLookup = Boolean(clientLookupRect && clientLookupRect.height > 0);
        const hasFieldPicker = hasNativePicker || hasClientLookup;
        const availableRight = window.innerWidth - rect.right - margin;
        const availableLeft = rect.left - margin;
        let width = Math.min(Math.max(rect.width, 340), 430, window.innerWidth - margin * 2);
        let left = Math.min(Math.max(rect.left, margin), window.innerWidth - width - margin);
        let top = Math.max(rect.bottom + 10, margin);
        let isSidecar = false;

        if (hasFieldPicker && availableRight >= 360) {
            width = Math.min(430, availableRight - margin);
            left = rect.right + margin;
            top = Math.max(rect.top, margin);
            isSidecar = true;
        } else if (hasFieldPicker && availableLeft >= 360) {
            width = Math.min(430, availableLeft - margin);
            left = rect.left - width - margin;
            top = Math.max(rect.top, margin);
            isSidecar = true;
        } else if (hasClientLookup && clientLookupRect) {
            top = Math.max(clientLookupRect.bottom + 12, margin);
        } else if (hasNativePicker) {
            top = Math.max(rect.bottom + 230, margin);
        }

        preview.classList.toggle('is-sidecar', isSidecar);
        preview.classList.toggle('is-below-picker', hasFieldPicker && !isSidecar);
        top = Math.min(top, Math.max(margin, window.innerHeight - 180 - margin));
        const maxHeight = Math.max(180, Math.min(460, window.innerHeight - top - margin));

        preview.style.left = `${Math.round(left)}px`;
        preview.style.top = `${Math.round(top)}px`;
        preview.style.width = `${Math.round(width)}px`;
        preview.style.maxHeight = `${Math.round(maxHeight)}px`;
    };

    const renderEmpty = (message) => {
        stats.hidden = true;
        stats.innerHTML = '';
        results.innerHTML = '';
        summary.textContent = message;
    };

    const renderPreview = (data) => {
        summary.textContent = data.summary;

        if (!data.results || data.results.length === 0) {
            renderEmpty(data.summary);
            return;
        }

        stats.hidden = false;
        stats.innerHTML = `
            <span>${data.totalResults} hits</span>
            <span>${data.criticalCount} critical</span>
            <span>${data.highCount} high</span>
            <span>${data.relationshipCount} related</span>
            <span>${data.priorSearchCount} prior</span>`;

        results.innerHTML = data.results.map((item) => {
            const context = [
                item.clientNumber ? `Client ${String(item.clientNumber).padStart(8, '0')} ${item.clientName || ''}` : '',
                item.matterNumber ? `Matter ${String(item.matterNumber).padStart(8, '0')} ${item.matterName || ''}` : ''
            ].filter(Boolean).join(' - ');

            return `
                <article class="mf-conflict-preview-card ${riskClass(item.riskLevel)}">
                    <div class="d-flex justify-content-between gap-2">
                        <strong>${escapeHtml(item.matchedName)}</strong>
                        <span title="Match strength">${item.score}</span>
                    </div>
                    <small>${escapeHtml(item.riskLevel)} - ${escapeHtml(item.partyRole)}</small>
                    <p>${escapeHtml(item.matchType)} for "${escapeHtml(item.searchTerm)}"</p>
                    ${context ? `<small>${escapeHtml(context)}</small>` : ''}
                </article>`;
        }).join('');
    };

    const refreshPreview = async () => {
        const terms = collectTerms();
        const currentRequest = ++requestId;

        if (!terms) {
            status.textContent = 'Watching';
            renderEmpty(isPopover
                ? 'Start typing here and the radar will check this field.'
                : 'Start typing a client, party, parent company, or opposing counsel name.');
            positionPopover();
            return;
        }

        status.textContent = 'Checking';

        try {
            const separator = preview.dataset.previewUrl.includes('?') ? '&' : '?';
            const url = `${preview.dataset.previewUrl}${separator}terms=${encodeURIComponent(terms)}`;
            const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!response.ok) {
                throw new Error(`Preview failed: ${response.status}`);
            }

            const data = await response.json();
            if (currentRequest !== requestId) {
                return;
            }

                    status.textContent = data.totalResults > 0 ? 'Hits found' : 'No hits';
                    renderPreview(data);
                    positionPopover();
                } catch (error) {
            console.error(error);
            if (currentRequest === requestId) {
                        status.textContent = 'Unavailable';
                        renderEmpty('Conflict preview could not refresh. You can still run a formal search.');
                        positionPopover();
                    }
                }
            };

    const scheduleRefresh = () => {
        window.clearTimeout(timer);
        timer = window.setTimeout(refreshPreview, 650);
    };

    sources.forEach((source) => {
        source.addEventListener('focus', () => {
            openPopover(source);
            refreshPreview();
        });
        source.addEventListener('input', () => {
            activeSource = source;
            openPopover(source);
            scheduleRefresh();
        });
        source.addEventListener('blur', scheduleClosePopover);
    });

    if (isPopover) {
        preview.addEventListener('focusin', () => window.clearTimeout(hideTimer));
        preview.addEventListener('focusout', scheduleClosePopover);
        window.addEventListener('cmiforge:client-lookup-opened', positionPopover);
        window.addEventListener('cmiforge:client-lookup-closed', positionPopover);
        window.addEventListener('resize', positionPopover);
        window.addEventListener('scroll', positionPopover, true);
        renderEmpty('Focus a client, matter, contact, or party field to start the radar.');
    } else {
        refreshPreview();
    }
})();
