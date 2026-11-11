using FluentValidation;

namespace FurnitureEcommerce.Application.Carts;

public class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.ProductId).NotEmpty();
        RuleFor(request => request.Quantity).GreaterThan(0);
    }
}

public class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        // Zero is not a way to remove a product. The DELETE endpoint does that.
        RuleFor(request => request.Quantity).GreaterThan(0);
    }
}
