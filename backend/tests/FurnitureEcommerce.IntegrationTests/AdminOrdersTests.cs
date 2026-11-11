using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Common;
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
public class AdminOrdersTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string AdminOrdersPath = "/api/admin/orders";

    private FakePaymentGateway Payments => Factory.Payments;

    /// <summary>A new customer orders 2 of a new product. The order is paid unless told otherwise.</summary>
    private async Task<(HttpClient Customer, ProductResponse Product, OrderResponse Order)> PlaceOrderAsync(
        string email = "ayse@example.com",
        bool pay = true)
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
            Payments.SetResult(
                order.Id,
                CheckoutResult.Paid($"payment-{order.Id}", order.TotalAmount, order.Id.ToString()));
            await AnonymousClient.PostAsync(
                "/api/payments/callback",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = Payments.TokenFor(order.Id) }));
        }

        return (customer, product, order);
    }

    private Task<HttpResponseMessage> ShipAsync(Guid orderId) =>
        Client.PostAsync($"{AdminOrdersPath}/{orderId}/ship", null);

    private Task<HttpResponseMessage> DeliverAsync(Guid orderId) =>
        Client.PostAsync($"{AdminOrdersPath}/{orderId}/deliver", null);

    private async Task<AdminOrderResponse> GetAsync(Guid orderId) =>
        (await Client.GetFromJsonAsync<AdminOrderResponse>($"{AdminOrdersPath}/{orderId}"))!;

    private async Task<PagedResult<AdminOrderSummaryResponse>> ListAsync(string query = "") =>
        (await Client.GetFromJsonAsync<PagedResult<AdminOrderSummaryResponse>>($"{AdminOrdersPath}{query}"))!;

    private async Task SetStatusAsync(Guid orderId, OrderStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Status, status));
    }

    [Fact]
    public async Task GetAll_ReturnsOrdersOfAllCustomers_NewestFirst()
    {
        var (_, _, first) = await PlaceOrderAsync("ayse@example.com");
        var (_, _, second) = await PlaceOrderAsync("mehmet@example.com", pay: false);

        var orders = await ListAsync();

        Assert.Equal(2, orders.TotalCount);
        Assert.Equal([second.Id, first.Id], orders.Items.Select(order => order.Id));
        Assert.Equal(["PendingPayment", "Paid"], orders.Items.Select(order => order.Status));
        var paid = orders.Items[1];
        Assert.Equal("Ayse Yilmaz", paid.RecipientName);
        Assert.Equal("Istanbul", paid.City);
        Assert.Equal(2000, paid.TotalAmount);
        Assert.Equal(2, paid.TotalQuantity);
        Assert.NotNull(paid.PaidAt);
    }

    [Fact]
    public async Task GetAll_FiltersByStatus_AndPages()
    {
        var (_, _, ayses) = await PlaceOrderAsync("ayse@example.com");
        var (_, _, mehmets) = await PlaceOrderAsync("mehmet@example.com");
        await PlaceOrderAsync("zeynep@example.com", pay: false);

        var paid = await ListAsync("?status=paid");
        var secondPage = await ListAsync("?status=Paid&page=2&pageSize=1");
        var shipped = await ListAsync("?status=Shipped");

        Assert.Equal([mehmets.Id, ayses.Id], paid.Items.Select(order => order.Id));
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal(2, secondPage.TotalPages);
        Assert.Equal(ayses.Id, Assert.Single(secondPage.Items).Id);
        Assert.Empty(shipped.Items);
    }

    [Theory]
    [InlineData("?status=Unknown")]
    [InlineData("?status=3")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    public async Task GetAll_ReturnsBadRequest_ForInvalidQuery(string query)
    {
        var response = await Client.GetAsync($"{AdminOrdersPath}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsAnyCustomersOrder_WithPaymentDetails()
    {
        var (_, product, order) = await PlaceOrderAsync();

        var stored = await GetAsync(order.Id);

        Assert.Equal("Paid", stored.Status);
        Assert.Equal($"payment-{order.Id}", stored.PaymentId);
        Assert.Equal("Moda Cd. No: 1", stored.AddressLine);
        Assert.Equal(new OrderItemResponse(product.Id, "Oslo Sofa", 1000, 2, 2000), Assert.Single(stored.Items));
    }

    [Fact]
    public async Task Ship_ThenDeliver_MovesPaidOrderThroughItsStatuses()
    {
        var (customer, product, order) = await PlaceOrderAsync();

        var ship = await ShipAsync(order.Id);
        var shipped = await ship.Content.ReadFromJsonAsync<AdminOrderResponse>();
        var deliver = await DeliverAsync(order.Id);
        var delivered = await deliver.Content.ReadFromJsonAsync<AdminOrderResponse>();
        var customersView = await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}");
        var stock = (await Client.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{product.Id}"))!;

        Assert.Equal(HttpStatusCode.OK, ship.StatusCode);
        Assert.Equal("Shipped", shipped!.Status);
        Assert.Equal(HttpStatusCode.OK, deliver.StatusCode);
        Assert.Equal("Delivered", delivered!.Status);
        Assert.Equal("Delivered", customersView!.Status);
        Assert.False(customersView.CanCancel);
        Assert.Equal(8, stock.StockQuantity);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Cancelling)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Ship_ReturnsConflict_UnlessOrderIsPaid(OrderStatus status)
    {
        var (_, _, order) = await PlaceOrderAsync();
        await SetStatusAsync(order.Id, status);

        var response = await ShipAsync(order.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(status.ToString(), (await GetAsync(order.Id)).Status);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelling)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Deliver_ReturnsConflict_UnlessOrderIsShipped(OrderStatus status)
    {
        var (_, _, order) = await PlaceOrderAsync();
        await SetStatusAsync(order.Id, status);

        var response = await DeliverAsync(order.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(status.ToString(), (await GetAsync(order.Id)).Status);
    }

    [Fact]
    public async Task ShippedOrder_CanNoLongerBeCancelledByCustomer()
    {
        var (customer, _, order) = await PlaceOrderAsync();
        await ShipAsync(order.Id);

        var cancel = await customer.PostAsync($"/api/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("Shipped", (await GetAsync(order.Id)).Status);
        Assert.Empty(Payments.CancelledPaymentIds);
    }

    [Fact]
    public async Task ShipAndCancel_AtTheSameTime_NeverBothSucceed()
    {
        var (customer, _, order) = await PlaceOrderAsync();

        var cancelTask = customer.PostAsync($"/api/orders/{order.Id}/cancel", null);
        var shipTask = ShipAsync(order.Id);
        var cancelResponse = await cancelTask;
        var shipResponse = await shipTask;

        var cancelled = cancelResponse.StatusCode == HttpStatusCode.OK;
        var shipped = shipResponse.StatusCode == HttpStatusCode.OK;
        var status = (await GetAsync(order.Id)).Status;

        // Exactly one of them wins, and the money is refunded only if the cancellation won.
        Assert.True(cancelled ^ shipped, $"cancel={cancelResponse.StatusCode}, ship={shipResponse.StatusCode}");
        Assert.Equal(cancelled ? "Cancelled" : "Shipped", status);
        Assert.Equal(cancelled ? 1 : 0, Payments.CancelledPaymentIds.Count);
    }

    [Fact]
    public async Task Endpoints_ReturnNotFound_ForMissingOrder()
    {
        var id = Guid.NewGuid();

        var get = await Client.GetAsync($"{AdminOrdersPath}/{id}");
        var ship = await ShipAsync(id);
        var deliver = await DeliverAsync(id);

        Assert.All([get, ship, deliver], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
    }

    [Fact]
    public async Task Endpoints_ReturnForbidden_ForCustomer()
    {
        var (customer, _, order) = await PlaceOrderAsync();

        var list = await customer.GetAsync(AdminOrdersPath);
        var get = await customer.GetAsync($"{AdminOrdersPath}/{order.Id}");
        var ship = await customer.PostAsync($"{AdminOrdersPath}/{order.Id}/ship", null);
        var deliver = await customer.PostAsync($"{AdminOrdersPath}/{order.Id}/deliver", null);

        Assert.All([list, get, ship, deliver], response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        Assert.Equal("Paid", (await GetAsync(order.Id)).Status);
    }
}
