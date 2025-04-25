using AutoMapper;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using SherElec_Back_end.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;
using SherElec_Back_end.Services.Interfaces;
using SherElec_Back_end.Repositories.Interfaces;
using SherElec_Back_end.Data; 
using SherElec_Back_end.DTOs.Request;
using SherElec_Back_end.DTOs.Response;
using System;
using Microsoft.Extensions.Configuration; 
using System.Linq; 

namespace SherElec_Back_end.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;
       

      
        public UserService(IUserRepository userRepo, IMapper mapper, ILogger<UserService> logger, IConfiguration configuration)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString(); // 6 chiffres
        }

        private async Task SendVerificationEmail(string email, string code)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            if (string.IsNullOrEmpty(smtpSettings["Server"]) || string.IsNullOrEmpty(smtpSettings["Port"]) ||
                string.IsNullOrEmpty(smtpSettings["SenderEmail"]) || string.IsNullOrEmpty(smtpSettings["SenderPassword"]) ||
                string.IsNullOrEmpty(smtpSettings["EnableSsl"]))
            {
                _logger.LogCritical("Configuration SMTP incomplète dans appsettings.json !");
                throw new InvalidOperationException("La configuration SMTP est incomplète.");
            }

            using var client = new SmtpClient(smtpSettings["Server"])
            {
                Port = int.Parse(smtpSettings["Port"]),
                Credentials = new NetworkCredential(smtpSettings["SenderEmail"], smtpSettings["SenderPassword"]),
                EnableSsl = bool.Parse(smtpSettings["EnableSsl"])
            };
            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpSettings["SenderEmail"], smtpSettings["SenderName"]), 
                Subject = "Code de vérification SherElec",
                Body = $"Votre code de vérification est: {code} \n" +
                $"ce code s'expire dans 25 min ", 
                IsBodyHtml = false
            };
            mailMessage.To.Add(email);
            await client.SendMailAsync(mailMessage); 
        }

        public async Task InitiateEmailVerification(UserRequestDTO req)
        {
            var existingUser = await _userRepo.GetUserByEmailAsync(req.email);

            // Si l'utilisateur existe ET n'est PAS supprimé, refuser.
            if (existingUser != null && !existingUser.IsDeleted)
            {
                throw new InvalidOperationException("Cet email est déjà utilisé par un compte actif.");
            }
            // Si l'utilisateur n'existe pas OU est supprimé, on continue.

            string verificationCode = GenerateVerificationCode();
            var emailVerifier = new EmailVerifier
            {
                Email = req.email,
                VerificationCode = verificationCode,
                CreatedAt = DateTime.UtcNow,
                Nom = req.nom,
                Prenom = req.prenom,
                MotDePasse = req.motDePasse, // Stockage temporaire du mdp en clair
                NumeroTelephone = req.numeroTelephone,
                sommeEnergie = 200 // Solde initial
            };

            await _userRepo.AddEmailVerification(emailVerifier); // Ajoute/remplace le code en attente

            try
            {
                await SendVerificationEmail(req.email, verificationCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Echec envoi email verification à {req.email}, mais code enregistré.");
            }
        }

        public async Task<bool> VerifyEmailAndCreateUser(string email, string code)
        {
            var verification = await _userRepo.GetEmailVerification(email, code);
            if (verification == null)
            {
                return false; // Code invalide ou expiré
            }

            var existingUser = await _userRepo.GetUserByEmailAsync(email);

            if (existingUser != null && existingUser.IsDeleted) // Cas: Réactivation
            {
                existingUser.Nom = verification.Nom;
                existingUser.Prenom = verification.Prenom;
                existingUser.NumeroTelephone = verification.NumeroTelephone;
                // Met à jour et hache le nouveau mot de passe
                existingUser.MotDePasse = BCrypt.Net.BCrypt.HashPassword(verification.MotDePasse);
                existingUser.IsDeleted = false; // Réactive
                existingUser.sommeEnergie = verification.sommeEnergie; // Solde depuis vérif (0)

                await _userRepo.UpdateUser(existingUser);
            }
            else if (existingUser == null) // Cas: Création
            {
                var newUser = new User
                {
                    Email = verification.Email,
                    Nom = verification.Nom,
                    Prenom = verification.Prenom,
                    MotDePasse = verification.MotDePasse, // Sera haché par AddUser
                    NumeroTelephone = verification.NumeroTelephone,
                    sommeEnergie = verification.sommeEnergie,
                    IsDeleted = false
                };
                await _userRepo.AddUser(newUser);
            }
            else // Cas: Utilisateur existe et est déjà actif (ne devrait pas arriver via ce flux)
            {
                _logger.LogWarning($"Tentative de vérification pour un compte ({email}) déjà actif.");
                return false; // Échec logique
            }

            // Optionnel: Supprimer la vérification de la DB ici
            return true;
        }
        
        public async Task<UserRespenseDTO> AuthentifierUtilisateurAsync(string email, string motDePasse)
        {
            var utilisateur = await _userRepo.GetUserByEmailAsync(email);

            // Échec si utilisateur non trouvé OU supprimé OU mot de passe incorrect
            if (utilisateur == null || utilisateur.IsDeleted || !BCrypt.Net.BCrypt.Verify(motDePasse, utilisateur.MotDePasse))
            {
                throw new UnauthorizedAccessException("Email ou mot de passe incorrect."); // Message générique
            }

            return _mapper.Map<UserRespenseDTO>(utilisateur);
        }
       

        public string GenererToken(UserRespenseDTO utilisateur)
        {
            var secret = _configuration["Jwt:Secret"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
                throw new InvalidOperationException("Configuration JWT incomplète.");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, utilisateur.email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("Id", utilisateur.ID.ToString())
            };
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1), 
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<UserRespenseDTO> UpdateUserAsync(int id, UserRequestDTO requestDto)
        {
            var user = await _userRepo.GetUserById(id);
            if (user == null)
            {
                return null; 
            }

            if (user.Email != requestDto.email)
            {
                throw new InvalidOperationException("Vous ne pouvez pas modifier votre email.");
            }

            user.Nom = requestDto.nom;
            user.Prenom = requestDto.prenom;
            user.NumeroTelephone = requestDto.numeroTelephone;

            if (!string.IsNullOrWhiteSpace(requestDto.motDePasse))
            {
                user.MotDePasse = BCrypt.Net.BCrypt.HashPassword(requestDto.motDePasse);
            }

            await _userRepo.UpdateUser(user);

            return _mapper.Map<UserRespenseDTO>(user);
        }
        public async Task<UserRespenseDTO> GetUserInfo(int id)
        {
            var user = await _userRepo.GetUserById(id);

            if (user == null || user.IsDeleted)
            {
                return null; 
            }

            return _mapper.Map<UserRespenseDTO>(user);
        }
     
        async Task IUserService.removeUser(int id)
        {
            await _userRepo.DeleteUser(id);
        }
    }
}