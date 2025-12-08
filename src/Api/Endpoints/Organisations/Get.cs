using Api.Authentication;
using Api.Dtos;
using Api.Mapping;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations;

public static class Get
{
    public static void MapOrganisationsGet(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{id:guid}", Handle)
            .WithName("GetOrganisation")
            .WithTags("Organisations")
            .WithSummary("Get an organisation by ID")
            .WithDescription("Returns the organisation details for a specific organisation ID")
            .Produces<Organisation>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromServices] IOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await organisationService.Get(id, cancellationToken);

        return organisation is null ? Results.NotFound() : Results.Ok(organisation.ToDto());
    }
}
