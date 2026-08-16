(function ($) {
    'use strict';

    const antiForgeryToken = () => $('input[name="__RequestVerificationToken"]').first().val();

    function save($item, value) {
        const key = $item.data('setting-key');
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
                window.personalToolsAppearance?.applySetting(key, value);
                $item.addClass('is-saved');
                window.setTimeout(() => $item.removeClass('is-saved'), 650);
            });
    }
    $('.settings-item select').on('change', function () { save($(this).closest('.settings-item'), $(this).val()); });
    $('.settings-item input[type="checkbox"]').on('change', function () { save($(this).closest('.settings-item'), this.checked ? 'true' : 'false'); });
    $('.settings-item[data-secret="true"] button').on('click', function () { const $item = $(this).closest('.settings-item'); const $input = $item.find('input'); const value = String($input.val() || ''); if (!value) { window.personalToolsToast.info('Enter a new key only when you want to replace the saved one.'); return; } save($item, value); $input.val(''); $item.find('small').removeClass('d-none'); });

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
