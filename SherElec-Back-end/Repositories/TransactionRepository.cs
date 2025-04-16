using Microsoft.EntityFrameworkCore;
using SherElec_Back_end.Data;
using SherElec_Back_end.Models;
using SherElec_Back_end.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SherElec_Back_end.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public TransactionRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Transaction> GetTransactionByIdAsync(int id)
        {
            // Inclure Acheteur et Vendeur est crucial pour le DTO de réponse
            return await _context.Transactions
                .Include(t => t.Acheteur)
                .Include(t => t.Vendeur)
                .Include(t => t.Offre) // Inclure l'offre si besoin
                .FirstOrDefaultAsync(t => t.ID == id);
        }

        public async Task<IEnumerable<Transaction>> GetAllTransactionsAsync()
        {
            return await _context.Transactions
                .Include(t => t.Acheteur)
                .Include(t => t.Vendeur)
                .Include(t => t.Offre)
                .OrderByDescending(t => t.DateTransaction) // Trier par date
                .ToListAsync();
        }

        public async Task CreateTransactionAsync(Transaction transaction)
        {
          

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync(); // Sauvegarde la nouvelle transaction
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsVenduesAsync(int vendeurId)
        {
            return await _context.Transactions
                .Include(t => t.Acheteur) // Besoin des détails de l'acheteur pour l'historique
                .Include(t => t.Vendeur)
                .Include(t => t.Offre)
                .Where(t => t.IdVendeur == vendeurId)
                .OrderByDescending(t => t.DateTransaction)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsAcheteesAsync(int acheteurId)
        {
            return await _context.Transactions
                .Include(t => t.Acheteur)
                .Include(t => t.Vendeur) // Besoin des détails du vendeur pour l'historique
                .Include(t => t.Offre)
                .Where(t => t.IdAcheteur == acheteurId)
                .OrderByDescending(t => t.DateTransaction)
                .ToListAsync();
        }
    }
}