$(function () {
    const $page = $('.monitor-page[data-monitor-scope="logs"]');
    if (!$page.length) return;

    const endpoint = $page.data('logs-endpoint');

    const $refresh = $('#btnRefreshLogs');
    const $connection = $('#logsConnectionState');
    const $search = $('#logsSearch');
    const $minimumLevel = $('#logsMinimumLevel');
    const $autoRefresh = $('#logsAutoRefresh');
    const $tableBody = $('#logsTableBody');
    const $detailsModal = $('#logDetailsModal');

    const maxRenderedRows = 250;
    const visibleEntries = new Map();
    const summaryValues = new Map();

    let loading = false;
    let reloadAfterCurrent = false;
    let lastId = 0;
    let searchTimer = null;
    let fallbackTimer = null;
    let signalRConnected = false;
    let firstRender = true;
    let selectedEntry = null;

    if ($detailsModal.length) {
        $detailsModal.appendTo(document.body);
    }

    revealPage();
    loadLogs(true);
    connectSignalR();

    function revealPage() {
        if (!window.personalToolsMotion?.reveal) return;

        window.personalToolsMotion.reveal(
            document.querySelectorAll('[data-monitor-reveal="summary"] > *'),
            {
                start: 40,
                delay: 55,
                fromY: 10,
                fromScale: .98,
                duration: 420
            });

        window.personalToolsMotion.reveal(
            document.querySelectorAll('[data-monitor-reveal="filters"], [data-monitor-reveal="table"]'),
            {
                start: 150,
                delay: 70,
                fromY: 12,
                fromScale: .99,
                duration: 430
            });
    }

    function loadLogs(replace) {
        if (loading) {
            if (replace) reloadAfterCurrent = true;
            return;
        }

        loading = true;

        if (replace) {
            lastId = 0;
        }

        $refresh.addClass('is-loading').prop('disabled', true);

        $.ajax({
            url: endpoint,
            method: 'GET',
            cache: false,
            showLoader: false,
            data: {
                afterId: replace ? 0 : lastId,
                minimumLevel: $minimumLevel.val(),
                search: ($search.val() || '').trim(),
                take: 200
            }
        })
            .done(function (result) {
                $('#logsUnavailable').addClass('d-none');

                updateSummary(result);
                renderEntries(result.entries || [], replace);

                lastId = Math.max(lastId, Number(result.latestId) || 0);

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
                    loadLogs(true);
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
            window.personalToolsMotion?.flash(
                $(`[data-log-summary-card="${key}"]`).get(0));
        }

        summaryValues.set(key, numeric);
    }

    function renderEntries(entries, replace) {
        if (replace) {
            $tableBody.empty();
            visibleEntries.clear();
        }

        if (!entries.length) {
            if (replace) renderEmptyState();
            return;
        }

        $tableBody.find('[data-logs-empty]').remove();

        if (replace) {
            entries.forEach(function (entry) {
                visibleEntries.set(Number(entry.id), entry);
                $tableBody.append(createLogRow(entry));
            });

            return;
        }

        const animatedTargets = [];

        entries.slice().reverse().forEach(function (entry) {
            const id = Number(entry.id);

            if (visibleEntries.has(id)) return;

            visibleEntries.set(id, entry);

            const $row = createLogRow(entry);

            $tableBody.prepend($row);

            const target = $row.find('[data-log-open]').first().get(0);
            if (target) animatedTargets.push(target);
        });

        trimRenderedRows();

        if (animatedTargets.length) {
            window.personalToolsMotion?.reveal(animatedTargets, {
                fromY: -4,
                fromScale: .99,
                delay: 25,
                duration: 260
            });
        }
    }

    function createLogRow(entry) {
        const $row = $('<tr>', {
            tabindex: 0,
            role: 'button',
            'aria-label': 'View log details'
        }).attr('data-log-id', entry.id);

        const $time = $('<td>', {
            class: 'text-nowrap font-monospace small'
        }).text(formatTableTime(entry.capturedUtc));

        const $level = $('<td>').append(
            createLevelBadge(entry.level));

        const $source = $('<td>', {
            class: 'logs-source-cell font-monospace small'
        })
            .attr('title', entry.category || '')
            .text(shortCategory(entry.category));

        const $messageButton = $('<button>', {
            type: 'button',
            class: 'btn btn-link p-0 text-start text-decoration-none logs-message-button',
            'aria-label': 'View log details'
        }).text(entry.message || '—');

        const $message = $('<td>', {
            class: 'logs-message-cell'
        }).append($messageButton);

        const $detailsButton = $('<button>', {
            type: 'button',
            class: 'btn btn-sm btn-outline-secondary',
            'aria-label': 'View log details'
        }).append(
            $('<i>', {
                class: 'fa-solid fa-arrow-up-right-from-square',
                'aria-hidden': 'true'
            }));

        const $details = $('<td>', {
            class: 'text-end'
        }).append($detailsButton);

        return $row.append(
            $time,
            $level,
            $source,
            $message,
            $details);
    }

    function createLevelBadge(level) {
        return $('<span>', {
            class: levelBadgeClasses(level)
        }).text(level || 'Unknown');
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

    function renderEmptyState() {
        $tableBody.empty().append(
            $('<tr>', {
                'data-logs-empty': 'true'
            }).append(
                $('<td>', {
                    colspan: 5,
                    class: 'text-center py-5 small-muted'
                }).text('No logs match the current filters.')
            )
        );
    }

    function trimRenderedRows() {
        let $rows = $tableBody.children('tr[data-log-id]');

        while ($rows.length > maxRenderedRows) {
            const $last = $rows.last();
            visibleEntries.delete(Number($last.attr('data-log-id')));
            $last.remove();
            $rows = $tableBody.children('tr[data-log-id]');
        }
    }

    function openLog(entry) {
        if (!entry) return;

        selectedEntry = entry;

        $('#logDetailsLevel')
            .attr('class', levelBadgeClasses(entry.level))
            .text(entry.level || 'Unknown');

        $('#logDetailsTime').text(formatDateTime(entry.capturedUtc));
        $('#logDetailsSource').text(entry.category || '—');

        const eventText = entry.eventName
            ? `${entry.eventId} · ${entry.eventName}`
            : String(entry.eventId ?? '—');

        $('#logDetailsEvent').text(eventText);
        $('#logDetailsMessage').text(entry.message || '—');

        const hasException = !!String(entry.exception || '').trim();

        $('#logExceptionSection').toggleClass('d-none', !hasException);
        $('#logDetailsException').text(hasException ? entry.exception : '');

        bootstrap.Modal
            .getOrCreateInstance(document.getElementById('logDetailsModal'))
            .show();
    }

    function copySelectedLog() {
        if (!selectedEntry) return;

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

        if (!navigator.clipboard?.writeText) {
            window.personalToolsToast?.warning('Clipboard access is not available in this browser.');
            return;
        }

        navigator.clipboard.writeText(copy)
            .then(function () {
                window.personalToolsToast?.success({
                    message: 'Log entry copied.',
                    delay: 1800
                });
            })
            .catch(function () {
                window.personalToolsToast?.error('The log entry could not be copied.');
            });
    }

    function resetAndLoad() {
        lastId = 0;
        loadLogs(true);
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
                loadLogs(false);
            }
        });

        connection.onreconnecting(function () {
            signalRConnected = false;

            if ($autoRefresh.is(':checked')) {
                setConnection('Reconnecting', false);
            }
        });

        connection.onreconnected(function () {
            signalRConnected = true;
            stopFallbackPolling();

            if ($autoRefresh.is(':checked')) {
                setConnection('Live', true);
                loadLogs(false);
            }
        });

        connection.onclose(function () {
            signalRConnected = false;
            startFallbackPolling();
        });

        connection.start()
            .then(function () {
                signalRConnected = true;
                stopFallbackPolling();

                if ($autoRefresh.is(':checked')) {
                    setConnection('Live', true);
                }
            })
            .catch(function () {
                signalRConnected = false;
                startFallbackPolling();
            });
    }

    function startFallbackPolling() {
        if ($autoRefresh.is(':checked')) {
            setConnection('Polling', true);
        }

        if (fallbackTimer) return;

        fallbackTimer = window.setInterval(function () {
            if ($autoRefresh.is(':checked')) {
                loadLogs(false);
            }
        }, 5000);
    }

    function stopFallbackPolling() {
        if (!fallbackTimer) return;

        window.clearInterval(fallbackTimer);
        fallbackTimer = null;
    }

    function setConnection(label, live) {
        const previous = $connection.find('span:last').text();

        $connection
            .toggleClass('is-live', live)
            .toggleClass('is-offline', label === 'Offline')
            .find('span:last')
            .text(label);

        if (previous !== label) {
            window.personalToolsMotion?.pop($connection.get(0), {
                fromScale: .96,
                fromOpacity: .6,
                duration: 280
            });
        }
    }

    function shortCategory(category) {
        return String(category || '—')
            .replace(/^PersonalTools\./, '');
    }

    function formatTableTime(value) {
        const date = new Date(value);

        if (Number.isNaN(date.getTime())) return '—';

        return date.toLocaleTimeString([], {
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        });
    }

    function formatTime(value) {
        return formatTableTime(value);
    }

    function formatDateTime(value) {
        const date = new Date(value);

        if (Number.isNaN(date.getTime())) return '—';

        return date.toLocaleString([], {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        });
    }

    $(document).on('click', '#logsTableBody tr[data-log-id]', function () {
        openLog(
            visibleEntries.get(Number($(this).data('log-id'))));
    });

    $(document).on('keydown', '#logsTableBody tr[data-log-id]', function (event) {
        if (event.target !== this) return;
        if (event.key !== 'Enter' && event.key !== ' ') return;

        event.preventDefault();
        $(this).trigger('click');
    });

    $('#btnCopyLog').on('click', copySelectedLog);

    $refresh.on('click', function () {
        resetAndLoad();
    });

    $minimumLevel.on('change', resetAndLoad);

    $search.on('input', function () {
        window.clearTimeout(searchTimer);

        searchTimer = window.setTimeout(function () {
            resetAndLoad();
        }, 300);
    });

    $autoRefresh.on('change', function () {
        if ($(this).is(':checked')) {
            setConnection(
                signalRConnected ? 'Live' : 'Polling',
                true);

            loadLogs(false);
            return;
        }

        setConnection('Paused', false);
    });
});