using System.Net;
using System.Text.Json;
using CryptoRiskAnalysis.API.Wrappers;
using CryptoRiskAnalysis.API.Exceptions;
using Microsoft.Extensions.Hosting;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CryptoRiskAnalysis.API.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request was cancelled by the client.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = exception switch
            {
                AssetNotFoundException => (int)HttpStatusCode.NotFound,
                UpstreamRateLimitException => (int)HttpStatusCode.TooManyRequests,
                MarketDataProviderException => (int)HttpStatusCode.BadGateway,
                BrokenCircuitException => (int)HttpStatusCode.ServiceUnavailable,
                TimeoutRejectedException => (int)HttpStatusCode.GatewayTimeout,
                TimeoutException => (int)HttpStatusCode.GatewayTimeout,
                _ => (int)HttpStatusCode.InternalServerError
            };

            // In production, hide the exception message for security
            string message = exception switch
            {
                AssetNotFoundException or UpstreamRateLimitException => exception.Message,
                MarketDataProviderException => "Market data provider is temporarily unavailable.",
                BrokenCircuitException => "Market data provider is temporarily unavailable.",
                TimeoutRejectedException or TimeoutException => "Market data request timed out.",
                _ when _env.IsDevelopment() => exception.Message,
                _ => "Internal Server Error"
            };

            var response = new ApiResponse<string>(message)
            {
                Succeeded = false,
                Errors = _env.IsDevelopment()
                    ? new List<string> { exception.Message, exception.StackTrace ?? string.Empty }
                    : new List<string> { "An error occurred while processing your request." }
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
}
