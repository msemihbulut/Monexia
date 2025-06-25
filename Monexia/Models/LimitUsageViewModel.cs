namespace Monexia.Models
{
    public class LimitUsageViewModel
    {
        public int Id { get; set; }
        public string? Category { get; set; }
        public decimal Limit { get; set; }
        public decimal Spent { get; set; }
        public double UsagePercent => Limit == 0 ? 0 : (double)(Spent / Limit) * 100;
    }
}
