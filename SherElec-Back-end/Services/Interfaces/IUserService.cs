using SherElec_Back_end.DTOs.Request;
using SherElec_Back_end.DTOs.Response;

namespace SherElec_Back_end.Services.Interfaces
{
    public interface IUserService
    {
        string GenererToken(UserRespenseDTO utilisateur);
        Task<UserRespenseDTO> AuthentifierUtilisateurAsync(string email, string motDePasse);

        Task<UserRespenseDTO> UpdateUserAsync(int id, UserRequestDTO requestDto);
        Task removeUser(int id);
        Task<UserRespenseDTO> GetUserInfo(int id);
        Task InitiateEmailVerification(UserRequestDTO req);
        Task<bool> VerifyEmailAndCreateUser(string email, string code);

    }
}
