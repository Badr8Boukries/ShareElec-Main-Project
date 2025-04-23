using Microsoft.AspNetCore.Mvc;
using SherElec_Back_end.DTOs.Request;
using SherElec_Back_end.DTOs.Response;
using SherElec_Back_end.Services.Interfaces;
using System;
using System.Collections.Generic; // Pour IEnumerable
using System.Linq; // Pour Any()
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Pour DbUpdateException
using Microsoft.AspNetCore.Authorization; // Pour [Authorize] si besoin
using Microsoft.AspNetCore.Http; // Pour StatusCodes

namespace SherElec_Back_end.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OffreController : Controller // Garde ControllerBase si pas de vues
    {
        private readonly IOffreService _offreService;
        private readonly ILogger<OffreController> _logger; // Logger pour tracer les erreurs

        public OffreController(IOffreService offreService, ILogger<OffreController> logger)
        {
            _offreService = offreService ?? throw new ArgumentNullException(nameof(offreService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // --- Méthodes existantes (inchangées) ---

        [HttpPost("add")]
        public async Task<ActionResult<OffreResponseDTO>> CreateOffer([FromBody] OffreRequestDTO offerRequestDto)
        {
            if (offerRequestDto == null)
            {
                return BadRequest("Invalid data.");
            }
            // Ajout try-catch basique pour robustesse
            try
            {
                var offre = await _offreService.CreateOfferAsync(offerRequestDto);
                // Vérification ajoutée si jamais le service retourne null
                if (offre == null || offre.Id == 0)
                {
                    _logger.LogError("CreateOfferAsync a retourné une offre invalide.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Erreur création offre.");
                }
                return CreatedAtAction(nameof(GetOfferById), new { id = offre.Id }, offre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur interne dans CreateOffer");
                return StatusCode(StatusCodes.Status500InternalServerError, "Erreur interne.");
            }
        }

        [HttpGet("offres")]
        public async Task<ActionResult<IEnumerable<OffreResponseDTO>>> GetAllOffers()
        {
            // Ajout try-catch basique
            try
            {
                var offres = await _offreService.GetAllOffersAsync();
                return Ok(offres);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur interne dans GetAllOffers");
                return StatusCode(StatusCodes.Status500InternalServerError, "Erreur interne.");
            }
        }

        [HttpGet("offres/user/{userId}")]
        public async Task<ActionResult<IEnumerable<MesOffreResponseDTO>>> GetOffresByUserId(int userId)
        {
            // Note: Devrait idéalement être protégé et utiliser l'ID du token
            try
            {
                var offres = await _offreService.GetOffresByUserIdAsync(userId);
                // La vérification Any() est bonne ici
                if (offres == null || !offres.Any())
                {
                    // Retourne 200 OK avec tableau vide est souvent préférable à 404
                    // return NotFound($"Aucune offre trouvée pour l'utilisateur {userId}.");
                    return Ok(new List<MesOffreResponseDTO>()); // Retourne tableau vide
                }
                return Ok(offres);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur interne dans GetOffresByUserId pour {UserId}", userId);
                return StatusCode(500, $"Erreur interne du serveur : {ex.Message}"); // Garde message simple
            }
        }

        [HttpGet("offres/{id}")]
        public async Task<ActionResult<OffreResponseDTO>> GetOfferById(int id)
        {
            // Ajout try-catch basique
            try
            {
                var offre = await _offreService.GetOfferByIdAsync(id);
                if (offre == null)
                {
                    return NotFound($"Offer with ID {id} not found.");
                }
                return Ok(offre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur interne dans GetOfferById pour {OffreId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Erreur interne.");
            }
        }

        [HttpPut("update/{id}")]
        // [Authorize] // À décommenter si besoin + vérifier propriétaire
        public async Task<ActionResult<OffreResponseDTO>> UpdateOffre(int id, [FromBody] OffreRequestDTO offerRequestDto)
        {
            if (offerRequestDto == null)
            {
                return BadRequest("Invalid data.");
            }
            // TODO: Vérifier que l'utilisateur authentifié est le propriétaire de l'offre id
            try
            {
                var updatedOffer = await _offreService.UpdateOfferAsync(id, offerRequestDto);
                if (updatedOffer == null)
                {
                    return NotFound($"Offer with ID {id} not found.");
                }
                return Ok(updatedOffer);
            }
            catch (Exception ex) // Attrape aussi DbUpdateConcurrencyException potentiellement
            {
                _logger.LogError(ex, "Erreur interne dans UpdateOffre pour {OffreId}", id);
                // Retourner Conflict (409) si c'est DbUpdateConcurrencyException?
                return StatusCode(StatusCodes.Status500InternalServerError, "Erreur interne.");
            }
        }

        // --- DELETE MODIFIÉ ---
        [HttpDelete("delete/{id}")]
        // [Authorize] // À décommenter si besoin + vérifier propriétaire
        public async Task<IActionResult> DeleteOffre(int id) // Retourne IActionResult
        {
            // TODO: Vérifier que l'utilisateur authentifié est le propriétaire de l'offre id
            try
            {
                // Appelle le service qui retourne maintenant bool
                var success = await _offreService.DeleteOfferAsync(id);

                if (!success)
                {
                    // Si false, l'offre n'a pas été trouvée par le service
                    _logger.LogWarning("Tentative de suppression pour une offre non trouvée ID: {OffreId}", id);
                    return NotFound(new { message = $"Offre avec ID {id} non trouvée." });
                }

                // Si true, la suppression (ou la tentative) s'est bien passée au niveau service/repo (sans exception bloquante)
                _logger.LogInformation("Suppression réussie (ou aucune action requise) pour Offre ID: {OffreId}", id);
                return NoContent(); // 204 No Content = succès pour DELETE
            }
            catch (DbUpdateException dbEx) // Attrape spécifiquement les erreurs EF lors du SaveChanges
            {
                _logger.LogError(dbEx, "Erreur DbUpdateException lors de la suppression de l'offre ID {OffreId}", id);
                // Vérifie l'erreur interne pour une contrainte de référence (FK)
                // Le message exact peut varier ("REFERENCE constraint", "FOREIGN KEY constraint")
                if (dbEx.InnerException != null && dbEx.InnerException.Message.Contains("constraint"))
                {
                    // 409 Conflict est approprié ici
                    return Conflict(new { message = "Impossible de supprimer cette offre, elle est liée à des transactions existantes." });
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur de base de données lors de la suppression." });
                }
            }
            catch (Exception ex) // Attrape toute autre erreur inattendue
            {
                _logger.LogError(ex, "Erreur inattendue lors de la suppression de l'offre ID {OffreId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Une erreur interne est survenue." });
            }
        }
        // --- FIN DELETE MODIFIÉ ---
    }
}