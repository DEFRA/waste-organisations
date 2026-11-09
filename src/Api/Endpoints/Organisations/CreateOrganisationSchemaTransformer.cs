using System.Text.Json.Nodes;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations;

/// <summary>
/// Adds to the <c>POST /organisations</c> request schema what the OpenAPI package cannot read from
/// the DTOs: the values of the members bound as strings and checked by validation attributes, and
/// the <c>uniqueItems</c> of <c>addressTypes</c> (spec 0005).
/// </summary>
public class CreateOrganisationSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var property = context.JsonPropertyInfo;

        if (property?.DeclaringType == typeof(OrganisationRequest))
        {
            switch (property.Name)
            {
                case OrganisationRequest.OrganisationTypeMember:
                    schema.Enum = [.. OrganisationTypeCodes.All.Select(c => JsonValue.Create(c.Code))];
                    schema.Description = AppendParagraph(schema.Description, OrganisationTypeCodesDescription());
                    break;
                case OrganisationRequest.RegulatorNationMember:
                    schema.Enum = [.. Enum.GetValues<RegulatorNation>().Select(n => JsonValue.Create(n.ToJsonValue()))];
                    break;
            }
        }
        else if (
            property?.DeclaringType == typeof(AddressRequest)
            && property.Name == AddressRequest.AddressTypesMember
        )
        {
            schema.MinItems ??= 1;
            schema.UniqueItems = true;
        }

        return Task.CompletedTask;
    }

    // Built from OrganisationTypeCodes.All, so the documented codes, meanings and rules cannot drift
    // from what validation accepts. Lines end in \n whatever the platform, so the document is stable
    private static string OrganisationTypeCodesDescription() =>
        string.Join(
            "\n\n",
            CodesParagraph(
                "Registered at Companies House: `name`, `companiesHouseNumber` and a `REGISTERED` address are required.",
                registeredAtCompaniesHouse: true
            ),
            CodesParagraph(
                "Not registered at Companies House: `tradingName` and a `PRINCIPAL` address are required, "
                    + "and `companiesHouseNumber` must be absent.",
                registeredAtCompaniesHouse: false
            )
        );

    private static string CodesParagraph(string heading, bool registeredAtCompaniesHouse) =>
        string.Join(
            "\n",
            OrganisationTypeCodes
                .All.Where(c => c.RegisteredAtCompaniesHouse == registeredAtCompaniesHouse)
                .Select(c => $"- `{c.Code}`: {c.Description}")
                .Prepend($"{heading}\n")
        );

    private static string AppendParagraph(string? text, string paragraph) =>
        string.IsNullOrWhiteSpace(text) ? paragraph : $"{text}\n\n{paragraph}";
}
