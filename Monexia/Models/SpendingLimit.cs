using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Monexia.Models
{
    public class SpendingLimit
    {
        public int Id { get; set; }

        public string? UserId { get; set; }

        [Required]
        public ExpenseCategory Category { get; set; }

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "Limit 1 ₺ ve üzeri olmalıdır.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LimitAmount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
