using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Products;

public interface IProductRepository
{
    Task<PagedResult<Product>> GetActivePagedAsync(ProductQuery query, CancellationToken cancellationToken);
    Task<Product?> GetActiveBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ConflictException">The slug is in use or the category was removed.</exception>
    Task AddAsync(Product product, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ConflictException">The category was removed.</exception>
    Task UpdateAsync(Product product, CancellationToken cancellationToken);

    /// <summary>Atomically adds the change to the stock unless the result would be negative.</summary>
    /// <returns>True if the stock was changed.</returns>
    Task<bool> TryAdjustStockAsync(Guid id, int change, CancellationToken cancellationToken);
}
