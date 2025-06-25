using Microsoft.AspNetCore.Mvc;
using Monexia.Data;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Monexia.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Monexia.Services;
using System;

[Authorize]
public class AIExportController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IReportService _reportService;

    public AIExportController(AppDbContext context, UserManager<ApplicationUser> userManager, IReportService reportService)
    {
        _context = context;
        _userManager = userManager;
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<IActionResult> ExportToExcel()
    {
        var userId = _userManager.GetUserId(User);
        var reportDate = DateTime.Now;
        var transactions = await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var content = await _reportService.CreateExcelReportAsync(transactions);

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Monexia_Rapor_{reportDate:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportToPdf()
    {
        var userId = _userManager.GetUserId(User);
        var user = await _userManager.GetUserAsync(User);
        var reportDate = DateTime.Now;
        var transactions = await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var documentBytes = await _reportService.CreatePdfReportAsync(transactions, user);

        return File(documentBytes, "application/pdf", $"Monexia_Rapor_{reportDate:yyyyMMdd}.pdf");
    }
}