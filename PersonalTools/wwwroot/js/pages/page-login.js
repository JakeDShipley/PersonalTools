(() => {
    'use strict';

    const form = $('#loginForm');
    if (!form.length) return;

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

        $.ajax({
            url: '/api/auth/login',
            method: 'POST',
            contentType: 'application/json',
            headers: { RequestVerificationToken: verificationToken },
            data: JSON.stringify({
                email: $('#Email').val(),
                password: $('#Password').val(),
                rememberMe: $('#RememberMe').is(':checked')
            })
        })
            .done(() => {
                const returnUrl = $('#ReturnUrl').val();
                window.location.assign(returnUrl || '/');
            })
            .fail((xhr) => {
                error.text(xhr.responseJSON?.message || 'Sign-in failed. Please try again.').removeClass('d-none');
            })
            .always(() => {
                button.prop('disabled', false).text('Sign in');
            });
    });
})();
