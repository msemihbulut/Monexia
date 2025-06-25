using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Monexia.Models;
using Newtonsoft.Json;
using System.Globalization;

public class CryptoController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;

    public CryptoController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClient = httpClientFactory.CreateClient();
        _memoryCache = cache;
    }

    public async Task<IActionResult> Index()
    {
        const string cacheKey = "cryptoData";
        const string updateKey = "cryptoUpdateTime";

        if (_memoryCache.TryGetValue(cacheKey, out List<CryptoViewModel> cachedData) &&
            _memoryCache.TryGetValue(updateKey, out DateTime cachedTime))
        {
            ViewBag.LastUpdated = cachedTime;
            return View(cachedData);
        }

        var ids = "bitcoin,ethereum,dogecoin,solana,cardano,ripple,polkadot,tron,chainlink,litecoin";
        var url = $"https://api.coingecko.com/api/v3/simple/price?ids={ids}&vs_currencies=try,usd";

        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        var parsed = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, decimal>>>(content);

        var result = parsed.Select(x => new CryptoViewModel
        {
            Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Key),
            PriceTry = x.Value.GetValueOrDefault("try"),
            PriceUsd = x.Value.GetValueOrDefault("usd")
        }).ToList();

        var updateTime = DateTime.Now;
        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        _memoryCache.Set(updateKey, updateTime, TimeSpan.FromMinutes(30));

        ViewBag.LastUpdated = updateTime;
        return View(result);
    }

    public async Task<IActionResult> Detail(string symbol, string currency)
    {
        var model = new CryptoChartViewModel { Symbol = symbol };

        var ranges = new Dictionary<string, int>
    {
        { "5 Gun", 5 },
        { "1 Ay", 30 },
        { "3 Ay", 90 },
        { "6 Ay", 180 },
        { "1 Yil", 365 }
    };

        var end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var coinIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Bitcoin", "bitcoin" },
        { "Ethereum", "ethereum" },
        { "Solana", "solana" },
        { "Ripple", "ripple" },
        { "Dogecoin", "dogecoin" },
        { "Cardano", "cardano" },
        { "Polkadot", "polkadot" },
        { "Tron", "tron" },
        { "Chainlink", "chainlink" },
        { "Litecoin", "litecoin" }
    };

        if (!coinIdMap.TryGetValue(symbol, out var coinId))
        {
            return Content("Coin ID bulunamadı.");
        }

        foreach (var range in ranges)
        {
            var start = DateTimeOffset.UtcNow.AddDays(-range.Value).ToUnixTimeSeconds();
            var url = $"https://api.coingecko.com/api/v3/coins/{coinId}/market_chart/range?vs_currency={currency}&from={start}&to={end}&x_cg_demo_api_key=CG-qA4ww41ZD6pAxuSPm6WuFETW";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(content))
                continue;

            var result = JsonConvert.DeserializeObject<CryptoMarketChartResponse>(content);
            if (result?.prices == null)
                continue;

            // Her gün için sadece en son fiyatı al
            var grouped = result.prices
                .GroupBy(p =>
                {
                    var date = DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]).DateTime;
                    return date.Date;
                })
                .Select(g =>
                {
                    var last = g.Last();
                    return new
                    {
                        Date = DateTimeOffset.FromUnixTimeMilliseconds((long)last[0]).DateTime,
                        Price = (decimal)last[1]
                    };
                })
                .OrderBy(x => x.Date)
                .ToList();

            var prices = grouped.Select(x => Math.Round(x.Price, 2)).ToList();
            var dates = grouped.Select(x => x.Date.ToString("dd MMM")).ToList();

            model.DateSets[range.Key] = dates;
            model.PriceSets[range.Key] = prices;
            model.Currency = currency;

            await Task.Delay(300);
        }

        return View(model);
    }

    public class CryptoMarketChartResponse
    {
        public List<List<decimal>> prices { get; set; }
    }

}
