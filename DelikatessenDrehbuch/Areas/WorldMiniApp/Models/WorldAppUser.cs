

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
    }
}
