using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
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
                "Create an organisation with its addresses. A Companies House number that is already stored gives 409, with the id of the existing organisation in organisationId and in the Location header. Organisations without a Companies House number are not checked."
            )
            .Produces<CreateOrganisationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
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
        [FromServices] LinkGenerator linkGenerator,
        HttpContext httpContext,
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

        var result = await organisationService.CreateAsync(
            organisation.ToEntity(organisationTypeId, usageTypeIds),
            cancellationToken
        );

        return result switch
        {
            OrganisationCreated { Organisation: var created } => TypedResults.Created(
                DetailsPath(linkGenerator, created.Id),
                created.ToResponse()
            ),
            OrganisationAlreadyExists { ExistingId: var existingId } => AlreadyExists(
                httpContext,
                DetailsPath(linkGenerator, existingId),
                existingId
            ),
            _ => throw new InvalidOperationException($"Unknown create result {result.GetType().Name}"),
        };
    }

    // The v2 read's path, built from its route name so the Location follows that endpoint
    private static string DetailsPath(LinkGenerator linkGenerator, Guid id) =>
        linkGenerator.GetPathByName(GetDetails.OperationId, new { id })
        ?? throw new InvalidOperationException($"No route named {GetDetails.OperationId}");

    private static ProblemHttpResult AlreadyExists(HttpContext httpContext, string location, Guid existingId)
    {
        httpContext.Response.Headers.Location = location;

        return TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Organisation already exists",
            detail: "An organisation with this companiesHouseNumber already exists.",
            extensions: new Dictionary<string, object?> { ["organisationId"] = existingId }
        );
    }
}
