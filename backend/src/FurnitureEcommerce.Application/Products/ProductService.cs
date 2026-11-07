using FluentValidation;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Products;

public class ProductService(
    IProductRepository repository,
    ICategoryRepository categoryRepository,
    IFileStorage fileStorage,
    IValidator<ProductQuery> queryValidator,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator,
    IValidator<AdjustStockRequest> stockValidator) : IProductService
{
    public async Task<PagedResult<ProductResponse>> GetPagedAsync(
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);

        return ToPagedResponse(await repository.GetActivePagedAsync(query, cancellationToken));
    }

    public async Task<PagedResult<ProductResponse>> GetAllPagedAsync(
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);

        return ToPagedResponse(await repository.GetAllPagedAsync(query, cancellationToken));
    }

    public async Task<ProductResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var product = await repository.GetActiveBySlugAsync(slug, cancellationToken)
            ?? throw new NotFoundException($"Product '{slug}' was not found.");

        return ToResponse(product);
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");

        return ToResponse(product);
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var category = await GetCategoryAsync(request.CategoryId, cancellationToken);

        var product = new Product
        {
            Category = category,
            Name = request.Name,
            Slug = await SlugGenerator.GenerateUniqueAsync(
                request.Name,
                repository.SlugExistsAsync,
                cancellationToken),
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            WidthCm = request.WidthCm,
            DepthCm = request.DepthCm,
            HeightCm = request.HeightCm,
            WeightKg = request.WeightKg,
            Material = request.Material,
            Color = request.Color
        };

        await repository.AddAsync(product, cancellationToken);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var product = await GetExistingAsync(id, cancellationToken);
        var category = await GetCategoryAsync(request.CategoryId, cancellationToken);

        // The slug stays the same so that existing links keep working.
        // Stock is changed only through AdjustStockAsync so that concurrent sales are not overwritten.
        product.Category = category;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.WidthCm = request.WidthCm;
        product.DepthCm = request.DepthCm;
        product.HeightCm = request.HeightCm;
        product.WeightKg = request.WeightKg;
        product.Material = request.Material;
        product.Color = request.Color;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(product, cancellationToken);
        return ToResponse(product);
    }

    public async Task<ProductResponse> AdjustStockAsync(
        Guid id,
        AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        await stockValidator.ValidateAndThrowAsync(request, cancellationToken);

        var adjusted = await repository.TryAdjustStockAsync(id, request.Change, cancellationToken);
        var product = await GetExistingAsync(id, cancellationToken);

        if (!adjusted)
        {
            throw new ConflictException(
                $"Stock cannot go below zero. Current stock is {product.StockQuantity}.");
        }

        return ToResponse(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await GetExistingAsync(id, cancellationToken);

        // Products are never removed because past orders refer to them.
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(product, cancellationToken);
    }

    private async Task<Product> GetExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");
    }

    private async Task<Category> GetCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        return await categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category '{id}' was not found.");
    }

    private PagedResult<ProductResponse> ToPagedResponse(PagedResult<Product> products)
    {
        return new PagedResult<ProductResponse>(
            products.Items.Select(ToResponse).ToList(),
            products.Page,
            products.PageSize,
            products.TotalCount);
    }

    private ProductResponse ToResponse(Product product) => new(
        product.Id,
        product.Category.Id,
        product.Category.Name,
        product.Category.Slug,
        product.Name,
        product.Slug,
        product.Description,
        product.Price,
        product.StockQuantity,
        product.WidthCm,
        product.DepthCm,
        product.HeightCm,
        product.WeightKg,
        product.Material,
        product.Color,
        product.IsActive,
        product.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => new ProductImageResponse(
                image.Id,
                fileStorage.GetUrl(image.ImagePath),
                image.AltText,
                image.SortOrder))
            .ToList());
}
