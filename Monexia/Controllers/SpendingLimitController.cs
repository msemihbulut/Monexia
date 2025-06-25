using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monexia.Data;
using Monexia.Models;
using Monexia.Services;
using System.Text;
using System.Text.Json;
using Monexia.Helpers;

namespace Monexia.Controllers
{
    [Authorize]
    public class SpendingLimitController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOpenAiService _openAiService;


        public SpendingLimitController(AppDbContext context, UserManager<ApplicationUser> userManager, IOpenAiService openAiService)
        {
            _context = context;
            _userManager = userManager;
            _openAiService = openAiService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var month = DateTime.Now.Month;
            var year = DateTime.Now.Year;

            var limits = await _context.SpendingLimits
                .Where(l => l.UserId == user.Id &&
                            l.CreatedDate.Month == month &&
                            l.CreatedDate.Year == year)
                .ToListAsync();

            var viewModel = new List<LimitUsageViewModel>();

            foreach (var limit in limits)
            {
                var spent = await _context.Transactions
                    .Where(t => t.UserId == user.Id &&
                                t.Type.ToString() == "Expense" &&
                                t.ExpenseCategory.ToString() == limit.Category.ToString() &&
                                t.TransactionDate.Month == month &&
                                t.TransactionDate.Year == year)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0;

                viewModel.Add(new LimitUsageViewModel
                {
                    Id = limit.Id,
                    Category = limit.Category.ToString(),
                    Limit = limit.LimitAmount,
                    Spent = spent
                });
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var limit = await _context.SpendingLimits.FindAsync(id);
            if (limit == null) return NotFound();
            return View(limit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SpendingLimit model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _userManager.GetUserAsync(User);
                    model.UserId = user.Id;

                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Limit başarıyla güncellendi.";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    return View(model);
                }
            }

            return View(model);
        }

        public IActionResult Add(string? category, decimal? limitAmount)
        {
            var model = new SpendingLimit();
            if (!string.IsNullOrEmpty(category) && Enum.TryParse<ExpenseCategory>(category, out var expenseCategory))
            {
                model.Category = expenseCategory;
            }

            if (limitAmount.HasValue)
            {
                model.LimitAmount = limitAmount.Value;
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(SpendingLimit model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var entry in ModelState)
                {
                    var key = entry.Key;
                    var errors = entry.Value.Errors;
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"HATA: {key} → {error.ErrorMessage}");
                    }
                }
                return View(model);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                model.UserId = user.Id;
                _context.SpendingLimits.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Limit başarıyla eklendi.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Hata oluştu: {ex.Message}");
                return View(model);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            var limit = await _context.SpendingLimits.FindAsync(id);
            if (limit == null) return NotFound();

            _context.SpendingLimits.Remove(limit);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Limit başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SuggestLimits()
        {
            var user = await _userManager.GetUserAsync(User);
            var threeMonthsAgo = DateTime.Now.AddMonths(-3);

            var transactions = await _context.Transactions
                .Where(t => t.UserId == user.Id && t.Type == TransactionType.Expense && t.TransactionDate >= threeMonthsAgo)
                .ToListAsync();

            if (!transactions.Any())
            {
                return Json(new { success = false, message = "Bütçe önermek için yeterli harcama veriniz (en az 3 aylık) bulunmamaktadır." });
            }

            var categorySpending = transactions
                .GroupBy(t => t.ExpenseCategory)
                .Select(g => new
                {
                    Category = g.Key.ToString(),
                    // Ortalama aylık harcamayı bulmak için toplamı 3'e bölüyoruz.
                    AverageMonthlyAmount = g.Sum(t => t.Amount) / 3
                })
                .Where(s => s.AverageMonthlyAmount > 50) // Önemsiz harcamaları filtrele
                .ToList();

            if (!categorySpending.Any())
            {
                return Json(new { success = false, message = "Öneri oluşturulabilecek belirgin harcama kategorileriniz bulunamadı." });
            }

            var summaryText = new StringBuilder("Kullanıcının son 3 aydaki ortalama aylık harcama verileri aşağıdadır:\n");
            foreach (var item in categorySpending)
            {
                summaryText.AppendLine($"- {item.Category}: {item.AverageMonthlyAmount:C}");
            }

            var instruction = @"Sen bir finansal danışmansın. Bu harcama verilerine dayanarak kullanıcı için mantıklı bir aylık bütçe oluştur. Her kategori için önerdiğin limiti, harcamaları hafifçe kısacak şekilde, en yakın 50'lik veya 100'lük basamağa yuvarlayarak ver. Cevabını SADECE JSON formatında, 'Category' ve 'Limit' anahtarlarını kullanarak bir dizi olarak döndür. Örneğin: [{""Category"": ""Gıda"", ""Limit"": 1500}, {""Category"": ""Ulaşım"", ""Limit"": 800}]. Başka hiçbir açıklama ekleme.";
            var prompt = summaryText.ToString() + "\n\n" + instruction;

            try
            {
                var aiResponse = await _openAiService.GetCompletionAsync(prompt);
                var cleanJson = aiResponse.Trim().Trim('`').Replace("json", "").Trim();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestedLimits = JsonSerializer.Deserialize<List<SuggestedLimit>>(cleanJson, options);

                var formattedLimits = suggestedLimits.Select(l => new
                {
                    Category = CommonHelper.ToHumanReadable(l.Category),
                    l.Limit
                }).ToList();

                return Json(new { success = true, limits = formattedLimits });
            }
            catch (JsonException)
            {
                return Json(new { success = false, message = "Yapay zekadan gelen yanıt anlaşılamadı. Lütfen tekrar deneyin." });
            }
            catch (Exception ex)
            {
                // Log exception (ex)
                return Json(new { success = false, message = "Yapay zeka ile iletişim kurarken bir hata oluştu. Lütfen tekrar deneyin." });
            }
        }

        private class SuggestedLimit
        {
            public string Category { get; set; }
            public decimal Limit { get; set; }
        }
    }
}
