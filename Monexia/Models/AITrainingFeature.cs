namespace Monexia.Models
{
    public class AITrainingFeature
    {
        public string UserId { get; set; }
        public decimal AvgIncome { get; set; }
        public decimal AvgExpense { get; set; }
        public decimal ExpenseRatio { get; set; }
        public string TopExpenseCategory { get; set; }
        public decimal LimitUsagePercent { get; set; }
    }
}
