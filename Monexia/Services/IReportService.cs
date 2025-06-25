using Monexia.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monexia.Services
{
    public interface IReportService
    {
        Task<byte[]> CreateExcelReportAsync(IEnumerable<Transaction> transactions);
        Task<byte[]> CreatePdfReportAsync(IEnumerable<Transaction> transactions, ApplicationUser user);
    }
}