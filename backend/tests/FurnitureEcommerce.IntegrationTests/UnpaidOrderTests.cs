using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Infrastructure.Persistence;
using FurnitureEcommerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FurnitureEcommerce.IntegrationTests;

/// <summary>
/// What a customer can do with an order that is still waiting for payment: go back to the payment
/// page, or give the order up. Giving up must never lose a payment that arrives afterwards.
/// </summary>
[Collection(nameof(ApiCollection))]
public class UnpaidOrderTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string PaymentId = "payment-1";

    // The callback answers with a redirect to the frontend, which the tests must see, not follow.
    private readonly HttpClient browser = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private FakePaymentGateway Payments => Factory.Payments;

    /// <summary>A customer with 10 in stock and an unpaid order for 2, which are also still in the cart.</summary>
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

    private Task<HttpResponseMessage> SendCallbackAsync(OrderResponse order) =>
        browser.PostAsync(
            "/api/payments/callback",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = Payments.TokenFor(order.Id) }));

    private static Task<HttpResponseMessage> CancelAsync(HttpClient customer, Guid orderId) =>
        customer.PostAsync($"/api/orders/{orderId}/cancel", null);

    private static async Task<OrderResponse> GetOrderAsync(HttpClient customer, Guid orderId) =>
        (await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}"))!;

    private async Task<int> GetStockAsync(Guid productId) =>
        (await Client.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{productId}"))!.StockQuantity;

    private async Task<ReconciliationSummary> RunReconciliationAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IOrderReconciliationService>()
            .RunOnceAsync(CancellationToken.None);
    }

    private async Task<(DateTime CreatedAt, DateTime? PaymentRecheckAt)> GetTimesAsync(Guid orderId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await dbContext.Orders.AsNoTracking().SingleAsync(order => order.Id == orderId);
        return (order.CreatedAt, order.PaymentRecheckAt);
    }

    /// <summary>Makes the order look as if it was created some time ago.</summary>
    private async Task AgeOrderAsync(Guid orderId, TimeSpan age)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var then = DateTime.UtcNow - age;

        await dbContext.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.CreatedAt, then));
    }

    /// <summary>Moves the recheck of a closed order into the past, as if its payment page had expired.</summary>
    private async Task MakeRecheckDueAsync(Guid orderId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var past = DateTime.UtcNow.AddMinutes(-1);

        var changed = await dbContext.Orders
            .Where(order => order.Id == orderId && order.PaymentRecheckAt != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.PaymentRecheckAt, past));

        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task UnpaidOrder_OffersItsPaymentPage_AndCanBeCancelled()
    {
        var (customer, _, order) = await PlaceOrderAsync();

        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal("PendingPayment", stored.Status);
        Assert.Equal($"https://pay.example.test/?token={Payments.TokenFor(order.Id)}", stored.PaymentPageUrl);
        Assert.True(stored.CanCancel);
    }

    [Theory]
    [InlineData(27, true)]
    // The page lives for 30 minutes. The link is withdrawn two minutes earlier.
    [InlineData(29, false)]
    [InlineData(31, false)]
    public async Task UnpaidOrder_OffersItsPaymentPage_OnlyWhileThePageIsOpen(int minutesOld, bool isOffered)
    {
        var (customer, _, order) = await PlaceOrderAsync();
        await AgeOrderAsync(order.Id, TimeSpan.FromMinutes(minutesOld));

        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal("PendingPayment", stored.Status);
        Assert.Equal(isOffered, stored.PaymentPageUrl is not null);
        // Giving the order up stays possible until the job closes it.
        Assert.True(stored.CanCancel);
    }

    [Fact]
    public async Task PaidOrder_NoLongerOffersItsPaymentPage()
    {
        var (customer, _, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);
        await SendCallbackAsync(order);

        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal("Paid", stored.Status);
        Assert.Null(stored.PaymentPageUrl);
    }

    [Fact]
    public async Task Cancel_ClosesUnpaidOrder_ReleasesStock_AndKeepsTheCart()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        var stockBefore = await GetStockAsync(product.Id);

        var response = await CancelAsync(customer, order.Id);
        var cancelled = await response.Content.ReadFromJsonAsync<OrderResponse>();
        var cart = await customer.GetFromJsonAsync<CartResponse>("/api/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal(OrderService.CancelledByCustomerMessage, cancelled.PaymentError);
        Assert.Null(cancelled.PaidAt);
        Assert.Null(cancelled.PaymentPageUrl);
        Assert.False(cancelled.CanCancel);
        Assert.Equal(8, stockBefore);
        Assert.Equal(10, await GetStockAsync(product.Id));
        Assert.Equal(2, cart!.TotalQuantity);
        // Nothing was paid, so there is nothing to refund.
        Assert.Empty(Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task Customer_CanOrderAgain_RightAfterCancellingAnUnpaidOrder()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        var addresses = await customer.GetFromJsonAsync<List<AddressResponse>>("/api/addresses");
        var blocked = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(addresses![0].Id));

        await CancelAsync(customer, order.Id);
        var retry = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest(addresses[0].Id));

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Cancel_ReturnsNotFound_ForAnotherUsersUnpaidOrder()
    {
        var (owner, product, order) = await PlaceOrderAsync();
        var other = await CreateCustomerClientAsync("mehmet@example.com");

        var response = await CancelAsync(other, order.Id);
        var stored = await GetOrderAsync(owner, order.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("PendingPayment", stored.Status);
        Assert.Equal(8, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Cancel_ReleasesStockOnce_WhenSentManyTimesAtOnce()
    {
        var (customer, product, order) = await PlaceOrderAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CancelAsync(customer, order.Id)));
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal("Cancelled", stored.Status);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Callback_RefundsAPayment_MadeAfterTheCustomerCancelled()
    {
        // The payment page stays open after the order is cancelled, and the customer pays there after all.
        var (customer, product, order) = await PlaceOrderAsync();
        await CancelAsync(customer, order.Id);
        ProviderReportsPaid(order);

        var response = await SendCallbackAsync(order);
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(
            $"{ApiFactory.FrontendBaseUrl}/orders/{order.Id}?payment=refunded",
            response.Headers.Location?.ToString());
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
        Assert.Equal("Cancelled", stored.Status);
        Assert.Null(stored.PaidAt);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task Callback_ReportsRefunded_WhenTheLatePaymentWasAlreadyRefunded()
    {
        // The browser repeats the callback. The provider refuses to refund the same payment twice,
        // and that must not be shown as a refund that failed.
        var (customer, _, order) = await PlaceOrderAsync();
        await CancelAsync(customer, order.Id);
        ProviderReportsPaid(order);
        await SendCallbackAsync(order);
        Payments.CancelSaysAlreadyCancelled = true;

        var repeated = await SendCallbackAsync(order);

        Assert.Equal(
            $"{ApiFactory.FrontendBaseUrl}/orders/{order.Id}?payment=refunded",
            repeated.Headers.Location?.ToString());
        Assert.Single(Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task CancelledUnpaidOrder_IsMarkedForARecheck_AfterItsPaymentPageExpires()
    {
        var (customer, _, order) = await PlaceOrderAsync();

        await CancelAsync(customer, order.Id);
        var (createdAt, recheckAt) = await GetTimesAsync(order.Id);
        var summary = await RunReconciliationAsync();

        // 35 minutes after the order: the payment page (30 minutes) has expired by then.
        Assert.NotNull(recheckAt);
        Assert.Equal(createdAt.AddMinutes(35), recheckAt.Value, TimeSpan.FromSeconds(1));
        // Not due yet, so the job leaves the order alone.
        Assert.Equal(0, summary.Total);
    }

    [Fact]
    public async Task Reconciliation_RefundsAPayment_MadeAfterTheCustomerCancelled_WhenTheCallbackNeverArrived()
    {
        // The worst case: the customer cancels, pays on the still open page, and closes the browser
        // before it returns to the store. Without the recheck nobody would know about the payment.
        var (customer, product, order) = await PlaceOrderAsync();
        await CancelAsync(customer, order.Id);
        ProviderReportsPaid(order);
        await MakeRecheckDueAsync(order.Id);

        var summary = await RunReconciliationAsync();
        var second = await RunReconciliationAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Rechecked);
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
        Assert.Equal("Cancelled", stored.Status);
        Assert.Null(stored.PaidAt);
        Assert.Equal(10, await GetStockAsync(product.Id));
        // The recheck happens once.
        Assert.Equal(0, second.Total);
        Assert.Null((await GetTimesAsync(order.Id)).PaymentRecheckAt);
    }

    [Fact]
    public async Task Reconciliation_FinishesTheRecheck_WhenNothingWasPaid()
    {
        var (customer, product, order) = await PlaceOrderAsync();
        await CancelAsync(customer, order.Id);
        await MakeRecheckDueAsync(order.Id);

        var summary = await RunReconciliationAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Rechecked);
        Assert.Empty(Payments.CancelledPaymentIds);
        Assert.Equal("Cancelled", stored.Status);
        Assert.Equal(OrderService.CancelledByCustomerMessage, stored.PaymentError);
        Assert.Equal(10, await GetStockAsync(product.Id));
        Assert.Null((await GetTimesAsync(order.Id)).PaymentRecheckAt);
    }

    [Fact]
    public async Task Reconciliation_KeepsTheRecheck_WhileTheProviderCannotBeReached()
    {
        var (customer, _, order) = await PlaceOrderAsync();
        await CancelAsync(customer, order.Id);
        ProviderReportsPaid(order);
        await MakeRecheckDueAsync(order.Id);
        Payments.IsUnreachable = true;

        var whileDown = await RunReconciliationAsync();
        var stillMarked = (await GetTimesAsync(order.Id)).PaymentRecheckAt;
        Payments.IsUnreachable = false;
        var afterwards = await RunReconciliationAsync();

        Assert.Equal(1, whileDown.Skipped);
        Assert.NotNull(stillMarked);
        Assert.Equal(1, afterwards.Rechecked);
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task Reconciliation_RefundsAPayment_MadeAfterAFailedAttemptClosedTheOrder()
    {
        // A failed card closes the order while the payment page is still open. The customer then
        // pays with another card, and that callback is lost.
        var (customer, product, order) = await PlaceOrderAsync();
        Payments.SetResult(order.Id, CheckoutResult.Failed("Insufficient funds"));
        await SendCallbackAsync(order);
        ProviderReportsPaid(order);
        await MakeRecheckDueAsync(order.Id);

        var summary = await RunReconciliationAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Rechecked);
        Assert.Equal([PaymentId], Payments.CancelledPaymentIds);
        Assert.Equal("PaymentFailed", stored.Status);
        Assert.Equal(10, await GetStockAsync(product.Id));
    }

    [Fact]
    public async Task OrderClosedByTheTimeout_NeedsNoRecheck()
    {
        // By the time the job closes an unpaid order, its payment page has already expired.
        var (customer, _, order) = await PlaceOrderAsync();
        await AgeOrderAsync(order.Id, TimeSpan.FromHours(1));

        var summary = await RunReconciliationAsync();
        var stored = await GetOrderAsync(customer, order.Id);

        Assert.Equal(1, summary.Closed);
        Assert.Equal("Cancelled", stored.Status);
        Assert.Null((await GetTimesAsync(order.Id)).PaymentRecheckAt);
    }

    [Fact]
    public async Task CancelAndPayment_AtTheSameTime_NeverLoseStockOrMoney()
    {
        // The customer presses "cancel" in one tab at the moment the payment in another tab succeeds.
        // Either side may win, but the stock and the money must agree with the order that results.
        var (customer, product, order) = await PlaceOrderAsync();
        ProviderReportsPaid(order);

        var cancels = Enumerable.Range(0, 4).Select(_ => CancelAsync(customer, order.Id));
        var callbacks = Enumerable.Range(0, 2).Select(_ => SendCallbackAsync(order));
        await Task.WhenAll(cancels.Concat(callbacks));

        var stored = await GetOrderAsync(customer, order.Id);
        var stock = await GetStockAsync(product.Id);

        if (stored.Status == "Paid")
        {
            // The payment won and every cancel request was too early or refused: the customer keeps the order.
            Assert.Equal(8, stock);
            Assert.Empty(Payments.CancelledPaymentIds);
        }
        else
        {
            // The order was cancelled, before or after the payment arrived. The stock went back once,
            // and the payment was refunded.
            Assert.Equal("Cancelled", stored.Status);
            Assert.Equal(10, stock);
            Assert.NotEmpty(Payments.CancelledPaymentIds);
            Assert.All(Payments.CancelledPaymentIds, paymentId => Assert.Equal(PaymentId, paymentId));
        }
    }
}
