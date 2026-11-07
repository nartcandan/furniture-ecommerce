using FluentValidation;

namespace FurnitureEcommerce.Application.Addresses;

public class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(50);
        RuleFor(request => request.RecipientFirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.RecipientLastName).NotEmpty().MaximumLength(100);

        // Digits only, with an optional leading "+". The client removes spaces before sending.
        RuleFor(request => request.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("Phone number must contain 10 to 15 digits and may start with '+'.");

        RuleFor(request => request.City).NotEmpty().MaximumLength(50);
        RuleFor(request => request.District).NotEmpty().MaximumLength(50);
        RuleFor(request => request.AddressLine).NotEmpty().MaximumLength(500);

        // Turkish postal codes have five digits.
        RuleFor(request => request.PostalCode)
            .Matches(@"^[0-9]{5}$")
            .When(request => request.PostalCode is not null)
            .WithMessage("Postal code must contain five digits.");
    }
}
