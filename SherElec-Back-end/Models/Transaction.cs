using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SherElec_Back_end.Models
{
    public class Transaction
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [ForeignKey("Acheteur")]
        public int IdAcheteur { get; set; }
        [DeleteBehavior(DeleteBehavior.NoAction)]
        public User Acheteur { get; set; }

        [Required]
        [ForeignKey("Vendeur")]
        public int IdVendeur { get; set; }
        [DeleteBehavior(DeleteBehavior.NoAction)]
        public User Vendeur { get; set; }

        [Required]
        public double Quantite { get; set; }

        [Required]
        public double PrixUnitaire { get; set; }

        [Required]
        public double PrixTotal { get; set; }

        [Required]
        public DateTime DateTransaction { get; set; }

        [ForeignKey("Offre")]
        public int? OffreId { get; set; } 

        public Offre? Offre { get; set; }
    }
}