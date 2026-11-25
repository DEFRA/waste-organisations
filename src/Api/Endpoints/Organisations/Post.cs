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
            .WithDescription("Create an organisation with its addresses. Duplicate organisations are not detected.")
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
                $"/organisations/{created.Id}",
                created.ToResponse()
            ),
            OrganisationAlreadyExists { ExistingId: var existingId } => AlreadyExists(httpContext, existingId),
            _ => throw new InvalidOperationException($"Unknown create result {result.GetType().Name}"),
        };
    }

    private static ProblemHttpResult AlreadyExists(HttpContext httpContext, Guid existingId)
    {
        httpContext.Response.Headers.Location = $"/organisations/{existingId}";

        return TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Organisation already exists",
            detail: "An organisation with this companiesHouseNumber already exists.",
            extensions: new Dictionary<string, object?> { ["organisationId"] = existingId }
        );
    }
}
