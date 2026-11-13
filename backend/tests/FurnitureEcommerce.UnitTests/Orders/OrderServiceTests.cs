using FluentValidation;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FurnitureEcommerce.UnitTests.Orders;

public class OrderServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IOrderRepository repository = Substitute.For<IOrderRepository>();
    private readonly ICartRepository cartRepository = Substitute.For<ICartRepository>();
    private readonly IAddressRepository addressRepository = Substitute.For<IAddressRepository>();
    private readonly IIdentityService identityService = Substitute.For<IIdentityService>();
    private readonly IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
    private readonly FixedClock clock = new();
    private readonly OrderService service;

    private readonly Address address = new()
    {
        UserId = UserId,
        Title = "Home",
        RecipientFirstName = "Ayse",
        RecipientLastName = "Yilmaz",
        PhoneNumber = "+905551112233",
        City = "Istanbul",
        District = "Kadikoy",
        AddressLine = "Moda Cd. No: 1",
        PostalCode = "34710"
    };

    public OrderServiceTests()
    {
        service = new OrderService(
            repository,
            cartRepository,
            addressRepository,
            identityService,
            gateway,
            new PaymentSettings("http://api/callback", "http://frontend"),
            clock,
            NullLogger<OrderService>.Instance,
            new CreateOrderRequestValidator());

        identityService.GetUserAsync(UserId)
            .Returns(new AuthUser(UserId, "ayse@example.com", "Ayse", "Yilmaz", [Roles.Customer]));
        gateway.StartCheckoutAsync(Arg.Any<CheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns(CheckoutStart.Started("token-1", "https://pay/token-1"));

        addressRepository.GetForUserAsync(address.Id, UserId, Arg.Any<CancellationToken>()).Returns(address);
        repository.CreateAndReserveStockAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);
    }

    private static Product NewProduct(
        string name = "Oslo Sofa",
        decimal price = 1000,
        int stock = 10,
        bool isActive = true,
        bool isCategoryActive = true)
    {
        return new Product
        {
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Price = price,
            StockQuantity = stock,
            IsActive = isActive,
            Category = new Category { Name = "Sofas", Slug = "sofas", IsActive = isCategoryActive }
        };
    }

    private void CartContains(params (Product Product, int Quantity)[] lines)
    {
        var items = lines
            .Select(line => new CartItem
            {
                Product = line.Product,
                ProductId = line.Product.Id,
                Quantity = line.Quantity
            })
            .ToList();

        cartRepository.GetItemsAsync(UserId, Arg.Any<CancellationToken>()).Returns(items);
    }

    private Task<OrderResponse> CreateAsync() =>
        service.CreateAsync(UserId, new CreateOrderRequest(address.Id), "10.0.0.1", CancellationToken.None);

    private Task AssertNothingSavedAsync() =>
        repository.DidNotReceiveWithAnyArgs().CreateAndReserveStockAsync(default!, default);

    [Fact]
    public async Task CreateAsync_CopiesAddressNamesAndPrices_AndWaitsForPayment()
    {
        var sofa = NewProduct("Oslo Sofa", price: 1000);
        var lamp = NewProduct("Malmo Lamp", price: 249.90m);
        CartContains((sofa, 2), (lamp, 3));

        var order = await CreateAsync();

        Assert.Equal("PendingPayment", order.Status);
        Assert.Equal(2749.70m, order.TotalAmount);
        Assert.Equal("Ayse", order.RecipientFirstName);
        Assert.Equal("Moda Cd. No: 1", order.AddressLine);
        Assert.Equal("34710", order.PostalCode);
        Assert.Equal(
            [
                new OrderItemResponse(sofa.Id, "Oslo Sofa", 1000, 2, 2000),
                new OrderItemResponse(lamp.Id, "Malmo Lamp", 249.90m, 3, 749.70m)
            ],
            order.Items.OrderByDescending(item => item.UnitPrice));
        await repository.Received(1).CreateAndReserveStockAsync(
            Arg.Is<Order>(saved => saved.UserId == UserId && saved.Status == OrderStatus.PendingPayment),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotFound_WhenAddressIsNotTheUsers()
    {
        CartContains((NewProduct(), 1));
        var otherAddressId = Guid.NewGuid();
        addressRepository.GetForUserAsync(otherAddressId, UserId, Arg.Any<CancellationToken>())
            .Returns((Address?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(UserId, new CreateOrderRequest(otherAddressId), null, CancellationToken.None));

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task CreateAsync_ThrowsConflict_WhenCartIsEmpty()
    {
        CartContains();

        await Assert.ThrowsAsync<ConflictException>(CreateAsync);

        await AssertNothingSavedAsync();
    }

    [Theory]
    [InlineData(false, true, 10)]
    [InlineData(true, false, 10)]
    [InlineData(true, true, 1)]
    public async Task CreateAsync_ThrowsConflict_WhenAnItemCannotBeOrdered(
        bool isActive,
        bool isCategoryActive,
        int stock)
    {
        CartContains(
            (NewProduct("Oslo Sofa"), 1),
            (NewProduct("Malmo Lamp", stock: stock, isActive: isActive, isCategoryActive: isCategoryActive), 2));

        var exception = await Assert.ThrowsAsync<ConflictException>(CreateAsync);

        Assert.Contains("Malmo Lamp", exception.Message);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task CreateAsync_ThrowsConflict_WhenStockIsTakenWhileSaving()
    {
        var sofa = NewProduct("Oslo Sofa");
        var lamp = NewProduct("Malmo Lamp");
        CartContains((sofa, 1), (lamp, 1));
        repository.CreateAndReserveStockAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(lamp.Id);

        var exception = await Assert.ThrowsAsync<ConflictException>(CreateAsync);

        Assert.Contains("Malmo Lamp", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_OpensPaymentPage_AndStoresItsToken()
    {
        CartContains((NewProduct(), 1));

        var order = await CreateAsync();

        Assert.Equal("https://pay/token-1", order.PaymentPageUrl);
        await gateway.Received(1).StartCheckoutAsync(
            Arg.Is<CheckoutRequest>(request =>
                request.Order.Id == order.Id
                && request.CallbackUrl == "http://api/callback"
                && request.Buyer == new PaymentBuyer(UserId, "Ayse", "Yilmaz", "ayse@example.com", "10.0.0.1")),
            Arg.Any<CancellationToken>());
        await repository.Received(1).SetPaymentPageAsync(
            order.Id, "token-1", "https://pay/token-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReleasesTheOrder_WhenPaymentPageCannotBeOpened()
    {
        CartContains((NewProduct(), 1));
        gateway.StartCheckoutAsync(Arg.Any<CheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns(CheckoutStart.Failed("Provider is down"));

        await Assert.ThrowsAsync<PaymentGatewayException>(CreateAsync);

        await repository.Received(1).TryMarkPaymentFailedAsync(
            Arg.Any<Guid>(), "Provider is down", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SetPaymentPageAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task CreateAsync_DoesNotCallProvider_WhenStockIsTakenWhileSaving()
    {
        var product = NewProduct();
        CartContains((product, 1));
        repository.CreateAndReserveStockAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(product.Id);

        await Assert.ThrowsAsync<ConflictException>(CreateAsync);

        await gateway.DidNotReceiveWithAnyArgs().StartCheckoutAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_ThrowsValidation_ForMissingAddressId()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(UserId, new CreateOrderRequest(Guid.Empty), null, CancellationToken.None));

        await AssertNothingSavedAsync();
    }

    private Order StoredOrder(OrderStatus status, int minutesOld = 1) => new()
    {
        UserId = UserId,
        Status = status,
        TotalAmount = 1000,
        CreatedAt = clock.UtcNow.UtcDateTime.AddMinutes(-minutesOld),
        PaymentToken = "token-1",
        PaymentPageUrl = "https://pay/token-1",
        RecipientFirstName = "Ayse",
        RecipientLastName = "Yilmaz",
        PhoneNumber = "+905551112233",
        City = "Istanbul",
        District = "Kadikoy",
        AddressLine = "Moda Cd. No: 1"
    };

    [Theory]
    [InlineData(1, true)]
    [InlineData(27, true)]
    // The page lives for 30 minutes. The link is withdrawn two minutes earlier.
    [InlineData(28, false)]
    [InlineData(40, false)]
    public async Task GetByIdAsync_OffersThePaymentPage_OnlyWhileItCanStillBePaid(int minutesOld, bool isOffered)
    {
        var order = StoredOrder(OrderStatus.PendingPayment, minutesOld);
        repository.GetForUserAsync(order.Id, UserId, Arg.Any<CancellationToken>()).Returns(order);

        var response = await service.GetByIdAsync(UserId, order.Id, CancellationToken.None);

        Assert.Equal(isOffered ? "https://pay/token-1" : null, response.PaymentPageUrl);
        Assert.True(response.CanCancel);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task GetByIdAsync_DoesNotOfferThePaymentPage_WhenOrderNoLongerWaits(OrderStatus status)
    {
        var order = StoredOrder(status);
        repository.GetForUserAsync(order.Id, UserId, Arg.Any<CancellationToken>()).Returns(order);

        var response = await service.GetByIdAsync(UserId, order.Id, CancellationToken.None);

        Assert.Null(response.PaymentPageUrl);
    }

    [Fact]
    public async Task CancelAsync_ClosesUnpaidOrder_WithoutCallingTheProvider()
    {
        var waiting = StoredOrder(OrderStatus.PendingPayment);
        var closed = StoredOrder(OrderStatus.Cancelled);
        repository.GetForUserAsync(waiting.Id, UserId, Arg.Any<CancellationToken>()).Returns(waiting, closed);
        repository.TryCancelUnpaidAsync(waiting.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var response = await service.CancelAsync(UserId, waiting.Id, CancellationToken.None);

        Assert.Equal("Cancelled", response.Status);
        await repository.Received(1).TryCancelUnpaidAsync(
            waiting.Id, OrderService.CancelledByCustomerMessage, Arg.Any<CancellationToken>());
        // Nothing was paid, so no refund is asked for, and the paid-order path is not taken.
        await gateway.DidNotReceiveWithAnyArgs().CancelPaymentAsync(default!, default);
        await repository.DidNotReceiveWithAnyArgs().TryBeginCancellationAsync(default, default, default);
    }

    [Fact]
    public async Task CancelAsync_ThrowsConflict_WhenUnpaidOrderChangedInTheSameMoment()
    {
        // For example the payment arrived between reading the order and closing it.
        var waiting = StoredOrder(OrderStatus.PendingPayment);
        repository.GetForUserAsync(waiting.Id, UserId, Arg.Any<CancellationToken>()).Returns(waiting);
        repository.TryCancelUnpaidAsync(waiting.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CancelAsync(UserId, waiting.Id, CancellationToken.None));

        await gateway.DidNotReceiveWithAnyArgs().CancelPaymentAsync(default!, default);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFound_WhenOrderIsNotTheUsers()
    {
        var orderId = Guid.NewGuid();
        repository.GetForUserAsync(orderId, UserId, Arg.Any<CancellationToken>()).Returns((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetByIdAsync(UserId, orderId, CancellationToken.None));
    }
}
