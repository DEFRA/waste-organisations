using Api.Authentication;
using Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations;

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
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        return Results.Created(
            $"/organisations/{id}",
            new Organisation
            {
                Id = id,
                Name = organisation.Name,
                TradingName = organisation.TradingName,
                BusinessCountry = organisation.BusinessCountry,
                CompaniesHouseNumber = organisation.CompaniesHouseNumber,
                Address = organisation.Address,
                Registrations = [organisation.Registration],
            }
        );
    }
}
