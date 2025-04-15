using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SherElec_Back_end.DTOs.Transaction; 
using SherElec_Back_end.Services.Interfaces; 
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper; 
using SherElec_Back_end.Models; 

namespace SherElec_Back_end.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Sécurise toutes les routes de ce contrôleur
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly IMapper _mapper; 

        public TransactionController(ITransactionService transactionService, IMapper mapper)
        {
            _transactionService = transactionService;
            _mapper = mapper;
        }

       
        [HttpGet("ventes")]
        [ProducesResponseType(typeof(IEnumerable<TransactionResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<TransactionResponseDTO>>> GetMySales()
        {
            var userIdString = User.FindFirst("Id")?.Value;
            if (userIdString == null || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Impossible d'identifier l'utilisateur.");
            }

            try
            {
                // Le service retourne directement les DTOs ici
                var transactions = await _transactionService.GetTransactionsVenduesAsync(userId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur GetMySales pour user {userId}: {ex}"); // Log simplifié
                return StatusCode(StatusCodes.Status500InternalServerError, "Erreur interne lors de la récupération de vos ventes.");
            }
        }

        
        [HttpGet("achats")]
        [ProducesResponseType(typeof(IEnumerable<TransactionResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<TransactionResponseDTO>>> GetMyPurchases()
        {
            var userIdString = User.FindFirst("Id")?.Value;
            if (userIdString == null || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Impossible d'identifier l'utilisateur.");
            }

            try
            {
                // Le service retourne directement les DTOs ici
                var transactions = await _transactionService.GetTransactionsAcheteesAsync(userId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur GetMyPurchases pour user {userId}: {ex}"); // Log simplifié
                return StatusCode(StatusCodes.Status500InternalServerError, "Erreur interne lors de la récupération de vos achats.");
            }
        }

      
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(TransactionResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TransactionResponseDTO>> GetTransactionById(int id)
        {
            var userIdString = User.FindFirst("Id")?.Value;
            if (userIdString == null || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Impossible d'identifier l'utilisateur.");
            }

            try
            {
                var transactionModel = await _transactionService.GetTransactionByIdAsync(id);

                if (transactionModel == null)
                {
                    return NotFound($"Transaction avec l'ID {id} non trouvée.");
                }

              
                if (transactionModel.IdAcheteur != userId && transactionModel.IdVendeur != userId)
                {
                    return Forbid("Vous n'êtes pas autorisé à accéder à cette transaction.");
                }

                var transactionDto = _mapper.Map<TransactionResponseDTO>(transactionModel);
                return Ok(transactionDto);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur GetTransactionById pour id {id}: {ex}"); 
                return StatusCode(StatusCodes.Status500InternalServerError, $"Erreur interne lors de la récupération de la transaction {id}.");
            }
        }

        
    }
}