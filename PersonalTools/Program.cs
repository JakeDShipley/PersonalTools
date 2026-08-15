using PersonalTools.Classes.CSMatches;
using PersonalTools.Classes;
using PersonalTools.Classes.Dashboard;
using PersonalTools.Classes.GrandExchange;
using PersonalTools.Classes.MediaExtractor;
using PersonalTools.Classes.Notes;
using PersonalTools.Classes.Skins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PersonalTools.Data.CSMatches;
using PersonalTools.Data.GrandExchange;
using PersonalTools.Data.Local;
using PersonalTools.Data.Skins;
using PersonalTools.Data;
using PersonalTools.Classes.Monitoring;
using PersonalTools.Data.Monitoring;
using PersonalTools.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IMariaDbDataAccess, MariaDbDataAccess>();
builder.Services.AddScoped<IAuthData, AuthData>();
builder.Services.AddScoped<IAuthFuncs, AuthFuncs>();
builder.Services.AddScoped<IQuickLinksData, QuickLinksData>();
builder.Services.AddScoped<IQuickLinksFuncs, QuickLinksFuncs>();
builder.Services.AddScoped<INotesData, NotesData>();
builder.Services.AddScoped<ITrackedSkinsData, TrackedSkinsData>();
builder.Services.AddSingleton<IServerMonitorData, ServerMonitorData>();
builder.Services.AddScoped<IServerMonitorFuncs, ServerMonitorFuncs>();
builder.Services.AddScoped<IDatabaseMonitorData, DatabaseMonitorData>();
builder.Services.AddScoped<IDatabaseMonitorFuncs, DatabaseMonitorFuncs>();
builder.Services.AddHostedService<MonitoringPulseService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PersonalTools.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = false;
        options.LoginPath = "/Login";
        options.Events.OnValidatePrincipal = async context =>
        {
            string? userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? sessionId = context.Principal?.FindFirstValue("session_id");
            if (!long.TryParse(userId, out long id) || string.IsNullOrWhiteSpace(sessionId) || !await context.HttpContext.RequestServices.GetRequiredService<IAuthFuncs>().IsSessionValid(sessionId, id)) context.RejectPrincipal();
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

// Grand Exchange
string? osrsWikiUserAgent = builder.Configuration["OsrsWikiPrices:UserAgent"];

builder.Services.AddScoped<ISteamInventoryFuncs, SteamInventoryFuncs>();
builder.Services.AddHttpClient<ISteamInventoryData, SteamInventoryData>(client =>
{
    client.BaseAddress = new Uri("https://steamcommunity.com/");
    client.Timeout = TimeSpan.FromSeconds(25);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me)");
});

if (string.IsNullOrWhiteSpace(osrsWikiUserAgent))
    throw new InvalidOperationException("The OsrsWikiPrices:UserAgent app setting is required.");

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<IGrandExchangeData, GrandExchangeData>(client =>
{
    client.BaseAddress = new Uri("https://prices.runescape.wiki/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(osrsWikiUserAgent);
});

builder.Services.AddScoped<IGrandExchangeFuncs, GrandExchangeFuncs>();

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
});
builder.Services.AddScoped<ILeetifyFuncs, LeetifyFuncs>();
builder.Services.AddHttpClient<IMapPoolSuggestionData, MapPoolSuggestionData>(client =>
{
    client.BaseAddress = new Uri("https://en.wikipedia.org/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTools/1.0 (+https://jakehutson.me; contact via GitHub)");
});
builder.Services.AddScoped<IMapPoolSuggestionFuncs, MapPoolSuggestionFuncs>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/auth/steam/link", (HttpContext context) =>
{
    string state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    context.Response.Cookies.Append("PersonalTools.SteamLinkState", state, new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(10), IsEssential = true });
    string callback = $"{context.Request.Scheme}://{context.Request.Host}/auth/steam/callback";
    Dictionary<string, string> values = new()
    {
        ["openid.ns"] = "http://specs.openid.net/auth/2.0", ["openid.mode"] = "checkid_setup",
        ["openid.return_to"] = callback + "?state=" + Uri.EscapeDataString(state), ["openid.realm"] = $"{context.Request.Scheme}://{context.Request.Host}/",
        ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select", ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select"
    };
    return Results.Redirect("https://steamcommunity.com/openid/login?" + string.Join("&", values.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value))));
});

app.MapGet("/auth/steam/callback", async (HttpContext context, IHttpClientFactory clientFactory, IAuthFuncs auth) =>
{
    string state = context.Request.Query["state"].FirstOrDefault() ?? string.Empty;
    string savedState = context.Request.Cookies["PersonalTools.SteamLinkState"] ?? string.Empty;
    context.Response.Cookies.Delete("PersonalTools.SteamLinkState", new CookieOptions { Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax });
    if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(savedState) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(savedState))) return Results.BadRequest("Steam linking could not be verified. Please try again.");
    if (!long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId)) return Results.Challenge();
    Dictionary<string, string> parameters = context.Request.Query.Where(x => x.Key.StartsWith("openid.", StringComparison.Ordinal)).ToDictionary(x => x.Key, x => x.Value.ToString());
    parameters["openid.mode"] = "check_authentication";
    using HttpClient client = clientFactory.CreateClient();
    using HttpResponseMessage response = await client.PostAsync("https://steamcommunity.com/openid/login", new FormUrlEncodedContent(parameters));
    string verification = await response.Content.ReadAsStringAsync();
    string identity = context.Request.Query["openid.identity"].FirstOrDefault() ?? string.Empty;
    var steamIdMatch = System.Text.RegularExpressions.Regex.Match(identity, @"^https://steamcommunity\.com/openid/id/(\d{17})$");
    if (!response.IsSuccessStatusCode || !verification.Contains("is_valid:true", StringComparison.Ordinal) || !steamIdMatch.Success) return Results.BadRequest("Steam linking could not be verified. Please try again.");
    await auth.LinkSteam(userId, steamIdMatch.Groups[1].Value);
    return Results.LocalRedirect("/Settings");
});


app.MapRazorPages();
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

app.Run();
