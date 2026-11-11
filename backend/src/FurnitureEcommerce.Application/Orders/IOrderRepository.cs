using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Orders;

public interface IOrderRepository
{
    /// <summary>
    /// Saves the order and takes its items out of stock in one transaction.
    /// A product is taken only if it can still be sold, has enough stock and still has the
    /// price copied into the order. If any product fails, nothing is saved.
    /// </summary>
    /// <returns>Null on success, otherwise the id of the first product that could not be taken.</returns>
    /// <exception cref="Common.Exceptions.ConflictException">
    /// The user already has an order that is waiting for payment.
    /// </exception>
    Task<Guid?> CreateAndReserveStockAsync(Order order, CancellationToken cancellationToken);

    Task SetPaymentTokenAsync(Guid orderId, string paymentToken, CancellationToken cancellationToken);

    /// <summary>Returns the order with its items, or null if no order has this payment token.</summary>
    Task<Order?> GetByPaymentTokenAsync(string paymentToken, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the order from PendingPayment to Paid and, in the same transaction, takes the
    /// ordered quantities out of the user's cart. Does nothing if the order is not waiting for payment.
    /// </summary>
    /// <returns>True only for the one call that changed the order.</returns>
    Task<bool> TryMarkPaidAsync(Guid orderId, string paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the order from PendingPayment to PaymentFailed and, in the same transaction, puts its
    /// items back in stock. Does nothing if the order is not waiting for payment.
    /// </summary>
    /// <returns>True only for the one call that changed the order.</returns>
    Task<bool> TryMarkPaymentFailedAsync(Guid orderId, string error, CancellationToken cancellationToken);

    /// <summary>Moves the user's order from Paid to Cancelling, which also blocks shipping.</summary>
    /// <returns>True only for the one call that changed the order.</returns>
    Task<bool> TryBeginCancellationAsync(Guid orderId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the order from Cancelling to Cancelled and, in the same transaction, puts its items back in stock.
    /// </summary>
    /// <returns>True only for the one call that changed the order.</returns>
    Task<bool> TryCompleteCancellationAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Moves the order from Cancelling back to Paid, because the payment could not be refunded.</summary>
    Task<bool> TryRevertCancellationAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Changes the status only if the order is currently in the expected status.</summary>
    /// <returns>True only for the one call that changed the order.</returns>
    Task<bool> TryChangeStatusAsync(Guid orderId, OrderStatus from, OrderStatus to, CancellationToken cancellationToken);

    /// <summary>Returns the orders of all users with their items, newest first.</summary>
    Task<PagedResult<Order>> GetPagedAsync(OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Returns any user's order with its items. For admin use.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns the user's orders with their items, newest first.</summary>
    Task<List<Order>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns the order with its items, or null if it is missing or belongs to another user.</summary>
    Task<Order?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}
