(function ($) {
    'use strict';

    const antiForgeryToken = () => $('input[name="__RequestVerificationToken"]').first().val();
    const appearanceKeys = ['AppearanceTheme', 'AppearanceMode', 'MatrixAmbientBackground'];

    function applyBrowserAppearance(key, value) {
        if (appearanceKeys.includes(key)) {
            window.personalToolsAppearance?.applySetting(key, value);
        }
    }

    function save($item, value) {
        const key = $item.data('setting-key');

        // Appearance changes are local and immediate. The authenticated request which follows
        // synchronises the preference to the account without holding the live interface hostage.
        applyBrowserAppearance(key, value);

        $.ajax({
            url: '/api/settings',
            method: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({ key: key, value: value }),
            headers: { RequestVerificationToken: antiForgeryToken() },
            successToast: 'Setting saved.',
            showLoader: true,
            loaderTitle: 'Saving setting',
            loaderMessage: 'Applying your preference…'
        })
            .done(function () {
                $item.addClass('is-saved');
                window.setTimeout(() => $item.removeClass('is-saved'), 650);
            });
    }

    const browserAppearance = window.personalToolsAppearance?.current();

    if (browserAppearance) {
        $('.settings-item[data-setting-key="AppearanceTheme"] select').val(browserAppearance.theme);
        $('.settings-item[data-setting-key="AppearanceMode"] select').val(browserAppearance.mode);
        $('.settings-item[data-setting-key="MatrixAmbientBackground"] input[type="checkbox"]')
            .prop('checked', browserAppearance.matrixAmbient);
    }

    $('.settings-item select').on('change', function () { save($(this).closest('.settings-item'), $(this).val()); });
    $('.settings-item input[type="checkbox"]').on('change', function () { save($(this).closest('.settings-item'), this.checked ? 'true' : 'false'); });
    $('.settings-item[data-secret="true"] button').on('click', function () { const $item = $(this).closest('.settings-item'); const $input = $item.find('input'); const value = String($input.val() || ''); if (!value) { window.personalToolsToast.info('Enter a new key only when you want to replace the saved one.'); return; } save($item, value); $input.val(''); $item.find('small').removeClass('d-none'); });

    $('#trackerAutoCloseDays').on('change', function () {
        const $input = $(this);
        const days = parseInt($input.val(), 10);
        if (!days || days < 1 || days > 365) {
            window.personalToolsToast.error('Enter a number of days between 1 and 365.');
            return;
        }

        $.ajax({
            url: '/api/tracker/settings',
            method: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({ autoCloseAfterDays: days }),
            headers: { RequestVerificationToken: antiForgeryToken() },
            showLoader: true,
            loaderTitle: 'Saving setting'
        }).fail(function (xhr) {
            window.personalToolsToast.error(xhr.responseJSON?.message || 'This setting could not be saved.');
        });
    });

    $('#pasteBinMaximumUploadSizeMb').on('change', function () {
        const size = parseInt($(this).val(), 10);
        if (!size || size < 1 || size > 50) {
            window.personalToolsToast.error('Enter a Paste Bin upload limit between 1 and 50 MB.');
            return;
        }

        $.ajax({
            url: '/api/paste-bin/settings',
            method: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({ maximumUploadSizeMb: size }),
            headers: { RequestVerificationToken: antiForgeryToken() },
            successToast: 'Paste Bin upload limit saved.',
            loaderTitle: 'Saving upload limit'
        });
    });

    $('#settingsSteamLinkForm').on('submit', function (event) {
        event.preventDefault();
        const $form = $(this);
        const profileReference = String($('#settingsManualSteamId').val() || '').trim();
        if (!profileReference) {
            window.personalToolsToast.info('Find a Steam profile before linking it.');
            return;
        }

        $.ajax({
            url: '/api/steam/link',
            method: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({ profileReference: profileReference }),
            headers: { RequestVerificationToken: $form.find('input[name="__RequestVerificationToken"]').val() },
            showLoader: true,
            loaderTitle: 'Linking Steam',
            loaderMessage: 'Confirming the selected profile…'
        })
            .done(function (response) {
                window.personalToolsToast.success(response.message || 'Steam account linked.');
                window.setTimeout(function () { window.location.reload(); }, 550);
            })
            .fail(function (xhr) {
                window.personalToolsToast.error(xhr.responseJSON?.message || 'The Steam account could not be linked.');
            });
    });
})(jQuery);
