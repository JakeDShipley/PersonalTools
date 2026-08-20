(function () {
    'use strict';

    const canvas = document.getElementById('appMatrixRainCanvas');

    if (!canvas) {
        window.personalToolsMatrixRain = { start() {}, stop() {}, sync() {}, setAmbient() {} };
        return;
    }

    const context = canvas.getContext('2d', { alpha: true, desynchronized: true });
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const glyphs = Array.from('0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz:;+-*/<>[]{}アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン');
    const signalText = [
        `USER // ${canvas.dataset.userLabel || 'GUEST'}`,
        'SKIN // AK-47 | VULCAN',
        'MAP // MIRAGE | 13-9',
        'MATCH // INFERNO | 16-14'
    ];

    let columns = [];
    let signal = null;
    let animationFrame = 0;
    let running = false;
    let lastFrame = 0;
    let resizeTimer = 0;
    let loaderActive = false;
    let ambientEnabled = document.documentElement.dataset.matrixAmbient === 'true';
    let width = 0;
    let height = 0;
    let fontSize = 16;
    let pixelRatio = 1;

    function random(min, max) {
        return min + Math.random() * (max - min);
    }

    function randomGlyph() {
        return glyphs[Math.floor(Math.random() * glyphs.length)];
    }

    function isMatrixTheme() {
        return document.documentElement.dataset.appTheme === 'matrix';
    }

    function makeColumn(index, initial) {
        const trailLength = Math.round(random(7, 23));

        return {
            x: (index * fontSize) + Math.round(random(-2, 2)),
            row: initial ? random(-height / fontSize, height / fontSize) : random(-18, -2),
            speed: random(6.5, 17),
            accumulator: 0,
            trailLength,
            glyphHistory: Array.from({ length: trailLength }, randomGlyph),
            brightness: random(.58, 1),
            gapChance: random(.025, .09)
        };
    }

    function sizeCanvas() {
        const bounds = canvas.getBoundingClientRect();
        width = Math.max(1, Math.round(bounds.width));
        height = Math.max(1, Math.round(bounds.height));
        pixelRatio = Math.min(window.devicePixelRatio || 1, 1.5);
        fontSize = width < 576 ? 14 : width < 1200 ? 15 : 17;

        canvas.width = Math.round(width * pixelRatio);
        canvas.height = Math.round(height * pixelRatio);
        context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
        context.textAlign = 'center';
        context.textBaseline = 'top';
        context.font = `600 ${fontSize}px "Cascadia Mono", Consolas, "Courier New", monospace`;

        const columnCount = Math.ceil(width / fontSize) + 1;
        columns = Array.from({ length: columnCount }, (_, index) => makeColumn(index, true));
        clear();
    }

    function clear() {
        context.clearRect(0, 0, width, height);
    }

    function advanceColumn(column, elapsedSeconds) {
        column.accumulator += elapsedSeconds * column.speed;

        while (column.accumulator >= 1) {
            column.accumulator -= 1;
            column.row += 1;
            column.glyphHistory.unshift(Math.random() < column.gapChance ? '' : randomGlyph());
            column.glyphHistory.length = column.trailLength;

            if (column.row * fontSize - column.trailLength * fontSize > height + random(0, height * .35)) {
                Object.assign(column, makeColumn(Math.round(column.x / fontSize), false));
            }
        }
    }

    function drawColumn(column) {
        for (let index = column.glyphHistory.length - 1; index >= 0; index -= 1) {
            const glyph = column.glyphHistory[index];

            if (!glyph) {
                continue;
            }

            const y = (column.row - index) * fontSize;

            if (y < -fontSize || y > height + fontSize) {
                continue;
            }

            const progress = 1 - (index / column.glyphHistory.length);
            const alpha = Math.pow(progress, 1.7) * column.brightness;
            const isHead = index === 0;

            context.shadowBlur = isHead ? 12 : progress > .72 ? 5 : 0;
            context.shadowColor = isHead ? 'rgba(170, 255, 205, .9)' : 'rgba(23, 237, 103, .7)';
            context.fillStyle = isHead
                ? `rgba(220, 255, 232, ${Math.min(1, alpha + .2)})`
                : `rgba(22, 225, 91, ${alpha})`;
            context.fillText(glyph, column.x, y);
        }
    }

    function updateSignal(elapsedSeconds) {
        if (!signal) {
            signal = {
                text: signalText[Math.floor(Math.random() * signalText.length)],
                x: random(width * .08, width * .68),
                y: random(-height * .1, height * .35),
                speed: random(28, 48),
                life: random(1.2, 2.1),
                age: 0,
                delay: random(1.2, 3.4)
            };
        }

        signal.age += elapsedSeconds;

        if (signal.age < signal.delay) {
            return;
        }

        const visibleAge = signal.age - signal.delay;

        if (visibleAge > signal.life) {
            signal = null;
            return;
        }

        signal.y += signal.speed * elapsedSeconds;
        const fade = Math.sin((visibleAge / signal.life) * Math.PI);

        context.save();
        context.font = `700 ${Math.max(11, fontSize - 3)}px "Cascadia Mono", Consolas, monospace`;
        context.textAlign = 'left';
        context.shadowBlur = 12;
        context.shadowColor = 'rgba(80, 255, 145, .9)';
        context.fillStyle = `rgba(182, 255, 207, ${fade * .9})`;
        context.fillText(signal.text, signal.x, signal.y);
        context.restore();
    }

    function draw(timestamp) {
        if (!running) {
            return;
        }

        // Thirty frames per second is visually smooth for falling glyphs and halves the canvas
        // work compared with blindly redrawing at the display's full refresh rate.
        if (timestamp - lastFrame < 33) {
            animationFrame = window.requestAnimationFrame(draw);
            return;
        }

        const elapsedSeconds = Math.min(.08, Math.max(.016, (timestamp - lastFrame) / 1000));
        lastFrame = timestamp;
        clear();

        columns.forEach(function (column) {
            advanceColumn(column, elapsedSeconds);
            drawColumn(column);
        });

        updateSignal(elapsedSeconds);
        animationFrame = window.requestAnimationFrame(draw);
    }

    function drawStillFrame() {
        clear();
        columns.forEach(drawColumn);
    }

    function activate() {
        if (running) {
            return;
        }

        sizeCanvas();

        if (reducedMotion.matches) {
            drawStillFrame();
            return;
        }

        running = true;
        lastFrame = performance.now() - 34;
        animationFrame = window.requestAnimationFrame(draw);
    }

    function deactivate() {
        running = false;
        window.cancelAnimationFrame(animationFrame);
        animationFrame = 0;
        signal = null;
        clear();
    }

    function sync() {
        const shouldRun = isMatrixTheme() && (loaderActive || ambientEnabled);

        if (shouldRun) {
            activate();
        } else {
            deactivate();
        }
    }

    function start() {
        loaderActive = true;
        sync();
    }

    function stop() {
        loaderActive = false;
        sync();
    }

    function setAmbient(enabled) {
        ambientEnabled = Boolean(enabled);
        document.documentElement.dataset.matrixAmbient = ambientEnabled ? 'true' : 'false';
        document.body.classList.toggle('matrix-ambient-active', ambientEnabled);

        const button = document.getElementById('matrixAmbientToggle');
        button?.setAttribute('aria-pressed', ambientEnabled ? 'true' : 'false');
        sync();
    }

    window.addEventListener('resize', function () {
        window.clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(function () {
            if (running || (reducedMotion.matches && isMatrixTheme())) {
                sizeCanvas();
                if (reducedMotion.matches) drawStillFrame();
            }
        }, 120);
    });
    document.addEventListener('visibilitychange', function () {
        if (document.hidden) {
            window.cancelAnimationFrame(animationFrame);
            animationFrame = 0;
        } else if (running && !animationFrame) {
            lastFrame = performance.now() - 34;
            animationFrame = window.requestAnimationFrame(draw);
        }
    });
    reducedMotion.addEventListener?.('change', sync);

    window.personalToolsMatrixRain = { start, stop, sync, setAmbient };
    sync();
})();
