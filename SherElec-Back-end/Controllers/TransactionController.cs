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
     
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly IMapper _mapper; // Injecter IMapper

        public TransactionsController(ITransactionService transactionService, IMapper mapper)
        {
            _transactionService = transactionService;
            _mapper = mapper;
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<TransactionResponseDTO>> GetTransactionById(int id)
        {
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
                return Forbid("Vous n'êtes pas autorisé à voir cette transaction.");
            }

            var transactionDto = _mapper.Map<TransactionResponseDTO>(transactionEntity);

            return Ok(transactionDto);
        }

        [HttpGet("achats")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<TransactionResponseDTO>>> GetTransactionsAchetees()
        {
            // Récupérer l'ID de l'utilisateur connecté depuis le token JWT
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null || !int.TryParse(userIdFromToken, out int acheteurId))
            {
                return Unauthorized("Token invalide ou ID utilisateur non trouvé.");
            }

            var transactions = await _transactionService.GetTransactionsAcheteesAsync(acheteurId);
            return Ok(transactions);
        }

        [HttpGet("ventes")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<TransactionResponseDTO>>> GetTransactionsVendues()
        {
            // Récupérer l'ID de l'utilisateur connecté depuis le token JWT
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null || !int.TryParse(userIdFromToken, out int vendeurId))
            {
                return Unauthorized("Token invalide ou ID utilisateur non trouvé.");
            }

            // Le service retourne déjà des DTOs mappés
            var transactions = await _transactionService.GetTransactionsVenduesAsync(vendeurId);
            return Ok(transactions);
        }

        

    }
}