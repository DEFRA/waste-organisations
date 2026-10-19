using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Defra.WasteOrganisations.Api.Data.Postgres;

/// <summary>
/// Sets <c>created_at</c>, <c>updated_at</c> and <c>valid_from</c> from <see cref="TimeProvider"/>
/// before EF Core writes them. The changelog gives these columns no default: EF always sends
/// every mapped column, so a database default would never apply and an unset
/// <see cref="DateTime"/> would be stored as <c>-infinity</c>.
/// </summary>
public class TimestampInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SetTimestamps(eventData.Context);

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        SetTimestamps(eventData.Context);

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// On insert, fills each timestamp that is still unset, so a caller (for example a data
    /// migration) can supply its own. On update, always moves <c>updated_at</c> to now.
    /// </summary>
    public void SetTimestamps(DbContext? context)
    {
        if (context is null)
            return;

        // Milliseconds, as on the Mongo side, so a value written equals the value read back
        var now = timeProvider.GetUtcNowWithoutMicroseconds();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is ITimestamped)
                    {
                        SetIfUnset(entry, nameof(ITimestamped.CreatedAt), now);
                        SetIfUnset(entry, nameof(ITimestamped.UpdatedAt), now);
                    }

                    if (entry.Entity is IValidFrom)
                        SetIfUnset(entry, nameof(IValidFrom.ValidFrom), now);

                    break;

                case EntityState.Modified when entry.Entity is ITimestamped:
                    // Through the entry, not the entity, so the change is tracked even when
                    // change detection has already run
                    entry.Property(nameof(ITimestamped.UpdatedAt)).CurrentValue = now;

                    break;
            }
        }
    }

    private static void SetIfUnset(EntityEntry entry, string propertyName, DateTime now)
    {
        var property = entry.Property(propertyName);
        if (Equals(property.CurrentValue, default(DateTime)))
            property.CurrentValue = now;
    }
}
