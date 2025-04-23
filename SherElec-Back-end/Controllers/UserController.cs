using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SherElec_Back_end.DTOs.Request; 
using SherElec_Back_end.DTOs.Response;
using SherElec_Back_end.Services.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; 

namespace SherElec_Back_end.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpPost("sign-in")]
        public async Task<IActionResult> InitiateVerification([FromBody] UserRequestDTO userRequestDTO)
        {
            if (userRequestDTO == null) return BadRequest("Les données de l'utilisateur sont incorrectes.");
            try
            {
                await _userService.InitiateEmailVerification(userRequestDTO);
                return Ok(new { message = "Un code de vérification a été envoyé à votre email." });
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Une erreur est survenue lors de l'initiation.", details = ex.Message }); }
        }

        [HttpPost("veriferEmail")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerificationRequest request) // Assure-toi que VerificationRequest est défini
        {
            if (request == null) return BadRequest("Les données de vérification sont incorrectes.");
            try
            {
                bool isVerified = await _userService.VerifyEmailAndCreateUser(request.Email, request.Code);
                if (!isVerified) return BadRequest("Code de vérification invalide ou expiré.");
                return Ok(new { message = "Compte créé ou réactivé avec succès." });
            }
            catch (Exception ex) { return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Une erreur est survenue lors de la vérification.", details = ex.Message }); }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Microsoft.AspNetCore.Identity.Data.LoginRequest request) 
        {
            if (request == null) return BadRequest("Données de connexion invalides.");
            try
            {
                var utilisateur = await _userService.AuthentifierUtilisateurAsync(request.Email, request.Password);
                var token = _userService.GenererToken(utilisateur);
                return Ok(new { Token = token, Utilisateur = utilisateur });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { Message = ex.Message }); }
            catch (Exception ex) { return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Une erreur interne est survenue.", details = ex.Message }); }
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserRespenseDTO>> GetUserInfo(int id)
        {
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null || userIdFromToken != id.ToString())
            {
                return Unauthorized("Accès non autorisé aux informations de cet utilisateur.");
            }
            var utilisateur = await _userService.GetUserInfo(id);
            if (utilisateur == null) return NotFound("Utilisateur non trouvé ou désactivé.");
            return Ok(utilisateur);
        }

        [Authorize]
        [HttpPut("maj/{id}")]
        public async Task<ActionResult<UserRespenseDTO>> UpdateUser(int id, [FromBody] UserRequestDTO userRequestDto)
        {
            if (userRequestDto == null) return BadRequest("Les données de l'utilisateur sont incorrectes.");
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null || userIdFromToken != id.ToString())
            {
                return Unauthorized("Vous ne pouvez modifier que votre propre compte.");
            }
            try
            {
                var updatedUser = await _userService.UpdateUserAsync(id, userRequestDto);
                if (updatedUser == null) return NotFound($"Utilisateur avec l'ID {id} non trouvé ou désactivé.");
                return Ok(updatedUser);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("compte supprimé") || ex.Message.Contains("modifier votre email"))
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur MAJ User {id}: {ex}"); // Log minimal
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Une erreur interne est survenue lors de la mise à jour." });
            }
        }

        // --- CORRIGÉ ---
        [Authorize]
        [HttpDelete("del/{id}")]
        public async Task<IActionResult> RemoveUser(int id) // Retourne IActionResult
        {
            var userIdFromToken = User.FindFirstValue("Id");
            if (userIdFromToken == null || userIdFromToken != id.ToString())
            {
                return Unauthorized("Vous ne pouvez pas supprimer un compte qui n'est pas le vôtre.");
            }
            try
            {
                // Utilise 'await' pour attendre la fin de l'opération du service
                await _userService.removeUser(id);
                // Si l'opération réussit sans exception, renvoie OK ou NoContent
                return Ok(new { Message = "Compte désactivé avec succès." });
                // Ou return NoContent(); // Pour un statut 204
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur Suppression User {id}: {ex}"); // Log minimal
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Une erreur est survenue lors de la désactivation." });
            }
        }
        
    }
}