using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FurnitureEcommerce.Infrastructure.Persistence.Repositories;

public class OrderRepository(AppDbContext dbContext, ReconciliationSettings reconciliationSettings) : IOrderRepository
{
    // Matches the column size in OrderConfiguration.
    private const int MaxPaymentErrorLength = 500;

    public async Task<Guid?> CreateAndReserveStockAsync(Order order, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Orders.Add(order);

        try
        {
            // The order is saved first. A second simultaneous request stops here on the unique
            // index, before it touches any stock.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("You already have an order that is waiting for payment.");
        }

        var updatedAt = DateTime.UtcNow;

        // Products are always updated in the same order, so two orders cannot deadlock each other.
        foreach (var item in order.Items.OrderBy(item => item.ProductId))
        {
            var productId = item.ProductId;
            var quantity = item.Quantity;
            var unitPrice = item.UnitPrice;

            // One statement checks and takes the stock, so it can never go below zero.
            var affectedRows = await dbContext.Products
                .Where(Product.SellableFilter)
                .Where(product => product.Id == productId
                    && product.StockQuantity >= quantity
                    && product.Price == unitPrice)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(product => product.StockQuantity, product => product.StockQuantity - quantity)
                        .SetProperty(product => product.UpdatedAt, updatedAt),
                    cancellationToken);

            if (affectedRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return productId;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public Task SetPaymentPageAsync(
        Guid orderId,
        string paymentToken,
        string paymentPageUrl,
        CancellationToken cancellationToken)
    {
        var updatedAt = DateTime.UtcNow;

        return dbContext.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(order => order.PaymentToken, paymentToken)
                    .SetProperty(order => order.PaymentPageUrl, paymentPageUrl)
                    .SetProperty(order => order.UpdatedAt, updatedAt),
                cancellationToken);
    }

    public Task<Order?> GetByPaymentTokenAsync(string paymentToken, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.PaymentToken == paymentToken, cancellationToken);
    }

    public async Task<bool> TryMarkPaidAsync(Guid orderId, string paymentId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // The status in the WHERE clause makes this safe against repeated and simultaneous callbacks:
        // the database lets only one of them change the row.
        var affectedRows = await dbContext.Orders
            .Where(order => order.Id == orderId && order.Status == OrderStatus.PendingPayment)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(order => order.Status, OrderStatus.Paid)
                    .SetProperty(order => order.PaymentId, paymentId)
                    .SetProperty(order => order.PaidAt, now)
                    .SetProperty(order => order.UpdatedAt, now),
                cancellationToken);

        if (affectedRows == 0)
        {
            return false;
        }

        var userId = await dbContext.Orders
            .Where(order => order.Id == orderId)
            .Select(order => order.UserId)
            .FirstAsync(cancellationToken);

