using Api.Authentication;
using Api.Dtos;
using Api.Mapping;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations;

public static class Search
{
    public const string OperationId = "SearchOrganisations";

    public static void MapOrganisationsSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations", Handle)
            .WithName(OperationId)
            .WithTags("Organisations")
            .WithSummary("Search organisations")
            .WithDescription("Returns all organisations filtered by multiple criteria")
            .Produces<OrganisationSearch>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [AsParameters] OrganisationSearchRequest request,
        [FromServices] IOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var organisations = await organisationService.Search(
            request.ParsedRegistrationTypes(),
            request.ParsedRegistrationYears(),
            request.ParsedRegistrationStatuses(),
            cancellationToken
        );

        return Results.Ok(new OrganisationSearch { Organisations = organisations.Select(x => x.ToDto()).ToArray() });
    }
}
