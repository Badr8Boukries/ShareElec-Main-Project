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

        // Dans la classe TransactionService :

        public async Task CreateTransactionAsync(TransactionRequest request)
        {
            Console.WriteLine($"[TransactionService] Début CreateTransactionAsync pour Acheteur:{request.AcheteurId}, Vendeur:{request.VendeurId}");
            // Utilisation d'une transaction de base de données pour assurer l'atomicité
            using (var dbTransaction = await _context.Database.BeginTransactionAsync())
            {
                Console.WriteLine("[TransactionService] Transaction DB démarrée.");
                try
                {
                    Console.WriteLine("[TransactionService] Récupération Acheteur...");
                    var acheteur = await _userRepository.GetUserById(request.AcheteurId);
                    Console.WriteLine($"[TransactionService] Acheteur récupéré: {(acheteur != null ? "OK" : "NON TROUVÉ")}");

                    Console.WriteLine("[TransactionService] Récupération Vendeur...");
                    var vendeur = await _userRepository.GetUserById(request.VendeurId);
                    Console.WriteLine($"[TransactionService] Vendeur récupéré: {(vendeur != null ? "OK" : "NON TROUVÉ")}");

                    if (acheteur == null || vendeur == null)
                    {
                        await dbTransaction.RollbackAsync();
                        Console.Error.WriteLine("[TransactionService] ECHEC: Acheteur ou Vendeur introuvable. Rollback.");
                        throw new Exception("Acheteur ou vendeur introuvable."); // Ou retourne sans exception ?
                    }

                    Console.WriteLine($"[TransactionService] Vérification solde Vendeur {vendeur.ID}. Requis: {request.Quantite}, Disponible: {vendeur.sommeEnergie}");
                    if (vendeur.sommeEnergie < request.Quantite)
                    {
                        await dbTransaction.RollbackAsync();
                        Console.Error.WriteLine("[TransactionService] ECHEC: Solde Vendeur insuffisant. Rollback.");
                        throw new InvalidOperationException("Solde d'énergie du vendeur insuffisant.");
                    }
                    Console.WriteLine("[TransactionService] Solde Vendeur OK.");

                    // Logique Offre (si applicable)
                    Offre offre = null;
                    if (request.OffreId != 0)
                    {
                        Console.WriteLine($"[TransactionService] Récupération Offre ID: {request.OffreId}");
                        offre = await _offreRepository.GetOfferById(request.OffreId);
                        Console.WriteLine($"[TransactionService] Offre récupérée: {(offre != null ? "OK" : "NON TROUVÉE")}");
                        if (offre != null)
                        {
                            Console.WriteLine($"[TransactionService] Vérification quantité Offre {offre.ID}. Demandé: {request.Quantite}, Disponible: {offre.Quantite}");
                            if (offre.Quantite < request.Quantite)
                            {
                                await dbTransaction.RollbackAsync();
                                Console.Error.WriteLine("[TransactionService] ECHEC: Quantité Offre insuffisante. Rollback.");
                                throw new InvalidOperationException("Quantité insuffisante dans l'offre.");
                            }
                            Console.WriteLine("[TransactionService] Quantité Offre OK.");
                        }
                        else
                        {
                            Console.WriteLine($"[TransactionService] AVERTISSEMENT: Offre ID {request.OffreId} fournie mais non trouvée.");
                            
                        }
                    }

                    double prixUnitaire = (request.Quantite == 0) ? 0 : (request.Amount / request.Quantite);
                    var transaction = new Transaction
                    {
                        IdAcheteur = request.AcheteurId,
                        IdVendeur = request.VendeurId,
                        Quantite = request.Quantite,
                        PrixUnitaire = prixUnitaire,
                        PrixTotal = request.Amount,
                        DateTransaction = DateTime.UtcNow,
                        OffreId = (offre != null) ? request.OffreId : (int?)null
                    };

                    Console.WriteLine("[TransactionService] Appel TransactionRepository.CreateTransactionAsync...");
                    await _transactionRepository.CreateTransactionAsync(transaction); // Contient le premier SaveChangesAsync
                    Console.WriteLine($"[TransactionService] RETOUR TransactionRepository.CreateTransactionAsync. Transaction ID (potentiel): {transaction.ID}");

                    // Mises à jour des soldes
                    Console.WriteLine($"[TransactionService] Mise à jour solde Acheteur {acheteur.ID}...");
                    acheteur.sommeEnergie += request.Quantite;
                    await _userRepository.UpdateUser(acheteur); // Contient SaveChangesAsync
                    Console.WriteLine($"[TransactionService] Solde Acheteur mis à jour. Nouveau solde: {acheteur.sommeEnergie}");

                    Console.WriteLine($"[TransactionService] Mise à jour solde Vendeur {vendeur.ID}...");
                    vendeur.sommeEnergie -= request.Quantite;
                    await _userRepository.UpdateUser(vendeur); // Contient SaveChangesAsync
                    Console.WriteLine($"[TransactionService] Solde Vendeur mis à jour. Nouveau solde: {vendeur.sommeEnergie}");

                    
                    if (offre != null)
                    {
                        Console.WriteLine($"[TransactionService] Mise à jour Offre {offre.ID}...");
                        offre.Quantite -= request.Quantite;
                        if (offre.Quantite <= 0)
                        {
                            offre.Status = false; 
                            offre.Quantite = 0; 
                            Console.WriteLine($"[TransactionService] Offre {offre.ID} marquée inactive.");
                        }
                        await _offreRepository.UpdateOffer(offre); // Contient SaveChangesAsync
                        Console.WriteLine($"[TransactionService] Offre mise à jour. Quantité restante: {offre.Quantite}");
                    }

                    // Commit Final
                    Console.WriteLine("[TransactionService] Tentative de COMMIT de la transaction DB...");
                    await dbTransaction.CommitAsync();
                    Console.WriteLine("✔️✔️✔️ [TransactionService] COMMIT RÉUSSI ! ✔️✔️✔️");

                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"****** ERREUR DANS CreateTransactionAsync - ROLLBACK ******\n{ex.ToString()}"); // ex.ToString() inclut la stack trace
                    try
                    {
                        await dbTransaction.RollbackAsync();
                        Console.WriteLine("[TransactionService] Rollback effectué suite à erreur.");
                    }
                    catch (Exception rbEx)
                    {
                        Console.Error.WriteLine($"[TransactionService] ERREUR CRITIQUE lors du Rollback : {rbEx.ToString()}");
                    }
                    
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
