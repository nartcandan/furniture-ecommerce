using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Carts;

public interface ICartRepository
{
    /// <summary>
    /// Returns the user's cart items in the order they were added, each with its product,
    /// the product's category and main image. Empty if the user has no cart yet.
    /// </summary>
    Task<List<CartItem>> GetItemsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns one cart item with its product, or null if the product is not in the cart.</summary>
    Task<CartItem?> GetItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically adds the quantity to the cart, creating the cart and the item when needed,
    /// unless the item's total quantity would exceed the product's stock.
    /// </summary>
    /// <returns>True if the cart was changed.</returns>
    Task<bool> TryAddItemAsync(Guid userId, Guid productId, int quantity, CancellationToken cancellationToken);

    /// <summary>Atomically sets the quantity of an item unless it would exceed the product's stock.</summary>
    /// <returns>True if the item was changed. False if it is not in the cart or the stock is too low.</returns>
    Task<bool> TrySetItemQuantityAsync(Guid userId, Guid productId, int quantity, CancellationToken cancellationToken);

    /// <returns>True if the item was in the cart.</returns>
    Task<bool> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken);

    Task ClearAsync(Guid userId, CancellationToken cancellationToken);
}
