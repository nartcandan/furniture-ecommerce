using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Domain.Entities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FurnitureEcommerce.UnitTests.Products;

public class ProductImageServiceTests
{
    private const string SavedPath = "products/saved.png";

    private readonly IProductRepository repository = Substitute.For<IProductRepository>();
    private readonly IFileStorage fileStorage = Substitute.For<IFileStorage>();
    private readonly ProductImageService service;
    private readonly Product product = new() { Name = "Oslo Sofa", Slug = "oslo-sofa" };

    public ProductImageServiceTests()
    {
        service = new ProductImageService(repository, fileStorage);

        repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        fileStorage
            .SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SavedPath);
    }

    private static MemoryStream PngStream() =>
        new([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0]);

    [Fact]
    public async Task AddAsync_DeletesSavedFile_WhenDatabaseSaveFails()
    {
        repository
            .AddImageAsync(Arg.Any<ProductImage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddAsync(product.Id, PngStream(), null, CancellationToken.None));

        await fileStorage.Received(1).DeleteAsync(SavedPath);
    }

    [Fact]
    public async Task AddAsync_KeepsSavedFile_WhenDatabaseSaveSucceeds()
    {
        await service.AddAsync(product.Id, PngStream(), null, CancellationToken.None);

        await fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task AddAsync_PlacesNewImageAfterExistingImages()
    {
        product.Images.Add(new ProductImage { ImagePath = "products/a.png", SortOrder = 4 });

        await service.AddAsync(product.Id, PngStream(), null, CancellationToken.None);

        await repository.Received(1).AddImageAsync(
            Arg.Is<ProductImage>(image => image.SortOrder == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecordBeforeFile()
    {
        var image = new ProductImage { ImagePath = "products/a.png" };
        product.Images.Add(image);

        await service.DeleteAsync(product.Id, image.Id, CancellationToken.None);

        Received.InOrder(() =>
        {
            repository.DeleteImageAsync(image, Arg.Any<CancellationToken>());
            fileStorage.DeleteAsync(image.ImagePath);
        });
    }
}
