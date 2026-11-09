# C# and .NET standards

Adapted for this repo from the DEFRA [.NET standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/net_standards.md), [C# coding standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/csharp_coding_standards.md) and [choosing packages guide](https://github.com/DEFRA/software-development-standards/blob/main/docs/guides/choosing_packages.md). Last synced 9 November 2026.

## Platform

- .NET is Defra's secondary stack. This repo uses it for a backend API on CDP; that decision stands and is not revisited per change. Do not add frontend code, Razor, Blazor or MVC views to this repo.
- Stay on the current .NET LTS (`net10.0` in every `.csproj`). Changing the target framework is a story of its own: the Docker base images, `actions/setup-dotnet` and the Sonar scan move with it.
- C# only. Follow Microsoft's [C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) as encoded in `.editorconfig`. CSharpier (`.csharpierrc`, print width 120) owns formatting; run `dotnet csharpier check .` and never hand-format.

## Language

- Nullable reference types and implicit usings are on in every project. Fix a nullable warning at its source; use `!` only with a comment explaining why the value cannot be null there.
- Use modern C#: records for DTOs and immutable data, pattern matching, collection expressions, primary constructors, as the existing code does.
- `async`/`await` end to end, passing the request's `CancellationToken`. Never block with `.Result`, `.Wait()` or `GetAwaiter().GetResult()`.
- Warnings are errors (`TreatWarningsAsErrors` in `Directory.Build.props`) and SonarAnalyzer.CSharp runs on every build. Suppress a rule only with a comment stating the reason, as the `CA1873` line in `.editorconfig` and the `NuGetAuditSuppress` entry do. Never suppress to get a build green.
- Follow SOLID: one responsibility per class, dependencies injected through small interfaces (`IOrganisationService`, `IPostgresOrganisationService`) registered in `Program.cs`.

## Project layout

```
src/Api/
├── Authentication/   # Basic API-key and JWT handlers, ACL options, scopes and policies
├── Data/             # Mongo context and Postgres DbContext, options; entities in Data/Postgres/Entities
├── Documentation/    # Embedded ReDoc page
├── Dtos/             # Request and response records; validators in Dtos/Validation
├── Endpoints/        # One static class per endpoint under Endpoints/<Resource>/, registered in Endpoints.cs
├── Mapping/          # Extension methods between DTOs and entities
├── Services/         # Business logic and persistence behind interfaces
├── Utils/            # Health, Logging, Metrics, Security, TrustStore
└── Versioning/       # Accept-header version resolution, matcher policy, middleware
```

Put new code in the existing folder for its role. Do not add parallel folders (`Controllers`, `Models`, `Repositories`) or a new project without a spec decision.

## Endpoints

- One static class per endpoint (`Get`, `Post`, `Put`, `Search`) with a `Map<Resource><Verb>` extension method that chains `.WithName`, `.WithTags`, `.WithSummary`, `.WithDescription`, `.Produces...`, `.RequireAuthorization(PolicyNames.X)` and `.WithApiVersions(...)`, registered in `Endpoints.MapApiEndpoints`.
- Handlers are private static methods that rely on the endpoint filter for validation, call one service method and return a `TypedResults` value. No data access, mapping or business rules in a handler.
- Authorisation is declared on the endpoint with the `read` or `write` policy. Never check scopes by hand inside a handler.
- Errors are problem details: `TypedResults.ValidationProblem(errors)` keyed by JSON path for `400`, `ProducesProblem` for the rest, and the global exception handler in `Program.cs` for the unexpected. Never return a stack trace, exception message or connection string to the client.

## Validation and DTOs

- Request and response DTOs are records in `Dtos/` with `[JsonPropertyName]` and data annotations. Members the client cannot set do not exist on the request record.
- v2 requests use nullable members with `[Required]`, so a missing member is a validation error keyed by JSON path (spec 0005, Decisions). v1 DTOs use the C# `required` modifier and are frozen.
- Validate at the boundary through the endpoint filter and validator pair (`CreateOrganisationValidationFilter`, `CreateOrganisationRequestValidator`): trim before pattern checks, validate every array item, return all errors at once.
- Configuration binds to options classes validated on start (`AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`, `PostgresOptionsValidator`). Fail fast on missing configuration; never read `IConfiguration` ad hoc in a service.

## Data access

- Postgres: EF Core LINQ on `PostgresDbContext`, `AsNoTracking()` for reads, one `SaveChangesAsync` per unit of work. `EnableRetryOnFailure` is on, so do not open explicit transactions outside the execution strategy. No SQL built from strings; if raw SQL is ever unavoidable, use `FromSql` with interpolated parameters.
- The schema is owned by the Liquibase changelog in `changelog/`. Entities in `Data/Postgres/Entities` follow the changelog, never the other way round, and there are no EF Core migrations. A schema change is a new `db.changelog-1.N.xml` included at the end of `db.changelog.xml`; a published changeset is never edited (README, "Postgres schema changes").
- Timestamps come from `TimeProvider` through `TimestampInterceptor`. Never call `DateTime.UtcNow` in domain or persistence code.
- MongoDB: typed `IMongoCollection<T>` with `Builders<T>.Filter`, never a filter built from a string. Indexes are declared in `MongoIndexService`.

## API versioning and OpenAPI

- The version comes from the `Accept` header. The mechanism and its exclusions (`/health`, `/openapi`, `/redoc`) are in the README section "API versioning" and spec 0004. Do not add URL-path versioning.
- v1 is the permanent default and its behaviour is frozen by `tests/Api.V1.Tests`. New or changed behaviour is an endpoint declaring `.WithApiVersions(2)` (or the next version), which may share its path with the v1 endpoint.
- Each version has its own OpenAPI document (`AddOpenApi("v2", ...)` in `Program.cs`) shaped by document, operation and schema transformers. A new version is added to `openApiDocuments` so the ReDoc dropdown lists it. Update the OpenAPI snapshot tests when the contract changes, and only then.

## Dependencies

- NuGet packages only, exact versions, declared in the project that uses them. `Directory.Build.props` carries only what applies to every project (currently the Sonar analyzer).
- Before adding a package, apply the DEFRA choosing packages guide: maintained, widely used, documented, no known vulnerabilities, and a licence compatible with the Open Government Licence. A package with no licence cannot be used. State the reason for the choice in the PR description.
- Update `.github/dependabot.yml` when a new package belongs to an existing group (see "Updating Dependabot" in `AGENTS.md`).
- Security advisories are never suppressed to pass the build. `NuGetAuditSuppress` is a last resort, carrying the advisory URL and the reason in a comment, and is removed when the fixed package arrives.

## Tests

See [quality-assurance.md](quality-assurance.md) for the test projects, naming and coverage rules.
