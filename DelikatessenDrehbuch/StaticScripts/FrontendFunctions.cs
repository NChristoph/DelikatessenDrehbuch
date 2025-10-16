namespace DelikatessenDrehbuch.StaticScripts
{
    public static class FrontendFunctions
    {
        public static bool CheckCategory(string category)
        {
            var stringToCheck = category.ToLower().Trim();

            if (stringToCheck == "dessert" || stringToCheck == "vorspeise")
                return true;

            return false;
        }

        public static string CaseInsensitiveName(string encodetString)
        {
            var name = encodetString.ToLowerInvariant()
                                    .Replace("ä", "ae")
                                    .Replace("ö", "oe")
                                    .Replace("ü", "ue")
                                    .Replace("ß", "ss")
                                    .Replace(" ", "-")
                                    .Replace("&", "und")
                                    .Replace("?", "")
                                    .Replace("!", "")
                                    .Replace(",", "")
                                    .Replace(".", "")
                                    .Replace(":", "")
                                    .Replace(";", "");

            return name;
        }

        public static string CaseInsensitivePath(string encodetString)
        {
            if (string.IsNullOrEmpty(encodetString)) 
                return encodetString;

            var caseSensetivePath = encodetString
                    .Replace(".webp", "")
                    .Replace(".jpg", "")
                    .Replace(".png", "")
                    .Replace(" ", "%20");

            return caseSensetivePath;
        }

        public static string GetSmallImagePath(string path)
        {
            var basePath = FrontendFunctions.CaseInsensitivePath(path);

            var smallPath = basePath?.Replace(Environment.GetEnvironmentVariable("BigPic"),
                                        Environment.GetEnvironmentVariable("SmalPic"));
           return smallPath + "_small.webp";

        }
    }
}
