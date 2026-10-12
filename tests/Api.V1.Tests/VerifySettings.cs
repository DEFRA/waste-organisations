using System.Runtime.CompilerServices;

namespace Defra.WasteOrganisations.Api.V1.Tests;

public static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.UseStrictJson();
        // Without this a snapshot drops "registrations": [], which is part of the v1 contract.
        VerifierSettings.DontIgnoreEmptyCollections();
        VerifierSettings.ScrubMember("traceId");
    }
}
