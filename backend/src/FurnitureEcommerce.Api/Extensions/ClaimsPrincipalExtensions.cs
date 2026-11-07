using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FurnitureEcommerce.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the user id from the token's "sub" claim. Only call it on authorized endpoints.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("The token has no valid 'sub' claim.");
    }
}
