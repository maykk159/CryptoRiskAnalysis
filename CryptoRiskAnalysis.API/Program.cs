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

// Configure CORS
builder.Services.AddCorsConfiguration();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
