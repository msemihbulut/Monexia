using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Monexia.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalTransactionAmount { get; set; }
        public List<ApplicationUser> AllUsers { get; set; }

        // For Charts
        public string MonthlyRegistrationsJson { get; set; }
        public string TransactionTypeDistributionJson { get; set; }
    }
}