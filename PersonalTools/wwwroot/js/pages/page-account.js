$(function () {
    const $form = $('#changePasswordForm');
    const fields = ['#currentPassword', '#newPassword', '#confirmPassword'];

    function clearForm() {
        $form[0].reset();
        fields.forEach(selector => {
            const $input = $(selector).attr('type', 'password').removeClass('is-invalid');
            const $button = $(`[data-password-toggle="${$input.attr('id')}"]`);
            $button.attr({ 'aria-label': 'Show password', title: 'Show password' });
            $button.find('i').attr('class', 'fa-regular fa-eye');
        });
    }

    $('[data-password-toggle]').on('click', function () {
        const $button = $(this);
        const $input = $(`#${$button.data('password-toggle')}`);
        const show = $input.attr('type') === 'password';
        $input.attr('type', show ? 'text' : 'password');
        $button.attr({ 'aria-label': `${show ? 'Hide' : 'Show'} password`, title: `${show ? 'Hide' : 'Show'} password` });
        $button.find('i').attr('class', `fa-regular fa-eye${show ? '-slash' : ''}`);
    });

    $('#resetPasswordForm').on('click', clearForm);

    $form.on('submit', function (event) {
        event.preventDefault();

        const currentPassword = $('#currentPassword').val();
        const newPassword = $('#newPassword').val();
        const confirmPassword = $('#confirmPassword').val();

        fields.forEach(selector => $(selector).removeClass('is-invalid'));

        if (!currentPassword || !newPassword || !confirmPassword) {
            fields.forEach(selector => { if (!$(selector).val()) $(selector).addClass('is-invalid'); });
            window.personalToolsToast?.warning('Complete all password fields.');
            return;
        }

        if (newPassword !== confirmPassword) {
            $('#newPassword, #confirmPassword').addClass('is-invalid');
            window.personalToolsToast?.warning('The new password and confirmation do not match.');
            return;
        }

        $.ajax({
            url: '/api/account/password',
            method: 'POST',
            contentType: 'application/json',
            headers: { RequestVerificationToken: $form.find('input[name="__RequestVerificationToken"]').val() },
            data: JSON.stringify({ currentPassword, newPassword, confirmPassword }),
            loaderTitle: 'Securing your account',
            loaderMessage: 'Updating your password and ending other sessions…'
        }).done(clearForm).fail(() => $('#currentPassword').val('').trigger('focus'));
    });
});
