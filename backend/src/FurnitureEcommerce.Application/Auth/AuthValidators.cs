using FluentValidation;

namespace FurnitureEcommerce.Application.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Email).NotEmpty().MaximumLength(256).EmailAddress();

        // The remaining password rules are configured in Identity.
        RuleFor(request => request.Password).NotEmpty().MaximumLength(128);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(256);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(128);
    }
}
