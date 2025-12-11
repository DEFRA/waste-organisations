using Api.Dtos;

namespace Api.Endpoints.Organisations.Registrations;

public static class RegistrationKeyErrorHandler
{
    public static string ReplaceSchemaReference(string error)
    {
        // Find usages of RegistrationTypeFromRoute to observe
        // all interaction with Open API related code
        return error.Replace(nameof(RegistrationTypeFromRoute), nameof(RegistrationType));
    }
}
