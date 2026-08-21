$(function () {
    const $page = $('.monitor-page[data-monitor-scope="database"]');
    if (!$page.length) return;

    const endpoint = $page.data('monitor-endpoint');
    const $refresh = $('#refreshMonitor');
    const $connection = $('#monitorConnectionState');

    let loading = false;
    let fallbackTimer = null;
    let firstSnapshot = true;

    const previousMetrics = new Map();
    const chart = createChart();

    initialiseTooltips();
    revealPage();
    loadSnapshot(false);
    connectSignalR();

    function initialiseTooltips() {
        $('[data-bs-toggle="tooltip"]').each(function () {
            new bootstrap.Tooltip(this);
        });
    }

    function revealPage() {
        if (!window.personalToolsMotion?.reveal) return;

        window.personalToolsMotion.reveal(
            document.querySelectorAll('[data-monitor-reveal="hero"]'),
            {
                start: 20,
                fromY: 8,
                fromScale: .99,
                duration: 420
            });

        window.personalToolsMotion.reveal(
            document.querySelectorAll('[data-monitor-reveal="primary"]'),
            {
                start: 90,
                delay: 65,
                fromY: 12,
                fromScale: .975,
                duration: 450
            });

        window.personalToolsMotion.reveal(
            document.querySelectorAll('[data-monitor-reveal="secondary"]'),
            {
                start: 180,
                delay: 48,
                fromY: 10,
                fromScale: .985,
                duration: 420
            });
    }

    function createChart() {
        const canvas = document.getElementById('databaseMonitorChart');

        if (!canvas || typeof Chart === 'undefined') {
            return null;
        }

        const colors = chartColors();

        return new Chart(canvas, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: 'Response time',
                        data: [],
                        borderColor: colors.coral,
                        backgroundColor: `${colors.coral}18`,
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 3,
                        fill: true,
                        tension: .38,
                        yAxisID: 'latency'
                    },
                    {
                        label: 'Connection use',
                        data: [],
                        borderColor: colors.mint,
                        backgroundColor: `${colors.mint}12`,
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 3,
                        fill: false,
                        tension: .38,
                        yAxisID: 'percent'
                    }
                ]
            },
            options: chartOptions(colors)
        });
    }

    function chartColors() {
        const styles = getComputedStyle(document.documentElement);

        return {
            ink: styles.getPropertyValue('--pt-ink').trim(),
            muted: styles.getPropertyValue('--pt-ink-soft').trim(),
            line: styles.getPropertyValue('--pt-line').trim(),
            coral: styles.getPropertyValue('--pt-coral').trim(),
            mint: styles.getPropertyValue('--pt-mint').trim()
        };
    }

    function chartOptions(colors) {
        return {
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 450
            },
            interaction: {
                intersect: false,
                mode: 'index'
            },
            scales: {
                x: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        color: colors.muted,
                        maxTicksLimit: 6
                    }
                },
                latency: {
                    beginAtZero: true,
                    position: 'left',
                    grid: {
                        color: colors.line
                    },
                    ticks: {
                        color: colors.muted,
                        callback: function (value) {
                            return `${value} ms`;
                        }
                    }
                },
                percent: {
                    beginAtZero: true,
                    max: 100,
                    position: 'right',
                    grid: {
                        display: false
                    },
                    ticks: {
                        color: colors.muted,
                        callback: function (value) {
                            return `${value}%`;
                        }
                    }
                }
            },
            plugins: {
                legend: {
                    labels: {
                        color: colors.ink,
                        boxWidth: 10,
                        boxHeight: 10,
                        usePointStyle: true
                    }
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            if (context.dataset.yAxisID === 'latency') {
                                return `${context.dataset.label}: ${Number(context.raw).toFixed(1)} ms`;
                            }

                            return `${context.dataset.label}: ${formatPercent(context.raw)}`;
                        }
                    }
                }
            }
        };
    }

    function loadSnapshot(forceRefresh) {
        if (loading) return;

        loading = true;

        $refresh.addClass('is-loading').prop('disabled', true);

        const requestUrl = forceRefresh
            ? `${endpoint}${String(endpoint).includes('?') ? '&' : '?'}forceRefresh=true`
            : endpoint;

        $.ajax({
            url: requestUrl,
            method: 'GET',
            cache: false
        })
            .done(function (snapshot) {
                render(snapshot);
            })
            .fail(function () {
                renderUnavailable();
            })
            .always(function () {
                loading = false;
                $refresh.removeClass('is-loading').prop('disabled', false);
            });
    }

    function render(snapshot) {
        if (!snapshot || !snapshot.isAvailable) {
            renderUnavailable();
            return;
        }

        $('#monitorUnavailable').addClass('d-none');

        markPollingHealthy();

        setHealth(snapshot.health, snapshot.summary);
        setSampleMode(snapshot.hasRecentSample);

        $('#monitorUpdated').text(formatTime(snapshot.capturedUtc));

        setMetric('responseTimeMs', Number(snapshot.responseTimeMs).toFixed(1), snapshot.responseTimeMs);
        setMetric('connectionUsagePercent', formatPercent(snapshot.connectionUsagePercent), snapshot.connectionUsagePercent);
        setMetric('queriesPerSecond', Number(snapshot.queriesPerSecond).toLocaleString(undefined, { maximumFractionDigits: 2 }), snapshot.queriesPerSecond);
        setMetric('structures', `${snapshot.requiredStructuresAvailable}/${snapshot.requiredStructuresTotal}`, snapshot.requiredStructuresAvailable);

        $('[data-database-caption="queriesPerSecond"]').text(snapshot.hasRecentSample ? 'recent statements per second' : 'service average until next sample');

        setBar(snapshot.connectionUsagePercent);

        setSignal('bufferPoolHitPercent', formatPercent(snapshot.bufferPoolHitPercent, 2), snapshot.bufferPoolHitPercent, snapshot.bufferPoolState);
        setSignal('diskTempTablePercent', formatPercent(snapshot.diskTempTablePercent, 2), snapshot.diskTempTablePercent, snapshot.diskTempTableState);
        setSignal('threadCreationPercent', formatPercent(snapshot.threadCreationPercent, 2), snapshot.threadCreationPercent, snapshot.threadCreationState);
        setSignal('activeRowLockWaits', Number(snapshot.activeRowLockWaits).toLocaleString(), snapshot.activeRowLockWaits, snapshot.rowLockState);

        setDetail('runningOperations', Number(snapshot.runningOperations).toLocaleString());
        setDetail('uptimeSeconds', formatDuration(snapshot.uptimeSeconds));
        setDetail('slowQueryPercent', formatPercent(snapshot.slowQueryPercent, 2));
        setDetail('connectionErrorPercent', formatPercent(snapshot.connectionErrorPercent, 2));
        setDetail('networkReceive', `${formatBytes(snapshot.networkReceiveBytesPerSecond)}/s`);
        setDetail('networkSend', `${formatBytes(snapshot.networkSendBytesPerSecond)}/s`);

        addChartSample(snapshot);
        animateSnapshotPulse();

        firstSnapshot = false;
    }

    function renderUnavailable() {
        $('#monitorUnavailable').removeClass('d-none');

        setHealth('Unavailable', 'A restricted database health snapshot could not be collected.');

        $('#monitorUpdated').text('—');
        $('#monitorSampleLabel').text('Unavailable');
    }

    function setHealth(health, summary) {
        const value = String(health || 'Unavailable');
        const $health = $('#monitorHealth');
        const previous = $health.text();

        $health
            .text(value)
            .attr('data-health', value.toLowerCase());

        $('#monitorSummary').text(summary);

        if (previous !== value && previous !== 'Loading') {
            window.personalToolsMotion?.pop($health.get(0), {
                fromScale: .88,
                duration: 360
            });
        }
    }

    function setSampleMode(hasRecentSample) {
        const label = hasRecentSample ? 'Live interval' : 'Baseline sample';
        const $label = $('#monitorSampleLabel');

        if ($label.text() === label) return;

        $label.text(label);

        window.personalToolsMotion?.pop(
            document.getElementById('monitorSampleMode'),
            {
                fromScale: .94,
                fromOpacity: .5,
                duration: 300
            });
    }

    function setBar(value) {
        const width = Math.max(0, Math.min(100, Number(value) || 0));
        const $bar = $('[data-database-bar="connectionUsagePercent"]');

        $bar
            .removeClass('is-watch is-danger')
            .toggleClass('is-watch', width >= 70 && width < 90)
            .toggleClass('is-danger', width >= 90);

        $bar.parent().attr('aria-valuenow', width);

        requestAnimationFrame(function () {
            $bar.css('width', `${width}%`);
        });
    }

    function setMetric(key, displayValue, numericValue) {
        const $value = $(`[data-database-value="${key}"]`);

        $value.text(displayValue);

        const numeric = Number(numericValue);
        const previous = previousMetrics.get(key);

        if (
            !firstSnapshot &&
            Number.isFinite(numeric) &&
            previous !== undefined &&
            Math.abs(previous - numeric) >= .01
        ) {
            window.personalToolsMotion?.flash(
                $value.closest('.monitor-metric-card').get(0));
        }

        if (Number.isFinite(numeric)) {
            previousMetrics.set(key, numeric);
        }
    }

    function setSignal(key, displayValue, numericValue, state) {
        setMetric(key, displayValue, numericValue);

        const $card = $(`[data-database-signal="${key}"]`);
        const $badge = $card.find('[data-signal-badge]');
        const normalizedState = String(state || 'Baseline').toLowerCase();
        const badgeLabel = signalLabel(key, normalizedState);

        const previousState = $card.attr('data-signal-health') || 'baseline';
        const previousLabel = $badge.text();

        if (normalizedState === 'baseline') {
            $card.removeAttr('data-signal-health');
            $badge.removeAttr('data-health');
        } else {
            $card.attr('data-signal-health', normalizedState);
            $badge.attr('data-health', normalizedState);
        }

        $badge.text(badgeLabel);

        if (
            !firstSnapshot &&
            (previousState !== normalizedState || previousLabel !== badgeLabel)
        ) {
            window.personalToolsMotion?.pop($badge.get(0), {
                fromScale: .9,
                duration: 320
            });
        }
    }

    function signalLabel(key, state) {
        const labels = {
            bufferPoolHitPercent: {
                baseline: 'Baseline',
                healthy: 'Strong',
                attention: 'Watch',
                critical: 'Low'
            },
            diskTempTablePercent: {
                baseline: 'Baseline',
                healthy: 'Low',
                attention: 'Watch',
                critical: 'High'
            },
            threadCreationPercent: {
                baseline: 'Baseline',
                healthy: 'Efficient',
                attention: 'Watch',
                critical: 'High'
            },
            activeRowLockWaits: {
                baseline: 'Checking',
                healthy: 'Clear',
                attention: 'Waiting',
                critical: 'Blocked'
            }
        };

        return labels[key]?.[state] || state;
    }

    function setDetail(key, value) {
        const $value = $(`[data-database-detail="${key}"]`);

        if ($value.text() === String(value)) return;

        $value.text(value);

        if (!firstSnapshot && window.personalToolsMotion?.available()) {
            window.personalToolsMotion.pop($value.get(0), {
                fromScale: .97,
                fromOpacity: .55,
                duration: 260
            });
        }
    }

    function addChartSample(snapshot) {
        if (!chart) return;

        chart.data.labels.push(
            new Date(snapshot.capturedUtc).toLocaleTimeString([], {
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit'
            }));

        chart.data.datasets[0].data.push(snapshot.responseTimeMs);
        chart.data.datasets[1].data.push(snapshot.connectionUsagePercent);

        while (chart.data.labels.length > 20) {
            chart.data.labels.shift();

            chart.data.datasets.forEach(function (dataset) {
                dataset.data.shift();
            });
        }

        chart.update('none');
    }

    function animateSnapshotPulse() {
        if (
            window.personalToolsMotion?.reducedMotion() ||
            !window.anime?.animate
        ) {
            return;
        }

        const icon = document.querySelector(
            '#monitorStatusCard .monitor-status-icon');

        const updated = document.getElementById('monitorUpdated');

        if (icon) {
            window.anime.animate(icon, {
                scale: {
                    from: .92,
                    to: 1
                },
                rotate: {
                    from: -4,
                    to: 0
                },
                duration: 420,
                ease: 'out(5)'
            });
        }

        if (updated) {
            window.anime.animate(updated, {
                opacity: {
                    from: .35,
                    to: 1
                },
                y: {
                    from: -3,
                    to: 0
                },
                duration: 300,
                ease: 'out(4)'
            });
        }
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
            if (scope === 'database') {
                loadSnapshot(false);
            }
        });

        connection.onreconnecting(function () {
            setConnection('Reconnecting', false);
        });

        connection.onreconnected(function () {
            stopFallbackPolling();
            setConnection('Live', true);
        });

        connection.onclose(function () {
            setConnection('Polling', true);
            startFallbackPolling();
        });

        connection.start()
            .then(function () {
                stopFallbackPolling();
                setConnection('Live', true);
            })
            .catch(function () {
                setConnection('Polling', true);
                startFallbackPolling();
            });
    }

    function startFallbackPolling() {
        setConnection('Polling', true);

        if (fallbackTimer) return;

        fallbackTimer = window.setInterval(function () {
            loadSnapshot(false);
        }, 15000);
    }

    function stopFallbackPolling() {
        if (!fallbackTimer) return;

        window.clearInterval(fallbackTimer);
        fallbackTimer = null;
    }

    function markPollingHealthy() {
        if ($connection.find('span:last').text() === 'Connecting') {
            setConnection('Polling', true);
        }
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

    function updateChartTheme() {
        if (!chart) return;

        const colors = chartColors();

        chart.options = chartOptions(colors);

        chart.data.datasets[0].borderColor = colors.coral;
        chart.data.datasets[0].backgroundColor = `${colors.coral}18`;

        chart.data.datasets[1].borderColor = colors.mint;
        chart.data.datasets[1].backgroundColor = `${colors.mint}12`;

        chart.update('none');
    }

    new MutationObserver(updateChartTheme).observe(
        document.documentElement,
        {
            attributes: true,
            attributeFilter: ['data-theme', 'data-app-theme']
        });

    $refresh.on('click', function () {
        loadSnapshot(true);
    });

    function formatPercent(value, decimals = 1) {
        return Number.isFinite(Number(value)) ? `${Number(value).toFixed(decimals)}%` : '—';
    }

    function formatTime(value) {
        const date = new Date(value);

        return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }

    function formatBytes(bytes) {
        if (!Number.isFinite(Number(bytes)) || Number(bytes) < 0) {
            return '—';
        }

        const units = ['B', 'KB', 'MB', 'GB'];
        let value = Number(bytes);
        let unit = 0;

        while (value >= 1024 && unit < units.length - 1) {
            value /= 1024;
            unit++;
        }

        return `${value.toFixed(unit < 2 ? 0 : 1)} ${units[unit]}`;
    }

    function formatDuration(seconds) {
        const total = Math.max(0, Number(seconds) || 0);
        const days = Math.floor(total / 86400);
        const hours = Math.floor((total % 86400) / 3600);
        const minutes = Math.floor((total % 3600) / 60);

        if (days > 0) {
            return `${days}d ${hours}h`;
        }

        if (hours > 0) {
            return `${hours}h ${minutes}m`;
        }

        return `${minutes}m`;
    }
});