using Defra.WasteOrganisations.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Defra.WasteOrganisations.Api.IntegrationTests;

public static class PostgresTestDataFixture
{
    private const string ComposeDatabaseName = "waste_organisations";
    private const string BaselineTag = "v1.0.0";

    public static void EnsureComposeDatabase(string databaseName, string host, IReadOnlyCollection<string> tags)
    {
        if (databaseName != ComposeDatabaseName)
            throw new InvalidOperationException(
                $"Refusing to clear Postgres test data in database '{databaseName}' on host '{host}': "
                    + $"the database is not '{ComposeDatabaseName}'"
            );

        if (!tags.Contains(BaselineTag))
            throw new InvalidOperationException(
                $"Refusing to clear Postgres test data in database '{databaseName}' on host '{host}': "
                    + $"databasechangelog does not hold the tag '{BaselineTag}'"
            );
    }

    public static Task ClearAsync(PostgresDbContext context, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlRawAsync("TRUNCATE organisation_address, organisation, address", cancellationToken);
}
