using Microsoft.EntityFrameworkCore;
using SherElec_Back_end.Models; // Assure-toi d'avoir les using nécessaires

namespace SherElec_Back_end.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Offre> Offers { get; set; }
        public DbSet<EmailVerifier> EmailVerifierTable { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Garde si tu hérites d'IdentityDbContext

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Offre) // La transaction a une Offre (ou null)
                .WithMany() // Une Offre peut avoir plusieurs Transactions (pas de navigation inverse définie)
                .HasForeignKey(t => t.OffreId) // La clé étrangère
                .IsRequired(false) // Confirme que la relation est optionnelle (OffreId est nullable)
                .OnDelete(DeleteBehavior.SetNull); // <<<=== LA MODIFICATION IMPORTANTE

            // Garde la configuration NoAction pour les utilisateurs si tu l'avais déjà
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Acheteur)
                .WithMany()
                .HasForeignKey(t => t.IdAcheteur)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Vendeur)
                .WithMany()
                .HasForeignKey(t => t.IdVendeur)
                .OnDelete(DeleteBehavior.NoAction);

        }
    }
}