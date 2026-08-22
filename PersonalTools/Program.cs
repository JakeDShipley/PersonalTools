using PersonalTools.Classes.CSMatches;
using PersonalTools.Classes;
using PersonalTools.Classes.Dashboard;
using PersonalTools.Classes.MediaExtractor;
using PersonalTools.Classes.Notes;
using PersonalTools.Classes.Skins;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using PersonalTools.Data.CSMatches;
using PersonalTools.Data.Skins;
using PersonalTools.Data;
using PersonalTools.Classes.Monitoring;
using PersonalTools.Data.Monitoring;
using PersonalTools.Hubs;
using PersonalTools.Classes.CSStats;
using PersonalTools.Data.CSStats;
using PersonalTools.Classes.CSDemos;
using PersonalTools.Data.CSDemos;
using PersonalTools.Classes.Tracker;
using PersonalTools.Data.Tracker;
using System.Net.Http.Headers;
using PersonalTools.Security;
using PersonalTools.Logging;
using PersonalTools.Classes.PasteBin;
using PersonalTools.Data.PasteBin;
using Microsoft.AspNetCore.Http.Features;
using PersonalTools.Classes.CaseOpening;
using PersonalTools.Data.CaseOpening;

var builder = WebApplication.CreateBuilder(args);

// Console output is captured by Visual Studio locally and systemd in production.
// Avoid the Windows Event Log provider, which can throw when the current account
// has no permission to register or write its event source.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddApplicationLogViewer();

// Keep authentication and protected user settings readable across restarts.
// Never put the key ring in the deployed application directory: production files
// are owned separately from the account that runs the service.
string defaultDataProtectionPath = OperatingSystem.IsWindows()
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalTools", "DataProtection-Keys")
    : "/var/lib/personaltools/data-protection-keys";
string dataProtectionPath = builder.Configuration["DataProtection:KeyDirectory"] ?? defaultDataProtectionPath;
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("PersonalTools");

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddMemoryCache();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 67_108_864);
builder.Services.AddScoped<IMariaDbDataAccess, MariaDbDataAccess>();
builder.Services.AddScoped<IAuthData, AuthData>();
builder.Services.AddScoped<IAuthFuncs, AuthFuncs>();
builder.Services.AddHttpClient<ISteamOpenIdData, SteamOpenIdData>(client =>
{
    client.BaseAddress = new Uri("https://steamcommunity.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me)");
});
builder.Services.AddScoped<ISteamOpenIdFuncs, SteamOpenIdFuncs>();
builder.Services.AddScoped<IAppSettingsData, AppSettingsData>();
builder.Services.AddScoped<IAppSettingsFuncs, AppSettingsFuncs>();
builder.Services.AddScoped<IQuickLinksData, QuickLinksData>();
builder.Services.AddScoped<IQuickLinksFuncs, QuickLinksFuncs>();
builder.Services.AddScoped<IDashboardWidgetOrderData, DashboardWidgetOrderData>();
builder.Services.AddScoped<IDashboardWidgetOrderFuncs, DashboardWidgetOrderFuncs>();
builder.Services.AddScoped<IDashboardWeatherData, DashboardWeatherData>();
builder.Services.AddScoped<IDashboardWeatherFuncs, DashboardWeatherFuncs>();
builder.Services.AddScoped<INotesData, NotesData>();
builder.Services.AddScoped<ITrackedSkinsData, TrackedSkinsData>();
builder.Services.AddScoped<IApplicationLogsData, ApplicationLogsData>();
builder.Services.AddScoped<ILogsViewerFuncs, LogsViewerFuncs>();
builder.Services.AddHostedService<ApplicationLogPersistenceService>();
builder.Services.AddHostedService<MonitoringPulseService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PersonalTools.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Local HTTP development still works, while a deployed account cookie is
        // never allowed to travel over an unencrypted connection.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = false;
        options.LoginPath = "/Login";
        options.Events.OnValidatePrincipal = async context =>
        {
            string? userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? sessionId = context.Principal?.FindFirstValue("session_id");
            if (!Guid.TryParse(userId, out Guid id) || !Guid.TryParse(sessionId, out Guid parsedSessionId) || !await context.HttpContext.RequestServices.GetRequiredService<IAuthFuncs>().IsSessionValid(parsedSessionId, id)) context.RejectPrincipal();
        };
    });
