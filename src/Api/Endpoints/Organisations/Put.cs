using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations;

public static class Put
{
    public static void MapOrganisationsPut(this IEndpointRouteBuilder app)
    {
        app.MapPut("/organisations/{id:guid}", Handle)
            .WithName("CreateOrUpdateOrganisation")
            .WithTags("Organisations")
            .WithSummary("Create or update organisation")
            .WithDescription(
                "Create or update an organisation, including creating or updating the given registration details"
            )
            .Produces<Organisation>()
            .Produces<Organisation>(statusCode: StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    [HttpPut]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromBody] OrganisationRegistration organisation,
        [FromServices] IOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var existing = await organisationService.Get(id, cancellationToken);
        if (existing is null)
        {
            var created = await organisationService.Create(organisation.ToEntity(id), cancellationToken);

            return Results.Created($"/organisations/{id}", created.ToDto());
        }

        var updated = existing.Patch(organisation);

        updated = await organisationService.Update(updated, cancellationToken);

        return Results.Ok(updated.ToDto());
    }
}
