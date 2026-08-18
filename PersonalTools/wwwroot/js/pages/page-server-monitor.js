$(function () {
    const $page = $('.monitor-page[data-monitor-scope="server"]');
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
                start: 80,
                delay: 58,
                fromY: 12,
                fromScale: .98,
                duration: 450
            });

        window.personalToolsMotion.reveal(
            document.querySelectorAll('[data-monitor-reveal="secondary"]'),
            {
                start: 170,
                delay: 46,
                fromY: 10,
                fromScale: .985,
                duration: 430
            });
    }

    function createChart() {
        const canvas = document.getElementById('serverMonitorChart');

        if (!canvas || typeof Chart === 'undefined') {
            return null;
        }

        const colors = chartColors();

        return new Chart(canvas, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    dataset('CPU', colors.coral),
                    dataset('Memory', colors.mint),
                    dataset('Storage', colors.blue)
                ]
            },
            options: chartOptions(colors)
        });
    }

    function dataset(label, color) {
        return {
            label: label,
            data: [],
            borderColor: color,
            backgroundColor: `${color}18`,
            borderWidth: 2,
            pointRadius: 0,
            pointHoverRadius: 3,
            fill: true,
            tension: .38
        };
    }

    function chartColors() {
        const styles = getComputedStyle(document.documentElement);

        return {
            ink: styles.getPropertyValue('--pt-ink').trim(),
            muted: styles.getPropertyValue('--pt-ink-soft').trim(),
            line: styles.getPropertyValue('--pt-line').trim(),
            coral: styles.getPropertyValue('--pt-coral').trim(),
            mint: styles.getPropertyValue('--pt-mint').trim(),
            blue: getComputedStyle($page[0]).getPropertyValue('--monitor-blue').trim()
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
                y: {
                    beginAtZero: true,
                    max: 100,
                    grid: {
                        color: colors.line
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

        const requestUrl = forceRefresh ? `${endpoint}${String(endpoint).includes('?') ? '&' : '?'}forceRefresh=true` : endpoint;

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
        setProcessSample(snapshot.applicationCpuUsagePercent !== null && snapshot.applicationCpuUsagePercent !== undefined);

        $('#monitorUpdated').text(formatTime(snapshot.capturedUtc));

        setPercentMetric('cpuUsagePercent', snapshot.cpuUsagePercent);
        setPercentMetric('memoryUsagePercent', snapshot.memoryUsagePercent);
        setPercentMetric('storageUsagePercent', snapshot.storageUsagePercent);

        setPercentMetric('applicationCpuUsagePercent', snapshot.applicationCpuUsagePercent);

        setMetric('applicationMemoryBytes', formatBytes(snapshot.applicationMemoryBytes), snapshot.applicationMemoryBytes, 1024 * 1024);
        setMetric('managedMemoryBytes', formatBytes(snapshot.managedMemoryBytes), snapshot.managedMemoryBytes, 512 * 1024);
        setMetric('applicationMemorySharePercent', formatPercent(snapshot.applicationMemorySharePercent, 2), snapshot.applicationMemorySharePercent, .1);

        setDetail('applicationUptimeSeconds', formatDuration(snapshot.applicationUptimeSeconds));
        setDetail('availableProcessorCount', Number(snapshot.availableProcessorCount).toLocaleString());
        setDetail('processThreadCount', Number(snapshot.processThreadCount).toLocaleString());

        addChartSample(snapshot);
        animateSnapshotPulse();

        firstSnapshot = false;
    }

    function renderUnavailable() {
        $('#monitorUnavailable').removeClass('d-none');

        setHealth('Unavailable', 'A restricted server health snapshot could not be collected.');

        $('#monitorUpdated').text('—');
        $('#monitorProcessSampleLabel').text('Unavailable');
    }

    function setHealth(health, summary) {
        const value = String(health || 'Unavailable');
        const $health = $('#monitorHealth');
        const previous = $health.text();

        $health.text(value).attr('data-health', value.toLowerCase());

        $('#monitorSummary').text(summary);

        if (previous !== value && previous !== 'Loading') {
            window.personalToolsMotion?.pop($health.get(0), {
                fromScale: .88,
                duration: 360
            });
        }
    }

    function setProcessSample(available) {
        const label = available ? 'Live process sample' : 'Process sampling';
        const $label = $('#monitorProcessSampleLabel');

        if ($label.text() === label) return;

        $label.text(label);

        window.personalToolsMotion?.pop(
            document.getElementById('monitorProcessSample'),
            {
                fromScale: .94,
                fromOpacity: .5,
                duration: 300
            });
    }

    function setPercentMetric(key, value) {
        const numeric = Number(value);
        const available = value !== null && value !== undefined && Number.isFinite(numeric);

        setMetric(
            key,
            available ? formatPercent(numeric) : 'Warming up',
            available ? numeric : null,
            .1);

        const $bar = $(`[data-server-bar="${key}"]`);
        const width = available
            ? Math.max(0, Math.min(100, numeric))
            : 0;

        $bar
            .removeClass('is-watch is-danger')
            .toggleClass('is-watch', width >= 75 && width < 90)
            .toggleClass('is-danger', width >= 90);

        $bar.parent().attr('aria-valuenow', available ? width : 0);

        requestAnimationFrame(function () {
            $bar.css('width', `${width}%`);
        });
    }

    function setMetric(key, displayValue, numericValue, changeThreshold) {
        const $value = $(`[data-server-value="${key}"]`);

        $value.text(displayValue);

        const numeric = Number(numericValue);
        const available = numericValue !== null &&
            numericValue !== undefined &&
            Number.isFinite(numeric);

        const previous = previousMetrics.get(key);

        if (
            !firstSnapshot &&
            available &&
            previous !== undefined &&
            Math.abs(previous - numeric) >= changeThreshold
        ) {
            window.personalToolsMotion?.flash(
                $value.closest('.monitor-metric-card').get(0));
        }

        if (available) {
            previousMetrics.set(key, numeric);
        }
    }

    function setDetail(key, value) {
        const $value = $(`[data-server-detail="${key}"]`);

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

        chart.data.datasets[0].data.push(snapshot.cpuUsagePercent);
        chart.data.datasets[1].data.push(snapshot.memoryUsagePercent);
        chart.data.datasets[2].data.push(snapshot.storageUsagePercent);

        trimChart(chart);

        if (firstSnapshot) {
            chart.update('none');
        } else {
            chart.update();
        }
    }

    function trimChart(instance) {
        while (instance.data.labels.length > 20) {
            instance.data.labels.shift();

            instance.data.datasets.forEach(function (item) {
                item.data.shift();
            });
        }
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
            if (scope === 'server') {
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
            startFallbackPolling();
        });

        connection.start()
            .then(function () {
                stopFallbackPolling();
                setConnection('Live', true);
            })
            .catch(function () {
                startFallbackPolling();
            });
    }

    function startFallbackPolling() {
        setConnection('Polling', true);

        if (fallbackTimer) return;

        fallbackTimer = window.setInterval(function () {
            loadSnapshot(false);
        }, 5000);
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
        chart.data.datasets[1].backgroundColor = `${colors.mint}18`;

        chart.data.datasets[2].borderColor = colors.blue;
        chart.data.datasets[2].backgroundColor = `${colors.blue}18`;

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
        return value !== null && value !== undefined && Number.isFinite(Number(value)) ? `${Number(value).toFixed(decimals)}%` : '—';
    }

    function formatTime(value) {
        const date = new Date(value);

        return Number.isNaN(date.getTime())? '—' : date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
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