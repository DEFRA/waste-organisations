using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations;

public static class Post
{
    public static void MapOrganisationsPost(this IEndpointRouteBuilder app)
    {
        app.MapPost("/organisations", Handle)
            .WithName("CreateOrganisation")
            .WithTags("Organisations")
            .WithSummary("Create organisation")
            .WithDescription(
                "Create an organisation with its addresses. An address is stored once and shared by every "
                    + "organisation that uses it. Duplicate organisations are not detected."
            )
            .Produces<CreateOrganisationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write)
            .WithApiVersions(2)
            // CreateOrganisationValidationFilter validates instead: the framework's validation keys
            // errors by C# names and misses some failing fields (spec 0005, Decisions)
            .DisableValidation()
            .AddEndpointFilter<CreateOrganisationValidationFilter>();
    }

    [HttpPost]
    private static async Task<IResult> Handle(
        [FromBody] CreateOrganisationRequest request,
        [FromServices] IPostgresOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        // The validation filter has checked the organisation and its type code
        var organisation = request.Organisation!;
        var organisationTypeId = await organisationService.OrganisationTypeIdAsync(
            organisation.OrganisationType!.Trim(),
            cancellationToken
        );
        var usageTypeIds = await organisationService.UsageTypeIdsAsync(cancellationToken);

        var created = await organisationService.CreateAsync(
            organisation.ToEntity(organisationTypeId, usageTypeIds),
            cancellationToken
        );

        return TypedResults.Created($"/organisations/{created.Id}", created.ToResponse());
    }
}
