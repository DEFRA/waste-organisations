using System.ComponentModel.DataAnnotations;
using Api.Authentication;
using Api.Dtos;
using Api.Mapping;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations.Registrations;

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

        var registration = organisation.FindRegistration(type.RegistrationType, registrationYear);
        if (registration is null)
            return Results.NotFound();

        var updated = organisation.Remove(registration);

        await organisationService.Update(updated, cancellationToken);

        return Results.NoContent();
    }
}
