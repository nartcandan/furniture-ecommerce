using FurnitureEcommerce.Application.Common;

namespace FurnitureEcommerce.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> GetPagedAsync(ProductQuery query, CancellationToken cancellationToken);
    Task<PagedResult<ProductResponse>> GetAllPagedAsync(ProductQuery query, CancellationToken cancellationToken);
    Task<ProductResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task<ProductResponse> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
