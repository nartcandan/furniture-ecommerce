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

public class OrderCancellationServiceTests
{
    private const string PaymentId = "payment-1";
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly IOrderRepository repository = Substitute.For<IOrderRepository>();
    private readonly IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
    private readonly FixedClock clock = new() { UtcNow = Now };
    private readonly OrderService service;

    public OrderCancellationServiceTests()
    {
        service = new OrderService(
            repository,
            Substitute.For<ICartRepository>(),
            Substitute.For<IAddressRepository>(),
            Substitute.For<IIdentityService>(),
            gateway,
            new PaymentSettings("http://api/callback", "http://frontend"),
            clock,
            NullLogger<OrderService>.Instance,
            new CreateOrderRequestValidator());

        gateway.CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(new PaymentCancellation(true));
    }

    private Order OrderIs(OrderStatus status, DateTimeOffset? paidAt = null)
    {
        var order = new Order
        {
            UserId = UserId,
            Status = status,
            PaymentId = PaymentId,
            PaidAt = (paidAt ?? Now.AddHours(-1)).UtcDateTime,
            RecipientFirstName = "Ayse",
            RecipientLastName = "Yilmaz",
            PhoneNumber = "+905551112233",
            City = "Istanbul",
            District = "Kadikoy",
            AddressLine = "Moda Cd. No: 1"
        };

        repository.GetForUserAsync(order.Id, UserId, Arg.Any<CancellationToken>()).Returns(order);
        repository.TryBeginCancellationAsync(order.Id, UserId, Arg.Any<CancellationToken>()).Returns(true);
        return order;
    }

    private Task<OrderResponse> CancelAsync(Order order) =>
        service.CancelAsync(UserId, order.Id, CancellationToken.None);

    private Task AssertProviderNotCalledAsync() =>
        gateway.DidNotReceiveWithAnyArgs().CancelPaymentAsync(default!, default);

    [Fact]
    public async Task CancelAsync_RefundsPayment_ThenCompletesCancellation()
    {
        var order = OrderIs(OrderStatus.Paid);

        await CancelAsync(order);

        Received.InOrder(() =>
        {
            repository.TryBeginCancellationAsync(order.Id, UserId, Arg.Any<CancellationToken>());
            gateway.CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>());
            repository.TryCompleteCancellationAsync(order.Id, Arg.Any<CancellationToken>());
        });
        await repository.DidNotReceiveWithAnyArgs().TryRevertCancellationAsync(default, default);
    }

    [Fact]
    public async Task CancelAsync_ThrowsNotFound_WhenOrderIsNotTheUsers()
    {
        var orderId = Guid.NewGuid();
        repository.GetForUserAsync(orderId, UserId, Arg.Any<CancellationToken>()).Returns((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CancelAsync(UserId, orderId, CancellationToken.None));
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Cancelling)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public async Task CancelAsync_ThrowsConflict_WhenOrderIsNotPaid(OrderStatus status)
    {
        var order = OrderIs(status);

        await Assert.ThrowsAsync<ConflictException>(() => CancelAsync(order));

        await repository.DidNotReceiveWithAnyArgs().TryBeginCancellationAsync(default, default, default);
        await AssertProviderNotCalledAsync();
    }

    [Fact]
    public async Task CancelAsync_ThrowsConflict_WhenPaymentDayHasPassed()
    {
        var order = OrderIs(OrderStatus.Paid, paidAt: Now.AddDays(-1));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CancelAsync(order));

        Assert.Contains("day it was paid", exception.Message);
        await AssertProviderNotCalledAsync();
    }

    [Fact]
    public async Task CancelAsync_ThrowsConflict_WithoutCallingProvider_WhenOrderChangedMeanwhile()
    {
        var order = OrderIs(OrderStatus.Paid);
        repository.TryBeginCancellationAsync(order.Id, UserId, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ConflictException>(() => CancelAsync(order));

        await AssertProviderNotCalledAsync();
    }

    [Fact]
    public async Task CancelAsync_PutsOrderBackToPaid_WhenProviderRefusesRefund()
    {
        var order = OrderIs(OrderStatus.Paid);
        gateway.CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(new PaymentCancellation(false, "Cannot cancel"));

        await Assert.ThrowsAsync<PaymentGatewayException>(() => CancelAsync(order));

        await repository.Received(1).TryRevertCancellationAsync(order.Id, Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().TryCompleteCancellationAsync(default, default);
    }

    [Fact]
    public async Task CancelAsync_LeavesOrderInCancelling_WhenProviderCannotBeReached()
    {
        var order = OrderIs(OrderStatus.Paid);
        gateway.CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentGatewayException("down"));

        await CancelAsync(order);

        // The refund may have happened, so the order is neither completed nor reverted here.
        await repository.DidNotReceiveWithAnyArgs().TryCompleteCancellationAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().TryRevertCancellationAsync(default, default);
    }
}
