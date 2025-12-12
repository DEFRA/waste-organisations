using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Testing;

public class EndpointFilter
{
    internal string Filter { get; }

    private EndpointFilter(string filter) => Filter = filter;

    public static EndpointFilter Registrations(RegistrationType[] registrations) =>
        Registrations(string.Join(",", registrations.Select(x => x.ToJsonValue())));

    public static EndpointFilter Registrations(string? registrations) => new($"registrations={registrations}");

    public static EndpointFilter RegistrationYears(int[] registrationYears) =>
        RegistrationYears(string.Join(",", registrationYears));

    public static EndpointFilter RegistrationYears(string? registrationYears) =>
        new($"registrationYears={registrationYears}");

    public static EndpointFilter Statuses(RegistrationStatus[] statuses) =>
        Statuses(string.Join(",", statuses.Select(x => x.ToJsonValue())));

    public static EndpointFilter Statuses(string? statuses) => new($"statuses={statuses}");
}
