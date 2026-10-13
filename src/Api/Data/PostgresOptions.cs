using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Defra.WasteOrganisations.Api.Data;

[ExcludeFromCodeCoverage]
public class PostgresOptions
{
    public const string SectionName = "Postgres";

    [Required]
    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    [Required]
    public string? DatabaseName { get; set; }

    [Required]
    public string? User { get; set; }

    public string? Password { get; set; }

    public bool UseIamAuthentication { get; set; }

    // Explicit because the enum's zero value is Unhealthy, which would take
    // the service out of service wherever the setting is missing
    public HealthStatus HealthCheckFailureStatus { get; set; } = HealthStatus.Degraded;
}
