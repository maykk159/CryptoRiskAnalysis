using CryptoRiskAnalysis.API.Wrappers;
using Microsoft.AspNetCore.Builder;
using System.Threading.RateLimiting;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        private static void AddRiskAnalysisRateLimiting(IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    var response = new ApiResponse<string>("Too many requests. Please try again later.");
                    await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
                };
                options.AddPolicy("RiskAnalysis", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetClientIpAddress(context),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            var address = context.Connection.RemoteIpAddress;
            if (address is null)
            {
                return "unknown";
            }

            return address.IsIPv4MappedToIPv6
                ? address.MapToIPv4().ToString()
                : address.ToString();
        }
    }
}
