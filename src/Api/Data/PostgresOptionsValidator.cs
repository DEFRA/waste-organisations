using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Defra.WasteOrganisations.Api.Data;

/// <summary>
/// Rules a data annotation cannot express. Runs alongside the annotations on <see cref="PostgresOptions"/>.
/// </summary>
public class PostgresOptionsValidator : IValidateOptions<PostgresOptions>
{
    public ValidateOptionsResult Validate(string? name, PostgresOptions options)
    {
        var failures = new List<string>();

        if (!options.UseIamAuthentication && string.IsNullOrWhiteSpace(options.Password))
            failures.Add(
                $"{PostgresOptions.SectionName}:{nameof(PostgresOptions.Password)} is required when "
                    + $"{nameof(PostgresOptions.UseIamAuthentication)} is false."
            );

        // Healthy would report a failing check as passing, which is the same as having no check
        if (options.HealthCheckFailureStatus is not (HealthStatus.Degraded or HealthStatus.Unhealthy))
            failures.Add(
                $"{PostgresOptions.SectionName}:{nameof(PostgresOptions.HealthCheckFailureStatus)} must be "
                    + $"{nameof(HealthStatus.Degraded)} or {nameof(HealthStatus.Unhealthy)}."
            );

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
