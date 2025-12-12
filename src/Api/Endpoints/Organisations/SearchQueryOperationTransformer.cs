using Defra.WasteOrganisations.Api.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations;

public class SearchQueryOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (operation.OperationId is not Search.OperationId)
            return Task.CompletedTask;

        var registrationsParameter = operation.Parameters?.FirstOrDefault(p =>
            p is { Name: "registrations", In: ParameterLocation.Query }
        );

        if (registrationsParameter != null)
        {
            operation.Parameters?.Remove(registrationsParameter);

            var newTypeParameter = new OpenApiParameter
            {
                Name = "registrations",
                In = ParameterLocation.Query,
                Description = registrationsParameter.Description,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchemaReference(nameof(RegistrationType))
                    {
                        Reference = new JsonSchemaReference
                        {
                            Type = ReferenceType.Schema,
                            Id = nameof(RegistrationType),
                        },
                    },
                },
            };

            operation.Parameters?.Insert(0, newTypeParameter);
        }

        var registrationYearsParameter = operation.Parameters?.FirstOrDefault(p =>
            p is { Name: "registrationYears", In: ParameterLocation.Query }
        );

        if (registrationYearsParameter != null)
        {
            operation.Parameters?.Remove(registrationYearsParameter);

            var newTypeParameter = new OpenApiParameter
            {
                Name = "registrationYears",
                In = ParameterLocation.Query,
                Description = registrationYearsParameter.Description,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Minimum = RegistrationYear.Minimum.ToString(),
                    Maximum = RegistrationYear.Maximum.ToString(),
                    Items = new OpenApiSchema
                    {
                        Pattern = "^-?(?:0|[1-9]\\d*)$",
                        Type = JsonSchemaType.Integer | JsonSchemaType.String,
                        Format = "int32",
                    },
                },
            };

            operation.Parameters?.Insert(1, newTypeParameter);
        }

        var statusesParameter = operation.Parameters?.FirstOrDefault(p =>
            p is { Name: "statuses", In: ParameterLocation.Query }
        );

        if (statusesParameter != null)
        {
            operation.Parameters?.Remove(statusesParameter);

            var newTypeParameter = new OpenApiParameter
            {
                Name = "statuses",
                In = ParameterLocation.Query,
                Description = statusesParameter.Description,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchemaReference(nameof(RegistrationStatus))
                    {
                        Reference = new JsonSchemaReference
                        {
                            Type = ReferenceType.Schema,
                            Id = nameof(RegistrationStatus),
                        },
                    },
                },
            };

            operation.Parameters?.Insert(2, newTypeParameter);
        }

        return Task.CompletedTask;
    }
}
