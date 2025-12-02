using System.Security.Claims;
using Api.Authentication;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Testing;

namespace Api.Tests.Authentication;

public class JwtAuthenticationHandlerTests
{
    private JwtAuthenticationHandler Subject { get; }

    private IOptionsMonitor<JwtBearerOptions> OptionsMonitor { get; } =
        Substitute.For<IOptionsMonitor<JwtBearerOptions>>();

    private AclOptions AclOptions { get; set; } = new();

    public JwtAuthenticationHandlerTests()
    {
        Subject = new JwtAuthenticationHandler(
            OptionsMonitor,
            Substitute.For<ILoggerFactory>(),
            new UrlTestEncoder(),
            new OptionsWrapper<AclOptions>(AclOptions)
        );

        OptionsMonitor
            .Get(JwtAuthenticationHandler.SchemeName)
            .Returns(
                new JwtBearerOptions
                {
                    TokenValidationParameters = new TokenValidationParameters
                    {
                        SignatureValidator = (token, _) => new JsonWebToken(token),
                        ValidateAudience = false,
                        ValidateIssuer = false,
                    },
                }
            );
    }

    [Fact]
    public async Task WhenNoClientId_ShouldSucceed()
    {
        AclOptions.Clients.Add(
            "client",
            new AclOptions.Client { Type = AclOptions.ClientType.OAuth, Scopes = ["scope1", "scope2"] }
        );
        await Subject.InitializeAsync(
            Scheme(),
            new DefaultHttpContext { Request = { Headers = { Authorization = $"Bearer {Jwt.GenerateJwt([])}" } } }
        );

        var result = await Subject.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal.Claims.Any(x => x.Type == Claims.Scope).Should().BeFalse();
    }

    [Theory]
    [InlineData(AclOptions.ClientType.OAuth, true)]
    [InlineData(AclOptions.ClientType.ApiKey, false)]
    public async Task WhenMatchingClientId_AndClientType_ShouldMapClaims(
        AclOptions.ClientType type,
        bool shouldMapClaims
    )
    {
        const string client = "client";
        AclOptions.Clients.Add(client, new AclOptions.Client { Type = type, Scopes = ["scope1", "scope2"] });
        var claims = new[] { new Claim(Claims.ClientId, client) };
        await Subject.InitializeAsync(
            Scheme(),
            new DefaultHttpContext { Request = { Headers = { Authorization = $"Bearer {Jwt.GenerateJwt(claims)}" } } }
        );

        var result = await Subject.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();

        if (shouldMapClaims)
        {
            result
                .Principal.Claims.FirstOrDefault(x => x is { Type: Claims.Scope, Value: "scope1" })
                .Should()
                .NotBeNull();
            result
                .Principal.Claims.FirstOrDefault(x => x is { Type: Claims.Scope, Value: "scope2" })
                .Should()
                .NotBeNull();
        }
    }

    [Fact]
    public async Task WhenNoMatchingClientId_ShouldSucceed()
    {
        AclOptions.Clients.Add(
            "client",
            new AclOptions.Client { Type = AclOptions.ClientType.OAuth, Scopes = ["scope1", "scope2"] }
        );
        var claims = new[] { new Claim(Claims.ClientId, "different-client") };
        await Subject.InitializeAsync(
            Scheme(),
            new DefaultHttpContext { Request = { Headers = { Authorization = $"Bearer {Jwt.GenerateJwt(claims)}" } } }
        );

        var result = await Subject.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal.Claims.Any(x => x.Type == Claims.Scope).Should().BeFalse();
    }

    private static AuthenticationScheme Scheme()
    {
        return new AuthenticationScheme("Bearer", "Bearer", typeof(JwtAuthenticationHandler));
    }
}
