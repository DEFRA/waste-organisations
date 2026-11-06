using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Dtos.Validation;

namespace Defra.WasteOrganisations.Api.Endpoints.Organisations;

/// <summary>
/// Runs <see cref="CreateOrganisationRequestValidator"/> on the bound body of
/// <c>POST /organisations</c> and returns validation problem details keyed by JSON path when it
/// fails, so the handler only sees a valid request. The route disables the framework's minimal
/// API validation, which cannot produce these keys (spec 0005, Decisions, <c>400</c> shape).
/// </summary>
public sealed class CreateOrganisationValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<CreateOrganisationRequest>().Single();
        var errors = CreateOrganisationRequestValidator.Validate(request);

        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        return await next(context);
    }
}
