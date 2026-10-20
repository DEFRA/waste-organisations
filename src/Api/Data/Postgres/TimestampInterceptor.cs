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
    /// <c>created_at</c> and <c>updated_at</c> record when this service stored and last changed
    /// a row, so only this service sets them: on insert both become now whatever the entity
    /// holds, and on update <c>updated_at</c> becomes now and <c>created_at</c> is left as
    /// stored. <c>valid_from</c> is a business date a caller may set, so on insert it becomes
    /// now only when unset.
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
                    // Through the entry, not the entity, so the change is tracked even when
                    // change detection has already run
                    if (entry.Entity is ITimestamped)
                    {
                        entry.Property(nameof(ITimestamped.CreatedAt)).CurrentValue = now;
                        entry.Property(nameof(ITimestamped.UpdatedAt)).CurrentValue = now;
                    }

                    if (entry.Entity is IValidFrom)
                        SetIfUnset(entry, nameof(IValidFrom.ValidFrom), now);

                    break;

                case EntityState.Modified when entry.Entity is ITimestamped:
                    entry.Property(nameof(ITimestamped.UpdatedAt)).CurrentValue = now;

                    // Left out of the UPDATE, so the stored value stands even for an entity
                    // attached with Update(), whose original values are not the stored ones
                    var createdAt = entry.Property(nameof(ITimestamped.CreatedAt));
                    createdAt.CurrentValue = createdAt.OriginalValue;
                    createdAt.IsModified = false;

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
