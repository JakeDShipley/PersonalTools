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
        const $identity = $('<div>', { class: 'admin-user-identity' }).append(
            $('<span>', { class: `admin-user-avatar ${admin ? 'is-admin' : ''}`, text: user.displayName.slice(0, 1).toUpperCase() }),
            $('<span>').append($('<strong>').text(user.displayName), $('<small>').text(user.email))
        );
        return $('<tr>').append(
            $('<td>').append($identity),
            $('<td>').append($('<span>', { class: `admin-role-badge ${admin ? 'is-admin' : 'is-user'}`, text: roleName(user) })),
            $('<td>').append($('<span>', { class: `admin-status-badge ${user.isActive ? 'is-active' : 'is-disabled'}`, text: user.isActive ? 'Active' : 'Disabled' })),
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
            $body.append($('<tr>').append($('<td>', { colspan: 6, class: 'text-center small-muted py-5', text: 'No users match those filters.' })));
        } else {
            visible.forEach(user => $body.append(createRow(user)));
            window.personalToolsMotion?.reveal($body.children().toArray(), { fromY: 8, delay: 26, duration: 240 });
        }
        renderPagination(totalPages);
    }

    function loadUsers() {
        $body.html('<tr><td colspan="6" class="text-center small-muted py-5">Loading registered users…</td></tr>');
        $.getJSON('/api/admin/users').done(response => {
            users = response || [];
            currentPage = 1;
            renderSummary();
            renderUsers();
        }).fail(xhr => {
            const error = xhr.responseJSON?.message || 'Registered users could not be loaded.';
            $body.html('<tr><td colspan="6" class="text-center text-danger py-5"></td></tr>').find('td').text(error);
        });
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
        modal.show();
    }

    $('#addManagedUser').on('click', () => openEditor(null));
    $('#managedUserSearch, #managedUserRoleFilter, #managedUserStatusFilter').on('input change', () => { currentPage = 1; renderUsers(); });
    $('#managedUsersPageSize').on('change', () => { currentPage = 1; renderUsers(); });
    $body.on('click', '.js-edit-managed-user', function () { openEditor(users.find(user => user.userId === $(this).data('user-id'))); });
    $('.js-password-toggle').on('click', function () { const input = document.getElementById($(this).data('target')); input.type = input.type === 'password' ? 'text' : 'password'; $(this).find('i').toggleClass('fa-eye fa-eye-slash'); });
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
