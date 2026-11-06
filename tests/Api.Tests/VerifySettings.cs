using System.Runtime.CompilerServices;

namespace Defra.WasteOrganisations.Api.Tests;

public static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.UseStrictJson();
        VerifierSettings.DontIgnoreEmptyCollections();
        VerifierSettings.ScrubMember("traceId");
        // Snapshots live in Snapshots/<TestClass>/ beside the test file, not next to it, so a
        // scenarios folder shows its test classes rather than every snapshot
        Verifier.DerivePathInfo(
            (sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(
                        Path.GetDirectoryName(sourceFile) ?? projectDirectory,
                        "Snapshots",
                        type.Name
                    ),
                    typeName: type.Name,
                    methodName: method.Name
                )
        );
    }
}
