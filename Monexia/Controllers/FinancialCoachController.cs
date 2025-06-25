using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Monexia.Data;
using Monexia.Helpers;
using Monexia.Models;
using Monexia.Services;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Monexia.Controllers
{
    public class FinancialCoachController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOpenAiService _openAiService;
        private readonly IMemoryCache _cache;

        public FinancialCoachController(AppDbContext context, UserManager<ApplicationUser> userManager, IOpenAiService openAiService, IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _openAiService = openAiService;
            _cache = cache;
        }

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
            {
                return BadRequest(new { success = false, message = "Soru boş olamaz." });
            }

            var user = await _userManager.GetUserAsync(User);
            var contextKey = $"FinancialContext_{user.Id}";

            // Finansal veri özetini önbellekten al veya oluştur
            if (!_cache.TryGetValue(contextKey, out string financialContext))
            {
                var ninetyDaysAgo = DateTime.Now.AddDays(-90);
                var transactions = await _context.Transactions
                    .Where(t => t.UserId == user.Id && t.TransactionDate >= ninetyDaysAgo)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                var goals = await _context.Goals.Where(g => g.UserId == user.Id).ToListAsync();
                var limits = await _context.SpendingLimits.Where(l => l.UserId == user.Id).ToListAsync();

                var summaryBuilder = new StringBuilder();
                summaryBuilder.AppendLine("--- KULLANICI FİNANSAL VERİ ÖZETİ (Son 90 Gün) ---");
                summaryBuilder.AppendLine($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy}");

                summaryBuilder.AppendLine("\n--- Son 3 Aylık Genel Bakış ---");
                for (int i = 0; i < 3; i++)
                {
                    var month = DateTime.Now.AddMonths(-i);
                    var monthlyIncome = transactions.Where(t => t.Type == TransactionType.Income && t.TransactionDate.Year == month.Year && t.TransactionDate.Month == month.Month).Sum(t => t.Amount);
                    var monthlyExpense = transactions.Where(t => t.Type == TransactionType.Expense && t.TransactionDate.Year == month.Year && t.TransactionDate.Month == month.Month).Sum(t => t.Amount);
                    summaryBuilder.AppendLine($"{new CultureInfo("tr-TR").DateTimeFormat.GetMonthName(month.Month)} {month.Year}: Gelir: {monthlyIncome:C}, Gider: {monthlyExpense:C}, Net: {monthlyIncome - monthlyExpense:C}");
                }

                var incomeByCategory = transactions.Where(t => t.Type == TransactionType.Income && t.IncomeCategory.HasValue).GroupBy(t => t.IncomeCategory.Value).Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) }).OrderByDescending(x => x.Total).ToList();
                if (incomeByCategory.Any())
                {
                    summaryBuilder.AppendLine("\n--- Son 90 Günlük Gelir Kategorileri ---");
                    foreach (var item in incomeByCategory) { summaryBuilder.AppendLine($"- {CommonHelper.ToHumanReadable(item.Category.ToString())}: {item.Total:C}"); }
                }

                var expenseByCategory = transactions.Where(t => t.Type == TransactionType.Expense && t.ExpenseCategory.HasValue).GroupBy(t => t.ExpenseCategory.Value).Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) }).OrderByDescending(x => x.Total).ToList();
                if (expenseByCategory.Any())
                {
                    summaryBuilder.AppendLine("\n--- Son 90 Günlük Harcama Kategorileri ---");
                    foreach (var item in expenseByCategory) { summaryBuilder.AppendLine($"- {CommonHelper.ToHumanReadable(item.Category.ToString())}: {item.Total:C}"); }
                }

                var activeGoals = goals.Where(g => g.Status != "Bitmiş").ToList();
                if (activeGoals.Any())
                {
                    summaryBuilder.AppendLine("\n--- Aktif Finansal Hedefler ---");
                    foreach (var goal in activeGoals)
                    {
                        var progress = goal.TargetAmount > 0 ? (goal.CurrentAmount / goal.TargetAmount) * 100 : 0;
                        summaryBuilder.AppendLine($"- '{goal.Name}': {goal.TargetAmount:C} hedefinin {goal.CurrentAmount:C} kadarı biriktirildi (%{progress:N0} tamamlandı).");
                    }
                }

                financialContext = summaryBuilder.ToString();
                _cache.Set(contextKey, financialContext, TimeSpan.FromMinutes(15));
            }

            var prompt = $"Sen 'Monexia' adında bir kişisel finans koçusun. Görevin, kullanıcının sorularını SADECE sana aşağıda sunulan, son 90 günü kapsayan finansal veri özetine dayanarak yanıtlamaktır. Bu özetin dışına çıkma. Cevapların kısa, net ve anlaşılır olsun. Cevaplarında, önemli noktaları vurgulamak için **kalın** metin, listeler için ise madde imleri (-) kullan. Bu, cevabın daha okunaklı olmasını sağlayacaktır.\n\n{financialContext}\n\n--- KULLANICI SORUSU ---\nKullanıcı: \"{request.Question}\"\nMonexia:";

            try
            {
                var answer = await _openAiService.GetCompletionAsync(prompt);
                return Ok(new { success = true, answer });
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(500, new { success = false, message = "Yapay zeka ile iletişim kurulamadı." });
            }
        }
    }

    public class AskRequest
    {
        public string Question { get; set; }
    }
}