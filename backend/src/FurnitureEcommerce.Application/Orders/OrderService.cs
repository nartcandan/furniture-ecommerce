using FluentValidation;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Orders;

public class OrderService(
    IOrderRepository repository,
    ICartRepository cartRepository,
    IAddressRepository addressRepository,
    IValidator<CreateOrderRequest> createValidator) : IOrderService
{
    public async Task<OrderResponse> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Another user's address gives the same 404 as a missing one.
        var address = await addressRepository.GetForUserAsync(request.AddressId, userId, cancellationToken)
            ?? throw new NotFoundException($"Address '{request.AddressId}' was not found.");

        var cartItems = await cartRepository.GetItemsAsync(userId, cancellationToken);

        if (cartItems.Count == 0)
        {
            throw new ConflictException("The cart is empty.");
        }

        // The same check that sets canCheckout in the cart. It gives a clear message early,
        // but the repository makes the final decision while it takes the stock.
        var blockedItem = cartItems.FirstOrDefault(item => !item.CanBeOrdered);

        if (blockedItem is not null)
        {
            throw Unavailable(blockedItem.Product.Name);
        }

        // Name and price are read from the product here, never taken from the client or the cart.
        var order = new Order
        {
            UserId = userId,
            RecipientFirstName = address.RecipientFirstName,
            RecipientLastName = address.RecipientLastName,
            PhoneNumber = address.PhoneNumber,
            City = address.City,
            District = address.District,
            AddressLine = address.AddressLine,
            PostalCode = address.PostalCode,
            TotalAmount = cartItems.Sum(item => item.Product.Price * item.Quantity),
            Items = cartItems
                .Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    UnitPrice = item.Product.Price,
                    Quantity = item.Quantity
                })
                .ToList()
        };

        // The cart is left as it is. It is emptied when the order is paid.
        var unavailableProductId = await repository.CreateAndReserveStockAsync(order, cancellationToken);

        if (unavailableProductId is { } productId)
        {
            throw Unavailable(order.Items.First(item => item.ProductId == productId).ProductName);
        }

        return ToResponse(order);
    }

    public async Task<List<OrderSummaryResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var orders = await repository.GetByUserAsync(userId, cancellationToken);

        return orders
            .Select(order => new OrderSummaryResponse(
                order.Id,
                order.Status.ToString(),
                order.TotalAmount,
                order.Items.Sum(item => item.Quantity),
                order.CreatedAt))
            .ToList();
    }

    public async Task<OrderResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var order = await repository.GetForUserAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException($"Order '{id}' was not found.");

        return ToResponse(order);
    }

    private static ConflictException Unavailable(string productName) =>
        new($"'{productName}' has changed or is no longer available in the requested quantity. Review your cart.");

    private static OrderResponse ToResponse(Order order) => new(
        order.Id,
        order.Status.ToString(),
        order.TotalAmount,
        order.RecipientFirstName,
        order.RecipientLastName,
        order.PhoneNumber,
        order.City,
        order.District,
        order.AddressLine,
        order.PostalCode,
        order.PaymentError,
        order.PaidAt,
        order.CreatedAt,
        // Order items have no position of their own, so the name gives a stable order.
        order.Items
            .OrderBy(item => item.ProductName, StringComparer.Ordinal)
            .ThenBy(item => item.ProductId)
            .Select(item => new OrderItemResponse(
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.UnitPrice * item.Quantity))
            .ToList());
}
