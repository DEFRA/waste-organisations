using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: AssemblyFixture(typeof(PostgresTestDataFixture))]

namespace Defra.WasteOrganisations.Api.IntegrationTests;

public class PostgresTestDataFixture : IAsyncLifetime
{
    private const string ComposeDatabaseName = "waste_organisations";
    private const string BaselineTag = "v1.0.0";

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = IntegrationTestBase.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;

        var databaseName = await context
            .Database.SqlQuery<string>($"SELECT current_database() AS \"Value\"")
            .SingleAsync(cancellationToken);
        var changelogExists = await context
            .Database.SqlQuery<bool>($"SELECT to_regclass('public.databasechangelog') IS NOT NULL AS \"Value\"")
            .SingleAsync(cancellationToken);
        var tags = changelogExists
            ? await context
                .Database.SqlQuery<string>($"SELECT tag AS \"Value\" FROM databasechangelog WHERE tag IS NOT NULL")
                .ToListAsync(cancellationToken)
            : [];

        // ValidateDataAnnotations requires PostgresOptions.DefaultHost, so .Value never yields a null DefaultHost
        EnsureComposeDatabase(databaseName, options.DefaultHost!, tags);
        await ClearAsync(context, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

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
        context.Database.ExecuteSqlAsync($"TRUNCATE organisation_address, organisation, address", cancellationToken);
}
