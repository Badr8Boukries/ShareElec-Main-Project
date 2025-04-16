using AutoMapper;
using SherElec_Back_end.DTOs.Response; // Pour UserRespenseDTO
using SherElec_Back_end.DTOs.Transaction; // Pour TransactionResponseDTO
using SherElec_Back_end.Models;

namespace SherElec_Back_end.Mappers
{
    public class TransactionMappingProfile : Profile
    {
      
        public TransactionMappingProfile()
        {
          

            CreateMap<Transaction, TransactionResponseDTO>()
                .ForMember(dest => dest.Acheteur, opt => opt.MapFrom(src => src.Acheteur == null ? null : new UserRespenseDTO
                {
                    ID = src.Acheteur.ID,
                    nom = src.Acheteur.Nom,
                    prenom = src.Acheteur.Prenom,
                    email = src.Acheteur.Email,
                    numeroTelephone = src.Acheteur.NumeroTelephone,
                    sommeEnergie = src.Acheteur.sommeEnergie
                }))
                .ForMember(dest => dest.Vendeur, opt => opt.MapFrom(src => src.Vendeur == null ? null : new UserRespenseDTO
                {
                    ID = src.Vendeur.ID,
                    nom = src.Vendeur.Nom,
                    prenom = src.Vendeur.Prenom,
                    email = src.Vendeur.Email,
                    numeroTelephone = src.Vendeur.NumeroTelephone,
                    sommeEnergie = src.Vendeur.sommeEnergie
                }));
        }
    }
}