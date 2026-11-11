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

    /// <summary>Returns the user's orders with their items, newest first.</summary>
    Task<List<Order>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns the order with its items, or null if it is missing or belongs to another user.</summary>
    Task<Order?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}
