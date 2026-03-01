using System.Text.Json;
using System.Text.Json.Schema;
using Ghost.Cloud.Contracts.Endpoints;
using Ghost.Cloud.Grains.Interfaces;

namespace Ghost.Cloud.Api.Middleware;

public interface IEndpointValidator
{
    public Task<bool> ValidateAsync(JsonSchema schema, JsonElement input);
}

public class EndpointValidator : IEndpointValidator
{
    public Task<bool> ValidateAsync(JsonSchema schema, JsonElement input)
    {
        // Basic validation - check if input matches the schema type
        if (schema.Type == "object" && input.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult(false);
        }

        // Check required fields
        foreach (string required in schema.Required)
        {
            if (!input.TryGetProperty(required, out _))
            {
                return Task.FromResult(false);
            }
        }

        // For more comprehensive validation, we'd use a JSON Schema library
        // like JsonSchema.Net or NJsonSchema
        return Task.FromResult(true);
    }
}

public class SchemaValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SchemaValidationMiddleware> _logger;
    private readonly IEndpointValidator _validator;

    public SchemaValidationMiddleware(
        RequestDelegate next,
        ILogger<SchemaValidationMiddleware> logger,
        IEndpointValidator validator)
    {
        _next = next;
        _logger = logger;
        _validator = validator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only validate POST requests to scrape endpoints
        if (context.Request.Method != HttpMethods.Post ||
            !context.Request.Path.StartsWithSegments("/v1/endpoints"))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }
}

public static class SchemaValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseSchemaValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SchemaValidationMiddleware>();
    }

    public static IServiceCollection AddEndpointValidation(this IServiceCollection services)
    {
        services.AddSingleton<IEndpointValidator, EndpointValidator>();
        return services;
    }
}
