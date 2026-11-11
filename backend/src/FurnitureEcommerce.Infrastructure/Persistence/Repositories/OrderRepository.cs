using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FurnitureEcommerce.Infrastructure.Persistence.Repositories;

public class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
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
