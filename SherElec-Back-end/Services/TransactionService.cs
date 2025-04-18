using SherElec_Back_end.Models;
using SherElec_Back_end.Repositories.Interfaces;
using SherElec_Back_end.Services.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using AutoMapper;
using Stripe;
using System.Linq;
using static SherElec_Back_end.Controllers.PaymentsController;
using SherElec_Back_end.Data;
using SherElec_Back_end.DTOs.Request;
using SherElec_Back_end.DTOs.Transaction;

namespace SherElec_Back_end.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;
        private readonly IOffreRepository _offreRepository;
        private readonly IMapper _mapper;

        private readonly string _stripeSecretKey = "sk_test_51R4SW0PENFnTPu7Q5LkDuMRp9Cr5zMNTuSfAtJiD60FdHNF0uXTG7RXqbJZJdi2rFgzhui2DnKPM2LgyiF3Sfwuy00LjWeid04";


        public TransactionService(ITransactionRepository transactionRepository, IUserRepository userRepository, ApplicationDbContext context, IOffreRepository offreRepository, IMapper mapper)
        {
            _transactionRepository = transactionRepository;
            _userRepository = userRepository;
            _context = context;
            _offreRepository = offreRepository;
            _mapper = mapper;
            Stripe.StripeConfiguration.ApiKey = _stripeSecretKey;

        }

        public async Task<Transaction> GetTransactionByIdAsync(int id)
        {
            var transaction = await _transactionRepository.GetTransactionByIdAsync(id);

            if (transaction == null)
            {
                return null;
            }

            if (transaction.OffreId.HasValue)
            {
                transaction.Offre = await _offreRepository.GetOfferById(transaction.OffreId.Value); // Utiliser GetOfferById
            }

            // Récupérer les utilisateurs (acheteur et vendeur) même s'ils sont supprimés
            transaction.Acheteur = await _userRepository.GetUserById(transaction.IdAcheteur);
            transaction.Vendeur = await _userRepository.GetUserById(transaction.IdVendeur);


            return transaction;
        }

        public async Task CreateTransactionAsync(TransactionRequest request)
        {
            Console.WriteLine($"[TransactionService] Début CreateTransactionAsync pour Acheteur:{request.AcheteurId}, Vendeur:{request.VendeurId}");
            using (var dbTransaction = await _context.Database.BeginTransactionAsync())
            {
                Console.WriteLine("[TransactionService] Transaction DB démarrée.");
                try
                {
                    Console.WriteLine("[TransactionService] Récupération Acheteur...");
                    var acheteur = await _userRepository.GetUserById(request.AcheteurId);
                    Console.WriteLine("[TransactionService] Récupération Vendeur...");
                    var vendeur = await _userRepository.GetUserById(request.VendeurId);

                    if (acheteur == null || vendeur == null) { }


                    Console.WriteLine($"[TransactionService] Vérification solde Vendeur {vendeur.ID}. Requis: {request.Quantite}, Disponible: {vendeur.sommeEnergie}");
                    if (vendeur.sommeEnergie < request.Quantite)
                    {
                        await dbTransaction.RollbackAsync();
                        Console.Error.WriteLine($"[TransactionService] ERREUR BACKEND: Solde énergie vendeur insuffisant ({vendeur.sommeEnergie} < {request.Quantite}). Rollback effectué.");
                        throw new InvalidOperationException($"Solde vendeur insuffisant pour la transaction. Vendeur: {vendeur.ID}"); // Lance une exception pour logger l'erreur
                                                                                                                                      // return;
                    }

                    Offre offre = null;
                    if (request.OffreId != 0)
                    {
                        Console.WriteLine($"[TransactionService] Récupération Offre ID: {request.OffreId} pour validation...");
                        offre = await _offreRepository.GetOfferById(request.OffreId);
                        if (offre == null)
                        {
                            await dbTransaction.RollbackAsync();
                            Console.Error.WriteLine($"[TransactionService] ERREUR BACKEND: Offre {request.OffreId} non trouvée. Rollback effectué.");
                            throw new InvalidOperationException($"Offre {request.OffreId} spécifiée introuvable.");
                            // return;
                        }
                        // Vérifier si l'offre appartient bien au vendeur spécifié (sécurité supplémentaire)
                        if (offre.UserID != request.VendeurId)
                        {
                            await dbTransaction.RollbackAsync();
                            Console.Error.WriteLine($"[TransactionService] ERREUR SÉCURITÉ: L'offre {request.OffreId} n'appartient pas au vendeur {request.VendeurId}. Rollback effectué.");
                            throw new InvalidOperationException("Incohérence entre l'offre et le vendeur.");
                            // return;
                        }
                        Console.WriteLine($"[TransactionService] Vérification quantité Offre {offre.ID}. Demandé: {request.Quantite}, Disponible: {offre.Quantite}");
                        if (!offre.Status || offre.Quantite < request.Quantite)
                        {
                            await dbTransaction.RollbackAsync();
                            Console.Error.WriteLine($"[TransactionService] ERREUR BACKEND: Quantité offre insuffisante ou offre inactive (Demandé: {request.Quantite}, Disponible: {offre.Quantite}, Status: {offre.Status}). Rollback effectué.");
                            throw new InvalidOperationException("Quantité insuffisante sur l'offre ou offre inactive.");
                            // return;
                        }
                        Console.WriteLine("[TransactionService] Validation quantité offre OK.");
                    }

                }
                catch (Exception ex)
                {
                 
                    await dbTransaction.RollbackAsync();
                    Console.Error.WriteLine($"[TransactionService] ❌ ERREUR GLOBALE (catch): {ex}"); // Message + StackTrace
                                                                                                     // Ne pas relancer forcément pour ne pas que Stripe réessaie indéfiniment
                                                                                                     // Si on lance, le webhook controller devrait retourner 500.
                }
             
            }
        }
        public async Task<IEnumerable<TransactionResponseDTO>> GetTransactionsVenduesAsync(int vendeurId)
        {
            var transactions = await _transactionRepository.GetTransactionsVenduesAsync(vendeurId);

            return _mapper.Map<IEnumerable<TransactionResponseDTO>>(transactions);
        }

        public async Task<IEnumerable<TransactionResponseDTO>> GetTransactionsAcheteesAsync(int acheteurId)
        {
            var transactions = await _transactionRepository.GetTransactionsAcheteesAsync(acheteurId);

            return _mapper.Map<IEnumerable<TransactionResponseDTO>>(transactions);
        }
    }
}
