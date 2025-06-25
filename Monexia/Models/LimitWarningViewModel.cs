namespace Monexia.Models
{
    public class LimitWarningViewModel
    {
        public string Category { get; set; }
        public decimal Limit { get; set; }
        public decimal CurrentSpent { get; set; }
        public double PercentageUsed { get; set; }
    }
}
