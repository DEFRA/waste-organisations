using Api.Authentication;
using Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations.Registrations;

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
        [FromRoute] int registrationYear,
        [FromBody] RegistrationRequest request,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        return Results.Created(
            $"/organisations/{id}/registrations/{type}-{registrationYear}",
            new Registration
            {
                Status = request.Status,
                Type = type.RegistrationType,
                RegistrationYear = registrationYear,
            }
        );
    }
}
