using FluentValidation;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Carts;

public class CartService(
    ICartRepository repository,
    IProductRepository productRepository,
    IFileStorage fileStorage,
    IValidator<AddCartItemRequest> addValidator,
    IValidator<UpdateCartItemRequest> updateValidator) : ICartService
{
    public async Task<CartResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await repository.GetItemsAsync(userId, cancellationToken);
        var lines = items.Select(ToResponse).ToList();

        return new CartResponse(
            lines,
            lines.Sum(line => line.Quantity),
            lines.Sum(line => line.LineTotal),
            lines.Count > 0 && lines.All(line => line.IsAvailable && !line.HasInsufficientStock));
    }

    public async Task<CartResponse> AddItemAsync(
        Guid userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        await addValidator.ValidateAndThrowAsync(request, cancellationToken);

        // A product that cannot be sold looks the same as a missing one, like in the public catalog.
        var product = await productRepository.GetActiveByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product '{request.ProductId}' was not found.");

        // This check does not reserve stock. The final decision is made when the order is created.
        var added = await repository.TryAddItemAsync(userId, product.Id, request.Quantity, cancellationToken);

        if (!added)
        {
            throw new ConflictException(
                $"Not enough stock. Only {product.StockQuantity} available, including what is already in the cart.");
        }

        return await GetAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> SetItemQuantityAsync(
        Guid userId,
        Guid productId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var updated = await repository.TrySetItemQuantityAsync(
            userId,
            productId,
            request.Quantity,
            cancellationToken);

        if (!updated)
        {
            var item = await repository.GetItemAsync(userId, productId, cancellationToken)
                ?? throw NotInCart(productId);

            throw new ConflictException($"Not enough stock. Only {item.Product.StockQuantity} available.");
        }

        return await GetAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        if (!await repository.RemoveItemAsync(userId, productId, cancellationToken))
        {
            throw NotInCart(productId);
        }

        return await GetAsync(userId, cancellationToken);
    }

    public Task ClearAsync(Guid userId, CancellationToken cancellationToken)
    {
        return repository.ClearAsync(userId, cancellationToken);
    }

    private static NotFoundException NotInCart(Guid productId) =>
        new($"Product '{productId}' is not in the cart.");

    private CartItemResponse ToResponse(CartItem item)
    {
        var product = item.Product;
        var mainImage = product.Images.OrderBy(image => image.SortOrder).FirstOrDefault();

        return new CartItemResponse(
            product.Id,
            product.Name,
            product.Slug,
            mainImage is null ? null : fileStorage.GetUrl(mainImage.ImagePath),
            product.Price,
            item.Quantity,
            product.Price * item.Quantity,
            product.StockQuantity,
            product.IsSellable,
            item.HasInsufficientStock);
    }
}
