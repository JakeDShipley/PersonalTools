using System.Security.Cryptography;

namespace PersonalTools.Security;

/// <summary>
/// Gives every HTML response a one-time script nonce and a restrictive CSP.
/// The nonce is intentionally generated per request and is never persisted.
/// </summary>
public sealed class ContentSecurityPolicyMiddleware
{
    public const string NonceItemKey = "PersonalTools.CspNonce";

    private readonly RequestDelegate _next;

    public ContentSecurityPolicyMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        // Exception handling can re-execute the pipeline for /Error. Reuse the original
        // request nonce in that case so the error page and response header still agree.
        string nonce = context.Items.TryGetValue(NonceItemKey, out object? value) && value is string existingNonce
            ? existingNonce
            : Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        context.Items[NonceItemKey] = nonce;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Content-Security-Policy"] = BuildPolicy(nonce);
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static string BuildPolicy(string nonce) =>
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        $"script-src 'self' 'nonce-{nonce}' https://code.jquery.com https://cdn.jsdelivr.net; " +
        "script-src-attr 'none'; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "style-src-attr 'unsafe-inline'; " +
        "font-src 'self' data: https://cdnjs.cloudflare.com; " +
        "img-src 'self' data: blob: https:; " +
        "media-src 'self' data: blob: https:; " +
        "connect-src 'self' https://api.open-meteo.com https://geocoding-api.open-meteo.com https://cdn.jsdelivr.net; " +
        "worker-src 'self' blob:; " +
        "manifest-src 'self'; " +
        "upgrade-insecure-requests";
}
