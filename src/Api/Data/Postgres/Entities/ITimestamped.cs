namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

/// <summary>
/// A row with <c>created_at</c> and <c>updated_at</c>, which <see cref="TimestampInterceptor"/>
/// sets on save. The columns have no database default.
/// </summary>
public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
