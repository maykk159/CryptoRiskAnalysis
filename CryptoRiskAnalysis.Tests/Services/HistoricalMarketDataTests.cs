using System.Net;
using System.Text.Json;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoRiskAnalysis.Tests.Services;

public sealed class HistoricalMarketDataTests : IDisposable
{
    [Theory]
    [InlineData(MarketDataSource.Binance)]
    [InlineData(MarketDataSource.CoinGecko)]
    public async Task WarmCache_BackfillsEmptyArchiveWithExactDailyVolumes(MarketDataSource source)
    {
        var points = Enumerable.Range(0, 3).Select(i => Row(Today.AddDays(-3 + i), 100 + i, 1000 + i * 250)).ToList();
        var payload = source == MarketDataSource.Binance
            ? JsonSerializer.Serialize(points.Select(p => new object[]
                { p.Timestamp, "100", "110", "90", p.Price, "10", p.Timestamp + 86_399_999L, p.Volume }))
            : JsonSerializer.Serialize(new
            {
                prices = points.Select(p => new[] { (decimal)p.Timestamp, p.Price }),
                total_volumes = points.Select(p => new[] { (decimal)p.Timestamp, p.Volume })
            });
        var calls = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) };
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        CryptoRiskAnalysis.API.Interfaces.ICryptoDataService warmProvider = source == MarketDataSource.Binance
            ? new BinanceSpotService(client, cache, NullLogger<BinanceSpotService>.Instance)
            : new CoinGeckoService(client, cache, NullLogger<CoinGeckoService>.Instance);
        await warmProvider.GetAllMarketDataAsync("bitcoin", 3, Token);
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var result = await CreateService(store, client, cache).GetAsync("bitcoin", source, 3, null, Token);
        Assert.Equal(points, result.Observations);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task BinanceAliases_ShareCacheButPersistBothAssetIds()
    {
        var row = Row(Today.AddDays(-1), 0.00000015m, 1500m);
        var calls = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new object[][]
                {
                    [row.Timestamp, "1e-7", "2e-7", "1e-7", "1.5e-7", "100", row.Timestamp + 86_399_999L, "1.5e3"]
                }))
            };
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var service = CreateService(store, client, cache);
        foreach (var assetId in new[] { "matic-network", "polygon-ecosystem-token" })
        {
            var result = await service.GetAsync(assetId, MarketDataSource.Binance, 1, null, Token);
            Assert.Equal(row, Assert.Single(result.Observations));
            Assert.Equal(row, Assert.Single(await store.ReadAsync(assetId, MarketDataSource.Binance, row.Date, row.Date, Token)));
        }
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bitcoin\r\nforged")]
    [InlineData("bitcoin\u2028forged")]
    [InlineData("bitcoin\u2029forged")]
    [InlineData("bitcoin\u001b[31m")]
    public async Task ControllerRejectsInvalidAssetBeforeAccessingService(string? assetId)
    {
        var controller = new CryptoRiskAnalysis.API.Controllers.MarketDataController(null!);
        var result = await controller.GetHistory(assetId!, cancellationToken: Token);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crypto-history-{Guid.NewGuid():N}.db");
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static DailyMarketObservation Row(DateOnly date, decimal price = 100m, decimal volume = 1000m) =>
        new(date, new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds(), price, volume);

    [Fact]
    public async Task Upsert_PersistsAcrossInstancesAndKeepsSourcesAndAssetsSeparate()
    {
        var date = Today.AddDays(-1);
        var exact = 0.1234567890123456789012345678m;
        using (var store = new SqliteHistoricalMarketDataStore(_path))
        {
            await store.SaveAsync("bitcoin", MarketDataSource.Binance, [Row(date)], Token);
            await store.SaveAsync("bitcoin", MarketDataSource.Binance, [Row(date, exact)], Token);
            await store.SaveAsync("bitcoin", MarketDataSource.CoinGecko, [Row(date, 200)], Token);
            await store.SaveAsync("ethereum", MarketDataSource.Binance, [Row(date, 300)], Token);
        }
        using var reopened = new SqliteHistoricalMarketDataStore(_path);
        var rows = await reopened.ReadAsync("bitcoin", MarketDataSource.Binance, date, date, Token);
        Assert.Equal(exact, Assert.Single(rows).Price);
        Assert.Equal(200m, Assert.Single(await reopened.ReadAsync("bitcoin", MarketDataSource.CoinGecko, date, date, Token)).Price);
        Assert.Equal(300m, Assert.Single(await reopened.ReadAsync("ethereum", MarketDataSource.Binance, date, date, Token)).Price);
    }

    [Fact]
    public async Task InvalidBatchAndCancellation_LeaveNoPartialWrites()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var date = Today.AddDays(-2);
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("bitcoin", MarketDataSource.Binance,
            [Row(date), Row(date.AddDays(1), -1)], Token));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync("bitcoin", MarketDataSource.Binance, [Row(date)], canceled.Token));
        Assert.Empty(await store.ReadAsync("bitcoin", MarketDataSource.Binance, date, date.AddDays(1), Token));
    }

    [Fact]
    public async Task ConcurrentOverlappingWrites_AreUniqueAndChronological()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var from = Today.AddDays(-7);
        var rows = Enumerable.Range(0, 7).Select(i => Row(from.AddDays(i))).Reverse().ToList();
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            store.SaveAsync("bitcoin", MarketDataSource.CoinGecko, rows, Token), Token)));
        var saved = await store.ReadAsync("bitcoin", MarketDataSource.CoinGecko, from, Today.AddDays(-1), Token);
        Assert.Equal(7, saved.Count);
        Assert.Equal(from, saved[0].Date);
        Assert.Equal(Today.AddDays(-1), saved[^1].Date);
    }

    [Fact]
    public async Task MissingMiddleDay_IsBackfilledThenServedWithoutNetwork()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var from = Today.AddDays(-7);
        await store.SaveAsync("bitcoin", MarketDataSource.CoinGecko,
            Enumerable.Range(0, 7).Where(i => i != 3).Select(i => Row(from.AddDays(i))).ToList(), Token);
        var calls = 0;
        using var handler = new StubHandler(request =>
        {
            calls++;
            // Oldest missing date is 4 days ago; provider requests one extra day.
            Assert.Contains("days=5", request.RequestUri!.Query);
            return CoinGeckoResponse(4);
        });
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var history = CreateService(store, client, cache);
        var result = await history.GetAsync("bitcoin", MarketDataSource.CoinGecko, 7, null, Token);
        Assert.Equal(7, result.Observations.Count);
        Assert.Equal("USD", result.QuoteCurrency);
        // Restart the reader and cache to prove the second request comes from SQLite.
        using var emptyCache = new MemoryCache(new MemoryCacheOptions());
        var again = await CreateService(store, client, emptyCache).GetAsync("bitcoin", MarketDataSource.CoinGecko, 7, null, Token);
        Assert.Equal(result.Observations, again.Observations);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ConcurrentColdReads_CoalesceAndPersistTheProviderWindow()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var calls = 0;
        using var handler = new StubHandler(_ => { Interlocked.Increment(ref calls); return CoinGeckoResponse(7); });
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var history = CreateService(store, client, cache);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            history.GetAsync("bitcoin", MarketDataSource.CoinGecko, 7, null, Token)));
        Assert.All(results, r => Assert.Equal(7, r.Observations.Count));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InvalidProviderResponse_DoesNotPolluteArchive()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        using var handler = new StubHandler(_ => CoinGeckoResponse(2));
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await Assert.ThrowsAsync<MarketDataProviderException>(() => CreateService(store, client, cache)
            .GetAsync("bitcoin", MarketDataSource.CoinGecko, 7, null, Token));
        Assert.Empty(await store.ReadAsync("bitcoin", MarketDataSource.CoinGecko, Today.AddDays(-7), Today.AddDays(-1), Token));
    }

    [Fact]
    public async Task OlderArchivedWindow_IsReadableButMissingAncientDataIsNotFabricated()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var end = Today.AddDays(-100);
        await store.SaveAsync("bitcoin", MarketDataSource.CoinGecko, [Row(end)], Token);
        using var handler = new StubHandler(_ => throw new InvalidOperationException("Network must not be used"));
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var history = CreateService(store, client, cache);
        Assert.Single((await history.GetAsync("bitcoin", MarketDataSource.CoinGecko, 1, end, Token)).Observations);
        await Assert.ThrowsAsync<HistoricalDataUnavailableException>(() => history.GetAsync("bitcoin", MarketDataSource.CoinGecko, 2, end, Token));
    }

    [Fact]
    public async Task BinanceSuccessfulFetch_PersistsDailyQuoteVolume()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        var date = Today.AddDays(-1);
        var timestamp = Row(date).Timestamp;
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new object[][]
            {
                [timestamp, "100", "110", "90", "105", "10", timestamp + 86_399_999L, "1050"]
            }))
        });
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var history = CreateService(store, client, cache);
        var result = await history.GetAsync("bitcoin", MarketDataSource.Binance, 1, null, Token);
        Assert.Equal("USDT", result.QuoteCurrency);
        Assert.Equal(1050m, Assert.Single(result.Observations).Volume);
    }

    private static HistoricalMarketDataService CreateService(SqliteHistoricalMarketDataStore store, HttpClient client, IMemoryCache cache)
    {
        var requestLock = new MarketDataRequestLock();
        return new HistoricalMarketDataService(store,
            new BinanceSpotService(client, cache, NullLogger<BinanceSpotService>.Instance, requestLock, store),
            new CoinGeckoService(client, cache, NullLogger<CoinGeckoService>.Instance, requestLock, store), requestLock, TimeProvider.System);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(91, null)]
    [InlineData(7, 999)]
    public async Task ControllerRejectsInvalidRangeAndSource(int days, int? source)
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException("No HTTP expected")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new CryptoRiskAnalysis.API.Controllers.MarketDataController(CreateService(store, client, cache));
        var response = await controller.GetHistory("bitcoin", days, (MarketDataSource?)source, cancellationToken: Token);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task ControllerServesSavedSourceAndRejectsIncompleteCurrentDay()
    {
        using var store = new SqliteHistoricalMarketDataStore(_path);
        await store.SaveAsync("bitcoin", MarketDataSource.Binance, [Row(Today.AddDays(-1))], Token);
        using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException("No HTTP expected")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new CryptoRiskAnalysis.API.Controllers.MarketDataController(CreateService(store, client, cache));
        var response = await controller.GetHistory("bitcoin", 1, cancellationToken: Token);
        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result);
        var envelope = Assert.IsType<CryptoRiskAnalysis.API.Wrappers.ApiResponse<HistoricalMarketData>>(ok.Value);
        Assert.True(envelope.Succeeded);
        Assert.Equal(MarketDataSource.Binance, envelope.Data!.Source);
        Assert.Equal("USDT", envelope.Data.QuoteCurrency);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(
            (await controller.GetHistory("bitcoin", 1, endDate: Today, cancellationToken: Token)).Result);
    }

    [Theory]
    [InlineData("today")]
    [InlineData("future")]
    [InlineData("underflow")]
    public async Task InvalidDateWindow_ThrowsTypedExceptionAndReturnsBadRequest(string scenario)
    {
        var today = new DateOnly(2026, 9, 10);
        var endDate = scenario switch
        {
            "today" => today,
            "future" => today.AddDays(1),
            _ => DateOnly.MinValue
        };
        var store = new Moq.Mock<CryptoRiskAnalysis.API.Interfaces.IHistoricalMarketDataStore>(Moq.MockBehavior.Strict);
        using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException("No HTTP expected")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var requestLock = new MarketDataRequestLock();
        var clock = new Moq.Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
        var history = new HistoricalMarketDataService(store.Object,
            new BinanceSpotService(client, cache, NullLogger<BinanceSpotService>.Instance),
            new CoinGeckoService(client, cache, NullLogger<CoinGeckoService>.Instance), requestLock, clock.Object);

        await Assert.ThrowsAsync<InvalidHistoricalDateRangeException>(() =>
            history.GetAsync("bitcoin", MarketDataSource.Binance, 7, endDate, Token));
        var controller = new CryptoRiskAnalysis.API.Controllers.MarketDataController(history);
        var response = await controller.GetHistory("bitcoin", 7, endDate: endDate, cancellationToken: Token);
        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(response.Result);
        var envelope = Assert.IsType<CryptoRiskAnalysis.API.Wrappers.ApiResponse<HistoricalMarketData>>(badRequest.Value);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.False(envelope.Succeeded);
        Assert.Equal("The window must contain valid completed UTC dates.", envelope.Message);
        store.VerifyNoOtherCalls();
    }

    private static HttpResponseMessage CoinGeckoResponse(int days)
    {
        var points = Enumerable.Range(0, days).Select(i => Row(Today.AddDays(-days + i))).ToList();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                prices = points.Select(p => new[] { (decimal)p.Timestamp, p.Price }),
                total_volumes = points.Select(p => new[] { (decimal)p.Timestamp, p.Volume })
            }))
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_path + suffix);
    }
}
