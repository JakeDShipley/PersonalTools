(() => {
    const input = document.getElementById('audioFileInput');
    if (!input) return;

    const dropZone = document.getElementById('audioDropZone');
    const editor = document.getElementById('audioEditor');
    const video = document.getElementById('audioVideoPreview');
    const audio = document.getElementById('audioPreview');
    const startRange = document.getElementById('audioStartRange');
    const endRange = document.getElementById('audioEndRange');
    const selection = document.getElementById('audioRangeSelection');
    const message = document.getElementById('audioStudioMessage');
    const exportButton = document.getElementById('audioExportButton');
    const ffmpeg = new FFmpegWASM.FFmpeg();
    const coreBaseUrl = 'https://cdn.jsdelivr.net/npm/@ffmpeg/core@0.12.10/dist/umd';
    let selectedFile;
    let objectUrl;
    let duration = 0;
    let ffmpegLoaded = false;

    const formatTime = (seconds) => {
        seconds = Math.max(0, Math.round(seconds || 0));
        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const remaining = seconds % 60;
        return hours ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remaining).padStart(2, '0')}` : `${minutes}:${String(remaining).padStart(2, '0')}`;
    };

    const showMessage = (text, type) => {
        message.textContent = text;
        message.className = `alert alert-${type} mt-4 mb-0`;
    };

    const setExportBusy = (busy, label) => {
        exportButton.disabled = busy;
        exportButton.innerHTML = busy
            ? `<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>${label}`
            : '<i class="fa-solid fa-scissors me-2"></i>Export trimmed audio';
    };

    const updateRange = () => {
        let start = Number(startRange.value);
        let end = Number(endRange.value);
        if (start >= end) {
            if (document.activeElement === startRange) start = Math.max(0, end - 0.1);
            else end = Math.min(duration, start + 0.1);
            startRange.value = start;
            endRange.value = end;
        }
        const startPercent = duration ? start / duration * 100 : 0;
        const endPercent = duration ? end / duration * 100 : 100;
        selection.style.left = `${startPercent}%`;
        selection.style.width = `${Math.max(0, endPercent - startPercent)}%`;
        document.getElementById('audioStartValue').textContent = formatTime(start);
        document.getElementById('audioEndValue').textContent = formatTime(end);
        document.getElementById('audioSelectionLength').textContent = formatTime(end - start);
    };

    const chooseFile = (file) => {
        if (!file) return;
        if (file.size > 500 * 1024 * 1024) return showMessage('Choose a file smaller than 500 MB for reliable device-side processing.', 'danger');
        selectedFile = file;
        duration = 0;
        if (objectUrl) URL.revokeObjectURL(objectUrl);
        objectUrl = URL.createObjectURL(file);
        const isVideo = file.type.startsWith('video/') || /\.(mp4|mov|webm|mkv|avi)$/i.test(file.name);
        video.classList.toggle('d-none', !isVideo);
        audio.classList.toggle('d-none', isVideo);
        const player = isVideo ? video : audio;
        player.src = objectUrl;
        player.onloadedmetadata = () => {
            duration = Number.isFinite(player.duration) ? player.duration : 0;
            if (!duration) return showMessage('The clip duration could not be read. Try a browser-supported media format.', 'danger');
            startRange.max = duration;
            endRange.max = duration;
            startRange.value = 0;
            endRange.value = duration;
            updateRange();
            editor.classList.remove('d-none');
            message.className = 'alert d-none mt-4 mb-0';
        };
        player.onerror = () => showMessage('This browser could not preview the selected file. Try MP4, WebM, MP3, WAV, or M4A.', 'danger');
        document.getElementById('audioFileName').textContent = file.name;
        document.getElementById('audioFileMeta').textContent = `${(file.size / (1024 * 1024)).toFixed(1)} MB · stays on this device`;
        player.load();
    };

    const toBlobUrl = async (url, mimeType) => {
        const response = await fetch(url, { cache: 'force-cache' });
        if (!response.ok) throw new Error('Could not download the local media processor.');
        return URL.createObjectURL(new Blob([await response.blob()], { type: mimeType }));
    };

    const ensureFfmpeg = async () => {
        if (ffmpegLoaded) return;
        setExportBusy(true, 'Preparing device processor');
        showMessage('Preparing the one-time browser audio processor. This can take a moment on the first use.', 'info');
        const coreURL = await toBlobUrl(`${coreBaseUrl}/ffmpeg-core.js`, 'text/javascript');
        const wasmURL = await toBlobUrl(`${coreBaseUrl}/ffmpeg-core.wasm`, 'application/wasm');
        await ffmpeg.load({ coreURL, wasmURL });
        ffmpegLoaded = true;
    };

    const extensionFor = (file) => {
        const match = /\.([a-z0-9]{1,8})$/i.exec(file.name);
        return match ? match[1].toLowerCase() : 'media';
    };

    dropZone.addEventListener('click', () => input.click());
    document.getElementById('audioChangeFile').addEventListener('click', () => input.click());
    input.addEventListener('change', () => chooseFile(input.files[0]));
    ['dragenter', 'dragover'].forEach((name) => dropZone.addEventListener(name, (event) => { event.preventDefault(); dropZone.classList.add('is-dragging'); }));
    ['dragleave', 'drop'].forEach((name) => dropZone.addEventListener(name, (event) => { event.preventDefault(); dropZone.classList.remove('is-dragging'); }));
    dropZone.addEventListener('drop', (event) => chooseFile(event.dataTransfer.files[0]));
    startRange.addEventListener('input', updateRange);
    endRange.addEventListener('input', updateRange);

    exportButton.addEventListener('click', async () => {
        if (!selectedFile || !duration) return showMessage('Choose a media file and wait for its duration to load.', 'danger');
        const outputFormat = document.getElementById('audioOutputFormat').value;
        const inputName = `input.${extensionFor(selectedFile)}`;
        const outputName = `personal-tools-trim.${outputFormat}`;
        const clipDuration = Number(endRange.value) - Number(startRange.value);
        try {
            await ensureFfmpeg();
            setExportBusy(true, 'Copying file locally');
            await ffmpeg.writeFile(inputName, new Uint8Array(await selectedFile.arrayBuffer()));
            setExportBusy(true, 'Extracting and trimming audio');
            const argumentsList = ['-ss', Number(startRange.value).toFixed(3), '-i', inputName, '-t', clipDuration.toFixed(3), '-vn', '-map', '0:a:0?'];
            if (outputFormat === 'm4a') argumentsList.push('-c:a', 'aac', '-b:a', '192k');
            else argumentsList.push('-c:a', 'libmp3lame', '-q:a', '2');
            argumentsList.push('-y', outputName);
            const exitCode = await ffmpeg.exec(argumentsList, 300_000);
            if (exitCode !== 0) throw new Error('The selected media could not be converted to audio.');
            const output = await ffmpeg.readFile(outputName);
            const blob = new Blob([output.buffer], { type: outputFormat === 'm4a' ? 'audio/mp4' : 'audio/mpeg' });
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = outputName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(link.href), 1000);
            await Promise.allSettled([ffmpeg.deleteFile(inputName), ffmpeg.deleteFile(outputName)]);
            showMessage('Your trimmed audio was created and downloaded on this device.', 'success');
        } catch (error) {
            console.error(error);
            showMessage(error?.message || 'The audio export could not be completed in this browser.', 'danger');
        } finally {
            setExportBusy(false);
        }
    });
})();
