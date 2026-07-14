using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldAppUser
    {
        public int Id { get; set; }
        public string? UserHash { get; set; }
        public string? UserName { get; set; }
        public string? IsVerified { get; set; }
        public DateTime? Lastlogin { get; set; }
        public bool RememberLogin { get; set; }
        public decimal WildCoinBalance { get; set; }
        public string? Bio { get; set; }
        public string? WalletAddress { get; set; }
        // Optionaler Link zum eigenen externen Store des Creators (nur Anzeige, keine bezahlte Werbung).
        public string? StoreUrl { get; set; }
        public DateTime? CreatedAt { get; set; }

        // Premium-Creator-Status. Bildet ein (auch monatlich zahlbares) Abo ab: der Nutzer ist so lange
        // Premium, wie PremiumUntil in der Zukunft liegt. Für Dauer-Premium ein fernes Datum setzen,
        // für "kein Premium" null/Vergangenheit. Gate für kostenpflichtige Creator-Funktionen (z.B. TTS).
        public DateTime? PremiumUntil { get; set; }

        /// <summary>True, solange das Premium-Abo (noch) gültig ist. Nicht in der DB gespeichert.</summary>
        [NotMapped]
        public bool IsPremiumActive => PremiumUntil.HasValue && PremiumUntil.Value > DateTime.UtcNow;
    }
}
