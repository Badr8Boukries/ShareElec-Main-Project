
using SherElec_Back_end.DTOs.Request;
using SherElec_Back_end.DTOs.Transaction; 
using SherElec_Back_end.Models;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SherElec_Back_end.Services.Interfaces
{
    public interface ITransactionService
    {
        Task<Transaction> GetTransactionByIdAsync(int id); 
        Task CreateTransactionAsync(TransactionRequest request);

       
        Task<IEnumerable<TransactionResponseDTO>> GetTransactionsVenduesAsync(int vendeurId);
        Task<IEnumerable<TransactionResponseDTO>> GetTransactionsAcheteesAsync(int acheteurId); 
    }
}