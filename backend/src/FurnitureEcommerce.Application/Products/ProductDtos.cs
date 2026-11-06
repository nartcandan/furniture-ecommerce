namespace FurnitureEcommerce.Application.Products;

public record ProductResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    int StockQuantity,
    decimal? WidthCm,
    decimal? DepthCm,
    decimal? HeightCm,
    decimal? WeightKg,
    string? Material,
    string? Color,
    bool IsActive);

public interface IProductRequest
{
    Guid CategoryId { get; }
    string Name { get; }
    string? Description { get; }
    decimal Price { get; }
    decimal? WidthCm { get; }
    decimal? DepthCm { get; }
    decimal? HeightCm { get; }
    decimal? WeightKg { get; }
    string? Material { get; }
    string? Color { get; }
}

public record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    decimal? WidthCm,
    decimal? DepthCm,
    decimal? HeightCm,
    decimal? WeightKg,
    string? Material,
    string? Color) : IProductRequest;

public record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    decimal? WidthCm,
    decimal? DepthCm,
    decimal? HeightCm,
    decimal? WeightKg,
    string? Material,
    string? Color,
    bool IsActive) : IProductRequest;

public record AdjustStockRequest(int Change);

public record ProductQuery
{
    public string? Category { get; init; }
    public string? Search { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
