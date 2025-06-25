using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Monexia.Data;
using Monexia.Helpers;
using Monexia.Models;
using Monexia.Services;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Diagnostics;

namespace Monexia.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IOpenAiService _openAiService;
        private readonly IMemoryCache _cache;

        public HomeController(AppDbContext context,
                              UserManager<ApplicationUser> userManager,
                              IWebHostEnvironment env,
                              IOpenAiService openAiService,
                              IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _openAiService = openAiService;
            _cache = cache;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var transactions = await _context.Transactions
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            // --- 1. Genel Dashboard Kart Verileri (Tüm Zamanlar) ---
            ViewBag.TotalTransactionCount = transactions.Count;
            var allTimeTotalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            ViewBag.TotalIncome = allTimeTotalIncome;
            var allTimeTotalExpense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            ViewBag.TotalExpense = allTimeTotalExpense;
            ViewBag.NetBalance = (allTimeTotalIncome - allTimeTotalExpense);

            var allTimeTopCategory = transactions
                .Where(t => t.Type == TransactionType.Expense && t.ExpenseCategory != null)
                .GroupBy(t => t.ExpenseCategory)
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(g => g.Total)
                .FirstOrDefault();
            ViewBag.TopCategory = allTimeTopCategory != null ? CommonHelper.ToHumanReadable(allTimeTopCategory.Category.ToString()) : "-";

            var goals = await _context.Goals.Where(g => g.UserId == user.Id).ToListAsync();
            ViewBag.TotalGoalCount = goals.Count;

            var limits = await _context.SpendingLimits.Where(l => l.UserId == user.Id).ToListAsync();
            ViewBag.TotalLimitCount = limits.Count;

            // --- 2. Yapay Zeka Tavsiyesi & Finans Skoru ---
            string aiAdvice;
            var cacheKey = $"FinancialAdvice_{user.Id}";
            if (!_cache.TryGetValue(cacheKey, out aiAdvice))
            {
                var last30Days = DateTime.Now.AddDays(-30);
                var last30DaysIncome = transactions.Where(t => t.Type == TransactionType.Income && t.TransactionDate >= last30Days).Sum(t => t.Amount);
                var last30DaysExpense = transactions.Where(t => t.Type == TransactionType.Expense && t.TransactionDate >= last30Days).Sum(t => t.Amount);
                var last30DaysTopCategory = transactions
                    .Where(t => t.Type == TransactionType.Expense && t.TransactionDate >= last30Days && t.ExpenseCategory != null)
                    .GroupBy(t => t.ExpenseCategory)
                    .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefault();

                var dataSummary = new StringBuilder();
                dataSummary.AppendLine($"Son 30 gündeki toplam gelir: {last30DaysIncome:N2} TL");
                dataSummary.AppendLine($"Son 30 gündeki toplam gider: {last30DaysExpense:N2} TL");
                dataSummary.AppendLine($"- En çok harcama yapılan kategori: {(last30DaysTopCategory != null ? CommonHelper.ToHumanReadable(last30DaysTopCategory.Category.ToString()) : "Belirtilmemiş")}");

                if (goals.Any(g => g.Status != "Bitmiş"))
                {
                    dataSummary.AppendLine("\n--- Kullanıcının Aktif Finansal Hedefleri ---");
                    foreach (var goal in goals.Where(g => g.Status != "Bitmiş").Take(3))
                    {
                        var progressPercentage = goal.TargetAmount > 0 ? (goal.CurrentAmount / goal.TargetAmount) * 100 : 0;
                        dataSummary.AppendLine($"- '{goal.Name}' hedefi: {goal.TargetAmount:N2} TL'nin {goal.CurrentAmount:N2} TL'si biriktirildi (%{progressPercentage:N0} tamamlandı).");
                    }
                }

                var prompt = "Sen Monexia adlı bir uygulama için yardımsever ve anlayışlı bir kişisel finans asistanısın. Amacın, kullanıcılara harcama ve gelir verilerine dayanarak teşvik edici, anlaşılır ve eyleme geçirilebilir finansal tavsiyeler sunmaktır. Sana verilen özeti analiz et ve kısa, samimi ve kişiselleştirilmiş bir tavsiye metni oluştur. Cevabın Türkçe olsun. İşte kullanıcının verileri:\n\n" + dataSummary.ToString();

                try
                {
                    aiAdvice = await _openAiService.GetCompletionAsync(prompt);
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(24));
                    _cache.Set(cacheKey, aiAdvice, cacheEntryOptions);
                }
                catch (Exception)
                {
                    aiAdvice = "Finansal tavsiyeniz şu anda oluşturulamıyor. Lütfen daha sonra tekrar deneyin.";
                }
            }
            ViewBag.AIAdvice = aiAdvice;

            var last30DaysIncomeForScore = transactions.Where(t => t.Type == TransactionType.Income && t.TransactionDate >= DateTime.Now.AddDays(-30)).Sum(t => t.Amount);
            var last30DaysExpenseForScore = transactions.Where(t => t.Type == TransactionType.Expense && t.TransactionDate >= DateTime.Now.AddDays(-30)).Sum(t => t.Amount);
            ViewBag.FinanceScore = CalculateFinanceScore(last30DaysIncomeForScore, last30DaysExpenseForScore, goals);

            // --- 3. Limit Verileri (Mevcut Ay) ---
            var limitUsagesForView = limits.Select(limit =>
            {
                var spent = transactions
                    .Where(t => t.UserId == user.Id &&
                                t.Type == TransactionType.Expense &&
                                t.ExpenseCategory == limit.Category &&
                                t.TransactionDate.Month == DateTime.Now.Month &&
                                t.TransactionDate.Year == DateTime.Now.Year)
                    .Sum(t => t.Amount);
                return new LimitUsageViewModel { Category = limit.Category.ToString(), Limit = limit.LimitAmount, Spent = spent };
            }).ToList();
            ViewBag.LimitUsages = limitUsagesForView;

            var limitWarnings = limitUsagesForView
                .Where(l => l.Limit > 0 && (l.UsagePercent >= 80))
                .Select(l => new LimitWarningViewModel { Category = l.Category, Limit = l.Limit, CurrentSpent = l.Spent, PercentageUsed = l.UsagePercent })
                .ToList();
            ViewBag.LimitWarnings = limitWarnings;

            // --- 4. Grafik Verileri ---
            var categorySummary = transactions.GroupBy(t => t.Type).Select(g => new { Category = g.Key.ToString(), Total = g.Sum(x => x.Amount) }).ToList();
            ViewBag.CategorySummary = JsonConvert.SerializeObject(categorySummary);
            var monthlyExpenses = transactions.Where(t => t.Type == TransactionType.Expense).GroupBy(t => t.TransactionDate.ToString("yyyy-MM")).OrderBy(g => g.Key).Select(g => new { Month = g.Key, Total = g.Sum(x => x.Amount) }).ToList();
            ViewBag.MonthlyExpenses = JsonConvert.SerializeObject(monthlyExpenses);
            var monthlyIncomes = transactions.Where(t => t.Type == TransactionType.Income).GroupBy(t => t.TransactionDate.ToString("yyyy-MM")).OrderBy(g => g.Key).Select(g => new { Month = g.Key, Total = g.Sum(x => x.Amount) }).ToList();
            ViewBag.MonthlyIncomes = JsonConvert.SerializeObject(monthlyIncomes);
            var monthlyBalances = transactions.OrderBy(t => t.TransactionDate).GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month }).Select(g => new { Year = g.Key.Year, Month = g.Key.Month, MonthlyNet = g.Sum(t => t.Type == TransactionType.Income ? t.Amount : -t.Amount) }).ToList();
            decimal cumulativeBalance = 0;
            var cumulativeData = monthlyBalances.Select(mb => { cumulativeBalance += mb.MonthlyNet; return new { Date = new DateTime(mb.Year, mb.Month, 1).ToString("MMM yyyy", new CultureInfo("tr-TR")), Balance = cumulativeBalance }; }).ToList();
            ViewBag.CumulativeBalanceData = JsonConvert.SerializeObject(new { labels = cumulativeData.Select(d => d.Date), data = cumulativeData.Select(d => d.Balance) });
            var last6Months = Enumerable.Range(0, 6).Select(i => DateTime.Now.AddMonths(-i)).OrderBy(d => d).ToList();
            var yearlyLabels = last6Months.Select(d => d.ToString("MMM yyyy", new CultureInfo("tr-TR"))).ToList();
            var monthlyIncomesForYearly = transactions.Where(t => t.Type == TransactionType.Income && t.TransactionDate >= last6Months.First().Date.AddDays(-last6Months.First().Day + 1)).GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month }).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
            var monthlyExpensesForYearly = transactions.Where(t => t.Type == TransactionType.Expense && t.TransactionDate >= last6Months.First().Date.AddDays(-last6Months.First().Day + 1)).GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month }).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
            var yearlyIncomeData = last6Months.Select(d => monthlyIncomesForYearly.GetValueOrDefault(new { d.Year, d.Month }, 0)).ToList();
            var yearlyExpenseData = last6Months.Select(d => monthlyExpensesForYearly.GetValueOrDefault(new { d.Year, d.Month }, 0)).ToList();
            ViewBag.YearlyCompareData = JsonConvert.SerializeObject(new { labels = yearlyLabels, incomeData = yearlyIncomeData, expenseData = yearlyExpenseData });
            var topExpenseCategories = transactions.Where(t => t.Type == TransactionType.Expense && t.ExpenseCategory.HasValue).GroupBy(t => t.ExpenseCategory.Value).Select(g => new { Category = CommonHelper.ToHumanReadable(g.Key.ToString()), Total = g.Sum(t => t.Amount) }).OrderByDescending(x => x.Total).Take(5).ToDictionary(x => x.Category, x => x.Total);
            var topIncomeCategories = transactions.Where(t => t.Type == TransactionType.Income && t.IncomeCategory.HasValue).GroupBy(t => t.IncomeCategory.Value).Select(g => new { Category = CommonHelper.ToHumanReadable(g.Key.ToString()), Total = g.Sum(t => t.Amount) }).OrderByDescending(x => x.Total).Take(5).ToDictionary(x => x.Category, x => x.Total);
            var topCategoriesLabels = topExpenseCategories.Keys.Union(topIncomeCategories.Keys).ToList();
            var topCategoriesExpenseData = topCategoriesLabels.Select(l => topExpenseCategories.GetValueOrDefault(l, 0)).ToList();
            var topCategoriesIncomeData = topCategoriesLabels.Select(l => topIncomeCategories.GetValueOrDefault(l, 0)).ToList();
            ViewBag.TopCategoriesData = JsonConvert.SerializeObject(new { labels = topCategoriesLabels, expenseData = topCategoriesExpenseData, incomeData = topCategoriesIncomeData });

            var recentTransactions = transactions.Take(7).ToList();
            ViewBag.Goals = goals;
            return View(recentTransactions);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Landing()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index");
            return View();
        }

        private int CalculateFinanceScore(decimal totalIncome, decimal totalExpense, List<Goal> goals)
        {
            var score = 50;

            if (totalIncome > 0)
            {
                var ratio = totalExpense / totalIncome;
                if (ratio < 0.5m) score += 25;
                else if (ratio < 0.75m) score += 15;
                else if (ratio > 1.0m) score -= 15;
            }
            else if (totalExpense > 0)
            {
                score -= 25;
            }

            var completedGoals = goals.Count(g => g.Status == "Bitmiş");
            if (completedGoals > 0) score += (completedGoals * 10);

            return Math.Max(0, Math.Min(100, score));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new Monexia.Models.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
