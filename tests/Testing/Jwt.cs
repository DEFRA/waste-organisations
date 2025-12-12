using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Defra.WasteOrganisations.Testing;

public static class Jwt
{
    public static string GenerateJwt(Claim[] claims)
    {
        var rand = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(rand);

        var token = new JwtSecurityToken(
            issuer: "Local",
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(rand), SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
