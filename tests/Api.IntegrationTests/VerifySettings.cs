using System.Runtime.CompilerServices;

namespace Defra.WasteOrganisations.Api.IntegrationTests;

public static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialize() => VerifierSettings.UseStrictJson();
}
