using Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Registrations;

public static class Put
{
    public static void MapRegistrationsPut(this IEndpointRouteBuilder app)
    {
        app.MapPut("/organisations/{id:guid}/registrations/{type}-{submissionYear:int}", Handle)
            .WithName("UpdateRegistration")
            .WithTags("Registrations")
            .WithSummary("Update an existing registration")
            .Produces<Registration>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    [HttpPut]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromRoute] RegistrationTypeFromRoute type,
        [FromRoute] int submissionYear,
        [FromBody] RegistrationStatus status,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        return Results.Ok(
            new Registration
            {
                Status = status,
                Type = type.RegistrationType,
                SubmissionYear = submissionYear,
            }
        );
    }
}
