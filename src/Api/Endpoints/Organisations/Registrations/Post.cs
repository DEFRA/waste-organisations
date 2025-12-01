using Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations.Registrations;

public static class Post
{
    public static void MapRegistrationsPost(this IEndpointRouteBuilder app)
    {
        app.MapPost("/organisations/{id:guid}/registrations/{type}-{submissionYear:int}", Handle)
            .WithName("CreateRegistration")
            .WithTags("Registrations")
            .WithSummary("Create a new registration")
            .Produces<Registration>(statusCode: StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    [HttpPost]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromRoute] RegistrationTypeFromRoute type,
        [FromRoute] int submissionYear,
        [FromBody] RegistrationRequest request,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        return Results.Created(
            $"/organisations/{id}/registrations/{type}-{submissionYear}",
            new Registration
            {
                Status = request.Status,
                Type = type.RegistrationType,
                SubmissionYear = submissionYear,
            }
        );
    }
}
