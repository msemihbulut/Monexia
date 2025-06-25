namespace Monexia.Models
{
    public class CryptoChartViewModel
    {
        public string Symbol { get; set; }
        public string Currency { get; set; }
        public Dictionary<string, List<string>> DateSets { get; set; } = new();
        public Dictionary<string, List<decimal>> PriceSets { get; set; } = new();
    }
}
