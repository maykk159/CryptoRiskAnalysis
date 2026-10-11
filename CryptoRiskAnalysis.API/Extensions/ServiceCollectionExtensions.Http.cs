using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace CryptoRiskAnalysis.API.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHttpsRedirectionConfiguration(
            this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = configuration.GetValue<int?>("HttpsRedirection:HttpsPort") ?? 443;
                if (options.HttpsPort is < 1 or > 65535)
                    throw new InvalidOperationException("HttpsRedirection:HttpsPort must be between 1 and 65535.");
                options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
            });
        }

        public static IServiceCollection AddForwardedHeadersConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var reverseProxySection = configuration.GetSection("ReverseProxy");

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                var forwardLimit = reverseProxySection.GetValue<int?>("ForwardLimit") ?? 1;
                if (forwardLimit < 1)
                {
                    throw new InvalidOperationException(
                        "ReverseProxy:ForwardLimit must be greater than zero.");
                }

                options.ForwardLimit = forwardLimit;

                foreach (var proxy in reverseProxySection
                    .GetSection("KnownProxies")
                    .Get<string[]>() ?? [])
                {
                    if (!IPAddress.TryParse(proxy, out var address))
                    {
                        throw new InvalidOperationException(
                            $"ReverseProxy:KnownProxies contains an invalid IP address: '{proxy}'.");
                    }

                    options.KnownProxies.Add(address);
                }

                foreach (var network in reverseProxySection
                    .GetSection("KnownNetworks")
                    .Get<string[]>() ?? [])
                {
                    if (!System.Net.IPNetwork.TryParse(network, out var addressRange))
                    {
                        throw new InvalidOperationException(
                            $"ReverseProxy:KnownNetworks contains an invalid CIDR range: '{network}'.");
                    }

                    options.KnownIPNetworks.Add(addressRange);
                }
            });

            return services;
        }

        public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    builder => builder.WithOrigins(origins)
                                      .AllowAnyMethod()
                                      .AllowAnyHeader());
            });

            return services;
        }
    }
}
