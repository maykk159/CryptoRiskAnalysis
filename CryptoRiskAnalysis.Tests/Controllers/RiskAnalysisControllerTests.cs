using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CryptoRiskAnalysis.API.Controllers;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Models;
using CryptoRiskAnalysis.API.DTOs;
using CryptoRiskAnalysis.API.Exceptions;
using CryptoRiskAnalysis.API.Wrappers;

namespace CryptoRiskAnalysis.Tests.Controllers
{
    public class RiskAnalysisControllerTests
    {
        private readonly Mock<ICryptoDataService> _mockCryptoService;
        private readonly Mock<IRiskEngine> _mockRiskEngine;
        private readonly Mock<ILogger<RiskAnalysisController>> _mockLogger;
        private readonly RiskAnalysisController _controller;

        public RiskAnalysisControllerTests()
        {
            _mockCryptoService = new Mock<ICryptoDataService>();
            _mockRiskEngine = new Mock<IRiskEngine>();
            _mockLogger = new Mock<ILogger<RiskAnalysisController>>();
            _controller = new RiskAnalysisController(
                _mockCryptoService.Object,
                _mockRiskEngine.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetRiskAnalysis_ValidAsset_ReturnsOkResult()
        {
            // Arrange
            var assetId = "bitcoin";
            var priceHistory = Enumerable.Range(0, 30)
                .Select(i => new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i).ToUnixTimeMilliseconds(),
                    Price = 100m + i
                })
                .ToList();

            var riskResult = new RiskScoreResult
            {
                VolatilityScore = 50m,
                TrendScore = 45m,
                VolumeScore = 55m,
                CompositeRiskScore = 50m,
                DownsideRisk = 10m,
                MaxDrawdown = 5m,
                SharpeRatio = 1.5m,
                ValueAtRisk95 = 3m,
                AnnualizedVolatility = 20m,
                PriceHistory = priceHistory
            };

            _mockCryptoService.Setup(s => s.GetAllMarketDataAsync(assetId, 30, It.IsAny<CancellationToken>()))
                .ReturnsAsync((priceHistory, 1000m, 900m));
            _mockRiskEngine.Setup(e => e.CalculateRisk(It.IsAny<List<PriceData>>(), 1000m, 900m))
                .Returns(riskResult);

            // Act
            var result = await _controller.GetRiskAnalysis(
                assetId, 30, TestContext.Current.CancellationToken);

            // Assert - Inspect result.Result because return type is ActionResult<T>
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<RiskAnalysisResponseDto>>(okResult.Value);
            Assert.True(response.Succeeded);
            Assert.NotNull(response.Data);
            Assert.Equal(50m, response.Data.CompositeRiskScore);
        }

        [Fact]
        public async Task GetRiskAnalysis_InvalidDays_ReturnsBadRequest()
        {
            // Arrange
            var assetId = "bitcoin";

            // Act - Pass invalid days (e.g., 0)
            var result = await _controller.GetRiskAnalysis(
                assetId, 0, TestContext.Current.CancellationToken);

            // Assert - Should return 400 Bad Request
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<RiskAnalysisResponseDto>>(badRequestResult.Value);
            Assert.False(response.Succeeded);
            Assert.Contains("Geçersiz gün", response.Message);
        }

        [Fact]
        public async Task GetRiskAnalysis_NoDataFound_ReturnsNotFound()
        {
            // Arrange
            var assetId = "invalid_coin";
            _mockCryptoService.Setup(s => s.GetAllMarketDataAsync(assetId, 30, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<PriceData>(), 0m, 0m));

            // Act
            var result = await _controller.GetRiskAnalysis(
                assetId, 30, TestContext.Current.CancellationToken);

            // Assert - Inspect result.Result because return type is ActionResult<T>
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<RiskAnalysisResponseDto>>(notFoundResult.Value);
            Assert.False(response.Succeeded);
            Assert.Contains("No data found", response.Message);
        }

