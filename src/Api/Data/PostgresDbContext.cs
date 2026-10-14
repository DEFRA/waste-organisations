using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace Defra.WasteOrganisations.Api.Data;

/// <summary>
/// Empty on purpose: the schema is owned by the Liquibase changelog in <c>changelog/</c>,
/// never by EF Core migrations, and entities arrive with the stories that need them.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options);
