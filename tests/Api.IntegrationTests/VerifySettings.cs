using System.Runtime.CompilerServices;

namespace Api.IntegrationTests;

public static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialize() => VerifierSettings.UseStrictJson();
}
