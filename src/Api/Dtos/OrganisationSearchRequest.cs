using System.ComponentModel;
using Defra.WasteOrganisations.Api.Dtos.Attributes;
using Defra.WasteOrganisations.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteOrganisations.Api.Dtos;

// ReSharper disable once ClassNeverInstantiated.Global
public record OrganisationSearchRequest
{
    [Description("Comma separated list of registration types")]
    [FromQuery(Name = "registrations")]
    [EnumCommaSeparatedList<RegistrationType>(ErrorMessage = "Invalid registration type(s)")]
    public string? Registrations { get; init; }

    [Description("Comma separated list of years")]
    [FromQuery(Name = "registrationYears")]
    [NumericCommaSeparatedList(
        RegistrationYear.Minimum,
        RegistrationYear.Maximum,
        ErrorMessage = "Invalid registration year(s)"
    )]
    public string? RegistrationYears { get; init; }

    [Description("Comma separated list of registration statuses")]
    [FromQuery(Name = "statuses")]
    [EnumCommaSeparatedList<RegistrationStatus>(ErrorMessage = "Invalid registration status(s)")]
    public string? Statuses { get; init; }

    public List<RegistrationType> ParsedRegistrationTypes() =>
        Registrations?.Split(',').NotNull().Select(x => x.FromJsonValue<RegistrationType>()).ToList() ?? [];

    public List<int> ParsedRegistrationYears() =>
        RegistrationYears?.Split(',').NotNull().Select(int.Parse).ToList() ?? [];

    public List<RegistrationStatus> ParsedRegistrationStatuses() =>
        Statuses?.Split(',').NotNull().Select(x => x.FromJsonValue<RegistrationStatus>()).ToList() ?? [];
}
