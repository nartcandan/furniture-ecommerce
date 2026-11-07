using System.Security.Claims;
using System.Text;
using FurnitureEcommerce.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FurnitureEcommerce.Infrastructure.Identity;

public class JwtTokenService(JwtOptions options) : ITokenService
{
    private const string RoleClaimType = "role";

    private readonly JsonWebTokenHandler tokenHandler = new();
    private readonly SigningCredentials signingCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public AccessToken CreateToken(AuthUser user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(RoleClaimType, role)));

        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = signingCredentials
        });

        return new AccessToken(token, expiresAt);
    }
}
