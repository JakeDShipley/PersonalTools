(() => {
    const reducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true;

    function available() {
        return !reducedMotion() && typeof window.anime?.animate === 'function' && typeof window.anime?.stagger === 'function';
    }

    function reveal(targets, options = {}) {
        const elements = Array.from(targets || []).filter(Boolean);
        if (!elements.length) return;
        if (!available()) {
            elements.forEach(element => element.style.removeProperty('opacity'));
            return;
        }

        const { animate, stagger } = window.anime;
        const delay = options.delay ?? 42;
        elements.forEach(element => element.style.opacity = '0');
        animate(elements, {
            opacity: 1,
            y: { from: options.fromY ?? 10 },
            scale: { from: options.fromScale ?? .985 },
            delay: stagger(delay, { start: options.start ?? 0 }),
            duration: options.duration ?? 360,
            ease: options.ease ?? 'out(4)'
        });
    }

    function pop(target, options = {}) {
        if (!target || !available()) return;
        window.anime.animate(target, {
            scale: { from: options.fromScale ?? .92 },
            opacity: { from: options.fromOpacity ?? .2 },
            duration: options.duration ?? 360,
            ease: options.ease ?? 'out(5)'
        });
    }

    function flash(target) {
        if (!target || !available()) return;
        target.classList.remove('motion-flash');
        void target.offsetWidth;
        target.classList.add('motion-flash');
        window.setTimeout(() => target.classList.remove('motion-flash'), 700);
    }

    window.personalToolsMotion = { available, reveal, pop, flash, reducedMotion };
})();
