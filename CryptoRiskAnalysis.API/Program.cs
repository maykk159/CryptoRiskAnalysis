using CryptoRiskAnalysis.API.Extensions;
using CryptoRiskAnalysis.API.Middleware;
using Serilog;

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
builder.Services.AddForwardedHeadersConfiguration(builder.Configuration);

// Configure CORS
builder.Services.AddCorsConfiguration();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Direct production deployments must never serve API responses over plain HTTP.
// The HTTPS endpoint/certificate can still be supplied by Kestrel configuration.
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 443;
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Resolve the original client IP and scheme before HTTPS redirects and rate limiting.
// Only proxies/networks explicitly trusted in configuration (plus loopback defaults)
// are allowed to supply these values.
app.UseForwardedHeaders();

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

app.Run();
