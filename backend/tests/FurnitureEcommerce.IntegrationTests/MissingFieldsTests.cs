using System.Net;
using System.Net.Http.Json;
using System.Text;
using FurnitureEcommerce.Application.Addresses;
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
    [InlineData("POST", "/api/addresses")]
    [InlineData("POST", "/api/cart/items")]
    [InlineData("POST", "/api/orders")]
    [InlineData("PUT", "/api/cart/items/018f4e2a-0000-7000-8000-000000000001")]
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
    public async Task UpdateAddress_ReturnsBadRequest_ForEmptyBody()
    {
        var create = await Client.PostAsJsonAsync(
            "/api/addresses",
            new AddressRequest("Home", "Ayse", "Yilmaz", "05551112233", "Istanbul", "Kadikoy", "Moda Cd. 1", null));
        var address = await create.Content.ReadFromJsonAsync<AddressResponse>();

        var response = await Client.PutAsync($"/api/addresses/{address!.Id}", EmptyJson());

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
