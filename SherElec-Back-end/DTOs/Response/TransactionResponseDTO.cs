using SherElec_Back_end.DTOs.Response; // Pour UserRespenseDTO
using System;

namespace SherElec_Back_end.DTOs.Transaction
{
    public class TransactionResponseDTO
    {
        public int ID { get; set; } // ID de la transaction
        public double Quantite { get; set; }
        public double PrixTotal { get; set; }
        public DateTime DateTransaction { get; set; }
        public int? OffreId { get; set; } // ID de l'offre associée (peut être null)

        // Détails importants pour l'affichage de l'historique
        public UserRespenseDTO Acheteur { get; set; } // Qui a acheté
        public UserRespenseDTO Vendeur { get; set; }  // À qui on a acheté
    }
}