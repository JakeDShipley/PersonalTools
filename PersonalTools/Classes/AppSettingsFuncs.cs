using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using PersonalTools.Data;
using PersonalTools.Entities;

namespace PersonalTools.Classes;

public interface IAppSettingsFuncs
{
    Task<List<AppSettingView>> Get(Guid userId, CancellationToken cancellationToken = default);
    Task Set(Guid userId, AppSettingKey key, string? value, CancellationToken cancellationToken = default);
    Task<string?> GetSecret(Guid userId, AppSettingKey key, CancellationToken cancellationToken = default);
}

public sealed class AppSettingsFuncs : IAppSettingsFuncs
{
    private readonly IAppSettingsData _data;
    private readonly IDataProtector _protector;

    public AppSettingsFuncs(IAppSettingsData data, IDataProtectionProvider protectionProvider) 
    { 
        _data = data;
        _protector = protectionProvider.CreateProtector("PersonalTools.AppSettings.v1"); 
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<AppSettingView>> Get(Guid userId, CancellationToken cancellationToken = default)
    {
        Dictionary<AppSettingKey, string> stored = await _data.Get(userId, cancellationToken);

        return AppSettingDefinitions.All.Select(definition => new AppSettingView(definition, definition.IsServerSecret ? string.Empty : stored.GetValueOrDefault(definition.Key, Default(definition.Key)), stored.ContainsKey(definition.Key))).ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task Set(Guid userId, AppSettingKey key, string? value, CancellationToken cancellationToken = default)
    {
        AppSettingDefinition definition = AppSettingDefinitions.Get(key);
        string clean = value?.Trim() ?? string.Empty;
        if (definition.IsServerSecret)
        {
            if (string.IsNullOrEmpty(clean)) 
                return;

            if (clean.Length is < 16 or > 256) 
                throw new InvalidOperationException("Enter a valid Steam Web API key.");

            await _data.Set(userId, key, _protector.Protect(clean), cancellationToken);

            return;
        }

        if (key == AppSettingKey.AppearanceTheme && clean is not ("personal" or "tactical"))
            throw new InvalidOperationException("Choose a valid workspace theme.");

        if (key == AppSettingKey.AppearanceMode && clean is not ("light" or "dark"))
            throw new InvalidOperationException("Choose a valid colour mode.");

        if (key == AppSettingKey.DashboardDefaultView && clean is not ("cards" or "list"))
            throw new InvalidOperationException("Choose a valid dashboard view.");

        if (key == AppSettingKey.DashboardMotion && clean is not ("true" or "false"))
            throw new InvalidOperationException("Choose a valid motion setting.");

        if (key == AppSettingKey.DashboardWeatherUnit && clean is not ("celsius" or "fahrenheit"))
            throw new InvalidOperationException("Choose a valid weather unit.");

        await _data.Set(userId, key, clean, cancellationToken);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string?> GetSecret(Guid userId, AppSettingKey key, CancellationToken cancellationToken = default)
    {
        if (!AppSettingDefinitions.Get(key).IsServerSecret) 
            return null;

        string? protectedValue = (await _data.Get(userId, cancellationToken)).GetValueOrDefault(key);

        if (string.IsNullOrWhiteSpace(protectedValue)) 
            return null;

        try 
        { 
            return _protector.Unprotect(protectedValue); 
        }
        catch (CryptographicException) 
        { 
            return null; 
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string Default(AppSettingKey key) => key switch 
    { 
        AppSettingKey.AppearanceTheme => "personal", 
        AppSettingKey.AppearanceMode => "light", 
        AppSettingKey.DashboardDefaultView => "cards", 
        AppSettingKey.DashboardMotion => "true", 
        AppSettingKey.DashboardWeatherUnit => "celsius", _ => string.Empty 
    };
}
