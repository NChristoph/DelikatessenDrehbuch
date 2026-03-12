using Microsoft.AspNetCore.Http;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public static class WorldMiniAppUserHashHelper
    {
        public const string SessionUserHashKey = "WorldMiniAppUserHash";
        public const string LegacySessionUserHashKey = "UserHash";
        public const string UserHashCookieKey = "WorldMiniAppUserHash";
        public const string TestUserHashCookieKey = "WorldMiniAppTestUserHash";
        public const string UserHashHeaderKey = "X-WorldMiniApp-UserHash";
        public const string UserHashQueryKey = "userHash";

        public static string Resolve(HttpContext httpContext, string? explicitUserHash = null, bool persistResolvedHash = true)
        {
            if (httpContext == null)
            {
                return string.Empty;
            }

            var resolvedHash = FirstNonEmpty(
                httpContext.Session.GetString(SessionUserHashKey),
                httpContext.Session.GetString(LegacySessionUserHashKey),
                explicitUserHash,
                httpContext.Request.Cookies[UserHashCookieKey],
                httpContext.Request.Cookies[TestUserHashCookieKey],
                httpContext.Request.Headers[UserHashHeaderKey].ToString(),
                httpContext.Request.Query[UserHashQueryKey].ToString());

            if (string.IsNullOrWhiteSpace(resolvedHash))
            {
                return string.Empty;
            }

            resolvedHash = resolvedHash.Trim();

            if (persistResolvedHash)
            {
                Persist(httpContext, resolvedHash, isTestHash: false);
            }

            return resolvedHash;
        }

        public static void Persist(HttpContext httpContext, string userHash, bool isTestHash)
        {
            if (httpContext == null || string.IsNullOrWhiteSpace(userHash))
            {
                return;
            }

            var normalizedHash = userHash.Trim();
            httpContext.Session.SetString(SessionUserHashKey, normalizedHash);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            };

            httpContext.Response.Cookies.Append(UserHashCookieKey, normalizedHash, cookieOptions);

            if (isTestHash)
            {
                httpContext.Response.Cookies.Append(TestUserHashCookieKey, normalizedHash, cookieOptions);
            }
            else
            {
                httpContext.Response.Cookies.Delete(TestUserHashCookieKey);
            }
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }
}
