using FluentValidation;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Application.Orders;

public class OrderService(
    IOrderRepository repository,
    ICartRepository cartRepository,
    IAddressRepository addressRepository,
    IIdentityService identityService,
    IPaymentGateway paymentGateway,
    PaymentSettings paymentSettings,
    TimeProvider timeProvider,
    ILogger<OrderService> logger,
    IValidator<CreateOrderRequest> createValidator) : IOrderService
{
    public async Task<OrderResponse> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await identityService.GetUserAsync(userId)
            ?? throw new UnauthorizedException("The account of this token no longer exists.");

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

        // The provider is called only after the transaction has ended, so a slow or failed
        // call never holds database locks.
        var buyer = new PaymentBuyer(user.Id, user.FirstName, user.LastName, user.Email, ipAddress);
        var checkout = await paymentGateway.StartCheckoutAsync(
            new CheckoutRequest(order, buyer, paymentSettings.CallbackUrl),
            cancellationToken);

        if (!checkout.IsSuccess)
        {
            // Without a payment page nobody can pay this order, so the stock goes back right away.
            await repository.TryMarkPaymentFailedAsync(
                order.Id,
                checkout.Error ?? "The payment could not be started.",
                cancellationToken);

            throw new PaymentGatewayException("The payment could not be started. Nothing was charged, try again.");
        }

        await repository.SetPaymentTokenAsync(order.Id, checkout.Token!, cancellationToken);

        return ToResponse(order, checkout.PaymentPageUrl);
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

    public async Task<OrderResponse> CancelAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var order = await repository.GetForUserAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException($"Order '{id}' was not found.");

        if (order.Status != OrderStatus.Paid)
        {
            throw new ConflictException("Only a paid order that has not been shipped can be cancelled.");
        }

        if (!order.CanBeCancelled(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new ConflictException("An order can only be cancelled on the day it was paid.");
        }

        // Cancelling is taken before the provider is called. From here on the order cannot be
        // shipped, and a second cancel request stops at this line.
        if (!await repository.TryBeginCancellationAsync(id, userId, cancellationToken))
        {
            throw new ConflictException("The order has changed and can no longer be cancelled.");
        }

        PaymentCancellation cancellation;

        try
        {
            cancellation = await paymentGateway.CancelPaymentAsync(order.PaymentId!, cancellationToken);
        }
        catch (PaymentGatewayException exception)
        {
            // The refund may or may not have happened. The order stays in Cancelling and the
            // reconciliation job finishes it, so the request is not repeated blindly here.
            logger.LogError(exception, "The cancellation of order {OrderId} could not be confirmed.", id);
            return await GetByIdAsync(userId, id, cancellationToken);
        }

        if (!cancellation.IsSuccess)
        {
            await repository.TryRevertCancellationAsync(id, cancellationToken);

            logger.LogWarning(
                "The payment of order {OrderId} could not be refunded: {Error}", id, cancellation.Error);

            throw new PaymentGatewayException(
                "The payment could not be refunded, so the order was not cancelled. Contact support.");
        }

        await repository.TryCompleteCancellationAsync(id, cancellationToken);

        return await GetByIdAsync(userId, id, cancellationToken);
    }

    private static ConflictException Unavailable(string productName) =>
        new($"'{productName}' has changed or is no longer available in the requested quantity. Review your cart.");

    private OrderResponse ToResponse(Order order, string? paymentPageUrl = null) => new(
        order.Id,
        order.Status.ToString(),
        order.TotalAmount,
        paymentPageUrl,
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
        order.CanBeCancelled(timeProvider.GetUtcNow().UtcDateTime),
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
