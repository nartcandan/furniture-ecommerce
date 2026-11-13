using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Domain.Entities;
using FurnitureEcommerce.Infrastructure.Persistence;
using FurnitureEcommerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class OrderCancellationTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string PaymentId = "payment-1";

    private FakePaymentGateway Payments => Factory.Payments;

    /// <summary>A customer with 10 in stock and an order for 2. The order is paid unless told otherwise.</summary>
    private async Task<(HttpClient Customer, ProductResponse Product, OrderResponse Order)> PlaceOrderAsync(
        bool pay = true,
        string email = "ayse@example.com")
    {
        var customer = await CreateCustomerClientAsync(email);
        var category = await CreateCategoryAsync($"Sofas of {email}");
        var product = await CreateProductAsync(category.Id, "Oslo Sofa", price: 1000, stock: 10);

        var address = await (await customer.PostAsJsonAsync(
                "/api/addresses",
                new AddressRequest(
                    "Home", "Ayse", "Yilmaz", "+905551112233", "Istanbul", "Kadikoy", "Moda Cd. No: 1", "34710")))
            .Content.ReadFromJsonAsync<AddressResponse>();

        await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id, 2));
        var response = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(address!.Id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;

        if (pay)
        {
            Payments.SetResult(order.Id, CheckoutResult.Paid(PaymentId, order.TotalAmount, order.Id.ToString()));
            await AnonymousClient.PostAsync(
                "/api/payments/callback",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = Payments.TokenFor(order.Id) }));
        }

        return (customer, product, order);
    }

    private static Task<HttpResponseMessage> CancelAsync(HttpClient customer, Guid orderId) =>
        customer.PostAsync($"/api/orders/{orderId}/cancel", null);

    private static async Task<OrderResponse> GetOrderAsync(HttpClient customer, Guid orderId) =>
        (await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}"))!;

    private async Task<int> GetStockAsync(Guid productId) =>
        (await Client.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{productId}"))!.StockQuantity;

    private async Task UpdateOrderAsync(Guid orderId, OrderStatus status, DateTime? paidAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.Status, status)
                .SetProperty(order => order.PaidAt, order => paidAt ?? order.PaidAt));
    }

    [Fact]
    public async Task PaidOrder_ShowsThatItCanBeCancelled()
    {
        var (customer, _, order) = await PlaceOrderAsync();

        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal("Paid", stored.Status);
        Assert.True(stored.CanCancel);
    }

    [Fact]
    public async Task Cancel_RefundsPayment_AndPutsItemsBackInStock()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        var stockBefore = await GetStockAsync(product.Id);

        var response = await CancelAsync(customer, order.Id);
        var cancelled = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.False(cancelled.CanCancel);
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
        Assert.Equal(8, stockBefore);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    // An order that is still waiting for payment can be cancelled too. UnpaidOrderTests covers that.
    [Theory]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Cancel_ReturnsConflict_ForOrderThatIsNoLongerPaid(OrderStatus status)
    {
        var (customer, _, order) = await PlaceOrderAsync();
        await UpdateOrderAsync(order.Id, status);

        var response = await CancelAsync(customer, order.Id);
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(status.ToString(), stored.Status);
        Assert.Empty(Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task Cancel_ReturnsConflict_WhenOrderWasPaidOnAnEarlierDay()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await UpdateOrderAsync(order.Id, OrderStatus.Paid, paidAt: DateTime.UtcNow.AddDays(-1));

        var response = await CancelAsync(customer, order.Id);
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Paid", stored.Status);
        Assert.False(stored.CanCancel);
        Assert.Empty(Payments.CancelledPaymentIds);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Cancel_ReturnsBadGateway_AndKeepsOrderPaid_WhenProviderRefusesRefund()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        Payments.CancelError = "Cannot cancel";

        var response = await CancelAsync(customer, order.Id);
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Paid", stored.Status);
        Assert.True(stored.CanCancel);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Cancel_ReturnsAccepted_AndLeavesOrderInCancelling_WhenProviderCannotBeReached()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        Payments.IsUnreachable = true;

        var response = await CancelAsync(customer, order.Id);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        var secondTry = await CancelAsync(customer, order.Id);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("Cancelling", body!.Status);
        Assert.False(body.CanCancel);
        // The stock stays reserved until the refund is confirmed.
        Assert.Equal(8, await GetStockAsync(product.Id));
        Assert.Equal(HttpStatusCode.Conflict, secondTry.StatusCode);
    }

    [Fact]
    public async Task Cancel_ReturnsNotFound_ForMissingOrOtherUsersOrder()
    {
        var (owner, _, order) = await PlaceOrderAsync(email: "ayse@example.com");
        var other = await CreateCustomerClientAsync("mehmet@example.com");

        var notOwned = await CancelAsync(other, order.Id);
        var missing = await CancelAsync(owner, Guid.NewGuid());
        var stored = await GetOrderAsync(owner, order.Id);

        Assert.Equal(HttpStatusCode.NotFound, notOwned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Paid", stored.Status);
        Assert.Empty(Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task Cancel_RefundsAndRestocksOnce_WhenSentManyTimesAtOnce()
    {
        var (customer, product, order) = await PlaceOrderAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CancelAsync(customer, order.Id)));
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal("Cancelled", stored.Status);
        Assert.Single(Payments.CancelledPaymentIds);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Customer_CanOrderAgain_AfterCancelling()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await CancelAsync(customer, order.Id);
        var addresses = await customer.GetFromJsonAsync<List<AddressResponse>>("/api/addresses");
        await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id, 3));

        var response = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(addresses![0].Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(7, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Cancel_ReturnsUnauthorized_WithoutToken()
    {
        var response = await CancelAsync(AnonymousClient, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
