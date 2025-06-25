using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monexia.Data;
using Monexia.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace Monexia.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            // Monthly Registrations Chart Data
            var monthlyRegistrationsData = await _userManager.Users
                .GroupBy(u => new { u.RegistrationDate.Year, u.RegistrationDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            var registrationLabels = monthlyRegistrationsData.Select(x => $"{x.Year}-{x.Month}").ToList();
            var registrationData = monthlyRegistrationsData.Select(x => x.Count).ToList();

            // Transaction Type Distribution Chart Data
            var transactionDistribution = await _context.Transactions
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key.ToString(), Total = g.Sum(t => t.Amount) })
                .ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalTransactions = await _context.Transactions.CountAsync(),
                TotalTransactionAmount = transactionDistribution.Sum(x => x.Total),
                AllUsers = await _userManager.Users.OrderBy(u => u.RegistrationDate).ToListAsync(),

                MonthlyRegistrationsJson = JsonSerializer.Serialize(new { labels = registrationLabels, data = registrationData }),
                TransactionTypeDistributionJson = JsonSerializer.Serialize(new
                {
                    labels = transactionDistribution.Select(x => x.Type == "Income" ? "Gelir" : "Gider").ToList(),
                    data = transactionDistribution.Select(x => x.Total).ToList()
                })
            };

            return View(viewModel);
        }

        public async Task<IActionResult> UserDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var transactions = await _context.Transactions
                .Where(t => t.UserId == id)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            var totalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var totalExpense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

            var viewModel = new UserDetailsViewModel
            {
                User = user,
                Transactions = transactions,
                TotalIncome = totalIncome,
                TotalExpense = totalExpense,
                NetBalance = totalIncome - totalExpense
            };

            return View(viewModel);
        }
    }
}