using FluentValidation;
using FurnitureEcommerce.Application.Common.Exceptions;

namespace FurnitureEcommerce.Application.Auth;

public class AuthService(
    IIdentityService identityService,
    ITokenService tokenService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await identityService.CreateUserAsync(new NewUser(
            request.FirstName,
            request.LastName,
            request.Email.Trim(),
            request.Password));

        return ToResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        // One message for every failure, so a login does not reveal which emails are registered.
        var user = await identityService.ValidateCredentialsAsync(request.Email.Trim(), request.Password)
            ?? throw new UnauthorizedException("Invalid email or password.");

        return ToResponse(user);
    }

    private AuthResponse ToResponse(AuthUser user)
    {
        var token = tokenService.CreateToken(user);

        return new AuthResponse(
            token.Value,
            token.ExpiresAt,
            new UserResponse(user.Id, user.Email, user.FirstName, user.LastName, user.Roles));
    }
}
