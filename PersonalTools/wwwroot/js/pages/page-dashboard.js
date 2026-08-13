$(function () {
    const weatherCodes={0:['fa-sun','Clear skies'],1:['fa-cloud-sun','Mainly clear'],2:['fa-cloud-sun','Partly cloudy'],3:['fa-cloud','Overcast'],45:['fa-smog','Foggy'],51:['fa-cloud-rain','Light drizzle'],61:['fa-cloud-rain','Rain'],71:['fa-snowflake','Snow'],80:['fa-cloud-showers-heavy','Showers'],95:['fa-cloud-bolt','Thunderstorms']};
    function loadWeather(location){$.getJSON(`https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(location)}&count=1&language=en&format=json`).done(geo=>{const place=geo.results?.[0];if(!place){$('#weatherWidget').html('<span class="small-muted">Location not found. Try a town or city.</span>');return;}$.getJSON(`https://api.open-meteo.com/v1/forecast?latitude=${place.latitude}&longitude=${place.longitude}&current=temperature_2m,weather_code,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=mph`).done(data=>{const c=data.current,condition=weatherCodes[c.weather_code]||['fa-cloud','Conditions unavailable'];$('#weatherWidget').empty().append($('<div class="weather-icon">').append($('<i>').addClass(`fa-solid ${condition[0]}`)),$('<div>').append($('<div class="weather-temperature">').text(`${Math.round(c.temperature_2m)}°`),$('<div class="weather-summary">').text(`${place.name} · ${condition[1]} · ${Math.round(c.wind_speed_10m)} mph`)));});});}
    const savedLocation=localStorage.getItem('personal-tools-weather-location');if(savedLocation){loadWeather(savedLocation);}
    $('#weatherLocationForm').on('submit',function(e){e.preventDefault();const location=$('#weatherLocation').val().trim();if(location){localStorage.setItem('personal-tools-weather-location',location);loadWeather(location);}});
    const calendarElement=document.getElementById('dashboardCalendar');if(calendarElement&&window.FullCalendar)new FullCalendar.Calendar(calendarElement,{initialView:'dayGridMonth',headerToolbar:{left:'prev,next',center:'title',right:''},height:'auto',fixedWeekCount:false}).render();
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
                linksGrid.append($('<div class="col-12 col-sm-6 col-lg-4 col-xl-3">').append(card.append(menu)));
            });
        }).fail(()=>linksGrid.html('<div class="col-12 small-muted">Quick links are unavailable until MariaDB is configured.</div>'));
    }
    function openQuickLink(link){$('#quickLinkError').addClass('d-none');$('#quickLinkId').val(link?.quickLinkId||'');$('#quickLinkTitle').val(link?.title||'');$('#quickLinkUrl').val(link?.url||'');$('#quickLinkIcon').val(link?.iconClass||'');$('#deleteQuickLink').toggleClass('d-none',!link);$('#quickLinkModalTitle').text(link?'Edit quick link':'Add quick link');bootstrap.Modal.getOrCreateInstance(quickLinkModal).show();}
    $('[data-bs-target="#quickLinkModal"]').on('click',()=>openQuickLink(null));
    $('#quickLinkForm').on('submit',function(e){e.preventDefault();const form=$(this),id=$('#quickLinkId').val(),button=form.find('button[type="submit"]'),payload={title:$('#quickLinkTitle').val(),url:$('#quickLinkUrl').val(),iconClass:$('#quickLinkIcon').val()||null};button.prop('disabled',true);$.ajax({url:id?`/api/quick-links/${id}`:'/api/quick-links',method:id?'PUT':'POST',contentType:'application/json',headers:{RequestVerificationToken:$('input[name="__RequestVerificationToken"]').first().val()},data:JSON.stringify(payload)}).done(()=>{bootstrap.Modal.getInstance(quickLinkModal).hide();loadQuickLinks();}).fail(xhr=>$('#quickLinkError').text(xhr.responseJSON?.message||'The quick link could not be saved.').removeClass('d-none')).always(()=>button.prop('disabled',false));});
    $('#deleteQuickLink').on('click',function(){const id=$('#quickLinkId').val();if(!id||!confirm('Remove this quick link?'))return;$(this).prop('disabled',true);$.ajax({url:`/api/quick-links/${id}`,method:'DELETE',headers:{RequestVerificationToken:$('input[name="__RequestVerificationToken"]').first().val()}}).done(()=>{bootstrap.Modal.getInstance(quickLinkModal).hide();loadQuickLinks();}).always(()=>$(this).prop('disabled',false));});
    loadQuickLinks();
});
