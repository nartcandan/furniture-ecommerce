namespace FurnitureEcommerce.Application.Carts;

public interface ICartService
{
    Task<CartResponse> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<CartResponse> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken cancellationToken);

    Task<CartResponse> SetItemQuantityAsync(
        Guid userId,
        Guid productId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken);

    Task<CartResponse> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken);

    Task ClearAsync(Guid userId, CancellationToken cancellationToken);
}
