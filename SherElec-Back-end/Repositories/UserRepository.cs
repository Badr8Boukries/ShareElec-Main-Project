using Microsoft.EntityFrameworkCore;
using SherElec_Back_end.Data;
using SherElec_Back_end.Models;
using SherElec_Back_end.Repositories.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace ShareElec.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddUser(User user)
        {
            user.MotDePasse = BCrypt.Net.BCrypt.HashPassword(user.MotDePasse);
            user.IsDeleted = false;
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task<User> GetUserById(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task UpdateUser(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id); 
            if (user != null)
            {
                user.IsDeleted = true;
                _context.Users.Update(user); 
                await _context.SaveChangesAsync(); 
            }
        }


        public async Task AddEmailVerification(EmailVerifier emailVerifier)
        {
            // Optionnel mais recommandé: Supprimer les anciennes vérifications pour le même email
            var oldVerifications = _context.EmailVerifierTable.Where(e => e.Email == emailVerifier.Email);
            _context.EmailVerifierTable.RemoveRange(oldVerifications);
            await _context.EmailVerifierTable.AddAsync(emailVerifier);
            await _context.SaveChangesAsync();
        }

        public async Task<EmailVerifier> GetEmailVerification(string email, string code)
        {
            // Ajout filtre de temps pour validité 
            var cutoff = DateTime.UtcNow.AddMinutes(-25);
            return await _context.EmailVerifierTable
                .Where(e => e.Email == email && e.VerificationCode == code && e.CreatedAt >= cutoff)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();
        }
    }
}