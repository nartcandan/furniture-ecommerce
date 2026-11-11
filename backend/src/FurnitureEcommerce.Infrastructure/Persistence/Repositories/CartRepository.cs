using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnitureEcommerce.Infrastructure.Persistence.Repositories;

public class CartRepository(AppDbContext dbContext) : ICartRepository
{
    public Task<List<CartItem>> GetItemsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.CartItems
            .AsNoTracking()
            .Where(item => item.Cart.UserId == userId)
            .Include(item => item.Product)
                .ThenInclude(product => product.Category)
            // The cart shows only the main image, which is the one with the lowest sort order.
            .Include(item => item.Product)
                .ThenInclude(product => product.Images.OrderBy(image => image.SortOrder).Take(1))
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<CartItem?> GetItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        return dbContext.CartItems
            .AsNoTracking()
            .Include(item => item.Product)
            .FirstOrDefaultAsync(
                item => item.Cart.UserId == userId && item.ProductId == productId,
                cancellationToken);
    }

    public async Task<bool> TryAddItemAsync(
        Guid userId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var cartId = await GetOrCreateCartIdAsync(userId, cancellationToken);
        var itemId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        // One statement inserts the item or increases its quantity, and both paths check the stock.
        // Two simultaneous requests for the same product therefore cannot lose an update,
        // hit the unique index, or push the quantity above the stock.
        var affectedRows = await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "CartItems" ("Id", "CartId", "ProductId", "Quantity", "CreatedAt")
            SELECT {itemId}, {cartId}, product."Id", {quantity}, {now}
            FROM "Products" AS product
            WHERE product."Id" = {productId} AND product."StockQuantity" >= {quantity}
            ON CONFLICT ("CartId", "ProductId") DO UPDATE
            SET "Quantity" = "CartItems"."Quantity" + EXCLUDED."Quantity"
            WHERE "CartItems"."Quantity" + EXCLUDED."Quantity" <= (
                SELECT product."StockQuantity"
                FROM "Products" AS product
                WHERE product."Id" = EXCLUDED."ProductId")
            """,
            cancellationToken);

        if (affectedRows == 0)
        {
            return false;
        }

        await TouchCartAsync(userId, cancellationToken);
        return true;
    }

    public async Task<bool> TrySetItemQuantityAsync(
        Guid userId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.CartItems
            .Where(item => item.Cart.UserId == userId
                && item.ProductId == productId
                && item.Product.StockQuantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Quantity, quantity),
                cancellationToken);

        if (affectedRows == 0)
        {
            return false;
        }

        await TouchCartAsync(userId, cancellationToken);
        return true;
    }

    public async Task<bool> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.CartItems
            .Where(item => item.Cart.UserId == userId && item.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);

        if (affectedRows == 0)
        {
            return false;
        }

        await TouchCartAsync(userId, cancellationToken);
        return true;
    }

    public async Task ClearAsync(Guid userId, CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.CartItems
            .Where(item => item.Cart.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (affectedRows > 0)
        {
            await TouchCartAsync(userId, cancellationToken);
        }
    }

    // The cart is created on the first add, not at registration.
    private async Task<Guid> GetOrCreateCartIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existingId = await FindCartIdAsync(userId, cancellationToken);

        if (existingId is { } id)
        {
            return id;
        }

        var cartId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        // If another request created the cart in the meantime, this does nothing and the cart is read again.
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "Carts" ("Id", "UserId", "CreatedAt")
            VALUES ({cartId}, {userId}, {now})
            ON CONFLICT ("UserId") DO NOTHING
            """,
            cancellationToken);

        return await FindCartIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"The cart of user '{userId}' could not be created.");
    }

    private async Task<Guid?> FindCartIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Carts
            .Where(cart => cart.UserId == userId)
            .Select(cart => (Guid?)cart.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task TouchCartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var updatedAt = DateTime.UtcNow;

        return dbContext.Carts
            .Where(cart => cart.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(cart => cart.UpdatedAt, updatedAt),
                cancellationToken);
    }
}
