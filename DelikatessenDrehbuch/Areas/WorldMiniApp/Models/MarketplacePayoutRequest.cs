namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Auszahlungs-Antrag eines Verkäufers (Marktplatz, Modell B).
    /// Der Verkäufer-Anteil liegt als internes Guthaben (WorldAppUser.WildCoinBalance);
    /// mit einem Antrag fordert er die Auszahlung an seine Wallet an. Die Plattform zahlt
    /// (aktuell manuell aus der Plattform-Wallet) und trägt den TxHash ein.
    /// </summary>
    public class MarketplacePayoutRequest
    {
        public int Id { get; set; }
        public string SellerHash { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Token { get; set; } = "WLD";            // WLD | USDCE
        public string WalletAddress { get; set; } = string.Empty; // Auszahlungsziel
        public string Status { get; set; } = "pending";        // pending | paid | rejected
        public string? TxHash { get; set; }                    // On-Chain-Tx der Auszahlung
        public string? Note { get; set; }                      // z.B. Ablehnungsgrund
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessedBy { get; set; }               // Admin-Hash
    }
}
