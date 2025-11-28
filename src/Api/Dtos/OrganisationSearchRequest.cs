using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Api.Dtos;

// ReSharper disable once ClassNeverInstantiated.Global
public record OrganisationSearchRequest
{
    [Description("Command separated list of registration types - example SMALL_PRODUCER,LARGE_PRODUCER")]
    [FromQuery(Name = "registrations")]
    public RegistrationType[] Registrations { get; init; } = [];

    [Description("Comma separated list of years - example 2025,2026")]
    [FromQuery(Name = "submissionYears")]
    [Range(2023, 2050)]
    public int[] SubmissionYears { get; init; } = [];
}
