using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace Monexia.Controllers
{
    public class LoanCalculatorController : Controller
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
            // Anüite formülü ile aylık taksit
            var monthlyPayment = principal * (monthlyRate * (decimal)Math.Pow(1 + (double)monthlyRate, n)) / ((decimal)Math.Pow(1 + (double)monthlyRate, n) - 1);
            var totalPayment = monthlyPayment * n;
            // Ödeme planı
            var plan = new List<object>();
            decimal remaining = principal;
            for (int i = 1; i <= n; i++)
            {
                var interestPayment = remaining * monthlyRate;
                var principalPayment = monthlyPayment - interestPayment;
                remaining -= principalPayment;
                plan.Add(new
                {
                    Month = i,
                    MonthlyPayment = Math.Round(monthlyPayment, 2),
                    Principal = Math.Round(principalPayment, 2),
                    Interest = Math.Round(interestPayment, 2),
                    Remaining = Math.Round(remaining > 0 ? remaining : 0, 2)
                });
            }
            ViewBag.MonthlyPayment = Math.Round(monthlyPayment, 2);
            ViewBag.TotalPayment = Math.Round(totalPayment, 2);
            ViewBag.Plan = plan;
            ViewBag.Amount = amount;
            ViewBag.Interest = interest;
            ViewBag.Term = term;
            return View();
        }
    }
}