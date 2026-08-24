using System.Security.Claims;

namespace PersonalTools.Security;

/// <summary>
/// Roles are stored as stable numeric values in MariaDB and issued as standard cookie role claims.
/// Add new values without changing the existing numbers so current accounts retain their access.
/// </summary>
public enum AppRole : byte
{
    User = 1,
    Admin = 2
}

public static class AppAuthorizationPolicies
{
    public const string AdminOnly = "PersonalTools.AdminOnly";
}

public static class ClaimsPrincipalRoleExtensions
{
    /// <summary>
    /// Centralises page/controller role checks so access decisions use the role from the validated
    /// authentication ticket rather than a browser-controlled value.
    /// </summary>
    public static bool IsUserAllowedHere(this ClaimsPrincipal? user, params AppRole[] allowedRoles)
    {
        if (user?.Identity?.IsAuthenticated != true || allowedRoles.Length == 0)
        {
            return false;
        }

        return allowedRoles.Any(role => user.IsInRole(role.ToString()));
    }
}
