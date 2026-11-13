using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FurnitureEcommerce.UnitTests.Orders;

public class OrderReconciliationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly IOrderRepository repository = Substitute.For<IOrderRepository>();
    private readonly IPaymentService paymentService = Substitute.For<IPaymentService>();
    private readonly IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
    private readonly OrderReconciliationService service;

    public OrderReconciliationServiceTests()
    {
        service = new OrderReconciliationService(
            repository,
            paymentService,
            gateway,
            new ReconciliationSettings(TimeSpan.FromMinutes(35), TimeSpan.FromMinutes(2)),
            new FixedClock { UtcNow = Now },
            NullLogger<OrderReconciliationService>.Instance);

        Unpaid();
        Cancelling();
        ClosedEarly();
    }

    private static Order NewOrder(OrderStatus status, string? token = "token", string? paymentId = null) => new()
    {
        Status = status,
        PaymentToken = token,
        PaymentId = paymentId,
        RecipientFirstName = "Ayse",
        RecipientLastName = "Yilmaz",
        PhoneNumber = "+905551112233",
        City = "Istanbul",
        District = "Kadikoy",
        AddressLine = "Moda Cd. No: 1"
    };

    private void Unpaid(params Order[] orders) =>
        repository.GetPendingPaymentCreatedBeforeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(orders.ToList());

    private void Cancelling(params Order[] orders) =>
        repository.GetCancellingUpdatedBeforeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(orders.ToList());

    private void ClosedEarly(params Order[] orders) =>
        repository.GetPaymentRecheckDueAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(orders.ToList());

    private void CallbackGives(Order order, PaymentOutcome outcome) =>
        paymentService.HandleCallbackAsync(order.PaymentToken, Arg.Any<CancellationToken>())
            .Returns(new PaymentCallbackResult(order.Id, outcome));

    private Task<ReconciliationSummary> RunAsync() => service.RunOnceAsync(CancellationToken.None);

    [Fact]
    public async Task LooksOnlyAtOrdersOlderThanTheTimeouts()
    {
        await RunAsync();

        await repository.Received(1).GetPendingPaymentCreatedBeforeAsync(
            Now.UtcDateTime.AddMinutes(-35), 50, Arg.Any<CancellationToken>());
        await repository.Received(1).GetCancellingUpdatedBeforeAsync(
            Now.UtcDateTime.AddMinutes(-2), 50, Arg.Any<CancellationToken>());
        await repository.Received(1).GetPaymentRecheckDueAsync(Now.UtcDateTime, 50, Arg.Any<CancellationToken>());
    }

    [Theory]
    // Nothing was paid on the page.
    [InlineData(PaymentOutcome.Pending)]
    [InlineData(PaymentOutcome.Failed)]
    // A late payment was found. The callback code has already tried to refund it.
    [InlineData(PaymentOutcome.Refunded)]
    [InlineData(PaymentOutcome.RefundFailed)]
    public async Task RechecksClosedOrderOnce_WhateverTheProviderSays(PaymentOutcome outcome)
    {
        var order = NewOrder(OrderStatus.Cancelled);
        ClosedEarly(order);
        CallbackGives(order, outcome);

        var summary = await RunAsync();

        Assert.Equal(
            new ReconciliationSummary(Closed: 0, Paid: 0, Cancelled: 0, RevertedToPaid: 0, Skipped: 0, Rechecked: 1),
            summary);
        await paymentService.Received(1).HandleCallbackAsync(order.PaymentToken, Arg.Any<CancellationToken>());
        await repository.Received(1).ClearPaymentRecheckAsync(order.Id, Arg.Any<CancellationToken>());
        // The order is closed already. The recheck never changes its status itself.
        await repository.DidNotReceiveWithAnyArgs().TryCancelUnpaidAsync(default, default!, default);
    }

    [Fact]
    public async Task KeepsTheRecheck_WhenProviderCannotBeReached()
    {
        var order = NewOrder(OrderStatus.Cancelled);
        ClosedEarly(order);
        CallbackGives(order, PaymentOutcome.ProviderUnreachable);

        var summary = await RunAsync();

        Assert.Equal(1, summary.Skipped);
        Assert.Equal(0, summary.Rechecked);
        await repository.DidNotReceiveWithAnyArgs().ClearPaymentRecheckAsync(default, default);
    }

    [Fact]
    public async Task KeepsTheRecheck_WhenItThrows_AndContinuesWithTheNextOrder()
    {
        var broken = NewOrder(OrderStatus.Cancelled, token: "broken");
        var healthy = NewOrder(OrderStatus.Cancelled, token: "healthy");
        ClosedEarly(broken, healthy);
        paymentService.HandleCallbackAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database failure"));
        CallbackGives(healthy, PaymentOutcome.Pending);

        var summary = await RunAsync();

        Assert.Equal(1, summary.Skipped);
        Assert.Equal(1, summary.Rechecked);
        await repository.DidNotReceive().ClearPaymentRecheckAsync(broken.Id, Arg.Any<CancellationToken>());
        await repository.Received(1).ClearPaymentRecheckAsync(healthy.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClosesUnpaidOrder_WhenPaymentWasNeverCompleted()
    {
        var order = NewOrder(OrderStatus.PendingPayment);
        Unpaid(order);
        CallbackGives(order, PaymentOutcome.Pending);

        var summary = await RunAsync();

        Assert.Equal(new ReconciliationSummary(Closed: 1, Paid: 0, Cancelled: 0, RevertedToPaid: 0, Skipped: 0), summary);
        await repository.Received(1).TryCancelUnpaidAsync(
            order.Id, OrderReconciliationService.TimeoutMessage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClosesUnpaidOrder_WithoutAskingProvider_WhenItHasNoPaymentPage()
    {
        var order = NewOrder(OrderStatus.PendingPayment, token: null);
        Unpaid(order);

        var summary = await RunAsync();

        Assert.Equal(1, summary.Closed);
        await repository.Received(1).TryCancelUnpaidAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await paymentService.DidNotReceiveWithAnyArgs().HandleCallbackAsync(default, default);
    }

    [Fact]
    public async Task KeepsOrder_WhenItTurnsOutToBePaid()
    {
        var order = NewOrder(OrderStatus.PendingPayment);
        Unpaid(order);
        CallbackGives(order, PaymentOutcome.Success);

        var summary = await RunAsync();

        Assert.Equal(1, summary.Paid);
        await repository.DidNotReceiveWithAnyArgs().TryCancelUnpaidAsync(default, default!, default);
    }

    [Fact]
    public async Task LeavesUnpaidOrder_WhenProviderCannotBeReached()
    {
        var order = NewOrder(OrderStatus.PendingPayment);
        Unpaid(order);
        CallbackGives(order, PaymentOutcome.ProviderUnreachable);

        var summary = await RunAsync();

        Assert.Equal(1, summary.Skipped);
        await repository.DidNotReceiveWithAnyArgs().TryCancelUnpaidAsync(default, default!, default);
    }

    [Fact]
    public async Task DoesNotCancelAgain_WhenCallbackCodeAlreadyClosedTheOrderAsFailed()
    {
        var order = NewOrder(OrderStatus.PendingPayment);
        Unpaid(order);
        CallbackGives(order, PaymentOutcome.Failed);

        var summary = await RunAsync();

        Assert.Equal(1, summary.Closed);
        await repository.DidNotReceiveWithAnyArgs().TryCancelUnpaidAsync(default, default!, default);
    }

    [Fact]
    public async Task ContinuesWithOtherOrders_WhenOneThrows()
    {
        var broken = NewOrder(OrderStatus.PendingPayment, token: "broken");
        var healthy = NewOrder(OrderStatus.PendingPayment, token: "healthy");
        Unpaid(broken, healthy);
        paymentService.HandleCallbackAsync("broken", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database failure"));
        CallbackGives(healthy, PaymentOutcome.Pending);

        var summary = await RunAsync();

        Assert.Equal(1, summary.Skipped);
        Assert.Equal(1, summary.Closed);
        await repository.Received(1).TryCancelUnpaidAsync(healthy.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletesCancellation_WhenRetriedRefundSucceeds()
    {
        var order = NewOrder(OrderStatus.Cancelling, paymentId: "payment-1");
        Cancelling(order);
        gateway.CancelPaymentAsync("payment-1", Arg.Any<CancellationToken>()).Returns(new PaymentCancellation(true));

        var summary = await RunAsync();

        Assert.Equal(1, summary.Cancelled);
        await repository.Received(1).TryCompleteCancellationAsync(order.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletesCancellation_WhenProviderSaysPaymentWasAlreadyCancelled()
    {
        var order = NewOrder(OrderStatus.Cancelling, paymentId: "payment-1");
        Cancelling(order);
        gateway.CancelPaymentAsync("payment-1", Arg.Any<CancellationToken>())
            .Returns(new PaymentCancellation(false, "Process has been locked before (8514)", WasAlreadyCancelled: true));

        var summary = await RunAsync();

        Assert.Equal(1, summary.Cancelled);
        await repository.Received(1).TryCompleteCancellationAsync(order.Id, Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().TryRevertCancellationAsync(default, default);
    }

    [Fact]
    public async Task PutsOrderBackToPaid_WhenRefundIsRefused()
    {
        var order = NewOrder(OrderStatus.Cancelling, paymentId: "payment-1");
        Cancelling(order);
        gateway.CancelPaymentAsync("payment-1", Arg.Any<CancellationToken>())
            .Returns(new PaymentCancellation(false, "Merchant balance is not sufficient for refund (5117)"));

        var summary = await RunAsync();

        Assert.Equal(1, summary.RevertedToPaid);
        await repository.Received(1).TryRevertCancellationAsync(order.Id, Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().TryCompleteCancellationAsync(default, default);
    }

    [Fact]
    public async Task LeavesOrderInCancelling_WhenProviderCannotBeReached()
    {
        var order = NewOrder(OrderStatus.Cancelling, paymentId: "payment-1");
        Cancelling(order);
        gateway.CancelPaymentAsync("payment-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentGatewayException("down"));

        var summary = await RunAsync();

        Assert.Equal(1, summary.Skipped);
        await repository.DidNotReceiveWithAnyArgs().TryCompleteCancellationAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().TryRevertCancellationAsync(default, default);
    }
}
