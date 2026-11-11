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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class PaymentsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string CallbackPath = "/api/payments/callback";
    private const string PaymentId = "payment-1";

    // The callback answers with a redirect to the frontend, which the tests must see, not follow.
    private readonly HttpClient browser = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private FakePaymentGateway Payments => Factory.Payments;

    /// <summary>A customer with 10 in stock, 2 in an order waiting for payment and 5 in the cart.</summary>
    private async Task<(HttpClient Customer, ProductResponse Product, OrderResponse Order)> PlaceOrderAsync(
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
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        // Added after the order, so it must survive when the order is paid.
        await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id, 3));

        return (customer, product, order!);
    }

    private void ProviderReportsPaid(OrderResponse order, decimal? amount = null) =>
        Payments.SetResult(order.Id, CheckoutResult.Paid(PaymentId, amount ?? order.TotalAmount, order.Id.ToString()));

    private Task<HttpResponseMessage> SendCallbackAsync(string? token)
    {
        var fields = token is null ? [] : new Dictionary<string, string> { ["token"] = token };
        return browser.PostAsync(CallbackPath, new FormUrlEncodedContent(fields));
    }

    private Task<HttpResponseMessage> SendCallbackAsync(OrderResponse order) =>
        SendCallbackAsync(Payments.TokenFor(order.Id));

    private static void AssertRedirect(HttpResponseMessage response, string expectedPathAndQuery)
    {
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal($"{ApiFactory.FrontendBaseUrl}{expectedPathAndQuery}", response.Headers.Location?.ToString());
    }

    private static async Task<OrderResponse> GetOrderAsync(HttpClient customer, Guid orderId) =>
        (await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}"))!;

    private static async Task<int> GetCartQuantityAsync(HttpClient customer) =>
        (await customer.GetFromJsonAsync<CartResponse>("/api/cart"))!.TotalQuantity;

    private async Task<int> GetStockAsync(Guid productId) =>
        (await Client.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{productId}"))!.StockQuantity;

    private async Task SetStatusAsync(Guid orderId, OrderStatus status, string? paymentId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.Status, status)
                .SetProperty(order => order.PaymentId, paymentId));
    }

    [Fact]
    public async Task CreateOrder_ReturnsPaymentPageUrl()
    {
        var (customer, _, order) = await PlaceOrderAsync();

        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal($"https://pay.example.test/?token={Payments.TokenFor(order.Id)}", order.PaymentPageUrl);
        Assert.Null(stored.PaymentPageUrl);
    }

    [Fact]
    public async Task CreateOrder_ReturnsBadGateway_AndReleasesStock_WhenPaymentPageCannotBeOpened()
    {
        Payments.StartError = "Provider is down";
        var customer = await CreateCustomerClientAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, stock: 10);
        var address = await (await customer.PostAsJsonAsync(
                "/api/addresses",
                new AddressRequest("Home", "Ayse", "Yilmaz", "+905551112233", "Istanbul", "Kadikoy", "Moda Cd. 1", null)))
            .Content.ReadFromJsonAsync<AddressResponse>();
        await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id, 2));

        var failed = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(address!.Id));
        var orders = await customer.GetFromJsonAsync<List<OrderSummaryResponse>>("/api/orders");
        var stockAfterFailure = await GetStockAsync(product.Id);

        Payments.StartError = null;
        var retry = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(address.Id));

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal("PaymentFailed", Assert.Single(orders!).Status);
        Assert.Equal(10, stockAfterFailure);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Callback_MarksOrderPaid_AndTakesOrderedItemsOutOfCart()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(response, $"/orders/{order.Id}?payment=success");
        Assert.Equal("Paid", stored.Status);
        Assert.NotNull(stored.PaidAt);
        Assert.Null(stored.PaymentError);
        Assert.Equal(3, await GetCartQuantityAsync(customer));
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Callback_RemovesCartLine_WhenWholeQuantityWasOrdered()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await customer.PutAsJsonAsync($"/api/cart/items/{product.Id}", new UpdateCartItemRequest(2));
        ProviderReportsPaid(order);

        await SendCallbackAsync(order);
        var cart = await customer.GetFromJsonAsync<CartResponse>("/api/cart");

        Assert.Empty(cart!.Items);
    }

    [Fact]
    public async Task Callback_MarksOrderFailed_ReleasesStock_AndKeepsCart()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        Payments.SetResult(order.Id, CheckoutResult.Failed("Insufficient funds"));

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(response, $"/orders/{order.Id}?payment=failed");
        Assert.Equal("PaymentFailed", stored.Status);
        Assert.Equal("Insufficient funds", stored.PaymentError);
        Assert.Null(stored.PaidAt);
        Assert.Equal(5, await GetCartQuantityAsync(customer));
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Callback_ReleasesStockOnce_WhenFailureIsReportedManyTimesAtOnce()
    {
        var (_, product, order) = await PlaceOrderAsync();
        Payments.SetResult(order.Id, CheckoutResult.Failed("Insufficient funds"));

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendCallbackAsync(order)));

        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Callback_LeavesOrderWaiting_WhenPaymentIsNotCompleted()
    {
        var (customer, product, order) = await PlaceOrderAsync();

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(response, $"/orders/{order.Id}?payment=pending");
        Assert.Equal("PendingPayment", stored.Status);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Callback_LeavesOrderWaiting_WhenProviderCannotBeReached()
    {
        var (customer, _, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);
        Payments.IsUnreachable = true;

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(response, $"/orders/{order.Id}?payment=pending");
        Assert.Equal("PendingPayment", stored.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Callback_RedirectsWithUnknown_ForMissingOrUnknownToken(string? token)
    {
        var response = await SendCallbackAsync(token);

        AssertRedirect(response, "/orders?payment=unknown");
    }

    [Fact]
    public async Task Callback_ChangesNothing_WhenRepeated()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);

        var first = await SendCallbackAsync(order);
        var paidAt = (await GetOrderAsync(customer, order.Id)).PaidAt;
        var second = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(first, $"/orders/{order.Id}?payment=success");
        AssertRedirect(second, $"/orders/{order.Id}?payment=success");
        Assert.Equal(paidAt, stored.PaidAt);
        Assert.Equal(3, await GetCartQuantityAsync(customer));
        Assert.Equal(8, await GetStockAsync(product.Id));
        Assert.Empty(Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task Callback_PaysOrderOnce_WhenSentManyTimesAtOnce()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendCallbackAsync(order)));
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.All(responses, response => AssertRedirect(response, $"/orders/{order.Id}?payment=success"));
        Assert.Equal("Paid", stored.Status);
        // 5 in the cart, 2 ordered. A second decrement would leave 1.
        Assert.Equal(3, await GetCartQuantityAsync(customer));
        Assert.Equal(8, await GetStockAsync(product.Id));
        Assert.Empty(Payments.CancelledPaymentIds);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.PaymentFailed)]
    public async Task Callback_RefundsLatePayment_AndLeavesClosedOrderAsItIs(OrderStatus status)
    {
        var (customer, _, order) = await PlaceOrderAsync();
        await SetStatusAsync(order.Id, status);
        ProviderReportsPaid(order);

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(response, $"/orders/{order.Id}?payment=refunded");
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
        Assert.Equal(status.ToString(), stored.Status);
        Assert.Null(stored.PaidAt);
        Assert.Equal(5, await GetCartQuantityAsync(customer));
    }

    [Fact]
    public async Task Callback_ReportsRefundFailed_WhenLatePaymentCannotBeRefunded()
    {
        var (_, _, order) = await PlaceOrderAsync();
        await SetStatusAsync(order.Id, OrderStatus.Cancelled);
        ProviderReportsPaid(order);
        Payments.CancelError = "Cannot cancel";

        var response = await SendCallbackAsync(order);

        AssertRedirect(response, $"/orders/{order.Id}?payment=refund-failed");
    }

    [Fact]
    public async Task Callback_DoesNotRefund_WhenRepeatedAfterOrderWasShipped()
    {
        var (customer, _, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);
        await SendCallbackAsync(order);
        await SetStatusAsync(order.Id, OrderStatus.Shipped, PaymentId);

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(response, $"/orders/{order.Id}?payment=success");
        Assert.Empty(Payments.CancelledPaymentIds);
        Assert.Equal("Shipped", stored.Status);
    }

    [Fact]
    public async Task Callback_RefundsAndFailsOrder_WhenPaidAmountDoesNotMatch()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order, amount: 1.00m);

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        AssertRedirect(response, $"/orders/{order.Id}?payment=refunded");
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
        Assert.Equal("PaymentFailed", stored.Status);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Customer_CanOrderAgain_AfterFailedPayment()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        Payments.SetResult(order.Id, CheckoutResult.Failed("Insufficient funds"));
        await SendCallbackAsync(order);
        var addresses = await customer.GetFromJsonAsync<List<AddressResponse>>("/api/addresses");

        var retry = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(addresses![0].Id));
        var newOrder = await retry.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.NotEqual(order.Id, newOrder!.Id);
        Assert.Equal(5000, newOrder.TotalAmount);
        Assert.Equal(5, await GetStockAsync(product.Id));
    }
}
