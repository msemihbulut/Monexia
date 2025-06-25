using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace Monexia.Controllers
{
    public class DepositCalculatorController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Index(decimal amount, decimal interest, int term)
        {
            // Aylık faiz oranı
            var monthlyRate = interest / 100m / 12m;
            var n = term;
            var principal = amount;
            var total = principal;
            var plan = new List<object>();
            for (int i = 1; i <= n; i++)
            {
                var interestEarned = total * monthlyRate;
                total += interestEarned;
                plan.Add(new
                {
                    Month = i,
                    InterestEarned = Math.Round(interestEarned, 2),
                    Total = Math.Round(total, 2)
                });
            }
            ViewBag.Total = Math.Round(total, 2);
            ViewBag.Earned = Math.Round(total - principal, 2);
            ViewBag.Plan = plan;
            ViewBag.Amount = amount;
            ViewBag.Interest = interest;
            ViewBag.Term = term;
            return View();
        }
    }
}