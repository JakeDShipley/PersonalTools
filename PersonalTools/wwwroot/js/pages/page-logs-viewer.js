$(function () {
    const $page = $('.monitor-page[data-monitor-scope="logs"]');

    if (!$page.length) {
        return;
    }

    const endpoint = $page.data('logs-endpoint');
    const $refresh = $('#btnRefreshLogs');
    const $connection = $('#logsConnectionState');
    const $search = $('#logsSearch');
    const $minimumLevel = $('#logsMinimumLevel');
    const $autoRefresh = $('#logsAutoRefresh');
    const $pageSize = $('#logsPageSize');
    const $tableBody = $('#logsTableBody');
    const $detailsModal = $('#logDetailsModal');
    const visibleEntries = new Map();
    const summaryValues = new Map();

    let currentPage = 1;
    let pageCount = 1;
    let loading = false;
    let reloadAfterCurrent = false;
    let searchTimer = null;
    let fallbackTimer = null;
    let signalRConnected = false;
    let firstRender = true;
    let selectedEntry = null;

    $detailsModal.appendTo(document.body);
    revealPage();
    loadLogs();
    connectSignalR();

    // One request returns only the selected page. MariaDB performs the search and level filter,
    // which keeps the viewer responsive even after the log table has grown substantially.
    function loadLogs() {
        if (loading) {
            reloadAfterCurrent = true;
            return;
        }

        loading = true;
        $refresh.addClass('is-loading').prop('disabled', true);

        $.ajax({
            url: endpoint,
            method: 'GET',
            cache: false,
            showLoader: false,
            data: {
                page: currentPage,
                pageSize: Number($pageSize.val()) || 25,
                minimumLevel: $minimumLevel.val(),
                search: ($search.val() || '').trim()
            }
        })
            .done(function (result) {
                $('#logsUnavailable').addClass('d-none');

                pageCount = Math.max(1, Number(result.pageCount) || 1);

                // A deleted or expired final page can disappear between refreshes.
                if (currentPage > pageCount) {
                    currentPage = pageCount;
                    reloadAfterCurrent = true;
                    return;
                }

                updateSummary(result);
                renderEntries(result.entries || []);
                renderPagination(result);

                $('#logsCaptureStarted').text(formatDateTime(result.captureStartedUtc));
                $('#logsUpdated').text(formatTime(result.capturedUtc));
                firstRender = false;
            })
            .fail(function () {
                $('#logsUnavailable').removeClass('d-none');
            })
            .always(function () {
                loading = false;
                $refresh.removeClass('is-loading').prop('disabled', false);

                if (reloadAfterCurrent) {
                    reloadAfterCurrent = false;
                    loadLogs();
                }
            });
    }

    function updateSummary(result) {
        updateSummaryValue('retained', '#logsRetainedCount', result.retainedCount);
        updateSummaryValue('warning', '#logsWarningCount', result.warningCount);
        updateSummaryValue('error', '#logsErrorCount', result.errorCount);
        updateSummaryValue('critical', '#logsCriticalCount', result.criticalCount);
    }

    function updateSummaryValue(key, selector, value) {
        const numeric = Number(value) || 0;
        const previous = summaryValues.get(key);

        $(selector).text(numeric.toLocaleString());

        if (!firstRender && previous !== undefined && previous !== numeric) {
            window.personalToolsMotion?.flash($(`[data-log-summary-card="${key}"]`).get(0));
        }

        summaryValues.set(key, numeric);
    }

    // Rows are always generated with jQuery; the Razor page only supplies the Bootstrap table
    // shell. This keeps user data out of server-rendered markup and avoids a full page refresh.
    function renderEntries(entries) {
        $tableBody.empty();
        visibleEntries.clear();

        if (!entries.length) {
            renderEmptyState();
            return;
        }

        entries.forEach(function (entry) {
            visibleEntries.set(String(entry.logId), entry);
            $tableBody.append(createLogRow(entry));
        });

        window.personalToolsMotion?.reveal(
            $tableBody.children('tr').toArray(),
            { fromY: 5, delay: 18, duration: 240 });
    }

    function createLogRow(entry) {
        const $row = $('<tr>', {
            tabindex: 0,
            role: 'button',
            'aria-label': 'View log details'
        }).attr('data-log-id', entry.logId);

        return $row.append(
            $('<td>', { class: 'text-nowrap font-monospace small' }).text(formatTableTime(entry.capturedUtc)),
            $('<td>').append(createLevelBadge(entry.level)),
            $('<td>', { class: 'logs-source-cell font-monospace small', title: entry.category || '' })
                .text(shortCategory(entry.category)),
            $('<td>', { class: 'logs-message-cell' }).append(
                $('<button>', {
                    type: 'button',
                    class: 'btn btn-link p-0 text-start text-decoration-none logs-message-button',
                    'aria-label': 'View log details',
                    text: entry.message || '—'
                })),
            $('<td>', { class: 'text-end' }).append(
                $('<button>', {
                    type: 'button',
                    class: 'btn btn-sm btn-outline-secondary',
                    'aria-label': 'View log details'
                }).append($('<i>', {
                    class: 'fa-solid fa-arrow-up-right-from-square',
                    'aria-hidden': 'true'
                })))
        );
    }

    function renderPagination(result) {
        const total = Number(result.filteredCount) || 0;
        const size = Number(result.pageSize) || 25;
        const start = total === 0 ? 0 : ((currentPage - 1) * size) + 1;
        const end = Math.min(currentPage * size, total);
        const $pagination = $('#logsPagination').empty();

        $('#logsResultCount').text(
            total === 0
                ? '0 results'
                : `${start.toLocaleString()}–${end.toLocaleString()} of ${total.toLocaleString()}`);

        appendPageButton($pagination, currentPage - 1, 'Previous', currentPage === 1, '‹');

        pageNumbers(currentPage, pageCount).forEach(function (page) {
            if (page === null) {
                $pagination.append(
                    $('<li>', { class: 'page-item disabled' }).append(
                        $('<span>', { class: 'page-link', text: '…' })));
                return;
            }

            appendPageButton($pagination, page, `Page ${page}`, false, String(page), page === currentPage);
        });

        appendPageButton($pagination, currentPage + 1, 'Next', currentPage === pageCount, '›');
    }

    function pageNumbers(active, total) {
        if (total <= 7) {
            return Array.from({ length: total }, (_, index) => index + 1);
        }

        const pages = new Set([1, total, active - 1, active, active + 1]);
        const sorted = Array.from(pages).filter(page => page > 0 && page <= total).sort((a, b) => a - b);
        const result = [];

        sorted.forEach(function (page, index) {
            if (index && page - sorted[index - 1] > 1) {
                result.push(null);
            }

            result.push(page);
        });

        return result;
    }

    function appendPageButton($pagination, page, label, disabled, text, active) {
        const $item = $('<li>', {
            class: `page-item${disabled ? ' disabled' : ''}${active ? ' active' : ''}`
        });
        const $button = $('<button>', {
            type: 'button',
            class: 'page-link',
            text,
            'aria-label': label,
            'aria-current': active ? 'page' : null,
            disabled
        }).attr('data-page', page);

        $pagination.append($item.append($button));
    }

    function renderEmptyState() {
        $tableBody.append(
            $('<tr>', { 'data-logs-empty': 'true' }).append(
                $('<td>', {
                    colspan: 5,
                    class: 'text-center py-5 small-muted',
                    text: 'No saved logs match the current filters.'
                })));
    }

    function createLevelBadge(level) {
        return $('<span>', { class: levelBadgeClasses(level), text: level || 'Unknown' });
    }

    function levelBadgeClasses(level) {
        switch (String(level || '').toLowerCase()) {
            case 'critical':
            case 'error':
                return 'badge rounded-pill text-bg-danger logs-level-badge';
            case 'warning':
                return 'badge rounded-pill text-bg-warning logs-level-badge';
            case 'information':
                return 'badge rounded-pill text-bg-light border logs-level-badge';
            default:
                return 'badge rounded-pill text-bg-secondary logs-level-badge';
        }
    }

    function openLog(entry) {
        if (!entry) {
            return;
        }

        selectedEntry = entry;
        $('#logDetailsLevel').attr('class', levelBadgeClasses(entry.level)).text(entry.level || 'Unknown');
        $('#logDetailsTime').text(formatDateTime(entry.capturedUtc));
        $('#logDetailsSource').text(entry.category || '—');
        $('#logDetailsEvent').text(entry.eventName ? `${entry.eventId} · ${entry.eventName}` : String(entry.eventId ?? '—'));
        $('#logDetailsMessage').text(entry.message || '—');

        const hasException = Boolean(String(entry.exception || '').trim());
        $('#logExceptionSection').toggleClass('d-none', !hasException);
        $('#logDetailsException').text(hasException ? entry.exception : '');
        bootstrap.Modal.getOrCreateInstance(document.getElementById('logDetailsModal')).show();
    }

    function copySelectedLog() {
        if (!selectedEntry || !navigator.clipboard?.writeText) {
            window.personalToolsToast?.warning('Clipboard access is not available in this browser.');
            return;
        }

        const eventText = selectedEntry.eventName
            ? `${selectedEntry.eventId} · ${selectedEntry.eventName}`
            : String(selectedEntry.eventId ?? '');
        const copy = [
            formatDateTime(selectedEntry.capturedUtc),
            selectedEntry.level,
            selectedEntry.category,
            `Event: ${eventText}`,
            '',
            selectedEntry.message,
            selectedEntry.exception ? `\n${selectedEntry.exception}` : ''
        ].join('\n');

        navigator.clipboard.writeText(copy)
            .then(() => window.personalToolsToast?.success({ message: 'Log entry copied.', delay: 1800 }))
            .catch(() => window.personalToolsToast?.error('The log entry could not be copied.'));
    }

    function connectSignalR() {
        if (typeof signalR === 'undefined') {
            startFallbackPolling();
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/monitoring')
            .withAutomaticReconnect()
            .build();

        connection.on('monitoringPulse', function (scope) {
            if (scope === 'logs' && $autoRefresh.is(':checked')) {
                loadLogs();
            }
        });
        connection.onreconnecting(() => setConnection('Reconnecting', false));
        connection.onreconnected(function () {
            signalRConnected = true;
            stopFallbackPolling();
            setConnection('Live', true);
            loadLogs();
        });
        connection.onclose(function () {
            signalRConnected = false;
            startFallbackPolling();
        });
        connection.start()
            .then(function () {
                signalRConnected = true;
                stopFallbackPolling();
                setConnection('Live', true);
            })
            .catch(startFallbackPolling);
    }

    function startFallbackPolling() {
        if ($autoRefresh.is(':checked')) {
            setConnection('Polling', true);
        }

        if (!fallbackTimer) {
            fallbackTimer = window.setInterval(function () {
                if ($autoRefresh.is(':checked')) {
                    loadLogs();
                }
            }, 5000);
        }
    }

    function stopFallbackPolling() {
        window.clearInterval(fallbackTimer);
        fallbackTimer = null;
    }

    function setConnection(label, live) {
        $connection
            .toggleClass('is-live', live)
            .toggleClass('is-offline', label === 'Offline')
            .find('span:last')
            .text(label);
    }

    function revealPage() {
        window.personalToolsMotion?.reveal(
            document.querySelectorAll('[data-monitor-reveal="summary"] > *'),
            { start: 40, delay: 55, fromY: 10, fromScale: .98, duration: 420 });
        window.personalToolsMotion?.reveal(
            document.querySelectorAll('[data-monitor-reveal="filters"], [data-monitor-reveal="table"]'),
            { start: 150, delay: 70, fromY: 12, fromScale: .99, duration: 430 });
    }

    function shortCategory(category) {
        return String(category || '—').replace(/^PersonalTools\./, '');
    }

    function formatTableTime(value) {
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString([], {
            hour: '2-digit', minute: '2-digit', second: '2-digit'
        });
    }

    function formatTime(value) {
        return formatTableTime(value);
    }

    function formatDateTime(value) {
        if (!value) {
            return '—';
        }

        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString([], {
            day: '2-digit', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit', second: '2-digit'
        });
    }

    $(document).on('click', '#logsTableBody tr[data-log-id]', function () {
        openLog(visibleEntries.get(String($(this).attr('data-log-id'))));
    });

    $(document).on('keydown', '#logsTableBody tr[data-log-id]', function (event) {
        if (event.target === this && (event.key === 'Enter' || event.key === ' ')) {
            event.preventDefault();
            $(this).trigger('click');
        }
    });

    $('#logsPagination').on('click', '.page-link[data-page]', function () {
        const page = Number($(this).data('page'));
        if (page < 1 || page > pageCount || page === currentPage) return;
        currentPage = page;
        loadLogs();
    });

    $('#btnCopyLog').on('click', copySelectedLog);
    $refresh.on('click', loadLogs);
    $minimumLevel.on('change', function () {
        currentPage = 1;
        loadLogs();
    });
    $pageSize.on('change', function () {
        currentPage = 1;
        loadLogs();
    });
    $search.on('input', function () {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(function () {
            currentPage = 1;
            loadLogs();
        }, 300);
    });
    $autoRefresh.on('change', function () {
        setConnection($(this).is(':checked') ? (signalRConnected ? 'Live' : 'Polling') : 'Paused', $(this).is(':checked'));
        if ($(this).is(':checked')) loadLogs();
    });
});
