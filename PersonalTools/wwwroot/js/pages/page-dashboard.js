$(function () {
    const dashboardToolGroups = document.getElementById('dashboardToolGroups');

    function applyDashboardViewPreference() {
        if (!dashboardToolGroups) return;

        $.getJSON('/api/settings').done((settings) => {
            const viewSetting = settings.find((setting) => {
                const key = setting.definition?.key;
                return key === 'DashboardDefaultView' || key === 0;
            });

            dashboardToolGroups.classList.toggle('is-list-view', viewSetting?.value === 'list');
        });
    }

    applyDashboardViewPreference();

    window.requestAnimationFrame(() => {
        window.personalToolsMotion?.reveal(document.querySelectorAll('.dashboard-tool-card'), { fromY: 12, delay: 34, duration: 340 });
        window.personalToolsMotion?.reveal(document.querySelectorAll('.dashboard-widget'), { fromY: 14, delay: 55, start: 150, duration: 380 });
    });
    const weatherCodes={0:['fa-sun','Clear skies'],1:['fa-cloud-sun','Mainly clear'],2:['fa-cloud-sun','Partly cloudy'],3:['fa-cloud','Overcast'],45:['fa-smog','Foggy'],51:['fa-cloud-rain','Light drizzle'],61:['fa-cloud-rain','Rain'],71:['fa-snowflake','Snow'],80:['fa-cloud-showers-heavy','Showers'],95:['fa-cloud-bolt','Thunderstorms']};
    const token=()=>$('input[name="__RequestVerificationToken"]').first().val(),weatherWidget=$('#weatherWidget');
    function weatherCard(location,forecast){const current=forecast.current,condition=weatherCodes[current.weather_code]||['fa-cloud','Conditions unavailable'];return $('<article class="weather-location-card">').append($('<span class="weather-icon">').append($('<i>').addClass(`fa-solid ${condition[0]}`)),$('<span class="weather-location-copy">').append($('<strong>').text(location.displayName),$('<small>').text(`${condition[1]} · ${Math.round(current.wind_speed_10m)} mph`)),$('<strong class="weather-temperature">').text(`${Math.round(current.temperature_2m)}°`),$('<button class="weather-location-remove" type="button" aria-label="Remove weather location">').data('location-id',location.weatherLocationId).append('<i class="fa-solid fa-xmark"></i>'));}
    function loadWeatherLocations(){weatherWidget.html('<span class="small-muted">Loading saved locations&hellip;</span>');$.getJSON('/api/dashboard/weather-locations').done(locations=>{weatherWidget.empty();if(!locations.length){weatherWidget.append('<span class="small-muted">Add a town or city to see its weather here.</span>');return;}locations.forEach(location=>{const placeholder=$('<div class="weather-location-card is-loading"><span class="weather-icon"><i class="fa-solid fa-circle-notch fa-spin"></i></span><span class="weather-location-copy"><strong></strong><small>Checking conditions&hellip;</small></span></div>');placeholder.find('strong').text(location.displayName);weatherWidget.append(placeholder);$.getJSON(`https://api.open-meteo.com/v1/forecast?latitude=${encodeURIComponent(location.latitude)}&longitude=${encodeURIComponent(location.longitude)}&current=temperature_2m,weather_code,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=mph`).done(forecast=>placeholder.replaceWith(weatherCard(location,forecast))).fail(()=>placeholder.replaceWith($('<div class="weather-location-card weather-location-unavailable">').append($('<span>').text(location.displayName),$('<small>').text('Conditions are unavailable right now.'))));});}).fail(()=>weatherWidget.html('<span class="small-muted">Saved weather locations are unavailable right now.</span>'));}
    $('#weatherLocationForm').on('submit',function(event){event.preventDefault();const input=$('#weatherLocation'),query=input.val().trim(),feedback=$('#weatherLocationFeedback');if(!query)return;feedback.text('Finding that location&hellip;');$.getJSON(`https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(query)}&count=1&language=en&format=json`).done(geo=>{const place=geo.results?.[0];if(!place){feedback.text('No matching town or city was found.');return;}$.ajax({url:'/api/dashboard/weather-locations',method:'POST',contentType:'application/json',data:JSON.stringify({displayName:[place.name,place.admin1,place.country].filter(Boolean).join(', '),latitude:place.latitude,longitude:place.longitude}),headers:{RequestVerificationToken:token()}}).done(()=>{input.val('');feedback.text('Location saved.');loadWeatherLocations();}).fail(xhr=>feedback.text(xhr.responseJSON?.message||'That location could not be saved.'));}).fail(()=>feedback.text('Location search is unavailable right now.'));});
    weatherWidget.on('click','.weather-location-remove',function(){const button=$(this).prop('disabled',true);$.ajax({url:`/api/dashboard/weather-locations/${encodeURIComponent(button.data('location-id'))}`,method:'DELETE',headers:{RequestVerificationToken:token()}}).done(loadWeatherLocations).always(()=>button.prop('disabled',false));});
    loadWeatherLocations();
    const calendarViewSelect = $('#dashboardCalendarView');
    let calendarView = calendarViewSelect.val() || 'dayGridMonth';
    let dashboardCalendar = null;
    let expandedCalendar = null;

    function calendarDateLabel(date) {
        return date.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
    }

    function matchesForDay(calendar, date) {
        const day = date.toISOString().slice(0, 10);
        return calendar.getEvents().filter(event => event.startStr.slice(0, 10) === day);
    }

    function showDayDetails(date, events) {
        $('#calendarSelectedDay').text(calendarDateLabel(date));
        const $details = $('#calendarDayDetails').empty();

        if (!events.length) {
            $details.text('No matches were logged on this day.');
            return;
        }

        events.forEach(event => {
            const match = event.extendedProps;
            const result = match.isWin ? 'Win' : 'Loss';
            const $result = $('<span>', {
                class: `calendar-match-result ${match.isWin ? 'is-win' : 'is-loss'}`,
                text: result
            });

            $details.append($('<article>', { class: 'calendar-match-summary' }).append(
                $('<div>', { class: 'd-flex align-items-start justify-content-between gap-2' }).append(
                    $('<strong>', { text: `${match.mapName} · ${match.gameType}` }),
                    $result
                ),
                $('<span>', { text: `${match.teamScore}–${match.opponentScore} · Started ${match.startSide}${match.overtimeCount ? ` · OT ${match.overtimeCount}` : ''}` })
            ));
        });
    }

    function buildCalendar(element, expanded) {
        if (!element || !window.FullCalendar) return null;

        return new FullCalendar.Calendar(element, {
            initialView: calendarView,
            headerToolbar: {
                left: 'prev,next',
                center: 'title',
                right: expanded ? 'dayGridMonth,dayGridWeek,dayGridDay' : ''
            },
            buttonText: { month: 'Month', week: 'Week', day: 'Day' },
            height: 'auto',
            contentHeight: 'auto',
            expandRows: false,
            fixedWeekCount: false,
            dayMaxEvents: expanded ? 3 : 2,
            eventDisplay: 'block',
            events: '/api/csmatches/calendar',
            loading(isLoading) {
                $(element).toggleClass('is-calendar-loading', isLoading);
            },
            datesSet(info) {
                if (expanded && info.view.type !== calendarView) {
                    calendarView = info.view.type;
                    calendarViewSelect.val(calendarView);
                    dashboardCalendar?.changeView(calendarView);
                }
            },
            dateClick(info) {
                showDayDetails(info.date, matchesForDay(info.view.calendar, info.date));
            },
            eventClick(info) {
                const date = info.event.start || new Date();
                showDayDetails(date, matchesForDay(info.view.calendar, date));
            }
        });
    }

    function setCalendarView(view) {
        calendarView = ['dayGridMonth', 'dayGridWeek', 'dayGridDay'].includes(view) ? view : 'dayGridMonth';
        calendarViewSelect.val(calendarView);
        dashboardCalendar?.changeView(calendarView);
        expandedCalendar?.changeView(calendarView);
    }

    const calendarElement = document.getElementById('dashboardCalendar');
    dashboardCalendar = buildCalendar(calendarElement, false);
    dashboardCalendar?.render();

    calendarViewSelect.on('change', function () {
        setCalendarView(this.value);
    });

    $('#expandCalendar').on('click', () => {
        if (dashboardCalendar && expandedCalendar) {
            expandedCalendar.gotoDate(dashboardCalendar.getDate());
            expandedCalendar.changeView(calendarView);
        }

        bootstrap.Modal.getOrCreateInstance(document.getElementById('dashboardCalendarModal')).show();
    });

    document.getElementById('dashboardCalendarModal')?.addEventListener('shown.bs.modal', function () {
        const target = document.getElementById('expandedDashboardCalendar');
        if (!expandedCalendar) {
            expandedCalendar = buildCalendar(target, true);
            expandedCalendar.render();
            window.personalToolsMotion?.reveal(target.querySelectorAll('.fc-header-toolbar, .fc-view-harness'), { fromY: 10, duration: 300 });
        }

        if (dashboardCalendar) expandedCalendar.gotoDate(dashboardCalendar.getDate());
        expandedCalendar.changeView(calendarView);
        expandedCalendar.updateSize();
    });
    const widgetGrid=document.querySelector('.dashboard-widget-grid');
    if(widgetGrid){
        $.getJSON('/api/dashboard/widget-order').always(order=>{
            const keys=Array.isArray(order)?order:[];
            keys.forEach(key=>{const item=widgetGrid.querySelector(`:scope > [data-sortable-id="${CSS.escape(key)}"]`);if(item)widgetGrid.append(item);});
            window.personalToolsSortable?.initialise(widgetGrid);
        });
    }
    $.getJSON('/api/notes').done(notes=>{const target=$('#recentNotesWidget').empty();notes.slice(0,4).forEach(note=>$('<a class="recent-note-item" href="/Notes">').append($('<strong>').text(note.title),$('<span>').text(`Updated ${new Date(note.updated).toLocaleDateString(undefined,{day:'numeric',month:'short'})}`)).appendTo(target));if(!notes.length)target.append($('<span class="small-muted">').text('No notes yet.'));}).fail(()=>$('#recentNotesWidget').html('<span class="small-muted">Recent notes are unavailable.</span>'));

    const linksGrid=$('#quickLinksGrid'),quickLinkModal=document.getElementById('quickLinkModal');
    function host(url){try{return new URL(url).hostname.replace('www.','');}catch{return url;}}
    function loadQuickLinks(){
        $.getJSON('/api/quick-links').done(links=>{
            linksGrid.empty();
            if(!links.length){
                linksGrid.append($('<div class="col-12 small-muted">').text('Add the sites you use most.'));
                return;
            }
            links.forEach(link=>{
                const card=$('<a class="quick-link-card" target="_blank" rel="noopener">')
                    .attr('href',link.url)
                    .append(
                        $('<span class="quick-link-icon">').append($('<i>').addClass(link.iconClass||'fa-solid fa-arrow-up-right-from-square')),
                        $('<span>').append($('<strong>').text(link.title),$('<small>').text(host(link.url)))
                    );
                const menu=$('<button class="quick-link-menu" type="button" aria-label="Edit quick link">')
                    .data('link',link)
                    .append($('<i class="fa-solid fa-ellipsis">'));
                menu.on('click',e=>{e.preventDefault();openQuickLink(menu.data('link'));});
                linksGrid.append($('<div class="col-12 col-sm-6 col-lg-4 col-xl-3" data-sortable-id="">').attr('data-sortable-id',link.quickLinkId).append(card.append(menu)));
            });
            window.personalToolsSortable?.initialise(linksGrid.get(0));
            window.personalToolsMotion?.reveal(linksGrid.children().get(), { fromY: 8, delay: 28, duration: 300 });
        }).fail(()=>linksGrid.html('<div class="col-12 small-muted">Quick links are unavailable until MariaDB is configured.</div>'));
    }
    function openQuickLink(link){$('#quickLinkError').addClass('d-none');$('#quickLinkId').val(link?.quickLinkId||'');$('#quickLinkTitle').val(link?.title||'');$('#quickLinkUrl').val(link?.url||'');$('#quickLinkIcon').val(link?.iconClass||'');$('#deleteQuickLink').toggleClass('d-none',!link);$('#quickLinkModalTitle').text(link?'Edit quick link':'Add quick link');bootstrap.Modal.getOrCreateInstance(quickLinkModal).show();}
    $('[data-bs-target="#quickLinkModal"]').on('click',()=>openQuickLink(null));
    $('#quickLinkForm').on('submit',function(e){e.preventDefault();const form=$(this),id=$('#quickLinkId').val(),button=form.find('button[type="submit"]'),payload={title:$('#quickLinkTitle').val(),url:$('#quickLinkUrl').val(),iconClass:$('#quickLinkIcon').val()||null};button.prop('disabled',true);$.ajax({url:id?`/api/quick-links/${id}`:'/api/quick-links',method:id?'PUT':'POST',contentType:'application/json',headers:{RequestVerificationToken:$('input[name="__RequestVerificationToken"]').first().val()},data:JSON.stringify(payload)}).done(()=>{bootstrap.Modal.getInstance(quickLinkModal).hide();loadQuickLinks();}).fail(xhr=>$('#quickLinkError').text(xhr.responseJSON?.message||'The quick link could not be saved.').removeClass('d-none')).always(()=>button.prop('disabled',false));});
    $('#deleteQuickLink').on('click',function(){const id=$('#quickLinkId').val();if(!id||!confirm('Remove this quick link?'))return;$(this).prop('disabled',true);$.ajax({url:`/api/quick-links/${id}`,method:'DELETE',headers:{RequestVerificationToken:$('input[name="__RequestVerificationToken"]').first().val()}}).done(()=>{bootstrap.Modal.getInstance(quickLinkModal).hide();loadQuickLinks();}).always(()=>$(this).prop('disabled',false));});
    loadQuickLinks();
});
