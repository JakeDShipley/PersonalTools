namespace PersonalTools.Entities;

public enum AppSettingKey { AppearanceTheme, AppearanceMode, DashboardDefaultView, DashboardMotion, DashboardWeatherUnit, SteamWebApiKey }
public enum AppSettingInputType { Select, Switch, Secret }
public sealed record AppSettingDefinition(AppSettingKey Key, string Group, string Name, string Description, AppSettingInputType InputType, IReadOnlyList<(string Value, string Label)>? Options = null, bool IsServerSecret = false);
public sealed record AppSettingView(AppSettingDefinition Definition, string Value, bool IsConfigured);

public static class AppSettingDefinitions
{
    public static readonly IReadOnlyList<AppSettingDefinition> All =
    [
        new(AppSettingKey.AppearanceTheme, "Appearance", "Workspace theme", "Choose the visual language for Personal Tools. Personal keeps the current clean workspace look; Tactical uses a compact gold and ink match-console style.", AppSettingInputType.Select, [("personal", "Personal"), ("tactical", "Tactical")]),
        new(AppSettingKey.AppearanceMode, "Appearance", "Colour mode", "Choose the light or dark version of your selected theme. The sidebar sun and moon button changes this setting too.", AppSettingInputType.Select, [("light", "Light"), ("dark", "Dark")]),
        new(AppSettingKey.DashboardDefaultView, "Workspace", "Dashboard tool view", "Choose whether your tools open in a compact list or a fixed card grid by default.", AppSettingInputType.Select, [("cards", "Card grid"), ("list", "List")]),
        new(AppSettingKey.DashboardMotion, "Workspace", "Interface motion", "Keep the small purposeful transitions that make actions feel responsive. Turn this off if you prefer a quieter workspace.", AppSettingInputType.Switch),
        new(AppSettingKey.DashboardWeatherUnit, "Dashboard", "Weather unit", "Use the unit that feels natural when checking the dashboard at a glance.", AppSettingInputType.Select, [("celsius", "Celsius (°C)"), ("fahrenheit", "Fahrenheit (°F)")]),
        new(AppSettingKey.SteamWebApiKey, "Integrations", "Steam Web API key", "Enables server-side public ban checks in CS2 Player Stats. The key is protected at rest and is never returned to your browser.", AppSettingInputType.Secret, IsServerSecret: true)
    ];
    public static AppSettingDefinition Get(AppSettingKey key) => All.Single(definition => definition.Key == key);
}
