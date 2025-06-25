using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Monexia.Data;
using Monexia.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public class GoalController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GoalController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var goals = await _context.Goals.Where(g => g.UserId == user.Id).ToListAsync();
        return View(goals);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.IncomeCategories = Enum.GetNames(typeof(IncomeCategory));
        ViewBag.ExpenseCategories = Enum.GetNames(typeof(ExpenseCategory));
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Goal goal, string customCategory)
    {
        var user = await _userManager.GetUserAsync(User);
        goal.UserId = user.Id;
        goal.CreatedAt = DateTime.Now;
        goal.UpdatedAt = DateTime.Now;
        goal.Status = "Devam";
        if (goal.Category == "Diğer" && !string.IsNullOrWhiteSpace(customCategory))
            goal.Category = customCategory;
        _context.Goals.Add(goal);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> AddProgress(int id)
    {
        var goal = await _context.Goals.FindAsync(id);
        if (goal == null) return NotFound();
        return View(goal);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProgress(int id, decimal amount, string description)
    {
        var goal = await _context.Goals.FindAsync(id);
        if (goal == null) return NotFound();
        goal.CurrentAmount += amount;
        goal.UpdatedAt = DateTime.Now;
        if (goal.CurrentAmount >= goal.TargetAmount)
            goal.Status = "Bitmiş";
        // Transaction kaydı
        var user = await _userManager.GetUserAsync(User);
        var transaction = new Transaction
        {
            UserId = user.Id,
            Amount = amount,
            IncomeCategory = IncomeCategory.Diğer,
            TransactionDate = DateTime.Now,
            Description = $"Hedef güncelleme: {goal.Name} - {description}",
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var goal = await _context.Goals.FindAsync(id);
        if (goal == null) return NotFound();
        ViewBag.IncomeCategories = Enum.GetNames(typeof(IncomeCategory));
        ViewBag.ExpenseCategories = Enum.GetNames(typeof(ExpenseCategory));
        return View(goal);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Goal goal, string customCategory)
    {
        var dbGoal = await _context.Goals.FindAsync(goal.Id);
        if (dbGoal == null) return NotFound();
        dbGoal.Name = goal.Name;
        dbGoal.Description = goal.Description;
        dbGoal.TargetAmount = goal.TargetAmount;
        dbGoal.StartDate = goal.StartDate;
        dbGoal.EndDate = goal.EndDate;
        dbGoal.Category = goal.Category == "Diğer" && !string.IsNullOrWhiteSpace(customCategory) ? customCategory : goal.Category;
        dbGoal.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var goal = await _context.Goals.FindAsync(id);
        if (goal == null) return NotFound();
        _context.Goals.Remove(goal);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}