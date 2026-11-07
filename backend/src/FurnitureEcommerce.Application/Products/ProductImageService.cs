using FluentValidation;
using FluentValidation.Results;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Products;

public class ProductImageService(
    IProductRepository repository,
    IFileStorage fileStorage) : IProductImageService
{
    public const long MaxFileSizeInBytes = 5 * 1024 * 1024;
    public const int MaxImagesPerProduct = 10;
    private const int MaxAltTextLength = 200;
    private const string ImageFolder = "products";

    public async Task<ProductImageResponse> AddAsync(
        Guid productId,
        Stream content,
        string? altText,
        CancellationToken cancellationToken)
    {
        if (content.Length > MaxFileSizeInBytes)
        {
            throw ValidationError("File", "The file must be 5 MB or smaller.");
        }

        if (altText?.Length > MaxAltTextLength)
        {
            throw ValidationError("AltText", $"Alt text must be {MaxAltTextLength} characters or fewer.");
        }

        var extension = await ImageFormat.DetectExtensionAsync(content, cancellationToken)
            ?? throw ValidationError("File", "The file must be a JPEG, PNG or WebP image.");

        var product = await GetProductAsync(productId, cancellationToken);

        if (product.Images.Count >= MaxImagesPerProduct)
        {
            throw new ConflictException($"A product can have at most {MaxImagesPerProduct} images.");
        }

        var path = await fileStorage.SaveAsync(content, ImageFolder, extension, cancellationToken);

        var image = new ProductImage
        {
            ProductId = product.Id,
            ImagePath = path,
            AltText = altText,
            SortOrder = product.Images.Select(existing => existing.SortOrder + 1).DefaultIfEmpty(0).Max()
        };

        try
        {
            await repository.AddImageAsync(image, cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(path);
            throw;
        }

        return ToResponse(image);
    }

    public async Task DeleteAsync(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var product = await GetProductAsync(productId, cancellationToken);

        var image = product.Images.FirstOrDefault(existing => existing.Id == imageId)
            ?? throw new NotFoundException($"Image '{imageId}' was not found.");

        // The record goes first: a leftover file is harmless, a broken image link is not.
        await repository.DeleteImageAsync(image, cancellationToken);
        await fileStorage.DeleteAsync(image.ImagePath);
    }

    public async Task<List<ProductImageResponse>> ReorderAsync(
        Guid productId,
        ReorderProductImagesRequest request,
        CancellationToken cancellationToken)
    {
        var product = await GetProductAsync(productId, cancellationToken);

        var currentIds = product.Images.Select(image => image.Id).Order();

        if (request.ImageIds is null || !currentIds.SequenceEqual(request.ImageIds.Order()))
        {
            throw ValidationError("ImageIds", "The list must contain every image of the product exactly once.");
        }

        var images = product.Images.ToDictionary(image => image.Id);

        for (var index = 0; index < request.ImageIds.Count; index++)
        {
            images[request.ImageIds[index]].SortOrder = index;
        }

        await repository.UpdateAsync(product, cancellationToken);

        return product.Images
            .OrderBy(image => image.SortOrder)
            .Select(ToResponse)
            .ToList();
    }

    private async Task<Product> GetProductAsync(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");
    }

    private ProductImageResponse ToResponse(ProductImage image) => new(
        image.Id,
        fileStorage.GetUrl(image.ImagePath),
        image.AltText,
        image.SortOrder);

    private static ValidationException ValidationError(string property, string message)
    {
        return new ValidationException([new ValidationFailure(property, message)]);
    }
}
