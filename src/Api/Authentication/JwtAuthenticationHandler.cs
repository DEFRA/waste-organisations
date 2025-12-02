using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Api.Authentication;

public class JwtAuthenticationHandler(
    IOptionsMonitor<JwtBearerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AclOptions> aclOptions
) : JwtBearerHandler(options, logger, encoder)
{
    public const string SchemeName = "Bearer";

    private readonly JwtBearerEvents _jwtBearerEvents = new()
    {
        OnTokenValidated = context =>
        {
            var claimsIdentity = new ClaimsIdentity(context.Principal!.Identity);

            var clientId = claimsIdentity.Claims.FirstOrDefault(c => c.Type == Claims.ClientId)?.Value;
            if (clientId == null)
                return Task.CompletedTask;

            aclOptions.Value.Clients.TryGetValue(clientId, out var client);
            if (client is not { Type: AclOptions.ClientType.OAuth })
                return Task.CompletedTask;

            claimsIdentity.AddClaims([.. client.Scopes.Select(x => new Claim(Claims.Scope, x))]);

            context.Principal = new ClaimsPrincipal(claimsIdentity);

            return Task.CompletedTask;
        },
    };

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Setting Events in a constructor seemed to be overriden or ignored when HandleAuthenticateAsync() is run
        Events = _jwtBearerEvents;

        return base.HandleAuthenticateAsync();
    }
}
