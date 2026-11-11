using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Infrastructure.Persistence;
using FurnitureEcommerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class ReconciliationTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string PaymentId = "payment-1";

    // Longer than both timeouts (35 minutes for unpaid orders, 2 minutes for cancelling ones).
    private static readonly TimeSpan LongAgo = TimeSpan.FromHours(1);

    private FakePaymentGateway Payments => Factory.Payments;

    /// <summary>A customer with 10 in stock and an unpaid order for 2.</summary>
    private async Task<(HttpClient Customer, ProductResponse Product, OrderResponse Order)> PlaceOrderAsync()
    {
        var customer = await CreateCustomerClientAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, "Oslo Sofa", price: 1000, stock: 10);

        var address = await (await customer.PostAsJsonAsync(
                "/api/addresses",
                new AddressRequest(
                    "Home", "Ayse", "Yilmaz", "+905551112233", "Istanbul", "Kadikoy", "Moda Cd. No: 1", "34710")))
            .Content.ReadFromJsonAsync<AddressResponse>();

        await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id, 2));
        var response = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(address!.Id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (customer, product, (await response.Content.ReadFromJsonAsync<OrderResponse>())!);
    }

    private void ProviderReportsPaid(OrderResponse order) =>
        Payments.SetResult(order.Id, CheckoutResult.Paid(PaymentId, order.TotalAmount, order.Id.ToString()));

    /// <summary>Pays the order, then starts a cancellation whose refund request gets no answer.</summary>
    private async Task LeaveInCancellingAsync(HttpClient customer, OrderResponse order)
    {
        ProviderReportsPaid(order);
        await AnonymousClient.PostAsync(
            "/api/payments/callback",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = Payments.TokenFor(order.Id) }));

        Payments.IsUnreachable = true;
        var response = await customer.PostAsync($"/api/orders/{order.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Payments.IsUnreachable = false;
    }

    /// <summary>Makes the order look as if it was created and last changed some time ago.</summary>
    private async Task AgeOrderAsync(Guid orderId, TimeSpan age, bool removePaymentToken = false)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var then = DateTime.UtcNow - age;

        await dbContext.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.CreatedAt, then)
                .SetProperty(order => order.UpdatedAt, then)
                .SetProperty(order => order.PaymentToken, order => removePaymentToken ? null : order.PaymentToken));
    }

    private async Task<ReconciliationSummary> RunAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IOrderReconciliationService>()
            .RunOnceAsync(CancellationToken.None);
    }

    private static async Task<OrderResponse> GetOrderAsync(HttpClient customer, Guid orderId) =>
        (await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}"))!;

    private async Task<int> GetStockAsync(Guid productId) =>
        (await Client.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{productId}"))!.StockQuantity;

    [Fact]
    public async Task UnpaidOrder_IsCancelledAndStockReleased_AfterTheTimeout()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await AgeOrderAsync(order.Id, LongAgo);

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Closed);
        Assert.Equal("Cancelled", stored.Status);
        Assert.Equal(OrderReconciliationService.TimeoutMessage, stored.PaymentError);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task UnpaidOrder_IsLeftAlone_BeforeTheTimeout()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        // The payment page lives for 30 minutes, so the order must survive that long.
        await AgeOrderAsync(order.Id, TimeSpan.FromMinutes(31));

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(0, summary.Total);
        Assert.Equal("PendingPayment", stored.Status);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task UnpaidOrder_BecomesPaid_WhenPaymentSucceededButCallbackNeverArrived()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);
        await AgeOrderAsync(order.Id, LongAgo);

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);
        var cart = await customer.GetFromJsonAsync<CartResponse>("/api/cart");

        Assert.Equal(1, summary.Paid);
        Assert.Equal("Paid", stored.Status);
        Assert.NotNull(stored.PaidAt);
        Assert.Empty(cart!.Items);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task UnpaidOrder_BecomesPaymentFailed_WhenLastAttemptFailed()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        Payments.SetResult(order.Id, CheckoutResult.Failed("Insufficient funds"));
        await AgeOrderAsync(order.Id, LongAgo);

        await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal("PaymentFailed", stored.Status);
        Assert.Equal("Insufficient funds", stored.PaymentError);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task UnpaidOrder_IsLeftAlone_WhenProviderCannotBeReached()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await AgeOrderAsync(order.Id, LongAgo);
        Payments.IsUnreachable = true;

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Skipped);
        Assert.Equal("PendingPayment", stored.Status);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task UnpaidOrder_WithoutPaymentPage_IsCancelled()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await AgeOrderAsync(order.Id, LongAgo, removePaymentToken: true);

        await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal("Cancelled", stored.Status);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Customer_CanOrderAgain_AfterUnpaidOrderTimedOut()
    {
        var (customer, _, order) = await PlaceOrderAsync();
        var addresses = await customer.GetFromJsonAsync<List<AddressResponse>>("/api/addresses");
        var blocked = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(addresses![0].Id));
        await AgeOrderAsync(order.Id, LongAgo);

        await RunAsync();
        var retry = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(addresses[0].Id));

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Run_ChangesNothing_WhenRepeated()
    {
        var (_, product, order) = await PlaceOrderAsync();
        await AgeOrderAsync(order.Id, LongAgo);

        await RunAsync();
        var second = await RunAsync();

        Assert.Equal(0, second.Total);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Run_ReleasesStockOnce_WhenStartedManyTimesAtOnce()
    {
        var (_, product, order) = await PlaceOrderAsync();
        await AgeOrderAsync(order.Id, LongAgo);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RunAsync()));

        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task CallbackAndReconciliation_AtTheSameTime_PayTheOrderOnce()
    {
        // The callback arrives late, at the moment the job is looking at the same order.
        var (customer, product, order) = await PlaceOrderAsync();
        await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id, 3));
        ProviderReportsPaid(order);
        await AgeOrderAsync(order.Id, LongAgo);
        var token = Payments.TokenFor(order.Id);

        var callbacks = Enumerable.Range(0, 3).Select(_ => AnonymousClient.PostAsync(
            "/api/payments/callback",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token })));
        var runs = Enumerable.Range(0, 3).Select(_ => RunAsync());
        await Task.WhenAll(callbacks.Cast<Task>().Concat(runs));

        var stored = await GetOrderAsync(customer, order.Id);
        var cart = await customer.GetFromJsonAsync<CartResponse>("/api/cart");

        Assert.Equal("Paid", stored.Status);
        // 5 in the cart, 2 ordered. A second decrement would leave 1.
        Assert.Equal(3, cart!.TotalQuantity);
        Assert.Equal(8, await GetStockAsync(product.Id));
        Assert.Empty(Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task CancellingOrder_IsCancelledAndStockReleased_WhenRefundSucceedsOnRetry()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await LeaveInCancellingAsync(customer, order);
        await AgeOrderAsync(order.Id, LongAgo);

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Cancelled);
        Assert.Equal("Cancelled", stored.Status);
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task CancellingOrder_IsCancelled_WhenProviderSaysPaymentWasAlreadyCancelled()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await LeaveInCancellingAsync(customer, order);
        await AgeOrderAsync(order.Id, LongAgo);
        Payments.CancelSaysAlreadyCancelled = true;

        await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal("Cancelled", stored.Status);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task CancellingOrder_GoesBackToPaid_WhenRefundIsRefused()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await LeaveInCancellingAsync(customer, order);
        await AgeOrderAsync(order.Id, LongAgo);
        Payments.CancelError = "Cannot cancel";

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.RevertedToPaid);
        Assert.Equal("Paid", stored.Status);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task CancellingOrder_IsLeftAlone_WhileItsCancelRequestMayStillBeRunning()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await LeaveInCancellingAsync(customer, order);

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(0, summary.Total);
        Assert.Equal("Cancelling", stored.Status);
        Assert.Empty(Payments.CancelledPaymentIds);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task CancellingOrder_IsLeftAlone_WhenProviderCannotBeReached()
    {
        var (customer, _, order) = await PlaceOrderAsync();
        await LeaveInCancellingAsync(customer, order);
        await AgeOrderAsync(order.Id, LongAgo);
        Payments.IsUnreachable = true;

        var summary = await RunAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Skipped);
        Assert.Equal("Cancelling", stored.Status);
    }
}
