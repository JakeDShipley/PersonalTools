$(function () {
    const cookieRows = [
        { name: 'PersonalTools.Auth', purpose: 'Encrypted authentication cookie that identifies your server-side session.', duration: 'Up to 24 hours by default.' },
        { name: 'PersonalTools.SteamLinkState', purpose: 'One-time security value used only while you actively link a Steam account.', duration: 'Up to 10 minutes, then deleted.' }
    ];

    const body = $('#cookieTableRows');
    cookieRows.forEach(row => $('<tr>').append($('<td>').append($('<code>').text(row.name)), $('<td>').text(row.purpose), $('<td>').text(row.duration)).appendTo(body));
});
