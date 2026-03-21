using System;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class Measure
    {
        public int Id { get; set; }
        public string Metrics_DE { get; set; }
        public string Metrics_EN { get; set; }
        public string Metrics_ESP { get; set; }
        public string Metrics_PRT { get; set; }
        public string Metrics_ID { get; set; }
        public string Metrics_MS { get; set; }
        public string Metrics_NL { get; set; }
        public string Metrics_SE { get; set; }
        public string Metrics_DK { get; set; }
        public string Metrics_NO { get; set; }

        [NotMapped]
        public string UnitOfMeasurement
        {
            get => Metrics_DE;
            set => Metrics_DE = value;
        }

        public string GetLocalized(string langKey)
        {
            var lang = (string.IsNullOrWhiteSpace(langKey) ? "de" : langKey).ToLowerInvariant();
            return lang switch
            {
                "en" => Metrics_EN,
                "esp" => Metrics_ESP,
                "prt" => Metrics_PRT,
                "id" => Metrics_ID,
                "ms" => Metrics_MS,
                "nl" => Metrics_NL,
                "sv" => Metrics_SE,
                "da" => Metrics_DK,
                "no" => Metrics_NO,
                _ => Metrics_DE
            } ?? Metrics_DE ?? string.Empty;
        }
        public bool IsPieceUnit()
        {
            var vals = new[] { Metrics_DE, Metrics_EN, Metrics_ESP, Metrics_PRT, Metrics_ID, Metrics_MS, Metrics_NL, Metrics_SE, Metrics_DK, Metrics_NO };
            return vals.Any(x => string.Equals((x ?? string.Empty).Trim(), "Stk.", StringComparison.OrdinalIgnoreCase)
                              || string.Equals((x ?? string.Empty).Trim(), "Stück", StringComparison.OrdinalIgnoreCase)
                              || string.Equals((x ?? string.Empty).Trim(), "piece", StringComparison.OrdinalIgnoreCase)
                              || string.Equals((x ?? string.Empty).Trim(), "pieces", StringComparison.OrdinalIgnoreCase));
        }
    }
}
