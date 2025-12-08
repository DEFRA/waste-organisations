using Api.Authentication;
using Api.Dtos;
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
            .WithSummary("Update an existing registration")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    [HttpDelete]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromRoute] RegistrationTypeFromRoute type,
        [FromRoute] int registrationYear,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        return Results.NoContent();
    }
}
