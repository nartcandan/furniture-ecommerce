using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Domain.Entities;
using FurnitureEcommerce.UnitTests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using NSubstitute;

namespace FurnitureEcommerce.UnitTests.Payments;

public class PaymentServiceTests
{
    private const string Token = "token-1";
    private const string PaymentId = "payment-1";

    private readonly IOrderRepository repository = Substitute.For<IOrderRepository>();
    private readonly IPaymentGateway gateway = Substitute.For<IPaymentGateway>();
    private readonly RecordingLogger<PaymentService> logger = new();
    private readonly PaymentService service;
    private readonly Order order = NewOrder(OrderStatus.PendingPayment);

    public PaymentServiceTests()
    {
        service = new PaymentService(repository, gateway, logger);

        repository.GetByPaymentTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(order);
        gateway.CancelPaymentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentCancellation(true));
    }

    private static Order NewOrder(OrderStatus status, string? paymentId = null) => new()
    {
        Status = status,
        TotalAmount = 1250.50m,
        PaymentToken = Token,
        PaymentId = paymentId,
        RecipientFirstName = "Ayse",
        RecipientLastName = "Yilmaz",
        PhoneNumber = "+905551112233",
        City = "Istanbul",
        District = "Kadikoy",
        AddressLine = "Moda Cd. No: 1"
    };

    private void GatewayReports(CheckoutResult result) =>
        gateway.GetCheckoutResultAsync(Token, Arg.Any<CancellationToken>()).Returns(result);

    private void GatewayReportsPaid(decimal amount = 1250.50m, string? orderReference = null) =>
        GatewayReports(CheckoutResult.Paid(PaymentId, amount, orderReference ?? order.Id.ToString()));

    private Task<PaymentCallbackResult> HandleAsync(string? token = Token) =>
        service.HandleCallbackAsync(token, CancellationToken.None);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-such-token")]
    public async Task ReturnsUnknown_WithoutCallingProvider_WhenTokenHasNoOrder(string? token)
    {
        var result = await HandleAsync(token);

        Assert.Equal(new PaymentCallbackResult(null, PaymentOutcome.Unknown), result);
        await gateway.DidNotReceiveWithAnyArgs().GetCheckoutResultAsync(default!, default);
    }

    [Fact]
    public async Task MarksOrderPaid_WhenProviderConfirmsPayment()
    {
        GatewayReportsPaid();
        repository.TryMarkPaidAsync(order.Id, PaymentId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await HandleAsync();

        Assert.Equal(new PaymentCallbackResult(order.Id, PaymentOutcome.Success), result);
        await gateway.DidNotReceiveWithAnyArgs().CancelPaymentAsync(default!, default);
    }

    [Fact]
    public async Task MarksOrderFailed_WhenProviderReportsFailure()
    {
        GatewayReports(CheckoutResult.Failed("Insufficient funds"));

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.Failed, result.Outcome);
        await repository.Received(1).TryMarkPaymentFailedAsync(
            order.Id, "Insufficient funds", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeavesOrderWaiting_WhenPaymentIsNotCompleted()
    {
        GatewayReports(CheckoutResult.NotCompleted());

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.Pending, result.Outcome);
        await repository.DidNotReceiveWithAnyArgs().TryMarkPaidAsync(default, default!, default);
        await repository.DidNotReceiveWithAnyArgs().TryMarkPaymentFailedAsync(default, default!, default);
    }

    [Fact]
    public async Task LeavesOrderWaiting_WhenProviderCannotBeReached()
    {
        gateway.GetCheckoutResultAsync(Token, Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentGatewayException("down"));

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.ProviderUnreachable, result.Outcome);
        await repository.DidNotReceiveWithAnyArgs().TryMarkPaymentFailedAsync(default, default!, default);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, PaymentOutcome.Success)]
    [InlineData(OrderStatus.Shipped, PaymentOutcome.Success)]
    [InlineData(OrderStatus.Delivered, PaymentOutcome.Success)]
    [InlineData(OrderStatus.Cancelling, PaymentOutcome.Refunded)]
    [InlineData(OrderStatus.Cancelled, PaymentOutcome.Refunded)]
    public async Task NeverRefunds_WhenCallbackIsRepeatedForRecordedPayment(
        OrderStatus status,
        PaymentOutcome expected)
    {
        var recorded = NewOrder(status, PaymentId);
        repository.GetByPaymentTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(recorded);
        GatewayReports(CheckoutResult.Paid(PaymentId, 1250.50m, recorded.Id.ToString()));
        repository.TryMarkPaidAsync(recorded.Id, PaymentId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await HandleAsync();

        Assert.Equal(expected, result.Outcome);
        await gateway.DidNotReceiveWithAnyArgs().CancelPaymentAsync(default!, default);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.PaymentFailed)]
    public async Task RefundsLatePayment_AndLeavesOrderAsItIs(OrderStatus status)
    {
        var closed = NewOrder(status);
        repository.GetByPaymentTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(closed);
        GatewayReports(CheckoutResult.Paid(PaymentId, 1250.50m, closed.Id.ToString()));
        repository.TryMarkPaidAsync(closed.Id, PaymentId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.Refunded, result.Outcome);
        await gateway.Received(1).CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().TryMarkPaymentFailedAsync(default, default!, default);
    }

    [Fact]
    public async Task ReportsRefunded_WhenLatePaymentWasAlreadyRefundedByAnEarlierRequest()
    {
        var closed = NewOrder(OrderStatus.Cancelled);
        repository.GetByPaymentTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(closed);
        GatewayReports(CheckoutResult.Paid(PaymentId, 1250.50m, closed.Id.ToString()));
        gateway.CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(new PaymentCancellation(false, "Process has been locked before (8514)", WasAlreadyCancelled: true));

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.Refunded, result.Outcome);
        // Read as refunded from what the sandbox showed, so it is logged to be checked.
        var warning = Assert.Single(logger.Of(LogEvents.PaymentAlreadyCancelled));
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Empty(logger.Of(LogEvents.ManualRefundRequired));
    }

    [Fact]
    public async Task ReportsRefundFailed_WhenLatePaymentCannotBeRefunded()
    {
        var closed = NewOrder(OrderStatus.Cancelled);
        repository.GetByPaymentTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(closed);
        GatewayReports(CheckoutResult.Paid(PaymentId, 1250.50m, closed.Id.ToString()));
        gateway.CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(new PaymentCancellation(false, "Cannot cancel"));

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.RefundFailed, result.Outcome);
        // Nobody else will give this money back, so it is logged at the highest level under a fixed name.
        var alert = Assert.Single(logger.Of(LogEvents.ManualRefundRequired));
        Assert.Equal(LogLevel.Critical, alert.Level);
        Assert.Contains(PaymentId, alert.Message);
        Assert.Contains(closed.Id.ToString(), alert.Message);
    }

    [Fact]
    public async Task LogsManualRefund_WhenTheProviderCannotBeReachedToRefundALatePayment()
    {
        var closed = NewOrder(OrderStatus.Cancelled);
        repository.GetByPaymentTokenAsync(Token, Arg.Any<CancellationToken>()).Returns(closed);
        GatewayReports(CheckoutResult.Paid(PaymentId, 1250.50m, closed.Id.ToString()));
        gateway.CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentGatewayException("unreachable"));

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.RefundFailed, result.Outcome);
        var alert = Assert.Single(logger.Of(LogEvents.ManualRefundRequired));
        Assert.Equal(LogLevel.Critical, alert.Level);
        Assert.IsType<PaymentGatewayException>(alert.Exception);
    }

    [Fact]
    public async Task RefundsAndFailsOrder_WhenPaidAmountDoesNotMatch()
    {
        GatewayReportsPaid(amount: 1.00m);

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.Refunded, result.Outcome);
        await gateway.Received(1).CancelPaymentAsync(PaymentId, Arg.Any<CancellationToken>());
        await repository.Received(1).TryMarkPaymentFailedAsync(
            order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().TryMarkPaidAsync(default, default!, default);
    }

    [Fact]
    public async Task RefundsAndFailsOrder_WhenPaymentBelongsToAnotherOrder()
    {
        GatewayReportsPaid(orderReference: Guid.NewGuid().ToString());

        var result = await HandleAsync();

        Assert.Equal(PaymentOutcome.Refunded, result.Outcome);
        await repository.DidNotReceiveWithAnyArgs().TryMarkPaidAsync(default, default!, default);
    }
}
