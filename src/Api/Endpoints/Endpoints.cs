using Defra.WasteOrganisations.Api.Endpoints.Organisations;
using Defra.WasteOrganisations.Api.Endpoints.Organisations.Registrations;

namespace Defra.WasteOrganisations.Api.Endpoints;

public static class Endpoints
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapOrganisationsSearch();
        app.MapOrganisationsGet();
        app.MapOrganisationsPut();

        app.MapRegistrationsPut();
        app.MapRegistrationsDelete();
    }
}
