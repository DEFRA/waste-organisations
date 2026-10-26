namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

/// <summary>
/// A row whose history is kept with <c>valid_from</c> and <c>valid_to</c>.
/// <see cref="TimestampInterceptor"/> sets <c>valid_from</c> on insert when it is not set.
/// </summary>
public interface IValidFrom
{
    DateTime ValidFrom { get; set; }
}
