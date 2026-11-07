namespace FurnitureEcommerce.Application.Auth;

public interface ITokenService
{
    AccessToken CreateToken(AuthUser user);
}
