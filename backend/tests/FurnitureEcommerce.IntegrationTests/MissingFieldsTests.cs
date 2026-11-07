using System.Net;
using System.Text;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

/// <summary>Fields left out of the body arrive as null and must give a 400, not a crash.</summary>
[Collection(nameof(ApiCollection))]
public class MissingFieldsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static StringContent EmptyJson() => new("{}", Encoding.UTF8, "application/json");

    [Theory]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("POST", "/api/auth/login")]
    [InlineData("POST", "/api/categories")]
    [InlineData("POST", "/api/products")]
    public async Task Create_ReturnsBadRequest_ForEmptyBody(string method, string path)
    {
        var response = await Client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), path) { Content = EmptyJson() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_ReturnsBadRequest_ForEmptyBody()
    {
        var category = await CreateCategoryAsync();

        var response = await Client.PutAsync($"/api/categories/{category.Id}", EmptyJson());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_ReturnsBadRequest_ForEmptyBody()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);

        var response = await Client.PutAsync($"/api/products/{product.Id}", EmptyJson());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReorderImages_ReturnsBadRequest_ForEmptyBody()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);

        var response = await Client.PutAsync($"/api/products/{product.Id}/images/order", EmptyJson());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_ReturnsBadRequest_WithoutFile()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);

        var form = new MultipartFormDataContent { { new StringContent("Front view"), "altText" } };

        var response = await Client.PostAsync($"/api/products/{product.Id}/images", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
