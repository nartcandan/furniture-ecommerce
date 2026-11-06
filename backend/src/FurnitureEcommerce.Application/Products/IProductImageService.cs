namespace FurnitureEcommerce.Application.Products;

public interface IProductImageService
{
    /// <param name="content">The file content. The stream must be seekable.</param>
    Task<ProductImageResponse> AddAsync(
        Guid productId,
        Stream content,
        string? altText,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid productId, Guid imageId, CancellationToken cancellationToken);

    Task<List<ProductImageResponse>> ReorderAsync(
        Guid productId,
        ReorderProductImagesRequest request,
        CancellationToken cancellationToken);
}
