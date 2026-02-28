using System;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class Measure
    {
        public int Id { get; set; }
        public string Metriks_DE { get; set; }
        public string Metriks_EN { get; set; }
        public string Metriks_ESP { get; set; }
        public string Metriks_PRT { get; set; }
        public string Metriks_ID { get; set; }
        public string Metriks_MS { get; set; }
        public string Metriks_NL { get; set; }
        public string Metriks_SE { get; set; }
        public string Metriks_DK { get; set; }
        public string Metriks_NO { get; set; }

        [NotMapped]
        public string UnitOfMeasurement
        {
            get => Metriks_DE;
            set => Metriks_DE = value;
        }

        public string GetLocalized(string langKey)
        {
            var lang = (string.IsNullOrWhiteSpace(langKey) ? "de" : langKey).ToLowerInvariant();
            return lang switch
            {
                "en" => Metriks_EN,
                "esp" => Metriks_ESP,
                "prt" => Metriks_PRT,
                "id" => Metriks_ID,
                "ms" => Metriks_MS,
                "nl" => Metriks_NL,
                "sv" => Metriks_SE,
                "da" => Metriks_DK,
                "no" => Metriks_NO,
                _ => Metriks_DE
            } ?? Metriks_DE ?? string.Empty;
        }
       

        public bool IsPieceUnit()
        {
            var vals = new[] { Metriks_DE, Metriks_EN, Metriks_ESP, Metriks_PRT, Metriks_ID, Metriks_MS, Metriks_NL, Metriks_SE, Metriks_DK, Metriks_NO };
            return vals.Any(x => string.Equals((x ?? string.Empty).Trim(), "Stk.", StringComparison.OrdinalIgnoreCase)
                              || string.Equals((x ?? string.Empty).Trim(), "Stück", StringComparison.OrdinalIgnoreCase)
                              || string.Equals((x ?? string.Empty).Trim(), "piece", StringComparison.OrdinalIgnoreCase)
                              || string.Equals((x ?? string.Empty).Trim(), "pieces", StringComparison.OrdinalIgnoreCase));
        }
    }
}
