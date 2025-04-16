namespace SherElec_Back_end.DTOs.Request
{
    public class TransactionRequest
    {
        public int OffreId { get; set; } // ID de l'offre
        public double Amount { get; set; }  // Montant total en euros
        public double Quantite { get; set; } // Quantité achetée
        public int AcheteurId { get; set; } // ID de l'acheteur
        public int VendeurId { get; set; } // ID du vendeur
    }
}
