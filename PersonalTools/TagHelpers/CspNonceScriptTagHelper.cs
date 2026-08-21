using Microsoft.AspNetCore.Razor.TagHelpers;
using PersonalTools.Security;

namespace PersonalTools.TagHelpers;

/// <summary>
/// Adds the current request's CSP nonce to every script element rendered by Razor.
/// This includes local scripts, approved CDN scripts and the few intentional inline scripts.
/// </summary>
[HtmlTargetElement("script")]
public sealed class CspNonceScriptTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CspNonceScriptTagHelper(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("nonce")) return;

        string? nonce = _httpContextAccessor.HttpContext?.Items[ContentSecurityPolicyMiddleware.NonceItemKey] as string;
        if (!string.IsNullOrWhiteSpace(nonce)) output.Attributes.SetAttribute("nonce", nonce);
    }
}
