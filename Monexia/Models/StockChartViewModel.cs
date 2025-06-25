namespace Monexia.Models
{
    public class StockChartViewModel
    {
        public string Symbol { get; set; }
        public Dictionary<string, List<string>> DateSets { get; set; } = new();
        public Dictionary<string, List<decimal>> PriceSets { get; set; } = new();
    }
}