        var items = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.OrderId == orderId)
            .ToListAsync(cancellationToken);

        // Only what was ordered leaves the cart. Anything added after the order stays.
        foreach (var item in items)
        {
            var productId = item.ProductId;
            var quantity = item.Quantity;

            var cartItems = dbContext.CartItems
                .Where(cartItem => cartItem.Cart.UserId == userId && cartItem.ProductId == productId);

            // Delete first. In the other order, a reduced row could be deleted by mistake.
            await cartItems
                .Where(cartItem => cartItem.Quantity <= quantity)
                .ExecuteDeleteAsync(cancellationToken);

            await cartItems
                .Where(cartItem => cartItem.Quantity > quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(cartItem => cartItem.Quantity, cartItem => cartItem.Quantity - quantity),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<bool> TryMarkPaymentFailedAsync(Guid orderId, string error, CancellationToken cancellationToken)
    {
        return TryCloseUnpaidAsync(orderId, OrderStatus.PaymentFailed, error, cancellationToken);
    }

    public Task<bool> TryCancelUnpaidAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        return TryCloseUnpaidAsync(orderId, OrderStatus.Cancelled, reason, cancellationToken);
    }

    public Task<List<Order>> GetPendingPaymentCreatedBeforeAsync(
        DateTime createdBefore,
        int limit,
        CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.PendingPayment && order.CreatedAt < createdBefore)
            .OrderBy(order => order.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Order>> GetCancellingUpdatedBeforeAsync(
        DateTime updatedBefore,
        int limit,
        CancellationToken cancellationToken)
    {
        // UpdatedAt is set when the order enters Cancelling.
        return dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Cancelling && order.UpdatedAt < updatedBefore)
            .OrderBy(order => order.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    // The only way out of PendingPayment without a payment. The stock goes back in the same transaction.
    private async Task<bool> TryCloseUnpaidAsync(
        Guid orderId,
        OrderStatus closedStatus,
        string error,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var storedError = error.Length > MaxPaymentErrorLength ? error[..MaxPaymentErrorLength] : error;

        var affectedRows = await dbContext.Orders
            .Where(order => order.Id == orderId && order.Status == OrderStatus.PendingPayment)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(order => order.Status, closedStatus)
                    .SetProperty(order => order.PaymentError, storedError)
                    .SetProperty(order => order.UpdatedAt, now),
                cancellationToken);

        if (affectedRows == 0)
        {
            return false;
        }

        await RestockAsync(orderId, now, cancellationToken);

        // The stock is free again, but a payment page that was opened for this order may still be open.
        // A payment made there normally comes back through the callback and is refunded. If that callback
        // is lost, nobody would notice the payment, so the provider is asked once more after the page
        // has expired. An order without a page, or one closed after that time, needs no recheck.
        var pageOpenedAt = await dbContext.Orders
            .Where(order => order.Id == orderId && order.PaymentToken != null)
            .Select(order => (DateTime?)order.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (pageOpenedAt + reconciliationSettings.PendingPaymentTimeout is { } recheckAt && recheckAt > now)
        {
            await dbContext.Orders
                .Where(order => order.Id == orderId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(order => order.PaymentRecheckAt, recheckAt),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<List<Order>> GetPaymentRecheckDueAsync(
        DateTime dueBefore,
        int limit,
        CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Where(order => order.PaymentRecheckAt < dueBefore)
            .OrderBy(order => order.PaymentRecheckAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task ClearPaymentRecheckAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(order => order.PaymentRecheckAt, (DateTime?)null),
                cancellationToken);
    }

    public async Task<bool> TryBeginCancellationAsync(Guid orderId, Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var affectedRows = await dbContext.Orders
            .Where(order => order.Id == orderId && order.UserId == userId && order.Status == OrderStatus.Paid)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(order => order.Status, OrderStatus.Cancelling)
                    .SetProperty(order => order.UpdatedAt, now),
                cancellationToken);

        return affectedRows > 0;
    }

    public async Task<bool> TryCompleteCancellationAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var affectedRows = await dbContext.Orders
            .Where(order => order.Id == orderId && order.Status == OrderStatus.Cancelling)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(order => order.Status, OrderStatus.Cancelled)
                    .SetProperty(order => order.UpdatedAt, now),
                cancellationToken);

        if (affectedRows == 0)
        {
            return false;
        }

        await RestockAsync(orderId, now, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryRevertCancellationAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var affectedRows = await dbContext.Orders
            .Where(order => order.Id == orderId && order.Status == OrderStatus.Cancelling)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(order => order.Status, OrderStatus.Paid)
                    .SetProperty(order => order.UpdatedAt, now),
                cancellationToken);

        return affectedRows > 0;
    }

    // Must run inside the transaction that changed the order's status. That status check lets only
    // one call through, so the stock goes back exactly once.
    private async Task RestockAsync(Guid orderId, DateTime now, CancellationToken cancellationToken)
    {
        var items = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.OrderId == orderId)
            .OrderBy(item => item.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var productId = item.ProductId;
            var quantity = item.Quantity;

            await dbContext.Products
                .Where(product => product.Id == productId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(product => product.StockQuantity, product => product.StockQuantity + quantity)
                        .SetProperty(product => product.UpdatedAt, now),
                    cancellationToken);
        }
    }

    public async Task<bool> TryChangeStatusAsync(
        Guid orderId,
        OrderStatus from,
        OrderStatus to,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var affectedRows = await dbContext.Orders
            .Where(order => order.Id == orderId && order.Status == from)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(order => order.Status, to)
                    .SetProperty(order => order.UpdatedAt, now),
                cancellationToken);

        return affectedRows > 0;
    }

    public async Task<PagedResult<Order>> GetPagedAsync(
        OrderStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var orders = dbContext.Orders.AsNoTracking();

        if (status is { } wanted)
        {
            orders = orders.Where(order => order.Status == wanted);
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        // Id is the tie-breaker so that pages stay stable when two orders share a timestamp.
        var items = await orders
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, page, pageSize, totalCount);
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public Task<List<Order>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id && order.UserId == userId, cancellationToken);
    }
}
