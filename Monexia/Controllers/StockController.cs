using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Monexia.Models;
using Newtonsoft.Json;

public class StockController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly string _apiKey = "3d4397ec0fed5af9f8efd4fef036774d";

    public StockController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        _httpClient = httpClientFactory.CreateClient();
        _memoryCache = memoryCache;
    }

    public async Task<IActionResult> Index()
    {
        const string cacheKey = "marketstackStockData";
        const string updateKey = "marketstackStockUpdate";

        if (_memoryCache.TryGetValue(cacheKey, out List<StockViewModel> cachedData) &&
            _memoryCache.TryGetValue(updateKey, out DateTime cachedTime))
        {
            ViewBag.LastUpdated = cachedTime;
            return View(cachedData);
        }

        var symbols = new Dictionary<string, string>
        {
            { "XU100.IS", "BIST 100" },
            { "THYAO.IS", "Türk Hava Yolları" },
            { "KRDMD.IS", "Kardemir D" },
            { "ASELS.IS", "Aselsan" },
            { "GARAN.IS", "Garanti BBVA" }
        };

        var symbolString = string.Join(",", symbols.Keys);
        var url = $"https://api.marketstack.com/v1/eod?access_key={_apiKey}&symbols={symbolString}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            ViewBag.Error = "API'den veri alınamadı.";
            return View(new List<StockViewModel>());
        }

        var content = await response.Content.ReadAsStringAsync();
        var data = JsonConvert.DeserializeObject<MarketStackResponse>(content);

        var result = data.data
            .GroupBy(d => d.symbol)
            .Select(g =>
            {
                var symbol = g.Key;
                var name = symbols.ContainsKey(symbol) ? symbols[symbol] : symbol;
                return new StockViewModel
                {
                    Symbol = symbol,
                    Name = name,
                    Price = g.First().close
                };
            }).ToList();

        var now = DateTime.Now;
        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        _memoryCache.Set(updateKey, now, TimeSpan.FromMinutes(30));
        ViewBag.LastUpdated = now;

        return View(result);
    }

    public async Task<IActionResult> Detail(string symbol)
    {
        var ranges = new Dictionary<string, int>
    {
        { "5 Gun", 5 },
        { "1 Ay", 22 },
        { "3 Ay", 66 },
        { "6 Ay", 132 },
        { "1 Yil", 264 }
    };

        var model = new StockChartViewModel { Symbol = symbol };

        foreach (var range in ranges)
        {
            var url = $"https://api.marketstack.com/v1/eod?access_key={_apiKey}&symbols={symbol}&limit={range.Value}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) continue;

            var content = await response.Content.ReadAsStringAsync();
            var parsed = JsonConvert.DeserializeObject<MarketStackResponse>(content);

            var dateList = parsed.data
                .Select(x => DateTime.Parse(x.date).ToString("dd MMM"))
                .Reverse()
                .ToList();

            var priceList = parsed.data
                .Select(x => x.close)
                .Reverse()
                .ToList();

            model.DateSets[range.Key] = dateList;
            model.PriceSets[range.Key] = priceList;

            await Task.Delay(300); // ücretsiz API yavaşlatmak için isteğe bağlı
        }

        return View(model);
    }



    public class MarketStackResponse
    {
        public List<MarketStackData> data { get; set; }
    }

    public class MarketStackData
    {
        public string symbol { get; set; }
        public string date { get; set; }
        public decimal close { get; set; }
    }
}
