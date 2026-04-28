using Microsoft.AspNetCore.Localization;

namespace DelikatessenDrehbuch.Middleware;

/// <summary>
/// Custom culture provider that reads the legacy "deli-lang" cookie
/// and normalizes it to standard CultureInfo codes.
/// </summary>
public class CustomCookieRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var cookie = httpContext.Request.Cookies["deli-lang"];

        if (string.IsNullOrWhiteSpace(cookie))
            return Task.FromResult<ProviderCultureResult?>(null);

        // Cookie-Normalisierung: esp → es, prt → pt, no → nb
        var normalizedCulture = cookie.ToLowerInvariant() switch
        {
            "esp" => "es",
            "prt" => "pt",
            "no" => "nb",
            "se" => "sv",
            "dk" => "da",
            var lang => lang
        };

        return Task.FromResult<ProviderCultureResult?>(
            new ProviderCultureResult(normalizedCulture));
    }
}
