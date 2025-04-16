// SherElec_Back_end/DTOs/Request/OffreRequestDTO.cs
namespace SherElec_Back_end.DTOs.Request
{
    public class OffreRequestDTO
    {
        // Change 'int' en 'double' ici
        public double Quantite { get; set; }
        public string Type { get; set; }
        public bool VendDetails { get; set; }
        public double PrixKw { get; set; }
        public DateOnly Date { get; set; }
        public bool Status { get; set; }
        public int Userid { get; set; } // Doit correspondre à l'ID de l'utilisateur authentifié
    }
}