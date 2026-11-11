---
paths:
  - "src/**/*.cs"
  - "src/**/*.csproj"
  - "Directory.Build.props"
  - "changelog/**"
---

Before changing C# source, project files or the Liquibase changelog, read and follow `doc/rules/csharp-dotnet.md` and `doc/rules/security-logging.md`. The schema is the changelog: a new changeset file, never an edit to a published one, never an EF Core migration.
