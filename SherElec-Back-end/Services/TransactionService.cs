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

            // Récupérer l'offre associée, même si l'utilisateur est supprimé
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
            using (var transactionScope = _context.Database.BeginTransaction())
            {
                try
                {
                    // 1. Récupérer l'acheteur et le vendeur
                    var acheteur = await _userRepository.GetUserById(request.AcheteurId);
                    var vendeur = await _userRepository.GetUserById(request.VendeurId);

                    if (acheteur == null || vendeur == null)
                    {
                        transactionScope.Rollback();
                        throw new Exception("Acheteur ou vendeur introuvable.");
                    }

                    // 2. Calculer le prix unitaire
                    double prixUnitaire = (double)request.Amount / request.Quantite;

                    // 3. Créer la transaction
                    var transaction = new Transaction
                    {
                        IdAcheteur = request.AcheteurId,
                        IdVendeur = request.VendeurId,
                        Quantite = request.Quantite,
                        PrixUnitaire = prixUnitaire,
                        PrixTotal = request.Amount,
                        DateTransaction = DateTime.UtcNow,
                        OffreId = request.OffreId
                    };

                    await _transactionRepository.CreateTransactionAsync(transaction);

                    // 4. Mettre à jour les soldes d'énergie
                    acheteur.sommeEnergie += request.Quantite;
                    vendeur.sommeEnergie -= request.Quantite;

                    await _userRepository.UpdateUser(acheteur);
                    await _userRepository.UpdateUser(vendeur);


                    // 5. Mettre à jour le statut de l'offre (si applicable)
                    if (request.OffreId != 0)
                    {
                        var offre = await _offreRepository.GetOfferById(request.OffreId);
                        if (offre != null)
                        {
                            offre.Quantite -= request.Quantite;
                            if (offre.Quantite <= 0)
                            {
                                offre.Status = false;
                            }
                            await _offreRepository.UpdateOffer(offre);
                        }
                    }

                    transactionScope.Commit();
                }
                catch (Exception ex)
                {
                    transactionScope.Rollback();
                    Console.WriteLine($"Erreur lors de la création de la transaction: {ex}");
                    throw;
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
