using FluentValidation;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Orders;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.AddressId).NotEmpty();
    }
}

public class AdminOrderQueryValidator : AbstractValidator<AdminOrderQuery>
{
    private static readonly string[] StatusNames = Enum.GetNames<OrderStatus>();

    public AdminOrderQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);

        // Names only. Enum.Parse would also accept numbers such as "3".
        RuleFor(query => query.Status)
            .Must(status => string.IsNullOrWhiteSpace(status)
                || StatusNames.Contains(status, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", StatusNames)}.");
    }
}
