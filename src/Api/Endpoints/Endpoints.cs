using Api.Endpoints.Organisations;
using Api.Endpoints.Organisations.Registrations;

namespace Api.Endpoints;

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
