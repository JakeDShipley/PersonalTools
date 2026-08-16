(() => {
    'use strict';

    const form = $('#loginForm');
    if (!form.length) return;

    const launch = document.getElementById('loginLaunchSequence');
    let launchStarted = false;
    let launchStartedAt = 0;

    function splitLaunchWordmark() {
        launch?.querySelectorAll('[data-launch-word]').forEach((word) => {
            if (word.dataset.split === 'true') return;
            const text = word.textContent || '';
            word.textContent = '';
            [...text].forEach((character) => {
                const letter = document.createElement('span');
                letter.className = 'launch-letter';
                letter.textContent = character;
                word.appendChild(letter);
            });
            word.dataset.split = 'true';
        });
    }

    function playLaunchSequence() {
        if (!launch || launchStarted) return;
        launchStarted = true;
        launchStartedAt = Date.now();
        launch.classList.add('is-active');
        launch.setAttribute('aria-hidden', 'false');
        document.body.classList.add('login-launch-active');
        splitLaunchWordmark();

        if (window.personalToolsMotion?.reducedMotion() || !window.anime?.animate) return;

        const { animate, stagger } = window.anime;
        const letters = launch.querySelectorAll('.launch-letter');
        const wordmark = launch.querySelector('.login-launch-wordmark');

        animate(letters, {
            opacity: { from: 0, to: 1 },
            x: { from: 34, to: 0 },
            scaleX: { from: .45, to: 1 },
            delay: stagger(18, { start: 95 }),
            duration: 420,
            ease: 'out(5)'
        });
        animate(wordmark, {
            opacity: { from: 0, to: 1 },
            scale: { from: .96, to: 1 },
            duration: 300,
            ease: 'out(4)'
        });
    }

    function clearLaunchSequence() {
        if (!launchStarted) return;
        launchStarted = false;
        launchStartedAt = 0;
        launch?.classList.remove('is-active');
        launch?.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('login-launch-active');
    }

    function runSplashSequence() {
        const card = document.getElementById('loginCard');
        const title = document.getElementById('loginSplashTitle');
        const eyebrow = document.getElementById('loginEyebrow');
        const subtitle = document.getElementById('loginSubtitle');
        const finalText = title?.dataset.finalText || '';

        if (!card || !title || !finalText || window.personalToolsMotion?.reducedMotion() || !window.anime?.animate) return;

        const { animate } = window.anime;
        const glyphs = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%&*+-<>/\\';
        const startedAt = performance.now();
        const duration = 620;
        title.textContent = '······················';
        title.setAttribute('aria-label', finalText);

        animate(card, {
            rotate: { from: 8, to: 0 },
            scale: { from: .94, to: 1 },
            opacity: { from: .18, to: 1 },
            duration: 620,
            ease: 'out(5)'
        });
        animate(title, {
            opacity: { from: 0, to: 1 },
            scaleX: { from: .2, to: 1 },
            letterSpacing: { from: '-.24em', to: '-.025em' },
            duration: 560,
            ease: 'out(5)'
        });
        animate([eyebrow, subtitle].filter(Boolean), {
            opacity: { from: 0, to: 1 },
            y: { from: 7, to: 0 },
            delay: 280,
            duration: 340,
            ease: 'out(4)'
        });

        const scramble = (now) => {
            const progress = Math.min((now - startedAt) / duration, 1);
            const resolved = Math.floor(finalText.length * progress);
            title.textContent = [...finalText].map((character, index) => {
                if (character === ' ' || index < resolved) return character;
                return glyphs[Math.floor(Math.random() * glyphs.length)];
            }).join('');

            if (progress < 1) window.requestAnimationFrame(scramble);
            else title.textContent = finalText;
        };

        window.requestAnimationFrame(scramble);
    }

    runSplashSequence();

    form.on('submit', function (event) {
        event.preventDefault();

        const button = form.find('button[type="submit"]');
        const error = $('#loginError');
        const verificationToken = form.find('input[name="__RequestVerificationToken"]').val();

        error.addClass('d-none').text('');
        button.prop('disabled', true).text('Signing in…');
        playLaunchSequence();

        $.ajax({
            url: '/api/auth/login',
            method: 'POST',
            contentType: 'application/json',
            headers: { RequestVerificationToken: verificationToken },
            data: JSON.stringify({
                email: $('#Email').val(),
                password: $('#Password').val(),
                rememberMe: $('#RememberMe').is(':checked')
            }),
            showLoader: false,
            showToast: false
        })
            .done(() => {
                const returnUrl = $('#ReturnUrl').val();
                const minimumDuration = window.personalToolsMotion?.reducedMotion() ? 120 : 850;
                const remaining = Math.max(0, minimumDuration - (Date.now() - launchStartedAt));
                window.setTimeout(() => window.location.assign(returnUrl || '/'), remaining);
            })
            .fail((xhr) => {
                clearLaunchSequence();
                error.text(xhr.responseJSON?.message || 'Sign-in failed. Please try again.').removeClass('d-none');
            })
            .always(() => {
                button.prop('disabled', false).text('Sign in');
            });
    });
})();
