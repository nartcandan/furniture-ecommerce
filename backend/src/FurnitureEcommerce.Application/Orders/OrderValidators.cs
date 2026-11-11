using FluentValidation;

namespace FurnitureEcommerce.Application.Orders;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.AddressId).NotEmpty();
    }
}
