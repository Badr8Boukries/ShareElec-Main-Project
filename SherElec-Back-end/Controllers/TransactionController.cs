using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SherElec_Back_end.DTOs.Transaction;
using SherElec_Back_end.Services.Interfaces;
using System.Collections.Generic;
using System.Security.Claims; // Pour récupérer l'ID utilisateur du token
using System.Threading.Tasks;
using AutoMapper; // Injecter IMapper pour GetTransactionByIdAsync
using SherElec_Back_end.Models; // Pour mapper Transaction vers DTO si besoin

namespace SherElec_Back_end.Controllers
{
    [Route("api/transactions")]
    [ApiController]
    [Authorize] // Sécuriser toutes les méthodes de ce contrôleur par défaut
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly IMapper _mapper; // Injecter IMapper

        public TransactionsController(ITransactionService transactionService, IMapper mapper)
        {
            _transactionService = transactionService;
            _mapper = mapper;
        }

        // GET: api/transactions/{id} - Récupérer une transaction par son ID
        [HttpGet("{id}")]
        public async Task<ActionResult<TransactionResponseDTO>> GetTransactionById(int id)
        {
            // Récupérer l'ID de l'utilisateur connecté depuis le token JWT
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null)
            {
                return Unauthorized("Token invalide ou ID utilisateur non trouvé.");
            }

            var transactionEntity = await _transactionService.GetTransactionByIdAsync(id); // Récupère l'entité Transaction

            if (transactionEntity == null)
            {
                return NotFound($"Transaction avec l'ID {id} non trouvée.");
            }

            // Vérifier si l'utilisateur connecté est l'acheteur OU le vendeur
            if (transactionEntity.IdAcheteur.ToString() != userIdFromToken && transactionEntity.IdVendeur.ToString() != userIdFromToken)
            {
                // Optionnel : Les admins pourraient avoir le droit de voir toutes les transactions
                // if (!User.IsInRole("Admin")) { ... }
                return Forbid("Vous n'êtes pas autorisé à voir cette transaction.");
            }

            // Mapper l'entité Transaction vers TransactionResponseDTO
            var transactionDto = _mapper.Map<TransactionResponseDTO>(transactionEntity);

            return Ok(transactionDto);
        }

        // GET: api/transactions/achats - Récupérer les transactions où l'utilisateur connecté est l'acheteur
        [HttpGet("achats")]
        public async Task<ActionResult<IEnumerable<TransactionResponseDTO>>> GetTransactionsAchetees()
        {
            // Récupérer l'ID de l'utilisateur connecté depuis le token JWT
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null || !int.TryParse(userIdFromToken, out int acheteurId))
            {
                return Unauthorized("Token invalide ou ID utilisateur non trouvé.");
            }

            // Appeler le service avec l'ID de l'utilisateur connecté
            // Le service retourne déjà des DTOs mappés
            var transactions = await _transactionService.GetTransactionsAcheteesAsync(acheteurId);
            return Ok(transactions);
        }

        // GET: api/transactions/ventes - Récupérer les transactions où l'utilisateur connecté est le vendeur
        [HttpGet("ventes")]
        public async Task<ActionResult<IEnumerable<TransactionResponseDTO>>> GetTransactionsVendues()
        {
            // Récupérer l'ID de l'utilisateur connecté depuis le token JWT
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null || !int.TryParse(userIdFromToken, out int vendeurId))
            {
                return Unauthorized("Token invalide ou ID utilisateur non trouvé.");
            }

            // Appeler le service avec l'ID de l'utilisateur connecté
            // Le service retourne déjà des DTOs mappés
            var transactions = await _transactionService.GetTransactionsVenduesAsync(vendeurId);
            return Ok(transactions);
        }

        

    }
}