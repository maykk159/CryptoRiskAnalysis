using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CryptoRiskAnalysis.API.OpenApi;

internal sealed class ValidationProblemResponseFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType?.IsDefined(typeof(ApiControllerAttribute), inherit: true) != true ||
            !operation.Responses.TryGetValue("400", out var response) ||
            !response.Content.ContainsKey("application/json"))
            return;

        response.Description = "Invalid request. Parameter binding errors use Problem Details. Endpoint validation uses the API response envelope.";
        response.Content["application/problem+json"] = new OpenApiMediaType
        {
            Schema = context.SchemaGenerator.GenerateSchema(typeof(ValidationProblemDetails), context.SchemaRepository)
        };
    }
}