builder.Services.AddAuthorization(options => options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Dashboard
builder.Services.AddScoped<IDashboardFuncs, DashboardFuncs>();

// Skins
builder.Services.AddHttpClient<ICs2SkinData, Cs2SkinData>();
builder.Services.AddScoped<ISkinFuncs, SkinFuncs>();

// Notes
builder.Services.AddScoped<INoteFuncs, NoteFuncs>();

builder.Services.AddScoped<ISteamInventoryFuncs, SteamInventoryFuncs>();
builder.Services.AddHttpClient<ISteamInventoryData, SteamInventoryData>(client =>
{
    client.BaseAddress = new Uri("https://steamcommunity.com/");
    client.Timeout = TimeSpan.FromSeconds(25);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me)");
});

// Media Extractor
builder.Services.AddHttpClient<IMediaExtractorFuncs, MediaExtractorFuncs>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalToolsMediaExtractor/1.0");
});

// CS Match Tracker
builder.Services.AddScoped<IMatchesData, MatchesData>();
builder.Services.AddScoped<ICSMatchFuncs, CSMatchFuncs>();
builder.Services.AddScoped<ICSMatchReferenceData, CSMatchReferenceData>();
builder.Services.AddScoped<IMatchProfilesData, MatchProfilesData>();
builder.Services.AddScoped<IMatchProfileFuncs, MatchProfileFuncs>();
builder.Services.AddHttpClient<ILeetifyData, LeetifyData>(client =>
{
    client.BaseAddress = new Uri("https://api-public.cs-prod.leetify.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me)");
    string? apiKey = builder.Configuration["Leetify:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
});
builder.Services.AddScoped<ILeetifyFuncs, LeetifyFuncs>();
builder.Services.AddHttpClient<ILeetifyProfileData, LeetifyProfileData>(client =>
{
    client.BaseAddress = new Uri("https://api-public.cs-prod.leetify.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me)");
    string? apiKey = builder.Configuration["Leetify:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
});
builder.Services.AddScoped<ICSStatsFuncs, CSStatsFuncs>();
builder.Services.AddScoped<IReportedPlayersData, ReportedPlayersData>();
builder.Services.AddScoped<IReportedPlayersFuncs, ReportedPlayersFuncs>();
builder.Services.AddScoped<ICSDemoData, CSDemoData>();
builder.Services.AddScoped<ICSDemoFuncs, CSDemoFuncs>();
builder.Services.AddHttpClient<IAccountStandingData, AccountStandingData>(client =>
{
    client.BaseAddress = new Uri("https://api.steampowered.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me)");
});
builder.Services.AddHttpClient<IMapPoolSuggestionData, MapPoolSuggestionData>(client =>
{
    client.BaseAddress = new Uri("https://en.wikipedia.org/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me; contact via GitHub)");
});
builder.Services.AddScoped<IMapPoolSuggestionFuncs, MapPoolSuggestionFuncs>();

// Bug/Feature Tracker
builder.Services.AddScoped<ITrackerItemsData, TrackerItemsData>();
builder.Services.AddScoped<ITrackerFuncs, TrackerFuncs>();
builder.Services.AddHostedService<TrackerAutoCloseService>();

// Paste Bin files live outside the web root and are only opened after controller access checks.
builder.Services.AddScoped<IPasteBinData, PasteBinData>();
builder.Services.AddSingleton<IPasteBinFileStorage, PasteBinFileStorage>();
builder.Services.AddScoped<IPasteBinFuncs, PasteBinFuncs>();
builder.Services.AddHostedService<PasteBinCleanupService>();

// CS2 Case Simulator
builder.Services.AddScoped<ICaseOpeningData, CaseOpeningData>();
builder.Services.AddScoped<ICS2ItemPriceData, NullCS2ItemPriceData>();
builder.Services.AddHttpClient<ICaseOpeningReferenceData, CaseOpeningReferenceData>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me)");
});
builder.Services.AddScoped<ICaseOpeningFuncs, CaseOpeningFuncs>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<ContentSecurityPolicyMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

app.Run();
