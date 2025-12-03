using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Api.Dtos;

// ReSharper disable once ClassNeverInstantiated.Global
public record OrganisationSearchRequest
{
    [Description("Comma separated list of registration types")]
    [FromQuery(Name = "registrations")]
    public RegistrationType[]? Registrations { get; init; } = [];

    [Description("Comma separated list of years")]
    [FromQuery(Name = "registrationYears")]
    [Range(2023, 2050)]
    public int[]? RegistrationYears { get; init; } = [];

    [Description("Comma separated list of registration statuses")]
    [FromQuery(Name = "statuses")]
    public RegistrationStatus[]? Statuses { get; init; } = [];
}
