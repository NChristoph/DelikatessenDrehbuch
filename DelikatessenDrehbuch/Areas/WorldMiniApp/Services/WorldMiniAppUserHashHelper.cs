using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    /// <summary>
    /// Identitäts-Helfer für die WorldMiniApp.
    ///
    /// Sicherheitsmodell (Weg A – ASP.NET Cookie-Authentication):
    /// Die Identität eines Nutzers wird AUSSCHLIESSLICH aus einem signierten und
    /// verschlüsselten Auth-Cookie (Scheme <see cref="AuthScheme"/>) gelesen, das nur
    /// nach erfolgreicher SIWE-/Worldcoin-Verifizierung über <see cref="SignInAsync"/>
    /// ausgestellt wird. Client-kontrollierte Eingaben (Query-String, Header, der
    /// kosmetische Anzeige-Cookie oder explizit übergebene Parameter) werden NICHT mehr
    /// als Identitätsnachweis akzeptiert – das schließt die frühere Impersonation-Lücke.
    /// </summary>
    public static class WorldMiniAppUserHashHelper
    {
        /// <summary>Name des Cookie-Authentication-Schemes (siehe Program.cs).</summary>
        public const string AuthScheme = "WorldMiniApp";

        /// <summary>Claim-Typ, der den verifizierten UserHash trägt.</summary>
        public const string UserHashClaimType = "WorldUserHash";

        /// <summary>Claim-Typ-Flag, ob es sich um einen Test-Hash handelt.</summary>
        public const string IsTestHashClaimType = "WorldUserIsTestHash";

        // --- Anzeige-Cookies (client-lesbar, NICHT vertrauenswürdig) -----------------
        // Werden nur für das Frontend geschrieben, damit es den aktuellen Hash anzeigen
        // kann. Der Server leitet daraus KEINE Identität ab.
        public const string UserHashCookieKey = "WorldMiniAppUserHash";
        public const string TestUserHashCookieKey = "WorldMiniAppTestUserHash";

        // --- Legacy-Schlüssel (nur noch zum Aufräumen beim Logout) -------------------
        public const string SessionUserHashKey = "WorldMiniAppUserHash";
        public const string LegacySessionUserHashKey = "UserHash";
        public const string UserHashHeaderKey = "X-WorldMiniApp-UserHash";
        public const string UserHashQueryKey = "userHash";

        /// <summary>
        /// Liefert den aktuell authentifizierten UserHash aus dem signierten Auth-Cookie.
        /// Die Parameter <paramref name="explicitUserHash"/> und
        /// <paramref name="persistResolvedHash"/> bleiben aus Kompatibilitätsgründen
        /// erhalten, werden aber bewusst IGNORIERT – Identität kommt nur aus dem Claim.
        /// </summary>
        public static string Resolve(HttpContext httpContext, string? explicitUserHash = null, bool persistResolvedHash = true)
        {
            var claim = httpContext?.User?.FindFirst(UserHashClaimType)?.Value;
            return string.IsNullOrWhiteSpace(claim) ? string.Empty : claim.Trim();
        }

        /// <summary>
        /// Stellt nach erfolgreicher Verifizierung das signierte Auth-Cookie aus und
        /// meldet den Nutzer an. NUR aus verifizierten Auth-Flows aufrufen.
        /// </summary>
        public static async Task SignInAsync(HttpContext httpContext, string userHash, bool isPersistent, bool isTestHash = false)
        {
            if (httpContext == null || string.IsNullOrWhiteSpace(userHash))
            {
                return;
            }

            var normalizedHash = userHash.Trim();

            var claims = new List<Claim>
            {
                new Claim(UserHashClaimType, normalizedHash),
                new Claim(IsTestHashClaimType, isTestHash ? "true" : "false")
            };

            var identity = new ClaimsIdentity(claims, AuthScheme);
            var principal = new ClaimsPrincipal(identity);

            var properties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                AllowRefresh = true
            };
            if (isPersistent)
            {
                // Nur bei "Login merken" ein persistentes Cookie (überlebt App-Neustart).
                properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
            }
            // Bei isPersistent=false: KEIN ExpiresUtc -> reines Session-Cookie. Es stirbt,
            // wenn die World App geschlossen wird -> beim nächsten Start ist wieder Login
            // nötig (das Login-Modal öffnet sich erneut).

            await httpContext.SignInAsync(AuthScheme, principal, properties);

            // Den aktuell angemeldeten Hash sofort für nachfolgende Resolve()-Aufrufe
            // im selben Request verfügbar machen.
            httpContext.User.AddIdentity(identity);

            WriteDisplayCookie(httpContext, normalizedHash, isTestHash, isPersistent);
        }

        /// <summary>
        /// Meldet den Nutzer ab: entfernt das Auth-Cookie, die Anzeige-Cookies und
        /// alte Session-Reste.
        /// </summary>
        public static async Task SignOutAsync(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                return;
            }

            await httpContext.SignOutAsync(AuthScheme);

            httpContext.Response.Cookies.Delete(UserHashCookieKey);
            httpContext.Response.Cookies.Delete(TestUserHashCookieKey);

            try
            {
                httpContext.Session?.Remove(SessionUserHashKey);
                httpContext.Session?.Remove(LegacySessionUserHashKey);
            }
            catch
            {
                // Session evtl. nicht verfügbar – ignorieren.
            }
        }

        private static void WriteDisplayCookie(HttpContext httpContext, string userHash, bool isTestHash, bool isPersistent)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps
            };
            if (isPersistent)
            {
                // Nur bei "Login merken" persistent; sonst Session-Cookie (kein Expires),
                // damit das Frontend nach App-Neustart keinen veralteten Login-Hinweis sieht.
                cookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(30);
            }

            httpContext.Response.Cookies.Append(UserHashCookieKey, userHash, cookieOptions);

            if (isTestHash)
            {
                httpContext.Response.Cookies.Append(TestUserHashCookieKey, userHash, cookieOptions);
            }
            else
            {
                httpContext.Response.Cookies.Delete(TestUserHashCookieKey);
            }
        }
    }
}
