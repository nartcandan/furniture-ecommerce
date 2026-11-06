using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class ProductsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Task<PagedResult<ProductResponse>?> ListAsync(string query) =>
        Client.GetFromJsonAsync<PagedResult<ProductResponse>>($"/api/products?{query}");

    private async Task<(CategoryResponse Sofas, CategoryResponse Lamps)> SeedCatalogAsync()
    {
        var sofas = await CreateCategoryAsync("Sofas");
        var lamps = await CreateCategoryAsync("Lamps");

        await CreateProductAsync(sofas.Id, "Oslo Sofa", 3000);
        await CreateProductAsync(sofas.Id, "Bergen 50% Sofa", 1500);
        await CreateProductAsync(lamps.Id, "Desk Lamp", 300);
        await CreateProductAsync(lamps.Id, "Floor Lamp", 800);

        return (sofas, lamps);
    }

    [Fact]
    public async Task Create_ReturnsProductWithCategory()
    {
        var category = await CreateCategoryAsync();

        var product = await CreateProductAsync(category.Id, "Oslo Sofa", 3000, 5);

        Assert.Equal("oslo-sofa", product.Slug);
        Assert.Equal(category.Slug, product.CategorySlug);
        Assert.Equal(3000, product.Price);
        Assert.Equal(5, product.StockQuantity);
    }

    [Fact]
    public async Task Create_AddsSuffix_ForSameName()
    {
        var category = await CreateCategoryAsync();

        await CreateProductAsync(category.Id, "Oslo Sofa");
        var second = await CreateProductAsync(category.Id, "Oslo Sofa");

        Assert.Equal("oslo-sofa-2", second.Slug);
    }

    [Fact]
    public async Task Create_ReturnsNotFound_ForMissingCategory()
    {
        var response = await Client.PostAsJsonAsync("/api/products", NewProduct(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_ForInvalidProduct()
    {
        var category = await CreateCategoryAsync();

        var response = await Client.PostAsJsonAsync("/api/products", NewProduct(category.Id, price: -1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("category=sofas", 2)]
    [InlineData("search=lamp", 2)]
    [InlineData("search=DESK", 1)]
    [InlineData("search=50%25", 1)]
    [InlineData("search=%25", 1)]
    [InlineData("search=_", 0)]
    [InlineData("minPrice=800&maxPrice=1500", 2)]
    [InlineData("category=lamps&minPrice=500", 1)]
    [InlineData("category=missing", 0)]
    public async Task List_AppliesFilters(string query, int expectedCount)
    {
        await SeedCatalogAsync();

        var result = await ListAsync(query);

        Assert.Equal(expectedCount, result!.TotalCount);
        Assert.Equal(expectedCount, result.Items.Count);
    }

    [Theory]
    [InlineData("price_asc", "Desk Lamp")]
    [InlineData("price_desc", "Oslo Sofa")]
    [InlineData("name", "Bergen 50% Sofa")]
    [InlineData("newest", "Floor Lamp")]
    public async Task List_AppliesSorting(string sort, string expectedFirst)
    {
        await SeedCatalogAsync();

        var result = await ListAsync($"sort={sort}");

        Assert.Equal(expectedFirst, result!.Items[0].Name);
    }

    [Fact]
    public async Task List_ReturnsRequestedPage()
    {
        await SeedCatalogAsync();

        var result = await ListAsync("sort=price_asc&pageSize=3&page=2");

        Assert.Equal("Oslo Sofa", Assert.Single(result!.Items).Name);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public async Task List_ReturnsEmptyItems_AfterLastPage()
    {
        await SeedCatalogAsync();

        var result = await ListAsync("page=9");

        Assert.Empty(result!.Items);
        Assert.Equal(4, result.TotalCount);
    }

    [Theory]
    [InlineData("sort=unknown")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("minPrice=5&maxPrice=1")]
    public async Task List_ReturnsBadRequest_ForInvalidQuery(string query)
    {
        var response = await Client.GetAsync($"/api/products?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesFieldsButNotSlugOrStock()
    {
        var sofas = await CreateCategoryAsync("Sofas");
        var lamps = await CreateCategoryAsync("Lamps");
        var product = await CreateProductAsync(sofas.Id, "Oslo Sofa", stock: 5);

        var request = UpdateOf(product) with { CategoryId = lamps.Id, Name = "Renamed", Price = 3600 };
        var response = await Client.PutAsJsonAsync($"/api/products/{product.Id}", request);
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal("oslo-sofa", updated.Slug);
        Assert.Equal(lamps.Slug, updated.CategorySlug);
        Assert.Equal(3600, updated.Price);
        Assert.Equal(5, updated.StockQuantity);
    }

    [Fact]
    public async Task AdjustStock_AddsChangeToCurrentStock()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, stock: 10);

        var response = await Client.PatchAsJsonAsync(
            $"/api/products/{product.Id}/stock",
            new AdjustStockRequest(-3));
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(7, updated!.StockQuantity);
    }

    [Fact]
    public async Task AdjustStock_ReturnsConflict_WhenStockWouldGoBelowZero()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, stock: 2);

        var response = await Client.PatchAsJsonAsync(
            $"/api/products/{product.Id}/stock",
            new AdjustStockRequest(-3));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AdjustStock_ReturnsNotFound_ForMissingProduct()
    {
        var response = await Client.PatchAsJsonAsync(
            $"/api/products/{Guid.NewGuid()}/stock",
            new AdjustStockRequest(1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdjustStock_NeverGoesBelowZero_UnderConcurrentRequests()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, stock: 12);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Client.PatchAsJsonAsync($"/api/products/{product.Id}/stock", new AdjustStockRequest(-1))));

        var current = await Client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Slug}");

        Assert.Equal(12, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(8, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, current!.StockQuantity);
    }

    [Fact]
    public async Task Delete_HidesProductWithoutRemovingIt()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);

        var delete = await Client.DeleteAsync($"/api/products/{product.Id}");
        var deleteAgain = await Client.DeleteAsync($"/api/products/{product.Id}");
        var get = await Client.GetAsync($"/api/products/{product.Slug}");
        var list = await ListAsync("");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteAgain.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Empty(list!.Items);
    }

    [Fact]
    public async Task ReactivatedProduct_ReturnsToTheStore()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        await Client.DeleteAsync($"/api/products/{product.Id}");

        await Client.PutAsJsonAsync($"/api/products/{product.Id}", UpdateOf(product, isActive: true));

        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/products/{product.Slug}")).StatusCode);
    }

    [Fact]
    public async Task InactiveCategory_HidesItsActiveProducts()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);

        await Client.PutAsJsonAsync(
            $"/api/categories/{category.Id}",
            new UpdateCategoryRequest(category.Name, null, false));

        var get = await Client.GetAsync($"/api/products/{product.Slug}");
        var list = await ListAsync("");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Empty(list!.Items);
    }
}
