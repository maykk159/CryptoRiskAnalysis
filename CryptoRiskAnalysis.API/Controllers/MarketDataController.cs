using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.Services;
using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CryptoRiskAnalysis.API.Controllers;

[ApiController]
[Route("api/marketdata")]
[EnableRateLimiting("RiskAnalysis")]
public sealed class MarketDataController(HistoricalMarketDataService history) : ControllerBase
{
    [HttpGet("{assetId}/history")]
    public async Task<ActionResult<ApiResponse<HistoricalMarketData>>> GetHistory(string assetId,
        [FromQuery] int days = 30, [FromQuery] MarketDataSource? source = null,
        [FromQuery] DateOnly? endDate = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetId) || assetId.Length > 200 ||
            assetId.Any(char.IsControl) || assetId.Contains('\u2028') || assetId.Contains('\u2029'))
            return BadRequest(new ApiResponse<HistoricalMarketData>("Invalid asset ID."));

        var selectedSource = source ?? (BinanceSymbolMapper.IsAvailableOnBinance(assetId)
            ? MarketDataSource.Binance : MarketDataSource.CoinGecko);
        if (days is < 1 or > 90 || !Enum.IsDefined(selectedSource))
            return BadRequest(new ApiResponse<HistoricalMarketData>("Invalid asset, source, or days (1–90)."));
        if (selectedSource == MarketDataSource.Binance && !BinanceSymbolMapper.IsAvailableOnBinance(assetId))
            return BadRequest(new ApiResponse<HistoricalMarketData>("This asset is not mapped to Binance; select source=CoinGecko."));
        try
        {
            return Ok(new ApiResponse<HistoricalMarketData>(await history.GetAsync(assetId, selectedSource, days, endDate, cancellationToken)));
        }
        catch (InvalidHistoricalDateRangeException ex)
        {
            return BadRequest(new ApiResponse<HistoricalMarketData>(ex.Message));
        }
    }
}
