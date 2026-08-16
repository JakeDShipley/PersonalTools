$(function () {
    const $page = $('.monitor-page[data-monitor-scope="server"]');
    if (!$page.length) return;

    const endpoint = $page.data('monitor-endpoint');
    const $refresh = $('#refreshMonitor');
    const $connection = $('#monitorConnectionState');
    let loading = false;
    let fallbackTimer = null;
    const previousMetrics = new Map();

    const chart = createChart();
    const tooltips = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltips.forEach(element => new bootstrap.Tooltip(element));

    function createChart() {
        const canvas = document.getElementById('serverMonitorChart');
        if (!canvas || typeof Chart === 'undefined') return null;
        const colors = chartColors();

        return new Chart(canvas, {
            type: 'line',
            data: { labels: [], datasets: [
                dataset('Processor', colors.coral),
                dataset('Memory', colors.mint),
                dataset('Storage', colors.blue)
            ]},
            options: chartOptions(colors)
        });
    }

    function dataset(label, color) {
        return { label, data: [], borderColor: color, backgroundColor: `${color}18`, borderWidth: 2, pointRadius: 0, pointHoverRadius: 3, fill: true, tension: .38 };
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
        return { responsive: true, maintainAspectRatio: false, animation: { duration: 450 }, interaction: { intersect: false, mode: 'index' },
            scales: { x: { grid: { display: false }, ticks: { color: colors.muted, maxTicksLimit: 6 } }, y: { beginAtZero: true, max: 100, grid: { color: colors.line }, ticks: { color: colors.muted, callback: value => `${value}%` } } },
            plugins: { legend: { labels: { color: colors.ink, boxWidth: 10, boxHeight: 10, usePointStyle: true } }, tooltip: { callbacks: { label: context => `${context.dataset.label}: ${formatPercent(context.raw)}` } } }
        };
    }

    function loadSnapshot() {
        if (loading) return;
        loading = true;
        $refresh.addClass('is-loading').prop('disabled', true);

        $.ajax({ url: endpoint, method: 'GET', cache: false })
            .done(render)
            .fail(renderUnavailable)
            .always(() => {
                loading = false;
                $refresh.removeClass('is-loading').prop('disabled', false);
            });
    }

    function render(snapshot) {
        if (!snapshot?.isAvailable) {
            renderUnavailable();
            return;
        }

        $('#monitorUnavailable').addClass('d-none');
        markPollingHealthy();
        setHealth(snapshot.health, snapshot.summary);
        $('#monitorUpdated').text(formatTime(snapshot.capturedUtc));
        setPercentMetric('cpuUsagePercent', snapshot.cpuUsagePercent);
        setPercentMetric('memoryUsagePercent', snapshot.memoryUsagePercent);
        setPercentMetric('storageUsagePercent', snapshot.storageUsagePercent);
        $('[data-server-detail="applicationUptimeSeconds"]').text(formatDuration(snapshot.applicationUptimeSeconds));
        $('[data-server-detail="applicationMemoryBytes"]').text(formatBytes(snapshot.applicationMemoryBytes));
        $('[data-server-detail="managedMemoryBytes"]').text(formatBytes(snapshot.managedMemoryBytes));
        addChartSample(snapshot);
    }

    function renderUnavailable() {
        $('#monitorUnavailable').removeClass('d-none');
        setHealth('Unavailable', 'A safe server snapshot could not be collected.');
        $('#monitorUpdated').text('—');
    }

    function setHealth(health, summary) {
        const value = String(health || 'Unavailable');
        $('#monitorHealth').text(value).attr('data-health', value.toLowerCase());
        $('#monitorSummary').text(summary);
    }

    function setPercentMetric(key, value) {
        const numeric = Number(value);
        const available = Number.isFinite(numeric);
        const $value = $(`[data-server-value="${key}"]`);
        $value.text(available ? formatPercent(numeric) : 'Warming up');
        const previous = previousMetrics.get(key);
        if (available && previous !== undefined && Math.abs(previous - numeric) >= .1) {
            window.personalToolsMotion?.flash($value.closest('.monitor-metric-card').get(0));
        }
        if (available) previousMetrics.set(key, numeric);
        const $bar = $(`[data-server-bar="${key}"]`);
        const width = available ? Math.max(0, Math.min(100, numeric)) : 0;
        $bar.removeClass('is-watch is-danger').toggleClass('is-watch', width >= 75 && width < 90).toggleClass('is-danger', width >= 90);
        $bar.parent().attr('aria-valuenow', available ? width : 0);
        requestAnimationFrame(() => $bar.css('width', `${width}%`));
    }

    function addChartSample(snapshot) {
        if (!chart) return;
        chart.data.labels.push(new Date(snapshot.capturedUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }));
        chart.data.datasets[0].data.push(snapshot.cpuUsagePercent);
        chart.data.datasets[1].data.push(snapshot.memoryUsagePercent);
        chart.data.datasets[2].data.push(snapshot.storageUsagePercent);
        trimChart(chart);
        chart.update('none');
    }

    function trimChart(instance) {
        while (instance.data.labels.length > 20) {
            instance.data.labels.shift();
            instance.data.datasets.forEach(item => item.data.shift());
        }
    }

    function connectSignalR() {
        if (typeof signalR === 'undefined') {
            setConnection('Polling', true);
            fallbackTimer = window.setInterval(loadSnapshot, 5000);
            return;
        }

        const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/monitoring').withAutomaticReconnect().build();
        connection.on('monitoringPulse', scope => { if (scope === 'server') loadSnapshot(); });
        connection.onreconnecting(() => setConnection('Reconnecting', false));
        connection.onreconnected(() => setConnection('Live', true));
        connection.onclose(() => { setConnection('Polling', true); if (!fallbackTimer) fallbackTimer = window.setInterval(loadSnapshot, 5000); });
        connection.start().then(() => setConnection('Live', true)).catch(() => { setConnection('Polling', true); fallbackTimer = window.setInterval(loadSnapshot, 5000); });
    }

    function markPollingHealthy() {
        if ($connection.find('span:last').text() === 'Connecting') setConnection('Polling', true);
    }

    function setConnection(label, live) {
        $connection.toggleClass('is-live', live).toggleClass('is-offline', label === 'Offline').find('span:last').text(label);
    }

    function updateChartTheme() {
        if (!chart) return;
        const colors = chartColors();
        chart.options = chartOptions(colors);
        chart.data.datasets[0].borderColor = colors.coral;
        chart.data.datasets[1].borderColor = colors.mint;
        chart.data.datasets[2].borderColor = colors.blue;
        chart.update('none');
    }

    const observer = new MutationObserver(updateChartTheme);
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'data-app-theme'] });
    $refresh.on('click', loadSnapshot);
    loadSnapshot();
    connectSignalR();

    function formatPercent(value) { return Number.isFinite(Number(value)) ? `${Number(value).toFixed(1)}%` : '—'; }
    function formatTime(value) { const date = new Date(value); return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }); }
    function formatBytes(bytes) { if (!Number.isFinite(Number(bytes)) || bytes < 0) return '—'; const units = ['B', 'KB', 'MB', 'GB']; let value = Number(bytes), unit = 0; while (value >= 1024 && unit < units.length - 1) { value /= 1024; unit++; } return `${value.toFixed(unit < 2 ? 0 : 1)} ${units[unit]}`; }
    function formatDuration(seconds) { const total = Math.max(0, Number(seconds) || 0); const days = Math.floor(total / 86400); const hours = Math.floor((total % 86400) / 3600); const minutes = Math.floor((total % 3600) / 60); return days > 0 ? `${days}d ${hours}h` : hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`; }
});
