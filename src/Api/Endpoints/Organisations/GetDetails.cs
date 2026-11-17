using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations;

public static class GetDetails
{
    public static void MapOrganisationsGetDetails(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{id:guid}", Handle)
            .WithName("GetOrganisationDetails")
            .WithTags("Organisations")
            .WithSummary("Get organisation details")
            .WithDescription("Returns the stored organisation with the given id as it is, with its current addresses.")
            .Produces<GetOrganisationDetailsResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read)
            .WithApiVersions(2);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromServices] IPostgresOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await organisationService.GetAsync(id, cancellationToken);

        return organisation is null ? TypedResults.NotFound() : TypedResults.Ok(organisation.ToDetailsResponse());
    }
}
