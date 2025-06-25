namespace Monexia.Models
{
    public class MetalViewModel
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public decimal PriceTry { get; set; }
        public decimal PriceUsd { get; set; }
        public decimal? Price24K { get; set; }
        public decimal? Price22K { get; set; }
        public decimal? Price21K { get; set; }
        public decimal? Price20K { get; set; }
        public decimal? Price18K { get; set; }
        public decimal? Price16K { get; set; }
        public decimal? Price14K { get; set; }
        public decimal? Price10K { get; set; }
        public decimal? Change { get; set; }
        public decimal? ChangePercentage { get; set; }
        public decimal? Ask { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Open { get; set; }
        public decimal? High { get; set; }
        public decimal? Low { get; set; }
        public decimal? Prev { get; set; }
    }
}