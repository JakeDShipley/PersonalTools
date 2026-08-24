$(function () {
    const $body = $('#managedUsersTable tbody');
    const $pagination = $('#managedUsersPagination');
    const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById('managedUserModal'));
    const token = () => $('input[name="__RequestVerificationToken"]').first().val();
    let users = [];
    let currentPage = 1;

    const isAdmin = user => user.role === 'Admin' || Number(user.role) === 2;
    const roleName = user => isAdmin(user) ? 'Admin' : 'User';
    const dateText = (value, empty) => value
        ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
        : empty;
    const isLockedOut = user => user.lockoutUntilUtc && new Date(user.lockoutUntilUtc).getTime() > Date.now();

    // The directory is rendered in JavaScript, so its security column and editor panel are kept
    // here as well. No account data is interpolated into HTML; all values are assigned with text().
    $('#managedUsersTable thead th').eq(3).before($('<th>', { text: 'Sign-in security' }));
    $body.find('td').attr('colspan', 7);

    const $securityPanel = $('<div>', { class: 'admin-user-modal-panel admin-user-security-panel mt-4 d-none', id: 'managedUserSecurityPanel' }).append(
        $('<div>', { class: 'd-flex flex-wrap align-items-start justify-content-between gap-3' }).append(
            $('<div>', { class: 'd-flex align-items-start gap-2' }).append(
                $('<span>', { class: 'admin-user-modal-section-icon' }).append($('<i>', { class: 'fa-solid fa-shield-halved' })),
                $('<div>').append(
                    $('<h3>', { class: 'h6 mb-1', text: 'Sign-in security' }),
                    $('<p>', { class: 'small-muted mb-0', text: 'Failed attempts and temporary account lockout.' })
                )
            ),
            $('<button>', { type: 'button', class: 'btn btn-sm btn-outline-warning', id: 'resetManagedUserLockout' }).append(
                $('<i>', { class: 'fa-solid fa-lock-open me-2' }),
                'Clear lockout'
            )
        ),
        $('<div>', { class: 'row g-2 mt-2' }).append(
            $('<div>', { class: 'col-sm-4' }).append($('<span>', { class: 'small-muted d-block', text: 'Current state' }), $('<strong>', { id: 'managedUserSecurityState' })),
            $('<div>', { class: 'col-sm-4' }).append($('<span>', { class: 'small-muted d-block', text: 'Failed attempts' }), $('<strong>', { id: 'managedUserFailedAttempts' })),
            $('<div>', { class: 'col-sm-4' }).append($('<span>', { class: 'small-muted d-block', text: 'Last failed attempt' }), $('<strong>', { id: 'managedUserLastFailedLogin' }))
        )
    );
    $('#managedUserPasswordHelp').closest('.mt-4').after($securityPanel);

    function matchingUsers() {
        const search = $('#managedUserSearch').val().trim().toLowerCase();
        const role = $('#managedUserRoleFilter').val();
        const status = $('#managedUserStatusFilter').val();
        return users.filter(user => {
            const searchable = `${user.displayName} ${user.email}`.toLowerCase();
            return (!search || searchable.includes(search))
                && (!role || roleName(user) === role)
                && (!status || (status === 'active' ? user.isActive : !user.isActive));
        });
    }

    function renderSummary() {
        const values = [
            users.length,
            users.filter(user => user.isActive).length,
            users.filter(isAdmin).length,
            users.filter(user => user.lastLoginUtc && Date.now() - new Date(user.lastLoginUtc).getTime() < 30 * 86400000).length
        ];
        $('#managedUserSummary .admin-users-stat').each(function (index) {
            $(this).removeClass('is-loading').find('strong').text(values[index]);
        });
    }

    function createRow(user) {
        const admin = isAdmin(user);
        const locked = isLockedOut(user);
        const $identity = $('<div>', { class: 'admin-user-identity' }).append(
            $('<span>', { class: `admin-user-avatar ${admin ? 'is-admin' : ''}`, text: user.displayName.slice(0, 1).toUpperCase() }),
            $('<span>').append($('<strong>').text(user.displayName), $('<small>').text(user.email))
        );
        const $loginSecurity = $('<td>').append($('<span>', {
            class: `admin-login-security-badge ${locked ? 'is-locked' : user.failedLoginAttempts ? 'has-attempts' : 'is-clear'}`,
            text: locked ? 'Temporarily locked' : user.failedLoginAttempts ? `${user.failedLoginAttempts} failed` : 'Clear'
        }));
        if (locked) {
            $loginSecurity.append($('<small>', { class: 'admin-login-security-until', text: `Until ${dateText(user.lockoutUntilUtc, '—')}` }));
        }

        return $('<tr>').append(
            $('<td>').append($identity),
            $('<td>').append($('<span>', { class: `admin-role-badge ${admin ? 'is-admin' : 'is-user'}`, text: roleName(user) })),
            $('<td>').append($('<span>', { class: `admin-status-badge ${user.isActive ? 'is-active' : 'is-disabled'}`, text: user.isActive ? 'Active' : 'Disabled' })),
            $loginSecurity,
            $('<td>').append($('<span>', { class: 'admin-users-date', text: dateText(user.lastLoginUtc, 'Not yet signed in') })),
            $('<td>').append($('<span>', { class: 'admin-users-date', text: dateText(user.createdUtc, '—') })),
            $('<td>', { class: 'text-end' }).append(
                $('<button>', { type: 'button', class: 'btn btn-sm btn-outline-primary js-edit-managed-user' })
                    .append($('<i>', { class: 'fa-solid fa-pen-to-square me-1' }), 'Edit')
                    .data('user-id', user.userId)
            )
        );
    }

    function appendPageButton(text, page, disabled, active, label) {
        const $button = $('<button>', { type: 'button', class: 'page-link', text, 'aria-label': label || `Page ${page}` }).prop('disabled', disabled);
        if (!disabled) $button.on('click', () => { currentPage = page; renderUsers(); });
        $pagination.append($('<li>', { class: `page-item${active ? ' active' : ''}${disabled ? ' disabled' : ''}` }).append($button));
    }

    function renderPagination(totalPages) {
        $pagination.empty();
        if (totalPages < 2) return;
        appendPageButton('‹', currentPage - 1, currentPage === 1, false, 'Previous page');
        const first = Math.max(1, currentPage - 2);
        const last = Math.min(totalPages, first + 4);
        for (let page = first; page <= last; page += 1) appendPageButton(String(page), page, false, page === currentPage);
        appendPageButton('›', currentPage + 1, currentPage === totalPages, false, 'Next page');
    }

    function renderUsers() {
        const matching = matchingUsers();
        const pageSize = Number($('#managedUsersPageSize').val());
        const totalPages = Math.max(1, Math.ceil(matching.length / pageSize));
        currentPage = Math.min(currentPage, totalPages);
        const start = (currentPage - 1) * pageSize;
        const visible = matching.slice(start, start + pageSize);
        $body.empty();
        $('#managedUserCount').text(`${users.length} ${users.length === 1 ? 'account' : 'accounts'}`);
        $('#managedUsersResultInfo').text(matching.length ? `Showing ${start + 1}–${start + visible.length} of ${matching.length} users` : 'No matching users');
        if (!visible.length) {
            $body.append($('<tr>').append($('<td>', { colspan: 7, class: 'text-center small-muted py-5', text: 'No users match those filters.' })));
        } else {
            visible.forEach(user => $body.append(createRow(user)));
            window.personalToolsMotion?.reveal($body.children().toArray(), { fromY: 8, delay: 26, duration: 240 });
        }
        renderPagination(totalPages);
    }

    function loadUsers() {
        $body.html('<tr><td colspan="7" class="text-center small-muted py-5">Loading registered users…</td></tr>');
        $.getJSON('/api/admin/users').done(response => {
            users = response || [];
            currentPage = 1;
            renderSummary();
            renderUsers();
        }).fail(xhr => {
            const error = xhr.responseJSON?.message || 'Registered users could not be loaded.';
            $body.html('<tr><td colspan="7" class="text-center text-danger py-5"></td></tr>').find('td').text(error);
        });
    }

    function renderSecurityPanel(user, isNew) {
        $('#managedUserSecurityPanel').toggleClass('d-none', isNew);
        if (isNew) return;

        const locked = isLockedOut(user);
        const attempts = Number(user.failedLoginAttempts || 0);
        $('#managedUserSecurityState').text(locked ? `Locked until ${dateText(user.lockoutUntilUtc, '—')}` : 'Available');
        $('#managedUserFailedAttempts').text(attempts);
        $('#managedUserLastFailedLogin').text(dateText(user.lastFailedLoginUtc, 'No failed attempts'));
        $('#resetManagedUserLockout').prop('disabled', !locked && attempts === 0).data('user-id', user.userId);
    }

    function openEditor(user) {
        const isNew = !user;
        $('#managedUserModalTitle').text(isNew ? 'Add user' : `Edit ${user.displayName}`);
        $('#managedUserModalCopy').text(isNew ? 'Create a new Personal Tools account.' : 'Update account details without exposing stored credentials.');
        $('#managedUserId').val(user?.userId || '');
        $('#managedUserDisplayName').val(user?.displayName || '');
        $('#managedUserEmail').val(user?.email || '');
        $('#managedUserRole').val(isAdmin(user || {}) ? 2 : 1);
        $('#managedUserActive').prop('checked', user?.isActive ?? true);
        $('#managedUserPassword, #managedUserConfirmPassword').val('').prop('required', isNew);
        $('#managedUserPasswordHelp').text(isNew ? 'Use 12–128 characters with uppercase, lowercase, a number and a symbol.' : 'Leave both fields blank to keep the existing password.');
        renderSecurityPanel(user, isNew);
        modal.show();
    }

    $('#addManagedUser').on('click', () => openEditor(null));
    $('#managedUserSearch, #managedUserRoleFilter, #managedUserStatusFilter').on('input change', () => { currentPage = 1; renderUsers(); });
    $('#managedUsersPageSize').on('change', () => { currentPage = 1; renderUsers(); });
    $body.on('click', '.js-edit-managed-user', function () { openEditor(users.find(user => user.userId === $(this).data('user-id'))); });
    $('.js-password-toggle').on('click', function () { const input = document.getElementById($(this).data('target')); input.type = input.type === 'password' ? 'text' : 'password'; $(this).find('i').toggleClass('fa-eye fa-eye-slash'); });
    $('#resetManagedUserLockout').on('click', function () {
        const userId = $(this).data('user-id');
        if (!userId) return;

        const $button = $(this).prop('disabled', true);
        $.ajax({
            url: `/api/admin/users/${encodeURIComponent(userId)}/login-lockout/reset`,
            method: 'POST',
            headers: { RequestVerificationToken: token() },
            successToast: 'Account sign-in lockout cleared.'
        }).done(updated => {
            const index = users.findIndex(user => user.userId === updated.userId);
            if (index >= 0) users[index] = updated;
            renderSummary();
            renderUsers();
            renderSecurityPanel(updated, false);
        }).always(() => $button.prop('disabled', false));
    });
    $('#managedUserForm').on('submit', function (event) {
        event.preventDefault();
        const userId = $('#managedUserId').val();
        const payload = { displayName: $('#managedUserDisplayName').val().trim(), email: $('#managedUserEmail').val().trim(), password: $('#managedUserPassword').val(), confirmPassword: $('#managedUserConfirmPassword').val(), role: Number($('#managedUserRole').val()), isActive: $('#managedUserActive').is(':checked') };
        $('#saveManagedUser').prop('disabled', true);
        $.ajax({ url: userId ? `/api/admin/users/${encodeURIComponent(userId)}` : '/api/admin/users', method: userId ? 'PUT' : 'POST', contentType: 'application/json; charset=utf-8', data: JSON.stringify(payload), headers: { RequestVerificationToken: token() } }).done(() => { modal.hide(); loadUsers(); }).always(() => $('#saveManagedUser').prop('disabled', false));
    });
    window.requestAnimationFrame(() => window.personalToolsMotion?.reveal(document.querySelectorAll('.admin-users-hero, .admin-users-summary, .admin-users-directory'), { fromY: 12, delay: 70, duration: 360 }));
    loadUsers();
});
