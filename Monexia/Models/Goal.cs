using System;
using System.ComponentModel.DataAnnotations;

namespace Monexia.Models
{
    public class Goal
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string UserId { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }
        [MaxLength(300)]
        public string Description { get; set; }
        [Required]
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        [Required]
        public string Category { get; set; }
        public string Status { get; set; } // "Devam" veya "Bitmiş"
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}