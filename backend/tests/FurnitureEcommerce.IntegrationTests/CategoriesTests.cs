using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class CategoriesTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_GeneratesSlugFromName()
    {
        var category = await CreateCategoryAsync("Çalışma Alanı");

        Assert.Equal("calisma-alani", category.Slug);
        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task Create_AddsSuffix_WhenOldSlugIsStillInUse()
    {
        var first = await CreateCategoryAsync("Living Room");

        var update = new UpdateCategoryRequest("Lounge", null, true);
        var updateResponse = await Client.PutAsJsonAsync($"/api/categories/{first.Id}", update);
        var renamed = await updateResponse.Content.ReadFromJsonAsync<CategoryResponse>();

        var second = await CreateCategoryAsync("Living Room");

        Assert.Equal("Lounge", renamed!.Name);
        Assert.Equal("living-room", renamed.Slug);
        Assert.Equal("living-room-2", second.Slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("!!!")]
    public async Task Create_ReturnsBadRequest_ForInvalidName(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsCategoriesOrderedByName()
    {
        await CreateCategoryAsync("Sofas");
        await CreateCategoryAsync("Lamps");

        var categories = await Client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");

        Assert.Equal(["Lamps", "Sofas"], categories!.Select(category => category.Name));
    }

    [Fact]
    public async Task GetBySlug_ReturnsCategory()
    {
        var created = await CreateCategoryAsync("Lamps");

        var category = await Client.GetFromJsonAsync<CategoryResponse>("/api/categories/lamps");

        Assert.Equal(created.Id, category!.Id);
    }

    [Fact]
    public async Task MissingCategory_ReturnsNotFound()
    {
        var id = Guid.NewGuid();

        var get = await Client.GetAsync("/api/categories/missing");
        var put = await Client.PutAsJsonAsync($"/api/categories/{id}", new UpdateCategoryRequest("X", null, true));
        var delete = await Client.DeleteAsync($"/api/categories/{id}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesEmptyCategory()
    {
        var category = await CreateCategoryAsync();

        var response = await Client.DeleteAsync($"/api/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/categories/{category.Slug}")).StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenCategoryHasProducts()
    {
        var category = await CreateCategoryAsync();
        await CreateProductAsync(category.Id);

        var response = await Client.DeleteAsync($"/api/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenCategoryHasOnlyInactiveProducts()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        await Client.DeleteAsync($"/api/products/{product.Id}");

        var response = await Client.DeleteAsync($"/api/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
