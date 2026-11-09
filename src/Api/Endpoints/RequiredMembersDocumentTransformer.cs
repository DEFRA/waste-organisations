using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteOrganisations.Api.Endpoints;

/// <summary>
/// Takes <c>null</c> out of the schema of every required member of every component schema. The
/// v2 request members are nullable with <c>[Required]</c>, so that a missing one is a validation
/// error rather than an unreadable body (spec 0005, Validation), and the generator then documents
/// them as accepting null: typed <c>["null", ...]</c>, or <c>oneOf</c> a null schema and a
/// reference. A document transformer runs once the document is built, which is when the
/// <c>oneOf</c> wrapper exists; a schema transformer runs too early to see it.
/// </summary>
public class RequiredMembersDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        foreach (var schema in document.Components?.Schemas?.Values.OfType<OpenApiSchema>() ?? [])
        {
            if (schema.Required is not { } required || schema.Properties is not { } properties)
                continue;

            foreach (var name in required)
            {
                if (properties.TryGetValue(name, out var member) && member is OpenApiSchema inline)
                    properties[name] = WithoutNull(inline);
            }
        }

        return Task.CompletedTask;
    }

    private static IOpenApiSchema WithoutNull(OpenApiSchema schema)
    {
        if (schema.Type is { } type)
            schema.Type = type & ~JsonSchemaType.Null;

        if (schema.OneOf is not { } oneOf)
            return schema;

        // A wrapper around a null schema and a reference is the reference itself once null goes
        var options = oneOf.Where(option => option.Type != JsonSchemaType.Null).ToList();
        if (options is [var only] && schema.Type is null && schema.Properties is not { Count: > 0 })
            return only;

        schema.OneOf = options;

        return schema;
    }
}