        [Fact]
        public async Task GetRiskAnalysis_InsufficientData_RejectsReport()
        {
            var assetId = "bitcoin";
            var priceHistory = new List<PriceData>
            {
                new() { Timestamp = 1000, Price = 100m },
                new() { Timestamp = 2000, Price = 105m }
            };
            _mockCryptoService
                .Setup(s => s.GetAllMarketDataAsync(assetId, 30, It.IsAny<CancellationToken>()))
                .ReturnsAsync((priceHistory, 1000m, 900m));

            await Assert.ThrowsAsync<MarketDataProviderException>(() =>
                _controller.GetRiskAnalysis(assetId, 30, TestContext.Current.CancellationToken));

            _mockRiskEngine.Verify(
                engine => engine.CalculateRisk(It.IsAny<List<PriceData>>(), It.IsAny<decimal>(), It.IsAny<decimal>()),
                Times.Never);
        }

        [Fact]
        public async Task GetRiskAnalysis_ServiceThrowsException_ThrowsExceptionToMiddleware()
        {
            // Arrange
            var assetId = "bitcoin";
            _mockCryptoService.Setup(s => s.GetAllMarketDataAsync(assetId, 30, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API failure"));

            // Act & Assert - Controller delegates exception to global ExceptionHandlingMiddleware at runtime
            await Assert.ThrowsAsync<Exception>(async () =>
                await _controller.GetRiskAnalysis(
                    assetId, 30, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRiskAnalysis_UsesRequestedWindowForAllMetrics()
        {
            var assetId = "bitcoin";
            var priceHistory = new List<PriceData>();
            for (int i = 0; i < 7; i++)
            {
                priceHistory.Add(new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-7 + i).ToUnixTimeMilliseconds(),
                    Price = 100m + i
                });
            }

            var riskResult = new RiskScoreResult
            {
                CompositeRiskScore = 50m,
                PriceHistory = priceHistory
            };

            _mockCryptoService.Setup(s => s.GetAllMarketDataAsync(assetId, 7, It.IsAny<CancellationToken>()))
                .ReturnsAsync((priceHistory, 1000m, 900m));
            _mockRiskEngine.Setup(e => e.CalculateRisk(priceHistory, 1000m, 900m))
                .Returns(riskResult);

            // Act - Request 7 days
            var result = await _controller.GetRiskAnalysis(
                assetId, 7, TestContext.Current.CancellationToken);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<RiskAnalysisResponseDto>>(okResult.Value);
            Assert.NotNull(response.Data);
            Assert.Equal(7, response.Data!.PriceHistory.Count);
            _mockCryptoService.Verify(
                service => service.GetAllMarketDataAsync(assetId, 7, It.IsAny<CancellationToken>()),
                Times.Once);
            _mockRiskEngine.Verify(engine => engine.CalculateRisk(priceHistory, 1000m, 900m), Times.Once);
        }

        [Fact]
        public async Task GetRiskAnalysis_PropagatesRequestCancellationToken()
        {
            var priceHistory = Enumerable.Range(0, 7)
                .Select(i => new PriceData
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-7 + i).ToUnixTimeMilliseconds(),
                    Price = 100m + i
                })
                .ToList();
            using var cancellationSource = new CancellationTokenSource();
            var cancellationToken = cancellationSource.Token;
            _mockCryptoService
                .Setup(service => service.GetAllMarketDataAsync("bitcoin", 7, cancellationToken))
                .ReturnsAsync((priceHistory, 1000m, 900m));
            _mockRiskEngine
                .Setup(engine => engine.CalculateRisk(priceHistory, 1000m, 900m))
                .Returns(new RiskScoreResult { PriceHistory = priceHistory });

            await _controller.GetRiskAnalysis("bitcoin", 7, cancellationToken);

            _mockCryptoService.Verify(
                service => service.GetAllMarketDataAsync("bitcoin", 7, cancellationToken),
                Times.Once);
        }
    }
}
