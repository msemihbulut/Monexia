using System.Collections.Generic;

namespace Monexia.Models
{
    public class UserDetailsViewModel
    {
        public ApplicationUser User { get; set; }
        public List<Transaction> Transactions { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetBalance { get; set; }
    }
}