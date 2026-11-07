using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Products;

namespace FurnitureEcommerce.IntegrationTests.Infrastructure;

public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];

    protected ApiFactory Factory { get; } = factory;

    /// <summary>Sends the admin token, because catalog writes require it.</summary>
    protected HttpClient Client { get; } = factory.CreateClient();

    protected HttpClient AnonymousClient { get; } = factory.CreateClient();

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();

        var response = await AnonymousClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Registers a new customer and returns a client that sends their token.</summary>
    protected async Task<HttpClient> CreateCustomerClientAsync(string email = "ayse@example.com")
    {
        var response = await AnonymousClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("Ayse", "Yilmaz", email, "Sifre1234"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    protected async Task<CategoryResponse> CreateCategoryAsync(string name = "Sofas")
    {
        var response = await Client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    protected async Task<ProductResponse> CreateProductAsync(
        Guid categoryId,
        string name = "Oslo Sofa",
        decimal price = 1000,
        int stock = 10)
    {
        var response = await Client.PostAsJsonAsync("/api/products", NewProduct(categoryId, name, price, stock));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    protected static CreateProductRequest NewProduct(
        Guid categoryId,
        string name = "Oslo Sofa",
        decimal price = 1000,
        int stock = 10)
    {
        return new CreateProductRequest(
            categoryId, name, null, price, stock, null, null, null, null, null, null);
    }

    protected static UpdateProductRequest UpdateOf(ProductResponse product, bool isActive = true)
    {
        return new UpdateProductRequest(
            product.CategoryId, product.Name, null, product.Price,
            null, null, null, null, null, null, isActive);
    }

    protected Task<HttpResponseMessage> UploadAsync(Guid productId, byte[] content, string fileName = "photo.png")
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(content), "file", fileName }
        };

        return Client.PostAsync($"/api/products/{productId}/images", form);
    }

    protected async Task<ProductImageResponse> UploadImageAsync(Guid productId)
    {
        var response = await UploadAsync(productId, PngBytes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductImageResponse>())!;
    }
}
