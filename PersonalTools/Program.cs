using PersonalTools.Classes.Dashboard;
using PersonalTools.Classes.MediaExtractor;
using PersonalTools.Classes.Notes;
using PersonalTools.Classes.Skins;
using PersonalTools.Classes.Settings;
using PersonalTools.Data.Local;
using PersonalTools.Data.Skins;

using PersonalTools.Classes.GrandExchange;
using PersonalTools.Data.GrandExchange;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();

// Dashboard
builder.Services.AddScoped<IDashboardFuncs, DashboardFuncs>();

// Storage
builder.Services.AddScoped<ILocalJsonData, LocalJsonData>();

// Skins
builder.Services.AddHttpClient<ICs2SkinData, Cs2SkinData>();
builder.Services.AddScoped<ISkinFuncs, SkinFuncs>();

// Notes
builder.Services.AddScoped<INoteFuncs, NoteFuncs>();

// Grand Exchange
string? osrsWikiUserAgent = builder.Configuration["OsrsWikiPrices:UserAgent"];

//Settings
builder.Services.AddScoped<ISettingsFuncs, SettingsFuncs>();

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
builder.Services.AddScoped<IMediaExtractorFuncs, MediaExtractorFuncs>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();