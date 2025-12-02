using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Authentication;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationAuthorization(this IServiceCollection services)
    {
        services.AddOptions<AclOptions>().BindConfiguration("Acl").ValidateDataAnnotations().ValidateOnStart();

        services
            .AddAuthentication(BasicAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
                BasicAuthenticationHandler.SchemeName,
                _ => { }
            )
            .AddScheme<JwtBearerOptions, JwtAuthenticationHandler>(
                JwtAuthenticationHandler.SchemeName,
                options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        SignatureValidator = (token, _) => new JsonWebToken(token),
                        ValidateAudience = false,
                        ValidateIssuer = false,
                    };
                }
            );

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                PolicyNames.Read,
                builder =>
                    builder
                        .RequireAuthenticatedUser()
                        .AddAuthenticationSchemes(
                            BasicAuthenticationHandler.SchemeName,
                            JwtAuthenticationHandler.SchemeName
                        )
                        .RequireClaim(Claims.Scope, Scopes.Read)
            )
            .AddPolicy(
                PolicyNames.Write,
                builder =>
                    builder
                        .RequireAuthenticatedUser()
                        .AddAuthenticationSchemes(
                            BasicAuthenticationHandler.SchemeName,
                            JwtAuthenticationHandler.SchemeName
                        )
                        .RequireClaim(Claims.Scope, Scopes.Write)
            );

        return services;
    }
}
