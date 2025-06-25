using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Monexia.Data;
using Monexia.Models;
using ClosedXML.Excel;
using QuestPDF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Monexia.Services;

public class ReportController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenAiService _openAiService;
    private readonly IMemoryCache _cache;

    public ReportController(AppDbContext context, UserManager<ApplicationUser> userManager, IOpenAiService openAiService, IMemoryCache cache)
    {
        _context = context;
        _userManager = userManager;
        _openAiService = openAiService;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge(); // veya kullanıcı girişe yönlendir
        }

        var transactions = await _context.Transactions
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var cacheKey = $"ExecutiveSummary_{user.Id}";

        if (!_cache.TryGetValue(cacheKey, out string executiveSummary))
        {
            // --- AI için Veri Toplama ---
            if (transactions.Any())
            {
                var lastMonth = DateTime.Now.AddMonths(-1);

                var totalIncome = transactions.Where(t => t.Type == TransactionType.Income && t.TransactionDate >= lastMonth).Sum(t => t.Amount);
                var totalExpense = transactions.Where(t => t.Type == TransactionType.Expense && t.TransactionDate >= lastMonth).Sum(t => t.Amount);

                var categoryExpenses = transactions
                    .Where(t => t.Type == TransactionType.Expense && t.TransactionDate >= lastMonth)
                    .GroupBy(t => t.ExpenseCategory.ToString())
                    .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Total)
                    .Take(5)
                    .ToList();

                var monthlyHistory = transactions
                    .Where(t => t.TransactionDate >= DateTime.Now.AddMonths(-6))
                    .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
                    .Select(g => new
                    {
                        Month = $"{g.Key.Year}-{g.Key.Month}",
                        Type = g.Key.Type.ToString(),
                        Total = g.Sum(t => t.Amount)
                    })
                    .ToList();

                // --- AI için Metin Oluşturma ---
                var dataSummary = $"Son 1 aylık finansal özet:\n" +
                                  $"- Toplam Gelir: {totalIncome:C}\n" +
                                  $"- Toplam Gider: {totalExpense:C}\n" +
                                  $"- Net Akış: {totalIncome - totalExpense:C}\n\n" +
                                  $"Son 1 aydaki en büyük 5 harcama kategorisi:\n";
                foreach (var item in categoryExpenses)
                {
                    dataSummary += $"- {item.Category}: {item.Total:C}\n";
                }

                dataSummary += "\nSon 6 aylık gelir-gider trendi:\n";
                foreach (var month in monthlyHistory.GroupBy(h => h.Month).OrderBy(g => g.Key))
                {
                    var income = month.FirstOrDefault(m => m.Type == "Income")?.Total ?? 0;
                    var expense = month.FirstOrDefault(m => m.Type == "Expense")?.Total ?? 0;
                    dataSummary += $"- {month.Key}: Gelir {income:C}, Gider {expense:C}\n";
                }

                // --- AI'ye Prompt Gönderme ---
                var prompt = "Sen bir kıdemli finansal analistsin. Aşağıdaki finansal verileri analiz ederek kullanıcıya özel bir 'Yönetici Özeti' hazırla. Özet, bir paragrafı geçmemeli. Kullanıcının mali durumunu övmeli, ardından dikkat etmesi gereken bir konuyu belirtmeli ve son olarak da genel bir tavsiye ile bitirmelisin. Analizini sadece verilen verilere dayanarak yap. İşte veriler:\n\n" + dataSummary;

                try
                {
                    executiveSummary = await _openAiService.GetCompletionAsync(prompt);

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(24));

                    _cache.Set(cacheKey, executiveSummary, cacheEntryOptions);
                }
                catch (Exception ex)
                {
                    // OpenAI'den yanıt alınamazsa veya bir hata oluşursa, genel bir mesaj göster.
                    // Bu hatayı loglamak daha iyi bir pratiktir.
                    executiveSummary = "Finansal özetiniz şu anda oluşturulamıyor. Lütfen daha sonra tekrar deneyin.";
                }
            }
            else
            {
                executiveSummary = "Henüz analiz edilecek bir finansal veriniz bulunmamaktadır. Lütfen işlem ekleyin.";
            }
        }

        ViewBag.ExecutiveSummary = executiveSummary;

        var monthlySummary = transactions
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM"),
                Category = g.Key.Type.ToString(),
                Total = g.Sum(x => x.Amount)
            })
            .ToList();

        ViewBag.MonthlyData = JsonConvert.SerializeObject(monthlySummary);

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf()
    {
        var user = await _userManager.GetUserAsync(User);
        var transactions = await _context.Transactions
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var stream = new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(QuestPDF.Helpers.Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text("Monexia - Aylık Finansal Rapor")
                    .SemiBold().FontSize(20).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);

                page.Content()
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80); // Tarih
                            columns.RelativeColumn();   // Kategori
                            columns.RelativeColumn();   // Tür
                            columns.ConstantColumn(80); // Tutar
                            columns.RelativeColumn();   // Açıklama
                        });

                        // Başlık
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Tarih").SemiBold();
                            header.Cell().Element(CellStyle).Text("Kategori").SemiBold();
                            header.Cell().Element(CellStyle).Text("Tür").SemiBold();
                            header.Cell().Element(CellStyle).Text("Tutar").SemiBold();
                            header.Cell().Element(CellStyle).Text("Açıklama").SemiBold();
                        });

                        // İçerik
                        foreach (var t in transactions)
                        {
                            table.Cell().Element(CellStyle).Text(t.TransactionDate.ToShortDateString());
                            table.Cell().Element(CellStyle).Text(t.Type.ToString());
                            table.Cell().Element(CellStyle).Text(t.Type);
                            table.Cell().Element(CellStyle).Text($"{t.Amount:C}");
                            table.Cell().Element(CellStyle).Text(t.Description ?? "-");
                        }

                        static IContainer CellStyle(IContainer container)
                        {
                            return container
                                .PaddingVertical(5)
                                .BorderBottom(1)
                                .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text($"Rapor tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}")
                    .FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
            });
        }).GeneratePdf(stream);

        stream.Position = 0;
        return File(stream.ToArray(), "application/pdf", "Monexia_Professional_Report.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel()
    {
        var user = await _userManager.GetUserAsync(User);
        var transactions = await _context.Transactions
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Rapor");

        worksheet.Cell(1, 1).Value = "Tarih";
        worksheet.Cell(1, 2).Value = "Kategori";
        worksheet.Cell(1, 3).Value = "Tür";
        worksheet.Cell(1, 4).Value = "Tutar";
        worksheet.Cell(1, 5).Value = "Açıklama";

        for (int i = 0; i < transactions.Count; i++)
        {
            var t = transactions[i];
            worksheet.Cell(i + 2, 1).Value = t.TransactionDate.ToShortDateString();
            worksheet.Cell(i + 2, 2).Value = t.Type.ToString();
            worksheet.Cell(i + 2, 3).Value = t.IncomeCategory.ToString() ?? t.ExpenseCategory.ToString();
            worksheet.Cell(i + 2, 4).Value = t.Amount;
            worksheet.Cell(i + 2, 5).Value = t.Description ?? "-";
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Monexia_Report.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> GetSummaryData(string period = "monthly")
    {
        var user = await _userManager.GetUserAsync(User);
        var transactions = await _context.Transactions
            .Where(t => t.UserId == user.Id)
            .ToListAsync();

        IEnumerable<dynamic> summary = null;
        if (period == "weekly")
        {
            summary = transactions
                .GroupBy(t => new { Year = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetYear(t.TransactionDate), Week = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(t.TransactionDate, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday), t.Type })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Week)
                .Select(g => new
                {
                    Period = $"{g.Key.Year}-W{g.Key.Week}",
                    Category = g.Key.Type.ToString(),
                    Total = g.Sum(x => x.Amount)
                });
        }
        else if (period == "yearly")
        {
            summary = transactions
                .GroupBy(t => new { t.TransactionDate.Year, t.Type })
                .OrderBy(g => g.Key.Year)
                .Select(g => new
                {
                    Period = g.Key.Year.ToString(),
                    Category = g.Key.Type.ToString(),
                    Total = g.Sum(x => x.Amount)
                });
        }
        else // monthly
        {
            summary = transactions
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Period = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM"),
                    Category = g.Key.Type.ToString(),
                    Total = g.Sum(x => x.Amount)
                });
        }
        return Json(summary);
    }

    [HttpGet]
    public async Task<IActionResult> GetCategoryData(string type = "expense", string period = "monthly")
    {
        var user = await _userManager.GetUserAsync(User);
        var transactions = await _context.Transactions
            .Where(t => t.UserId == user.Id)
            .ToListAsync();

        DateTime minDate = DateTime.MinValue;
        if (period == "weekly")
            minDate = DateTime.Now.AddDays(-7);
        else if (period == "monthly")
            minDate = DateTime.Now.AddMonths(-1);
        else if (period == "yearly")
            minDate = DateTime.Now.AddYears(-1);

        var filtered = transactions.Where(t => t.TransactionDate >= minDate && t.Type.ToString().ToLower() == type.ToLower());

        var categoryData = filtered
            .GroupBy(t => type.ToLower() == "income" ? t.IncomeCategory.ToString() : t.ExpenseCategory.ToString())
            .Select(g => new
            {
                Category = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        return Json(categoryData);
    }
}
