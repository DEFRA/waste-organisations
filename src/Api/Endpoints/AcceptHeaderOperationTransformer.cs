using System.Text.Json.Nodes;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteOrganisations.Api.Endpoints;

/// <summary>
/// Documents, on every operation of the v2 document, the <c>Accept</c> header that selects v2 and
/// the <c>406</c> an unsupported version gets (spec 0005). The v1 document has neither: a request
/// without the header is served v1.
/// </summary>
public class AcceptHeaderOperationTransformer : IOpenApiOperationTransformer
{
    internal const string MediaTypeV2 = $"{AcceptHeaderVersionResolver.MediaType}; version=2";

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        operation.Parameters ??= [];
        operation.Parameters.Insert(
            0,
            new OpenApiParameter
            {
                Name = "Accept",
                In = ParameterLocation.Header,
                Required = true,
                Description =
                    $"Selects v2 of the API: `{MediaTypeV2}`. Without it, or with `version=1`, the request is "
                    + "served v1.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create(MediaTypeV2)] },
            }
        );

        // The body ApiVersioningMiddleware writes. Registering the schema is a no-op when another
        // response of the document already did
        var problemDetails = await context.GetOrCreateSchemaAsync(
            typeof(ProblemDetails),
            cancellationToken: cancellationToken
        );
        context.Document?.AddComponent(nameof(ProblemDetails), problemDetails);

        operation.Responses ??= [];
        operation.Responses[StatusCodes.Status406NotAcceptable.ToString()] = new OpenApiResponse
        {
            Description = "Not Acceptable: the `Accept` header asks for an unsupported or malformed API version.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = new OpenApiSchemaReference(nameof(ProblemDetails), context.Document),
                },
            },
        };
    }
}
