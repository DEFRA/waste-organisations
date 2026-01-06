using System.Diagnostics.CodeAnalysis;

namespace Defra.WasteOrganisations.Api.Utils.Security;

[ExcludeFromCodeCoverage]
public static class WebApplicationExtensions
{
    public static void UseHstsUnconditionally(this WebApplication app)
    {
        app.Use(
            async (context, next) =>
            {
                context.Response.Headers.StrictTransportSecurity =
                    $"max-age={TimeSpan.FromDays(365).TotalSeconds}; includeSubDomains";
                await next();
            }
        );
    }
}
