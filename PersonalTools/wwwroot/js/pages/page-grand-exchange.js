$(function () {
    $('#grandExchangeSearchForm').on('submit', function () {
        var $button = $(this).find('button[type="submit"]');

        // A lookup can take a moment while the latest market prices are collected.
        $button.prop('disabled', true);
        $button.html('<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Searching...');
    });
});
