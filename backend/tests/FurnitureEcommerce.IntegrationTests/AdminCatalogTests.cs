using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AdminCatalogTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<CategoryResponse> CreateInactiveCategoryAsync(string name = "Archive")
    {
        var category = await CreateCategoryAsync(name);
        var response = await Client.PutAsJsonAsync(
            $"/api/categories/{category.Id}",
            new UpdateCategoryRequest(category.Name, null, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    private async Task<ProductResponse> CreateInactiveProductAsync(Guid categoryId, string name = "Old Sofa")
    {
        var product = await CreateProductAsync(categoryId, name);
        var response = await Client.DeleteAsync($"/api/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return product;
    }

    [Fact]
    public async Task PublicCategoryList_HidesInactiveCategories()
    {
        await CreateCategoryAsync("Sofas");
        await CreateInactiveCategoryAsync();

        var categories = await AnonymousClient.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");

        Assert.Equal("Sofas", Assert.Single(categories!).Name);
    }

    [Fact]
    public async Task PublicCategoryDetail_ReturnsNotFound_ForInactiveCategory()
    {
        var category = await CreateInactiveCategoryAsync();

        var response = await AnonymousClient.GetAsync($"/api/categories/{category.Slug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminCategoryList_IncludesInactiveCategories()
    {
        await CreateCategoryAsync("Sofas");
        await CreateInactiveCategoryAsync();

        var categories = await Client.GetFromJsonAsync<List<CategoryResponse>>("/api/admin/categories");

        Assert.Equal(2, categories!.Count);
        Assert.Contains(categories, category => !category.IsActive);
    }

    [Fact]
    public async Task AdminProductList_IncludesInactiveProducts_AndProductsOfInactiveCategories()
    {
        var active = await CreateCategoryAsync("Sofas");
        await CreateProductAsync(active.Id, "Oslo Sofa");
        await CreateInactiveProductAsync(active.Id);
        var hiddenCategory = await CreateCategoryAsync("Lamps");
        await CreateProductAsync(hiddenCategory.Id, "Desk Lamp");
        await Client.PutAsJsonAsync(
            $"/api/categories/{hiddenCategory.Id}",
            new UpdateCategoryRequest("Lamps", null, false));

        var adminList = await Client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/admin/products");
        var publicList = await AnonymousClient.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products");

        Assert.Equal(3, adminList!.TotalCount);
        Assert.Equal("Oslo Sofa", Assert.Single(publicList!.Items).Name);
    }

    [Fact]
    public async Task AdminProductList_AppliesFiltersAndPaging()
    {
        var category = await CreateCategoryAsync();
        await CreateProductAsync(category.Id, "Oslo Sofa");
        await CreateInactiveProductAsync(category.Id, "Bergen Sofa");
        await CreateProductAsync(category.Id, "Desk Lamp");

        var result = await Client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            "/api/admin/products?search=sofa&sort=name&pageSize=1");

        Assert.Equal(2, result!.TotalCount);
        Assert.Equal("Bergen Sofa", Assert.Single(result.Items).Name);
    }

    [Fact]
    public async Task AdminProductList_ReturnsBadRequest_ForInvalidQuery()
    {
        var response = await Client.GetAsync("/api/admin/products?sort=unknown");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminProductDetail_ReturnsInactiveProductWithAllImages()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        await UploadImageAsync(product.Id);
        await UploadImageAsync(product.Id);
        await Client.DeleteAsync($"/api/products/{product.Id}");

        var detail = await Client.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{product.Id}");

        Assert.False(detail!.IsActive);
        Assert.Equal(2, detail.Images.Count);
    }

    [Fact]
    public async Task AdminProductDetail_ReturnsNotFound_ForUnknownId()
    {
        var response = await Client.GetAsync($"/api/admin/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/categories")]
    [InlineData("/api/admin/products")]
    [InlineData("/api/admin/products/019a5c10-0000-7000-8000-000000000001")]
    public async Task AdminEndpoint_RejectsAnonymousAndCustomer(string path)
    {
        var anonymous = await AnonymousClient.GetAsync(path);

        var registration = await AnonymousClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("Ayse", "Yilmaz", "ayse@example.com", "Sifre1234"));
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var customer = await AnonymousClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
    }
}
