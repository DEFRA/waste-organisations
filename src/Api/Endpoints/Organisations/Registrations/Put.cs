using System.ComponentModel.DataAnnotations;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations.Registrations;

public static class Put
{
    public const string OperationId = "CreateOrUpdateRegistration";

    public static void MapRegistrationsPut(this IEndpointRouteBuilder app)
    {
        app.MapPut("/organisations/{id:guid}/registrations/{type}-{registrationYear:int}", Handle)
            .WithName(OperationId)
            .WithTags("Registrations")
            .WithSummary("Create or update a registration")
            .Produces<Registration>()
            .Produces<Registration>(statusCode: StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    [HttpPut]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromRoute] RegistrationTypeFromRoute type,
        [FromRoute] [Range(RegistrationYear.Minimum, RegistrationYear.Maximum)] int registrationYear,
        [FromBody] RegistrationRequest request,
        [FromServices] IOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await organisationService.Get(id, cancellationToken);
        if (organisation is null)
            return Results.NotFound();

        var (updated, isAdded) = organisation.Patch(type.RegistrationType, registrationYear, request);

        updated = await organisationService.Update(updated, cancellationToken);

        var registration = updated.GetRegistration(type.RegistrationType, registrationYear);
        var result = registration.ToDto();

        return isAdded
            ? Results.Created($"/organisations/{id}/registrations/{registration.Key}", result)
            : Results.Ok(result);
    }
}
