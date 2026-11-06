using System.Net;
using System.Net.Http.Json;
using System.Text;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class ProductImagesTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<ProductResponse> CreateProductAsync()
    {
        var category = await CreateCategoryAsync();
        return await CreateProductAsync(category.Id);
    }

    private static byte[] PngOfSize(long size)
    {
        var content = new byte[size];
        PngBytes.CopyTo(content, 0);
        return content;
    }

    private bool FileExists(string url)
    {
        var relativePath = new Uri(url).AbsolutePath["/uploads/".Length..];
        return File.Exists(Path.Combine(Factory.UploadsPath, relativePath));
    }

    [Fact]
    public async Task Upload_StoresFileAndServesIt()
    {
        var product = await CreateProductAsync();

        var image = await UploadImageAsync(product.Id);

        var response = await Client.GetAsync(new Uri(image.Url).AbsolutePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PngBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task Upload_IgnoresClientFileName()
    {
        var product = await CreateProductAsync();

        var response = await UploadAsync(product.Id, PngBytes, "../../evil.jpg");
        var image = await response.Content.ReadFromJsonAsync<ProductImageResponse>();

        Assert.DoesNotContain("evil", image!.Url);
        Assert.EndsWith(".png", image.Url);
    }

    [Fact]
    public async Task Upload_AcceptsFileAtSizeLimit()
    {
        var product = await CreateProductAsync();

        var response = await UploadAsync(product.Id, PngOfSize(ProductImageService.MaxFileSizeInBytes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsFileOverSizeLimit()
    {
        var product = await CreateProductAsync();

        var response = await UploadAsync(product.Id, PngOfSize(ProductImageService.MaxFileSizeInBytes + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("just some text, not an image", "fake.jpg")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", "image.svg")]
    public async Task Upload_RejectsUnsupportedContent(string content, string fileName)
    {
        var product = await CreateProductAsync();

        var response = await UploadAsync(product.Id, Encoding.UTF8.GetBytes(content), fileName);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ReturnsNotFound_ForMissingProduct()
    {
        var response = await UploadAsync(Guid.NewGuid(), PngBytes);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ReturnsConflict_WhenProductHasMaximumImages()
    {
        var product = await CreateProductAsync();

        for (var count = 0; count < ProductImageService.MaxImagesPerProduct; count++)
        {
            await UploadImageAsync(product.Id);
        }

        var response = await UploadAsync(product.Id, PngBytes);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Product_ReturnsAllImagesInDetailAndMainImageInList()
    {
        var product = await CreateProductAsync();
        var first = await UploadImageAsync(product.Id);
        await UploadImageAsync(product.Id);

        var detail = await Client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Slug}");
        var list = await Client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products");

        Assert.Equal(2, detail!.Images.Count);
        Assert.Equal(first.Id, Assert.Single(list!.Items[0].Images).Id);
    }

    [Fact]
    public async Task Reorder_ChangesMainImage()
    {
        var product = await CreateProductAsync();
        var first = await UploadImageAsync(product.Id);
        var second = await UploadImageAsync(product.Id);

        var response = await Client.PutAsJsonAsync(
            $"/api/products/{product.Id}/images/order",
            new ReorderProductImagesRequest([second.Id, first.Id]));

        var list = await Client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(second.Id, Assert.Single(list!.Items[0].Images).Id);
    }

    [Fact]
    public async Task Reorder_ReturnsBadRequest_ForIncompleteOrDuplicateList()
    {
        var product = await CreateProductAsync();
        var first = await UploadImageAsync(product.Id);
        await UploadImageAsync(product.Id);
        var url = $"/api/products/{product.Id}/images/order";

        var incomplete = await Client.PutAsJsonAsync(url, new ReorderProductImagesRequest([first.Id]));
        var duplicate = await Client.PutAsJsonAsync(url, new ReorderProductImagesRequest([first.Id, first.Id]));
        var foreign = await Client.PutAsJsonAsync(url, new ReorderProductImagesRequest([first.Id, Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesRecordAndFile()
    {
        var product = await CreateProductAsync();
        var image = await UploadImageAsync(product.Id);
        Assert.True(FileExists(image.Url));

        var delete = await Client.DeleteAsync($"/api/products/{product.Id}/images/{image.Id}");
        var deleteAgain = await Client.DeleteAsync($"/api/products/{product.Id}/images/{image.Id}");
        var detail = await Client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Slug}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
        Assert.False(FileExists(image.Url));
        Assert.Empty(detail!.Images);
    }

    [Fact]
    public async Task DeletingProduct_KeepsItsImages()
    {
        var product = await CreateProductAsync();
        var image = await UploadImageAsync(product.Id);

        await Client.DeleteAsync($"/api/products/{product.Id}");
        await Client.PutAsJsonAsync($"/api/products/{product.Id}", UpdateOf(product, isActive: true));

        var detail = await Client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Slug}");

        Assert.True(FileExists(image.Url));
        Assert.Equal(image.Id, Assert.Single(detail!.Images).Id);
    }
}
