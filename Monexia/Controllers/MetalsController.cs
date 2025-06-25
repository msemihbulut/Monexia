using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Monexia.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace Monexia.Controllers
{
    public class MetalsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;

        public MetalsController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
        {
            _httpClient = httpClientFactory.CreateClient();
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            var client = _httpClient;
            var request = new HttpRequestMessage(HttpMethod.Get, "https://gold.g.apised.com/v1/latest?metals=XAU,XAG&base_currency=TRY&currencies=TRY,USD&weight_unit=gram");
            request.Headers.Add("x-api-key", "sk_689c99A0117B9BCcf113dF50E8A188B0703D410E4845104A");
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Veri alınamadı.";
                return View(new List<MetalViewModel>());
            }
            var content = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(content)["data"];
            var metalPrices = data["metal_prices"];
            var currencyRates = data["currency_rates"];
            decimal rateUsd = currencyRates["USD"]?.Value<decimal>() ?? 0;
            decimal rateTry = currencyRates["TRY"]?.Value<decimal>() ?? 1;
            var result = new List<MetalViewModel>();
            foreach (var symbol in new[] { "XAU", "XAG" })
            {
                var m = metalPrices[symbol];
                var model = new MetalViewModel
                {
                    Symbol = symbol,
                    Name = symbol == "XAU" ? "Altın" : "Gümüş",
                    PriceTry = m["price"]?.Value<decimal>() ?? 0,
                    PriceUsd = rateUsd > 0 ? (m["price"]?.Value<decimal>() ?? 0) * rateUsd : 0,
                    Price24K = m["price_24k"]?.Value<decimal>(),
                    Price22K = m["price_22k"]?.Value<decimal>(),
                    Price21K = m["price_21k"]?.Value<decimal>(),
                    Price20K = m["price_20k"]?.Value<decimal>(),
                    Price18K = m["price_18k"]?.Value<decimal>(),
                    Price16K = m["price_16k"]?.Value<decimal>(),
                    Price14K = m["price_14k"]?.Value<decimal>(),
                    Price10K = m["price_10k"]?.Value<decimal>(),
                    Change = m["change"]?.Value<decimal>(),
                    ChangePercentage = m["change_percentage"]?.Value<decimal>(),
                    Ask = m["ask"]?.Value<decimal>(),
                    Bid = m["bid"]?.Value<decimal>(),
                    Open = m["open"]?.Value<decimal>(),
                    High = m["high"]?.Value<decimal>(),
                    Low = m["low"]?.Value<decimal>(),
                    Prev = m["prev"]?.Value<decimal>()
                };
                result.Add(model);
            }
            ViewBag.CurrencyRates = new { TRY = rateTry, USD = rateUsd };
            ViewBag.LastUpdated = DateTimeOffset.FromUnixTimeMilliseconds((long)data["timestamp"]).LocalDateTime;
            return View(result);
        }

        public async Task<IActionResult> Detail(string symbol, string currency)
        {
            // Gün aralıkları
            var ranges = new Dictionary<string, int>
            {
                { "5 Gun", 5 },
                { "1 Ay", 30 },
                { "3 Ay", 90 },
                { "6 Ay", 180 },
                { "1 Yil", 365 }
            };
            var apiKey = "sk_689c99A0117B9BCcf113dF50E8A188B0703D410E4845104A";
            var metalName = symbol == "XAU" ? "Altın" : "Gümüş";
            var chartData = new Dictionary<string, List<(string, decimal)>>();
            foreach (var range in ranges)
            {
                var endDate = DateTime.UtcNow.Date;
                var startDate = endDate.AddDays(-range.Value + 1);
                var url = $"https://api.metals.dev/v1/timeseries?api_key={apiKey}&start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}&unit=gram&currency={currency.ToUpper()}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;
                var content = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(content);
                if (data["status"]?.ToString() != "success") continue;
                var rates = data["rates"];
                var points = new List<(string, decimal)>();
                foreach (var day in rates)
                {
                    var date = day.Path;
                    var price = day.First["metals"][symbol == "XAU" ? "gold" : "silver"]?.Value<decimal>() ?? 0;
                    points.Add((DateTime.Parse(date).ToString("dd MMM"), price));
                }
                // Tarihleri sırala
                points.Sort((a, b) => DateTime.ParseExact(a.Item1, "dd MMM", System.Globalization.CultureInfo.CurrentCulture).CompareTo(DateTime.ParseExact(b.Item1, "dd MMM", System.Globalization.CultureInfo.CurrentCulture)));
                chartData[range.Key] = points;
                await Task.Delay(200); // API limiti için
            }
            ViewBag.Symbol = symbol;
            ViewBag.MetalName = metalName;
            ViewBag.Currency = currency.ToUpper();
            ViewBag.ChartData = chartData;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Calculate([FromBody] CalculateRequest request)
        {
            if (request?.Gram <= 0)
            {
                return Json(new { success = false, message = "Geçersiz gram miktarı." });
            }

            var client = _httpClient;
            var apiRequest = new HttpRequestMessage(HttpMethod.Get, "https://gold.g.apised.com/v1/latest?metals=XAU,XAG&base_currency=TRY&currencies=TRY,USD&weight_unit=gram");
            apiRequest.Headers.Add("x-api-key", "sk_689c99A0117B9BCcf113dF50E8A188B0703D410E4845104A");
            var response = await client.SendAsync(apiRequest);
            if (!response.IsSuccessStatusCode)
            {
                return Json(new { success = false, message = "Veri alınamadı." });
            }
            var content = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(content)["data"];
            var metalPrices = data["metal_prices"];
            decimal goldTry = metalPrices["XAU"]["price"]?.Value<decimal>() ?? 0;
            decimal goldUsd = (metalPrices["XAU"]["price"]?.Value<decimal>() ?? 0) * (data["currency_rates"]["USD"]?.Value<decimal>() ?? 0);
            decimal silverTry = metalPrices["XAG"]["price"]?.Value<decimal>() ?? 0;
            decimal silverUsd = (metalPrices["XAG"]["price"]?.Value<decimal>() ?? 0) * (data["currency_rates"]["USD"]?.Value<decimal>() ?? 0);
            return Json(new
            {
                success = true,
                gold = new { tryVal = (goldTry * request.Gram), usdVal = (goldUsd * request.Gram) },
                silver = new { tryVal = (silverTry * request.Gram), usdVal = (silverUsd * request.Gram) }
            });
        }
    }
}