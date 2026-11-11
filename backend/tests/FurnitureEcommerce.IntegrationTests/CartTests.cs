using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class CartTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string CartPath = "/api/cart";
    private const string ItemsPath = "/api/cart/items";

    private async Task<ProductResponse> CreateProductAsync(string name = "Oslo Sofa", decimal price = 1000, int stock = 10)
    {
        var category = await CreateCategoryAsync($"{name} category");
        return await CreateProductAsync(category.Id, name, price, stock);
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid productId, int quantity = 1)
    {
        return client.PostAsJsonAsync(ItemsPath, new AddCartItemRequest(productId, quantity));
    }

    private static async Task<CartResponse> GetCartAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<CartResponse>(CartPath))!;
    }

    [Fact]
    public async Task Get_ReturnsEmptyCart_ForNewCustomer()
    {
        var customer = await CreateCustomerClientAsync();

        var cart = await GetCartAsync(customer);

        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.TotalAmount);
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public async Task AddItem_ReturnsCart_WithPriceTotalsAndMainImage()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 1250.50m, stock: 10);
        var image = await UploadImageAsync(product.Id);

        var response = await AddAsync(customer, product.Id, 2);
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(cart!.Items);
        Assert.Equal(
            new CartItemResponse(
                product.Id, "Oslo Sofa", product.Slug, image.Url, 1250.50m, 2, 2501.00m, 10, true, false),
            item);
        Assert.Equal(2, cart.TotalQuantity);
        Assert.Equal(2501.00m, cart.TotalAmount);
        Assert.True(cart.CanCheckout);
    }

    [Fact]
    public async Task AddItem_IncreasesQuantity_WhenProductIsAlreadyInCart()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync();

        await AddAsync(customer, product.Id, 2);
        await AddAsync(customer, product.Id, 3);
        var cart = await GetCartAsync(customer);

        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task AddItem_KeepsItems_InTheOrderTheyWereAdded()
    {
        var customer = await CreateCustomerClientAsync();
        var sofa = await CreateProductAsync("Oslo Sofa");
        var lamp = await CreateProductAsync("Malmo Lamp");

        await AddAsync(customer, sofa.Id);
        await AddAsync(customer, lamp.Id);
        await AddAsync(customer, sofa.Id);
        var cart = await GetCartAsync(customer);

        Assert.Equal(["Oslo Sofa", "Malmo Lamp"], cart.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task AddItem_ReturnsConflict_WhenQuantityExceedsStock()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 3);

        var tooMany = await AddAsync(customer, product.Id, 4);
        var allOfIt = await AddAsync(customer, product.Id, 3);
        var oneMore = await AddAsync(customer, product.Id, 1);
        var cart = await GetCartAsync(customer);

        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allOfIt.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, oneMore.StatusCode);
        Assert.Equal(3, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task AddItem_ReturnsNotFound_ForProductThatCannotBeSold()
    {
        var customer = await CreateCustomerClientAsync();
        var inactive = await CreateProductAsync("Inactive product");
        await Client.DeleteAsync($"/api/products/{inactive.Id}");
        var inInactiveCategory = await CreateProductAsync("Hidden product");
        await Client.PutAsJsonAsync(
            $"/api/categories/{inInactiveCategory.CategoryId}",
            new UpdateCategoryRequest("Hidden", null, IsActive: false));

        var missing = await AddAsync(customer, Guid.NewGuid());
        var inactiveProduct = await AddAsync(customer, inactive.Id);
        var inactiveCategory = await AddAsync(customer, inInactiveCategory.Id);
        var cart = await GetCartAsync(customer);

        Assert.All(
            [missing, inactiveProduct, inactiveCategory],
            response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Empty(cart.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task AddItem_ReturnsBadRequest_ForNonPositiveQuantity(int quantity)
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync();

        var response = await AddAsync(customer, product.Id, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetItemQuantity_ReplacesQuantity()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 10);
        await AddAsync(customer, product.Id, 2);

        var response = await customer.PutAsJsonAsync($"{ItemsPath}/{product.Id}", new UpdateCartItemRequest(7));
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, Assert.Single(cart!.Items).Quantity);
    }

    [Fact]
    public async Task SetItemQuantity_ReturnsConflict_WhenQuantityExceedsStock()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 3);
        await AddAsync(customer, product.Id, 2);

        var response = await customer.PutAsJsonAsync($"{ItemsPath}/{product.Id}", new UpdateCartItemRequest(4));
        var cart = await GetCartAsync(customer);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task SetItemQuantity_ReturnsBadRequest_ForZero()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync();
        await AddAsync(customer, product.Id, 2);

        var response = await customer.PutAsJsonAsync($"{ItemsPath}/{product.Id}", new UpdateCartItemRequest(0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveItem_RemovesOnlyThatProduct()
    {
        var customer = await CreateCustomerClientAsync();
        var sofa = await CreateProductAsync("Oslo Sofa");
        var lamp = await CreateProductAsync("Malmo Lamp");
        await AddAsync(customer, sofa.Id);
        await AddAsync(customer, lamp.Id);

        var response = await customer.DeleteAsync($"{ItemsPath}/{sofa.Id}");
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Malmo Lamp", Assert.Single(cart!.Items).Name);
    }

    [Fact]
    public async Task ItemEndpoints_ReturnNotFound_WhenProductIsNotInCart()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync();

        var put = await customer.PutAsJsonAsync($"{ItemsPath}/{product.Id}", new UpdateCartItemRequest(1));
        var delete = await customer.DeleteAsync($"{ItemsPath}/{product.Id}");

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Clear_EmptiesCart_AndWorksOnUnusedCart()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync();

        var unused = await customer.DeleteAsync(CartPath);
        await AddAsync(customer, product.Id, 2);
        var used = await customer.DeleteAsync(CartPath);
        var cart = await GetCartAsync(customer);

        Assert.Equal(HttpStatusCode.NoContent, unused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, used.StatusCode);
        Assert.Empty(cart.Items);
    }

    [Fact]
    public async Task Get_ShowsCurrentPrice_AfterPriceChange()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 1000);
        await AddAsync(customer, product.Id, 2);

        await Client.PutAsJsonAsync($"/api/products/{product.Id}", UpdateOf(product) with { Price = 1200 });
        var cart = await GetCartAsync(customer);

        Assert.Equal(1200, Assert.Single(cart.Items).UnitPrice);
        Assert.Equal(2400, cart.TotalAmount);
    }

    [Fact]
    public async Task Get_WarnsAndBlocksCheckout_WhenProductIsDeactivated()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync();
        await AddAsync(customer, product.Id);

        await Client.DeleteAsync($"/api/products/{product.Id}");
        var cart = await GetCartAsync(customer);

        Assert.False(Assert.Single(cart.Items).IsAvailable);
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public async Task Get_WarnsAndBlocksCheckout_WhenCategoryIsDeactivated()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync();
        await AddAsync(customer, product.Id);

        await Client.PutAsJsonAsync(
            $"/api/categories/{product.CategoryId}",
            new UpdateCategoryRequest("Hidden", null, IsActive: false));
        var cart = await GetCartAsync(customer);

        Assert.False(Assert.Single(cart.Items).IsAvailable);
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public async Task Get_WarnsAndBlocksCheckout_WhenStockFallsBelowQuantity()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 5);
        await AddAsync(customer, product.Id, 4);

        await Client.PatchAsJsonAsync($"/api/products/{product.Id}/stock", new AdjustStockRequest(-3));
        var cart = await GetCartAsync(customer);

        // The quantity is not changed automatically. The customer has to fix it.
        var item = Assert.Single(cart.Items);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(2, item.StockQuantity);
        Assert.True(item.HasInsufficientStock);
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public async Task Carts_AreSeparatePerUser()
    {
        var ayse = await CreateCustomerClientAsync("ayse@example.com");
        var mehmet = await CreateCustomerClientAsync("mehmet@example.com");
        var product = await CreateProductAsync();
        await AddAsync(ayse, product.Id, 2);

        var put = await mehmet.PutAsJsonAsync($"{ItemsPath}/{product.Id}", new UpdateCartItemRequest(9));
        var delete = await mehmet.DeleteAsync($"{ItemsPath}/{product.Id}");
        await mehmet.DeleteAsync(CartPath);
        var mehmetsCart = await GetCartAsync(mehmet);
        var aysesCart = await GetCartAsync(ayse);

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Empty(mehmetsCart.Items);
        Assert.Equal(2, Assert.Single(aysesCart.Items).Quantity);
    }

    [Fact]
    public async Task AddItem_CountsEveryRequest_WhenSentAtTheSameTime()
    {
        // The customer has no cart yet, so the requests also race to create it.
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 10);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => AddAsync(customer, product.Id)));
        var cart = await GetCartAsync(customer);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(10, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task AddItem_NeverExceedsStock_WhenSentAtTheSameTime()
    {
        var customer = await CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 5);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => AddAsync(customer, product.Id)));
        var cart = await GetCartAsync(customer);

        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task Endpoints_ReturnUnauthorized_WithoutToken()
    {
        var itemPath = $"{ItemsPath}/{Guid.NewGuid()}";

        var get = await AnonymousClient.GetAsync(CartPath);
        var post = await AddAsync(AnonymousClient, Guid.NewGuid());
        var put = await AnonymousClient.PutAsJsonAsync(itemPath, new UpdateCartItemRequest(1));
        var deleteItem = await AnonymousClient.DeleteAsync(itemPath);
        var clear = await AnonymousClient.DeleteAsync(CartPath);

        Assert.All(
            [get, post, put, deleteItem, clear],
            response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
    }
}
