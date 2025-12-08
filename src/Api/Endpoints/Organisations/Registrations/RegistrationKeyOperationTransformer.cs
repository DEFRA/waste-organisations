using Api.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Endpoints.Organisations.Registrations;

public class RegistrationKeyOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (operation.OperationId is not (Put.OperationId or Delete.OperationId))
            return Task.CompletedTask;

        var typeParameter = operation.Parameters?.FirstOrDefault(p =>
            p is { Name: "type", In: ParameterLocation.Path }
        );

        if (typeParameter == null)
            return Task.CompletedTask;

        operation.Parameters?.Remove(typeParameter);

        var newTypeParameter = new OpenApiParameter
        {
            Name = "type",
            In = ParameterLocation.Path,
            Required = true,
            Schema = new OpenApiSchemaReference(nameof(RegistrationType))
            {
                Reference = new JsonSchemaReference { Type = ReferenceType.Schema, Id = nameof(RegistrationType) },
            },
        };

        operation.Parameters?.Insert(1, newTypeParameter);

        return Task.CompletedTask;
    }
}
