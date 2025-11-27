using Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations;

public static class Search
{
    public static void MapOrganisationsSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations", Handle)
            .WithName("SearchOrganisations")
            .WithTags("Organisations")
            .WithSummary("Search organisations")
            .WithDescription("Returns all organisations filtered by multiple criteria")
            .Produces<OrganisationSearch>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [AsParameters] OrganisationSearchRequest request,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        return Results.Ok(new OrganisationSearch());
    }
}
