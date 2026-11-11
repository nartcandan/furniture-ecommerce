using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class OrdersTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string OrdersPath = "/api/orders";

    private async Task<ProductResponse> CreateProductAsync(string name = "Oslo Sofa", decimal price = 1000, int stock = 10)
    {
        var category = await CreateCategoryAsync($"{name} category");
        return await CreateProductAsync(category.Id, name, price, stock);
    }

    private static async Task<AddressResponse> CreateAddressAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/addresses",
            new AddressRequest(
                "Home", "Ayse", "Yilmaz", "+905551112233", "Istanbul", "Kadikoy", "Moda Cd. No: 1", "34710"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AddressResponse>())!;
    }

    private static async Task AddToCartAsync(HttpClient client, Guid productId, int quantity = 1)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(productId, quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PlaceOrderAsync(HttpClient client, Guid addressId)
    {
        return client.PostAsJsonAsync(OrdersPath, new CreateOrderRequest(addressId));
    }

    private async Task<int> GetStockAsync(Guid productId)
    {
        var product = await Client.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{productId}");
        return product!.StockQuantity;
    }

    [Fact]
    public async Task Create_ReturnsPendingOrder_AndTakesStock_ButKeepsCart()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);
        var sofa = await CreateProductAsync("Oslo Sofa", price: 1000, stock: 10);
        var lamp = await CreateProductAsync("Malmo Lamp", price: 249.90m, stock: 5);
        await AddToCartAsync(customer, sofa.Id, 2);
        await AddToCartAsync(customer, lamp.Id, 3);

        var response = await PlaceOrderAsync(customer, address.Id);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        var cart = await customer.GetFromJsonAsync<CartResponse>("/api/cart");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/orders/{order!.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("PendingPayment", order.Status);
        Assert.Equal(2749.70m, order.TotalAmount);
        Assert.Equal("Ayse", order.RecipientFirstName);
        Assert.Equal("Kadikoy", order.District);
        Assert.Null(order.PaidAt);
        Assert.Equal(
            [
                new OrderItemResponse(lamp.Id, "Malmo Lamp", 249.90m, 3, 749.70m),
                new OrderItemResponse(sofa.Id, "Oslo Sofa", 1000, 2, 2000)
            ],
            order.Items);
        Assert.Equal(8, await GetStockAsync(sofa.Id));
        Assert.Equal(2, await GetStockAsync(lamp.Id));
        Assert.Equal(2, cart!.Items.Count);
    }

    [Fact]
    public async Task Create_ReturnsConflict_ForEmptyCart()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);

        var response = await PlaceOrderAsync(customer, address.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsNotFound_ForMissingOrOtherUsersAddress()
    {
        var customer = await CreateCustomerClientAsync("ayse@example.com");
        var other = await CreateCustomerClientAsync("mehmet@example.com");
        var othersAddress = await CreateAddressAsync(other);
        var product = await CreateProductAsync(stock: 10);
        await AddToCartAsync(customer, product.Id);

        var missing = await PlaceOrderAsync(customer, Guid.NewGuid());
        var notOwned = await PlaceOrderAsync(customer, othersAddress.Id);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notOwned.StatusCode);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Create_ReturnsConflict_AndTakesNoStock_WhenOneItemHasTooLittleStock()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);
        var sofa = await CreateProductAsync("Oslo Sofa", stock: 10);
        var lamp = await CreateProductAsync("Malmo Lamp", stock: 5);
        await AddToCartAsync(customer, sofa.Id, 2);
        await AddToCartAsync(customer, lamp.Id, 4);
        await Client.PatchAsJsonAsync($"/api/products/{lamp.Id}/stock", new AdjustStockRequest(-3));

        var response = await PlaceOrderAsync(customer, address.Id);
        var orders = await customer.GetFromJsonAsync<List<OrderSummaryResponse>>(OrdersPath);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10, await GetStockAsync(sofa.Id));
        Assert.Equal(2, await GetStockAsync(lamp.Id));
        Assert.Empty(orders!);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenProductOrCategoryWasDeactivated()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);
        var product = await CreateProductAsync(stock: 10);
        await AddToCartAsync(customer, product.Id);

        await Client.DeleteAsync($"/api/products/{product.Id}");
        var inactiveProduct = await PlaceOrderAsync(customer, address.Id);

        await Client.PutAsJsonAsync($"/api/products/{product.Id}", UpdateOf(product));
        await Client.PutAsJsonAsync(
            $"/api/categories/{product.CategoryId}",
            new UpdateCategoryRequest("Hidden", null, IsActive: false));
        var inactiveCategory = await PlaceOrderAsync(customer, address.Id);

        Assert.Equal(HttpStatusCode.Conflict, inactiveProduct.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, inactiveCategory.StatusCode);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Create_UsesCurrentPrice_NotThePriceWhenAddedToCart()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);
        var product = await CreateProductAsync(price: 1000);
        await AddToCartAsync(customer, product.Id, 2);
        await Client.PutAsJsonAsync($"/api/products/{product.Id}", UpdateOf(product) with { Price = 1200 });

        var response = await PlaceOrderAsync(customer, address.Id);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(1200, Assert.Single(order!.Items).UnitPrice);
        Assert.Equal(2400, order.TotalAmount);
    }

    [Fact]
    public async Task Create_ReturnsConflict_AndTakesNoStock_WhileAnotherOrderWaitsForPayment()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);
        var product = await CreateProductAsync(stock: 10);
        await AddToCartAsync(customer, product.Id, 2);

        var first = await PlaceOrderAsync(customer, address.Id);
        var second = await PlaceOrderAsync(customer, address.Id);
        var orders = await customer.GetFromJsonAsync<List<OrderSummaryResponse>>(OrdersPath);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(8, await GetStockAsync(product.Id));
        Assert.Single(orders!);
    }

    [Fact]
    public async Task Order_KeepsItsCopy_WhenProductAndAddressChangeLater()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);
        var product = await CreateProductAsync("Oslo Sofa", price: 1000);
        await AddToCartAsync(customer, product.Id);
        var created = await (await PlaceOrderAsync(customer, address.Id)).Content.ReadFromJsonAsync<OrderResponse>();

        await Client.PutAsJsonAsync(
            $"/api/products/{product.Id}",
            UpdateOf(product) with { Name = "Renamed Sofa", Price = 5000 });
        await customer.DeleteAsync($"/api/addresses/{address.Id}");
        var stored = await customer.GetFromJsonAsync<OrderResponse>($"{OrdersPath}/{created!.Id}");

        var item = Assert.Single(stored!.Items);
        Assert.Equal("Oslo Sofa", item.ProductName);
        Assert.Equal(1000, item.UnitPrice);
        Assert.Equal(1000, stored.TotalAmount);
        Assert.Equal("Moda Cd. No: 1", stored.AddressLine);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnOrders()
    {
        var ayse = await CreateCustomerClientAsync("ayse@example.com");
        var mehmet = await CreateCustomerClientAsync("mehmet@example.com");
        var product = await CreateProductAsync(price: 1000, stock: 10);
        await AddToCartAsync(ayse, product.Id, 3);
        await AddToCartAsync(mehmet, product.Id, 1);
        var aysesOrder = await (await PlaceOrderAsync(ayse, (await CreateAddressAsync(ayse)).Id))
            .Content.ReadFromJsonAsync<OrderResponse>();
        await PlaceOrderAsync(mehmet, (await CreateAddressAsync(mehmet)).Id);

        var orders = await ayse.GetFromJsonAsync<List<OrderSummaryResponse>>(OrdersPath);

        var summary = Assert.Single(orders!);
        Assert.Equal(aysesOrder!.Id, summary.Id);
        Assert.Equal("PendingPayment", summary.Status);
        Assert.Equal(3000, summary.TotalAmount);
        Assert.Equal(3, summary.TotalQuantity);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_ForMissingOrOtherUsersOrder()
    {
        var owner = await CreateCustomerClientAsync("ayse@example.com");
        var other = await CreateCustomerClientAsync("mehmet@example.com");
        var product = await CreateProductAsync();
        await AddToCartAsync(owner, product.Id);
        var order = await (await PlaceOrderAsync(owner, (await CreateAddressAsync(owner)).Id))
            .Content.ReadFromJsonAsync<OrderResponse>();

        var notOwned = await other.GetAsync($"{OrdersPath}/{order!.Id}");
        var missing = await owner.GetAsync($"{OrdersPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, notOwned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Create_SellsLastItemOnce_WhenTwoCustomersOrderAtTheSameTime()
    {
        var product = await CreateProductAsync(stock: 1);
        var customers = new List<(HttpClient Client, Guid AddressId)>();

        foreach (var email in new[] { "ayse@example.com", "mehmet@example.com", "zeynep@example.com" })
        {
            var client = await CreateCustomerClientAsync(email);
            await AddToCartAsync(client, product.Id);
            customers.Add((client, (await CreateAddressAsync(client)).Id));
        }

        var responses = await Task.WhenAll(
            customers.Select(customer => PlaceOrderAsync(customer.Client, customer.AddressId)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Create_MakesOneOrder_WhenTheSameCustomerSendsItManyTimesAtOnce()
    {
        var customer = await CreateCustomerClientAsync();
        var address = await CreateAddressAsync(customer);
        var product = await CreateProductAsync(stock: 10);
        await AddToCartAsync(customer, product.Id, 2);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => PlaceOrderAsync(customer, address.Id)));
        var orders = await customer.GetFromJsonAsync<List<OrderSummaryResponse>>(OrdersPath);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(4, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(8, await GetStockAsync(product.Id));
        Assert.Single(orders!);
    }

    [Fact]
    public async Task Endpoints_ReturnUnauthorized_WithoutToken()
    {
        var getAll = await AnonymousClient.GetAsync(OrdersPath);
        var get = await AnonymousClient.GetAsync($"{OrdersPath}/{Guid.NewGuid()}");
        var post = await PlaceOrderAsync(AnonymousClient, Guid.NewGuid());

        Assert.All(
            [getAll, get, post],
            response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
    }
}
