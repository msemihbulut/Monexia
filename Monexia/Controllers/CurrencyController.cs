using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Monexia.Models;
using Newtonsoft.Json;

public class CurrencyController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;

    public CurrencyController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        _httpClient = httpClientFactory.CreateClient();
        _memoryCache = memoryCache;
    }

    public async Task<IActionResult> Index()
    {
        const string cacheKey = "currencyData";
        const string updateKey = "currencyUpdateTime";

        // Eğer cache'te veri varsa onu döndür
        if (_memoryCache.TryGetValue(cacheKey, out List<CurrencyViewModel> cachedData) &&
            _memoryCache.TryGetValue(updateKey, out DateTime cachedTime))
        {
            ViewBag.LastUpdated = cachedTime;
            return View(cachedData);
        }

        // Cache yoksa API'den veri çek
        var url = "https://api.apilayer.com/exchangerates_data/latest?symbols=USD,EUR,AZN,GBP,SAR&base=TRY";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", "CgTyYSSwiIke347Eqh4JURzBsP3lilJI");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(content))
        {
            ViewBag.Error = "Kur verisi alınamadı.";
            return View(new List<CurrencyViewModel>());
        }

        var result = JsonConvert.DeserializeObject<ExchangeRateResponse>(content);

        var currencyList = result?.rates?
            .Where(x => x.Value != 0)
            .Select(x => new CurrencyViewModel
            {
                Code = x.Key,
                Rate = Math.Round(1 / x.Value, 4) // TL karşılığı
            })
            .OrderBy(x => x.Code)
            .ToList();

        if (currencyList == null || !currencyList.Any())
        {
            ViewBag.Error = "Kur verisi alınamadı veya boş.";
            return View(new List<CurrencyViewModel>());
        }

        // Güncellenme zamanını al
        var updateTime = DateTime.Now;

        // Cache'e kaydet (30 dakika)
        _memoryCache.Set(cacheKey, currencyList, TimeSpan.FromMinutes(30));
        _memoryCache.Set(updateKey, updateTime, TimeSpan.FromMinutes(30));

        ViewBag.LastUpdated = updateTime;
        return View(currencyList);
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

        var model = new CurrencyChartViewModel { Symbol = symbol };

        foreach (var range in ranges)
        {
            var url = $"https://api.apilayer.com/exchangerates_data/timeseries?start_date={DateTime.Today.AddDays(-range.Value):yyyy-MM-dd}&end_date={DateTime.Today:yyyy-MM-dd}&symbols={symbol}&base=TRY";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", "CgTyYSSwiIke347Eqh4JURzBsP3lilJI");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(content))
                continue;

            var result = JsonConvert.DeserializeObject<CurrencyTimeSeriesResponse>(content);

            if (result?.rates == null || !result.rates.Any())
                continue;

            // Sadece geçerli ve 0'dan farklı veri varsa
            var validRates = result.rates
                .Where(x => x.Value.ContainsKey(symbol) && x.Value[symbol] != 0)
                .OrderBy(x => x.Key)
                .ToList();

            var dates = validRates.Select(x => x.Key).ToList();
            var values = validRates.Select(x => Math.Round(1 / x.Value[symbol], 4)).ToList(); // 🔁 TL karşılığı

            model.DateSets.Add(range.Key, dates);
            model.PriceSets.Add(range.Key, values);

            await Task.Delay(300); // Rate limit'e yakalanmamak için
        }

        return View(model);
    }

}
public class ExchangeRateResponse
{
    public Dictionary<string, decimal> rates { get; set; }
}
