using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentGateway.Api.Swagger;

/// <summary>
/// Supplies a realistic example body for each error response.
/// </summary>
/// <remarks>
/// ProblemDetails carries no example values of its own, so Swagger UI synthesises them from the schema and renders
/// placeholders — <c>"title": "string"</c>, <c>"status": 0</c>, <c>"errors": { "additionalProp1": ["string"] }</c> —
/// which read as though they were part of the contract. The examples below are real responses from this API.
/// </remarks>
public class ProblemDetailsExampleOperationFilter : IOperationFilter
{
    private const string TraceId = "00-9655942df43c5a63d0d4ff9c983818db-ba91b1ae5d4d81d3-00";

    private static readonly Dictionary<string, Func<OpenApiObject>> ExampleFactories = new()
    {
        [StatusCodes.Status400BadRequest.ToString()] = () => new OpenApiObject
        {
            ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            ["title"] = new OpenApiString("One or more validation errors occurred."),
            ["status"] = new OpenApiInteger(StatusCodes.Status400BadRequest),
            ["errors"] = new OpenApiObject
            {
                ["CardNumber"] = new OpenApiArray
                {
                    new OpenApiString("'Card Number' must be between 14 and 19 characters.")
                },
                ["Currency"] = new OpenApiArray
                {
                    new OpenApiString("'Currency' must be a supported currency (GBP, USD, EUR).")
                }
            },
            ["traceId"] = new OpenApiString(TraceId)
        },
        [StatusCodes.Status404NotFound.ToString()] = () => new OpenApiObject
        {
            ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ["title"] = new OpenApiString("Not Found"),
            ["status"] = new OpenApiInteger(StatusCodes.Status404NotFound),
            ["traceId"] = new OpenApiString(TraceId)
        },
        [StatusCodes.Status422UnprocessableEntity.ToString()] = () => new OpenApiObject
        {
            ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc4918#section-11.2"),
            ["title"] = new OpenApiString("Payment rejected"),
            ["status"] = new OpenApiInteger(StatusCodes.Status422UnprocessableEntity),
            ["detail"] = new OpenApiString("Rejected: card has expired"),
            ["traceId"] = new OpenApiString(TraceId)
        }
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var (statusCode, createExample) in ExampleFactories)
        {
            if (!operation.Responses.TryGetValue(statusCode, out var response))
            {
                continue;
            }

            foreach (var content in response.Content.Values)
            {
                // A fresh instance per media type: OpenApiObject is mutable and must not be shared between them.
                content.Example = createExample();
            }
        }
    }
}
