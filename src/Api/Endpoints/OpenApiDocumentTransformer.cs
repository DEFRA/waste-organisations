using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteOrganisations.Api.Endpoints;

public class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        // One transformer serves every document; the document name says which API version it describes.
        document.Info = context.DocumentName switch
        {
            "v2" => new OpenApiInfo
            {
                Title = "Waste Organisations REST API",
                Version = "2.0.0",
                Description =
                    $"Create organisations with their addresses. Select v2 with the header `Accept: {AcceptHeaderVersionResolver.MediaType}; version=2`. "
                    + "A request without that media type, or without a `version`, is served v1. "
                    + "An unsupported, malformed or ambiguous version returns `406 Not Acceptable`.",
            },
            _ => new OpenApiInfo
            {
                Title = "Waste Organisations REST API",
                Version = "1.0.0",
                Description = "Save and retrieve organisations alongside their yearly registrations",
            },
        };

        // This is removed from the whole schema and where it's used,
        // the actual enum of RegistrationType will be used instead
        document.Components?.Schemas?.Remove(nameof(RegistrationTypeFromRoute));

        return Task.CompletedTask;
    }
}
