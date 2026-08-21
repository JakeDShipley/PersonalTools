$(function () {
    const token = () => $('input[name="__RequestVerificationToken"]').first().val();
    const payload = form => ({ startSide: form.find('[name="StartSide"]').val(), mapName: form.find('[name="MapName"]').val(), gameType: form.find('[name="GameType"]').val(), teamScore: Number(form.find('[name="TeamScore"]').val()), opponentScore: Number(form.find('[name="OpponentScore"]').val()), overtimeCount: Number(form.find('[name="OvertimeCount"]').val()) || 0 });
    const hide = selector => bootstrap.Modal.getInstance(document.querySelector(selector))?.hide();
    const refresh = () => window.setTimeout(() => window.location.reload(), 140);

    $(document).on('submit', '.js-match-form', function (event) {
        event.preventDefault();
        const form = $(this), id = form.find('[name="MatchId"]').val(), button = form.find('button[type="submit"]').prop('disabled', true);
        $.ajax({ url: id ? `/api/csmatches/${encodeURIComponent(id)}` : '/api/csmatches', method: id ? 'PUT' : 'POST', contentType: 'application/json', data: JSON.stringify(payload(form)), headers: { RequestVerificationToken: token() } })
            .done(() => { hide(id ? '#editMatchModal' : '#addMatchModal'); refresh(); })
            .always(() => button.prop('disabled', false));
    });

    $('#deleteMatchModal form').on('submit', function (event) {
        event.preventDefault();
        const id = $('#deleteMatchId').val(), button = $(this).find('button[type="submit"]').prop('disabled', true);
        if (!id) return;
        $.ajax({ url: `/api/csmatches/${encodeURIComponent(id)}`, method: 'DELETE', headers: { RequestVerificationToken: token() } })
            .done(() => { hide('#deleteMatchModal'); $(`.js-delete-match[data-match-id="${id}"]`).closest('.match-card-wrapper').fadeOut(180, function () { $(this).remove(); }); })
            .always(() => button.prop('disabled', false));
    });

    $('#deleteAllMatchesForm').on('submit', function (event) {
        event.preventDefault();
        $.ajax({ url: '/api/csmatches', method: 'DELETE', headers: { RequestVerificationToken: token() } })
            .done(() => { hide('#deleteAllMatchesModal'); $('#matchList').fadeOut(180, function () { $(this).empty().show(); }); window.personalToolsToast?.success('All matches deleted.'); });
    });
});
