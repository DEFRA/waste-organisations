using Api.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Endpoints;

public class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Info = new OpenApiInfo
        {
            Title = "Waste Organisations REST API",
            Version = "0.0.1",
            Description = "Save and retrieve organisations alongside their yearly registrations",
        };

        // This is removed from the whole schema and where it's used,
        // the actual enum of RegistrationType will be used instead
        document.Components?.Schemas?.Remove(nameof(RegistrationTypeFromRoute));

        return Task.CompletedTask;
    }
}
