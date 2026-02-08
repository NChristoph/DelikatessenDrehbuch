using System.Globalization;
using Microsoft.AspNetCore.Mvc.Razor;

namespace DelikatessenDrehbuch.Views;

public abstract class AppRazorPage<TModel> : RazorPage<TModel>
{
    protected string T(string de, string en)
    {
        var language = ResolveLanguage();
        return language == "de" ? de : en;
    }

    protected string T(string de, string en, string es, string pt)
    {
        var language = ResolveLanguage();
        return language switch
        {
            "de" => de,
            "es" => es,
            "pt" => pt,
            _ => en
        };
    }

    private string ResolveLanguage()
    {
        var cookieLang = Context?.Request?.Cookies?["deli-lang"];
        if (!string.IsNullOrWhiteSpace(cookieLang))
        {
            return cookieLang.ToLowerInvariant();
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
    }
}
