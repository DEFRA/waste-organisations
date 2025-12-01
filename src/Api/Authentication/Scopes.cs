using System.Diagnostics.CodeAnalysis;

namespace Api.Authentication;

[ExcludeFromCodeCoverage]
public static class Scopes
{
    public const string Read = "read";
    public const string Write = "write";
}
