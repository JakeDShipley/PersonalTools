(function () {
    'use strict';

    const keys = {
        theme: 'personal-tools-appearance-theme',
        mode: 'personal-tools-appearance-mode',
        matrixAmbient: 'personal-tools-matrix-ambient'
    };

    const validThemes = ['personal', 'tactical', 'matrix'];

    function read(key) {
        try {
            return window.localStorage.getItem(key);
        } catch {
            // Privacy modes can disable persistent storage. The server-rendered database value
            // remains a safe fallback and the appearance controls still work for this page.
            return null;
        }
    }

    function write(key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch {
            // A blocked storage write must never prevent the user from changing the live theme.
        }
    }

    function current() {
        const root = document.documentElement;
        const storedTheme = read(keys.theme);
        const storedMode = read(keys.mode);
        const storedAmbient = read(keys.matrixAmbient);

        return {
            theme: validThemes.includes(storedTheme) ? storedTheme : root.dataset.appTheme,
            mode: storedMode === 'dark' || storedMode === 'light' ? storedMode : root.dataset.theme,
            matrixAmbient: storedAmbient === 'true' || storedAmbient === 'false'
                ? storedAmbient === 'true'
                : root.dataset.matrixAmbient === 'true'
        };
    }

    function set(key, value) {
        if (key === 'AppearanceTheme' && validThemes.includes(value)) {
            write(keys.theme, value);
        }

        if (key === 'AppearanceMode' && (value === 'light' || value === 'dark')) {
            write(keys.mode, value);
        }

        if (key === 'MatrixAmbientBackground' && (value === 'true' || value === 'false')) {
            write(keys.matrixAmbient, value);
        }
    }

    // Apply browser preferences before stylesheets are parsed. This prevents a database-backed
    // fallback theme flashing briefly before the persistent browser appearance takes over.
    const initial = current();
    document.documentElement.dataset.appTheme = initial.theme;
    document.documentElement.dataset.theme = initial.mode;
    document.documentElement.dataset.matrixAmbient = initial.matrixAmbient ? 'true' : 'false';

    window.personalToolsAppearanceStorage = {
        current,
        set
    };
})();
