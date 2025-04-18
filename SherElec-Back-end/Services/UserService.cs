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
using SherElec_Back_end.Models;
using SherElec_Back_end.Services.Interfaces;
using SherElec_Back_end.Repositories.Interfaces;
using SherElec_Back_end.Data;
using SherElec_Back_end.DTOs.Request;
using SherElec_Back_end.DTOs.Response;

namespace SherElec_Back_end.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;


        public UserService(IUserRepository userRepo, IMapper mapper, ILogger<UserService> logger, ApplicationDbContext context, IConfiguration configuration)
        {
            _userRepo = userRepo;
            _mapper = mapper;
            _logger = logger;
            _context = context;
            _configuration = configuration;
        }

        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(1000, 9999).ToString();
        }
        private async Task SendVerificationEmail(string email, string code)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");

            using var client = new SmtpClient(smtpSettings["Server"])
            {
                Port = int.Parse(smtpSettings["Port"]),
                Credentials = new NetworkCredential(smtpSettings["SenderEmail"], smtpSettings["SenderPassword"]),
                EnableSsl = bool.Parse(smtpSettings["EnableSsl"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpSettings["SenderEmail"]),
                Subject = "Code de vérification",
                Body = $"Votre code de vérification est: {code} \n \n ne pas rependre a ce message ." +
                $" \n \n Merci pour Ton inscription a notre site ",
                IsBodyHtml = false
            };
            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
        }

        public async Task InitiateEmailVerification(UserRequestDTO req)
        {
            var existingUser = await _userRepo.GetUserByEmailAsync(req.email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("Cet email est déjà utilisé.");
            }

            string verificationCode = GenerateVerificationCode();

            var emailVerifier = new EmailVerifier
            {
                Email = req.email,
                VerificationCode = verificationCode,
                CreatedAt = DateTime.UtcNow,

                Nom = req.nom,
                Prenom = req.prenom,
                MotDePasse = req.motDePasse,
                NumeroTelephone = req.numeroTelephone
            };

            await _userRepo.AddEmailVerification(emailVerifier);
            await SendVerificationEmail(req.email, verificationCode);
        }

        public async Task<bool> VerifyEmailAndCreateUser(string email, string code)
        {
            var verification = await _userRepo.GetEmailVerification(email, code);

            if (verification == null)
            {
                return false;
            }

            if (DateTime.UtcNow.Subtract(verification.CreatedAt).TotalHours > 24)
            {
                return false;
            }

            // Créer l'utilisateur avec les données stockées
            var user = new User
            {
                Email = verification.Email,
                Nom = verification.Nom,
                Prenom = verification.Prenom,
                MotDePasse = verification.MotDePasse,
                NumeroTelephone = verification.NumeroTelephone
            };

            await _userRepo.AddUser(user);

            return true;
        }


        public async Task ajoutCompteAsync(UserRequestDTO req)
        {
            // Vérification : L'email existe déjà ?
            var existingUser = await _userRepo.GetUserByEmailAsync(req.email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("Cet email est déjà utilisé.");
            }

            var user = _mapper.Map<User>(req);


            await _userRepo.AddUser(user);
        }


        public async Task<UserRespenseDTO> AuthentifierUtilisateurAsync(string email, string motDePasse)
        {
            var utilisateur = await _context.Users
                                            .FirstOrDefaultAsync(u => u.Email == email); // Recherche de l'utilisateur par email

            if (utilisateur == null)
            {
                throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");
            }

            if (!BCrypt.Net.BCrypt.Verify(motDePasse, utilisateur.MotDePasse))
            {
                throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");
            }

            return _mapper.Map<UserRespenseDTO>(utilisateur);
        }
        public string GenererToken(UserRespenseDTO utilisateur)
        {

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("VotreClefSecreteTresLongueEtComplexe123!@#$%^&*"));

            // Définir les informations du JWT (claims)
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, utilisateur.email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("Id", utilisateur.ID.ToString())
        };

            // Créer la signature
            var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            // Créer l'objet JWT
            var token = new JwtSecurityToken(
                issuer: "TonNomDeDomaine",  // Émetteur du token
                audience: "TonPublic",  // Public cible
                claims: claims,  // Claims ajoutés dans le token
                expires: DateTime.UtcNow.AddHours(1),  // Expiration dans 1 heure
                signingCredentials: signingCredentials  // Signature du token
            );


            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<UserRespenseDTO> UpdateUserAsync(int id, UserRequestDTO requestDto)
        {
            // Récupérer l'utilisateur existant par son ID
            var user = await _userRepo.GetUserById(id);
            if (user == null)
            {
                return null; // Utilisateur non trouvé
            }

            // Vérifier si l'email a été modifié
            if (user.Email != requestDto.email)
            {
                throw new InvalidOperationException("Vous ne pouvez pas modifier votre email.");
            }

            // Update only the fields that are allowed to change
            user.Nom = requestDto.nom;
            user.Prenom = requestDto.prenom;
            user.NumeroTelephone = requestDto.numeroTelephone;

            // Only update password if it's provided and not empty
            if (!string.IsNullOrWhiteSpace(requestDto.motDePasse))
            {
                user.MotDePasse = BCrypt.Net.BCrypt.HashPassword(requestDto.motDePasse);
            }

            // Mettre à jour l'utilisateur dans le repo
            await _userRepo.UpdateUser(user);

            // Mapper l'utilisateur mis à jour vers le DTO de réponse
            return _mapper.Map<UserRespenseDTO>(user);
        }

        public async Task<UserRespenseDTO> GetUserInfo(int id)
        {
            var user = await _userRepo.GetUserById(id);

            if (user == null)
            {
                return null;
            }

            return _mapper.Map<UserRespenseDTO>(user);
        }


        async Task IUserService.removeUser(int id)
        {
            var user = await _userRepo.GetUserById(id);
            if (user == null)
            {
                Console.WriteLine("Cet ID n'existe plus .");
                return;
            }

            await _userRepo.DeleteUser(id);
        }



    }
}