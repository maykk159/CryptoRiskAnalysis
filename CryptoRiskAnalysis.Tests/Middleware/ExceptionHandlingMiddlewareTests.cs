using System.Text.Json;
using CryptoRiskAnalysis.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CryptoRiskAnalysis.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData("circuit", 503, "Market data provider is temporarily unavailable.")]
    [InlineData("polly-timeout", 504, "Market data request timed out.")]
    [InlineData("timeout", 504, "Market data request timed out.")]
    public async Task MapsResilienceFailuresToSafeJsonResponses(string kind, int status, string message)
    {
        Exception exception = kind switch
        {
            "circuit" => new BrokenCircuitException("private provider details"),
            "polly-timeout" => new TimeoutRejectedException("private provider details"),
            _ => new TimeoutException("private provider details")
        };
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        var middleware = Create(_ => throw exception, new Mock<ILogger<ExceptionHandlingMiddleware>>());

        await middleware.InvokeAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        body.Position = 0;
        using var payload = await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(payload.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.Equal(message, payload.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("private provider details", payload.RootElement.GetRawText());
    }

    [Fact]
    public async Task ClientCancellationDoesNotWriteAnErrorResponseOrLogAnError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        using var body = new MemoryStream();
        context.Response.Body = body;
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = Create(_ => throw new OperationCanceledException(cancellation.Token), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, body.Length);
        Assert.Null(context.Response.ContentType);
        logger.Verify(log => log.Log(LogLevel.Debug, It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public async Task CancellationWithoutClientAbortIsNotSilentlySwallowed()
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        var middleware = Create(_ => throw new OperationCanceledException(), new Mock<ILogger<ExceptionHandlingMiddleware>>());
        await middleware.InvokeAsync(context);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.True(body.Length > 0);
    }

    private static ExceptionHandlingMiddleware Create(RequestDelegate next, Mock<ILogger<ExceptionHandlingMiddleware>> logger)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Production);
        return new ExceptionHandlingMiddleware(next, logger.Object, environment.Object);
    }
}
