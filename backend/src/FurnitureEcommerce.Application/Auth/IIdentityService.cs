namespace FurnitureEcommerce.Application.Auth;

public interface IIdentityService
{
    Task<AuthUser> CreateUserAsync(NewUser newUser);

    /// <summary>Returns null when the email, the password or the account state does not allow a login.</summary>
    Task<AuthUser?> ValidateCredentialsAsync(string email, string password);
}
