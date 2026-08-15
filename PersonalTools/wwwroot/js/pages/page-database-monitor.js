$(function () {
    const $page = $('.monitor-page[data-monitor-scope="database"]');
    if (!$page.length) return;

    const endpoint = $page.data('monitor-endpoint');
    const $refresh = $('#refreshMonitor');
    const $connection = $('#monitorConnectionState');
    let loading = false;
    let fallbackTimer = null;

    const chart = createChart();
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(element => new bootstrap.Tooltip(element));

    function createChart() {
        const canvas = document.getElementById('databaseMonitorChart');
        if (!canvas || typeof Chart === 'undefined') return null;
        const colors = chartColors();

        return new Chart(canvas, {
            type: 'line',
            data: { labels: [], datasets: [
                { label: 'Response time', data: [], borderColor: colors.coral, backgroundColor: `${colors.coral}18`, borderWidth: 2, pointRadius: 0, fill: true, tension: .38, yAxisID: 'latency' },
                { label: 'Connection use', data: [], borderColor: colors.mint, backgroundColor: `${colors.mint}12`, borderWidth: 2, pointRadius: 0, fill: false, tension: .38, yAxisID: 'percent' }
            ]},
            options: chartOptions(colors)
        });
    }

    function chartColors() {
        const styles = getComputedStyle(document.documentElement);
        return { ink: styles.getPropertyValue('--pt-ink').trim(), muted: styles.getPropertyValue('--pt-ink-soft').trim(), line: styles.getPropertyValue('--pt-line').trim(), coral: styles.getPropertyValue('--pt-coral').trim(), mint: styles.getPropertyValue('--pt-mint').trim() };
    }

    function chartOptions(colors) {
        return { responsive: true, maintainAspectRatio: false, animation: { duration: 450 }, interaction: { intersect: false, mode: 'index' },
            scales: {
                x: { grid: { display: false }, ticks: { color: colors.muted, maxTicksLimit: 6 } },
                latency: { beginAtZero: true, position: 'left', grid: { color: colors.line }, ticks: { color: colors.muted, callback: value => `${value} ms` } },
                percent: { beginAtZero: true, max: 100, position: 'right', grid: { display: false }, ticks: { color: colors.muted, callback: value => `${value}%` } }
            },
            plugins: { legend: { labels: { color: colors.ink, boxWidth: 10, boxHeight: 10, usePointStyle: true } }, tooltip: { callbacks: { label: context => context.dataset.yAxisID === 'latency' ? `${context.dataset.label}: ${Number(context.raw).toFixed(1)} ms` : `${context.dataset.label}: ${formatPercent(context.raw)}` } } }
        };
    }

    function loadSnapshot() {
        if (loading) return;
        loading = true;
        $refresh.addClass('is-loading').prop('disabled', true);

        $.ajax({ url: endpoint, method: 'GET', cache: false })
            .done(render)
            .fail(renderUnavailable)
            .always(() => { loading = false; $refresh.removeClass('is-loading').prop('disabled', false); });
    }

    function render(snapshot) {
        if (!snapshot?.isAvailable) {
            renderUnavailable();
            return;
        }

        $('#monitorUnavailable').addClass('d-none');
        setHealth(snapshot.health, snapshot.summary);
        $('#monitorUpdated').text(formatTime(snapshot.capturedUtc));
        $('[data-database-value="responseTimeMs"]').text(Number(snapshot.responseTimeMs).toFixed(1));
        $('[data-database-value="connectionUsagePercent"]').text(formatPercent(snapshot.connectionUsagePercent));
        $('[data-database-value="queriesPerSecond"]').text(Number(snapshot.queriesPerSecond).toLocaleString(undefined, { maximumFractionDigits: 2 }));
        $('[data-database-value="structures"]').text(`${snapshot.requiredStructuresAvailable}/${snapshot.requiredStructuresTotal}`);
        setBar(snapshot.connectionUsagePercent);
        $('[data-database-detail="runningOperations"]').text(Number(snapshot.runningOperations).toLocaleString());
        $('[data-database-detail="uptimeSeconds"]').text(formatDuration(snapshot.uptimeSeconds));
        $('[data-database-detail="slowQueryPercent"]').text(formatPercent(snapshot.slowQueryPercent, 2));
        $('[data-database-detail="connectionErrorPercent"]').text(formatPercent(snapshot.connectionErrorPercent, 2));
        addChartSample(snapshot);
    }

    function renderUnavailable() {
        $('#monitorUnavailable').removeClass('d-none');
        setHealth('Unavailable', 'A restricted database health snapshot could not be collected.');
        $('#monitorUpdated').text('—');
    }

    function setHealth(health, summary) {
        const value = String(health || 'Unavailable');
        $('#monitorHealth').text(value).attr('data-health', value.toLowerCase());
        $('#monitorSummary').text(summary);
    }

    function setBar(value) {
        const width = Math.max(0, Math.min(100, Number(value) || 0));
        const $bar = $('[data-database-bar="connectionUsagePercent"]');
        $bar.removeClass('is-watch is-danger').toggleClass('is-watch', width >= 70 && width < 90).toggleClass('is-danger', width >= 90);
        $bar.parent().attr('aria-valuenow', width);
        requestAnimationFrame(() => $bar.css('width', `${width}%`));
    }

    function addChartSample(snapshot) {
        if (!chart) return;
        chart.data.labels.push(new Date(snapshot.capturedUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }));
        chart.data.datasets[0].data.push(snapshot.responseTimeMs);
        chart.data.datasets[1].data.push(snapshot.connectionUsagePercent);
        while (chart.data.labels.length > 20) {
            chart.data.labels.shift();
            chart.data.datasets.forEach(item => item.data.shift());
        }
        chart.update('none');
    }

    function connectSignalR() {
        if (typeof signalR === 'undefined') {
            setConnection('Polling', false);
            fallbackTimer = window.setInterval(loadSnapshot, 15000);
            return;
        }

        const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/monitoring').withAutomaticReconnect().build();
        connection.on('monitoringPulse', scope => { if (scope === 'database') loadSnapshot(); });
        connection.onreconnecting(() => setConnection('Reconnecting', false));
        connection.onreconnected(() => setConnection('Live', true));
        connection.onclose(() => { setConnection('Polling', false); if (!fallbackTimer) fallbackTimer = window.setInterval(loadSnapshot, 15000); });
        connection.start().then(() => setConnection('Live', true)).catch(() => { setConnection('Polling', false); fallbackTimer = window.setInterval(loadSnapshot, 15000); });
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
        chart.update('none');
    }

    new MutationObserver(updateChartTheme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    $refresh.on('click', loadSnapshot);
    loadSnapshot();
    connectSignalR();

    function formatPercent(value, decimals = 1) { return Number.isFinite(Number(value)) ? `${Number(value).toFixed(decimals)}%` : '—'; }
    function formatTime(value) { const date = new Date(value); return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }); }
    function formatDuration(seconds) { const total = Math.max(0, Number(seconds) || 0); const days = Math.floor(total / 86400); const hours = Math.floor((total % 86400) / 3600); const minutes = Math.floor((total % 3600) / 60); return days > 0 ? `${days}d ${hours}h` : hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`; }
});
