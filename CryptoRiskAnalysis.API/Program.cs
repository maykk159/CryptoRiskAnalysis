using System.Diagnostics;
using CryptoRiskAnalysis.API.Extensions;
using CryptoRiskAnalysis.API.Middleware;
using CryptoRiskAnalysis.API.Interfaces;
using CryptoRiskAnalysis.API.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) => configuration
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .ReadFrom.Configuration(context.Configuration));

// Add services to the container.
builder.Services.AddControllers();

// Configure Services using Extension Method
builder.Services.AddApplicationServices();
builder.Services.Configure<CoinGeckoOptions>(builder.Configuration.GetSection("CoinGecko"));
builder.Services.AddSingleton<IHistoricalMarketDataStore>(_ =>
    new SqliteHistoricalMarketDataStore(Path.GetFullPath(
        builder.Configuration["HistoricalData:DatabasePath"] ?? "Data/market-history.db", builder.Environment.ContentRootPath)));
builder.Services.AddScoped<HistoricalMarketDataService>();
builder.Services.AddOperationalHealthChecks();
builder.Services.AddForwardedHeadersConfiguration(builder.Configuration);

// Configure CORS
builder.Services.AddCorsConfiguration(builder.Configuration);

builder.Services.AddApiDocumentation();

// Direct production deployments must never serve API responses over plain HTTP.
// The HTTPS endpoint/certificate can still be supplied by Kestrel configuration.
builder.Services.AddHttpsRedirectionConfiguration(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
// Resolve the original client IP and scheme before HTTPS redirects and rate limiting.
// Only proxies/networks explicitly trusted in configuration (plus loopback defaults)
// are allowed to supply these values.
app.UseForwardedHeaders();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {Route} responded {StatusCode} in {Elapsed:0.0} ms (trace {TraceId})";
    options.GetMessageTemplateProperties = (context, _, elapsed, statusCode) =>
    [
        new("RequestMethod", new ScalarValue(context.Request.Method)),
        new("Route", new ScalarValue((context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)")),
        new("StatusCode", new ScalarValue(statusCode)),
        new("Elapsed", new ScalarValue(elapsed)),
        new("TraceId", new ScalarValue(Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier))
    ];
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("AllowReactApp");

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapOperationalHealthChecks();

app.Run();
