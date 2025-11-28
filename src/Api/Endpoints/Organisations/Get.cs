using Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.Organisations;

public static class Get
{
    public static void MapOrganisationsGet(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{id:guid}", Handle)
            .WithName("GetOrganisation")
            .WithTags("Organisations")
            .WithSummary("Get an organisation by ID")
            .WithDescription("Returns the organisation details for a specific organisation ID")
            .Produces<Organisation>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    [HttpGet]
    private static async Task<IResult> Handle([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await Task.Yield();

        return id == new Guid("b6f76437-65b6-4ed2-a7d5-c50e9af76201")
            ? Results.Ok(
                new Organisation
                {
                    Id = id,
                    Name = null!,
                    Address = null!,
                }
            )
            : Results.NotFound();
    }
}
