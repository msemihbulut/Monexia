using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Monexia.Models
{

    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        public TransactionType Type { get; set; }

        public IncomeCategory? IncomeCategory { get; set; }

        public ExpenseCategory? ExpenseCategory { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public string Description { get; set; } = "";
    }
}
