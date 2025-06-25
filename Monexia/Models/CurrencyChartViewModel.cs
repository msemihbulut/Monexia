namespace Monexia.Models
{
    public class CurrencyChartViewModel
    {
        public string Symbol { get; set; }
        public Dictionary<string, List<string>> DateSets { get; set; } = new();
        public Dictionary<string, List<decimal>> PriceSets { get; set; } = new();
    }

    public class CurrencyTimeSeriesResponse
    {
        public Dictionary<string, Dictionary<string, decimal>> rates { get; set; }
    }
}