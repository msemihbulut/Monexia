using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Monexia.Data;
using Monexia.Helpers;
using Monexia.Models;
using Monexia.Services;
using Newtonsoft.Json;

namespace Monexia.Controllers
{
    [Authorize]
    public class TransactionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOpenAiService _openAiService;

        public TransactionController(AppDbContext context, UserManager<ApplicationUser> userManager, IOpenAiService openAiService)
        {
            _context = context;
            _userManager = userManager;
            _openAiService = openAiService;
        }

        public async Task<IActionResult> Index(string categoryFilter, string searchTerm, string sortOrder, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 20)
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.Transactions.Where(t => t.UserId == user.Id);

            if (!string.IsNullOrEmpty(categoryFilter) &&
                Enum.TryParse<TransactionType>(categoryFilter, out var categoryEnum))
            {
                query = query.Where(t => t.Type == categoryEnum);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(t =>
                    t.IncomeCategory.ToString().Contains(searchTerm) ||
                    t.ExpenseCategory.ToString().Contains(searchTerm) ||
                    t.Description.Contains(searchTerm));
            }

            if (startDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate <= endDate.Value);
            }

            query = sortOrder == "asc"
                ? query.OrderBy(t => t.TransactionDate)
                : query.OrderByDescending(t => t.TransactionDate);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categorySummary = await query
                .GroupBy(t => t.Type)
                .Select(g => new
                {
                    Category = g.Key.ToString(),
                    Total = g.Sum(x => x.Amount)
                })
                .ToListAsync();

            // ViewBag'lerle UI'yi senkron tut
            ViewBag.CurrentCategory = categoryFilter;
            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.CategoryChart = JsonConvert.SerializeObject(categorySummary);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;

            return View(transactions);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.IncomeCategories = Enum.GetValues(typeof(IncomeCategory))
                .Cast<IncomeCategory>()
                .Select(c => new SelectListItem
                {
                    Value = c.ToString(),
                    Text = CommonHelper.ToHumanReadable(c.ToString())
                })
                .ToList();

            ViewBag.ExpenseCategories = Enum.GetValues(typeof(ExpenseCategory))
                .Cast<ExpenseCategory>()
                .Select(c => new SelectListItem
                {
                    Value = c.ToString(),
                    Text = CommonHelper.ToHumanReadable(c.ToString())
                })
                .ToList();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Transaction transaction)
        {
            if (!ModelState.IsValid)
                return View(transaction);

            var user = await _userManager.GetUserAsync(User);
            transaction.UserId = user.Id;
            transaction.TransactionDate = transaction.TransactionDate == default ? DateTime.Now : transaction.TransactionDate;

            if (transaction.Type == TransactionType.Income)
            {
                transaction.ExpenseCategory = null;
            }
            else if (transaction.Type == TransactionType.Expense)
            {
                transaction.IncomeCategory = null;
            }

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = "İşlem başarıyla eklendi.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SuggestCategory([FromBody] SuggestCategoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Description))
            {
                return BadRequest("Açıklama boş olamaz.");
            }

            try
            {
                var prompt = $"Kullanıcının girdiği şu işlem açıklamasına göre en uygun harcama kategorisini tek bir kelime olarak öner: '{request.Description}'. Olası kategoriler şunlardır: {string.Join(", ", Enum.GetNames(typeof(ExpenseCategory)))}. Sadece kategori adını döndür.";
                var suggestedCategoryName = await _openAiService.GetCompletionAsync(prompt);

                return Ok(new { category = suggestedCategoryName.Trim() });
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(500, "Kategori önerisi alınırken bir hata oluştu.");
            }
        }
    }

    public class SuggestCategoryRequest
    {
        public string Description { get; set; }
    }
}
