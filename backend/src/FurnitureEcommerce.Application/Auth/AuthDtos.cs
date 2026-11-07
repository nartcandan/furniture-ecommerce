namespace FurnitureEcommerce.Application.Auth;

public record RegisterRequest(string FirstName, string LastName, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record UserResponse(Guid Id, string Email, string FirstName, string LastName);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);

public record NewUser(string FirstName, string LastName, string Email, string Password);

public record AuthUser(Guid Id, string Email, string FirstName, string LastName, IReadOnlyList<string> Roles);

public record AccessToken(string Value, DateTime ExpiresAt);
