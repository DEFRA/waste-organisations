using System.ComponentModel.DataAnnotations;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations.Registrations;

public static class Delete
{
    public const string OperationId = "DeleteRegistration";

    public static void MapRegistrationsDelete(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/organisations/{id:guid}/registrations/{type}-{registrationYear:int}", Handle)
            .WithName(OperationId)
            .WithTags("Registrations")
            .WithSummary("Delete an existing registration")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    [HttpDelete]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromRoute] RegistrationTypeFromRoute type,
        [FromRoute] [Range(RegistrationYear.Minimum, RegistrationYear.Maximum)] int registrationYear,
        [FromServices] IOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await organisationService.Get(id, cancellationToken);
        if (organisation is null)
            return Results.NotFound();

        var registrations = organisation.RegistrationsAsDictionary();

        registrations.TryGetValue(
            new Data.Entities.RegistrationKey(type.RegistrationType, registrationYear),
            out var registration
        );

        if (registration is null)
            return Results.NotFound();

        registrations.Remove(registration.Key);

        var updated = organisation with { Registrations = registrations.Values.ToArray() };

        await organisationService.Update(updated, cancellationToken);

        return Results.NoContent();
    }
}
