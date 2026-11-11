using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Application.Orders;

public class OrderReconciliationService(
    IOrderRepository repository,
    IPaymentService paymentService,
    IPaymentGateway gateway,
    ReconciliationSettings settings,
    TimeProvider timeProvider,
    ILogger<OrderReconciliationService> logger) : IOrderReconciliationService
{
    public const string TimeoutMessage = "The payment was not completed in time.";

    public async Task<ReconciliationSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        int closed = 0, paid = 0, cancelled = 0, reverted = 0, skipped = 0;

        var unpaidOrders = await repository.GetPendingPaymentCreatedBeforeAsync(
            now - settings.PendingPaymentTimeout,
            settings.BatchSize,
            cancellationToken);

        foreach (var order in unpaidOrders)
        {
            // One broken order must not stop the others.
            try
            {
                switch (await ResolveUnpaidAsync(order, cancellationToken))
                {
                    case UnpaidResult.Closed: closed++; break;
                    case UnpaidResult.Paid: paid++; break;
                    default: skipped++; break;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                skipped++;
                logger.LogError(exception, "Unpaid order {OrderId} could not be reconciled.", order.Id);
            }
        }

        var cancellingOrders = await repository.GetCancellingUpdatedBeforeAsync(
            now - settings.CancellingTimeout,
            settings.BatchSize,
            cancellationToken);

        foreach (var order in cancellingOrders)
        {
            try
            {
                switch (await ResolveCancellingAsync(order, cancellationToken))
                {
                    case CancellingResult.Cancelled: cancelled++; break;
                    case CancellingResult.RevertedToPaid: reverted++; break;
                    default: skipped++; break;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                skipped++;
                logger.LogError(exception, "Cancelling order {OrderId} could not be reconciled.", order.Id);
            }
        }

        return new ReconciliationSummary(closed, paid, cancelled, reverted, skipped);
    }

    private async Task<UnpaidResult> ResolveUnpaidAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.PaymentToken is null)
        {
            // The API stopped between saving the order and saving its payment page.
            // The customer never got the page, so nothing can have been paid.
            return await CloseAsync(order, cancellationToken);
        }

        // The same code as the callback: it asks the provider and records a success or a failure.
        var result = await paymentService.HandleCallbackAsync(order.PaymentToken, cancellationToken);

        switch (result.Outcome)
        {
            case PaymentOutcome.Success:
                logger.LogInformation("Order {OrderId} was paid, but its callback never arrived.", order.Id);
                return UnpaidResult.Paid;

            case PaymentOutcome.Pending:
                // The payment page has expired by now, so the customer can no longer pay.
                return await CloseAsync(order, cancellationToken);

            case PaymentOutcome.ProviderUnreachable:
                // Nothing is known. Closing the order now could give away stock that was paid for.
                return UnpaidResult.Skipped;

            default:
                // Failed, Refunded or RefundFailed: the callback code has already closed the order.
                return UnpaidResult.Closed;
        }
    }

    private async Task<UnpaidResult> CloseAsync(Order order, CancellationToken cancellationToken)
    {
        if (await repository.TryCancelUnpaidAsync(order.Id, TimeoutMessage, cancellationToken))
        {
            logger.LogInformation("Order {OrderId} was not paid in time and was cancelled.", order.Id);
        }

        return UnpaidResult.Closed;
    }

    private async Task<CancellingResult> ResolveCancellingAsync(Order order, CancellationToken cancellationToken)
    {
        PaymentCancellation cancellation;

        try
        {
            // The provider does not show whether a payment was cancelled, so the only way to find
            // out is to ask for the cancellation again. It refuses a second one, so this cannot refund twice.
            cancellation = await gateway.CancelPaymentAsync(order.PaymentId!, cancellationToken);
        }
        catch (PaymentGatewayException exception)
        {
            logger.LogWarning(exception, "The refund of order {OrderId} could not be confirmed yet.", order.Id);
            return CancellingResult.Skipped;
        }

        if (cancellation.WasAlreadyCancelled)
        {
            // Read as "the first request did go through". Logged every time, because that reading
            // comes from observing the provider, not from its documentation.
            logger.LogWarning(
                "Order {OrderId}: the provider says payment {PaymentId} was cancelled before ({Error}). Treating it as refunded.",
                order.Id, order.PaymentId, cancellation.Error);
        }

        if (cancellation.IsSuccess || cancellation.WasAlreadyCancelled)
        {
            await repository.TryCompleteCancellationAsync(order.Id, cancellationToken);
            return CancellingResult.Cancelled;
        }

        logger.LogError(
            "The payment of order {OrderId} could not be refunded: {Error}. The order is paid again.",
            order.Id, cancellation.Error);

        await repository.TryRevertCancellationAsync(order.Id, cancellationToken);
        return CancellingResult.RevertedToPaid;
    }

    private enum UnpaidResult
    {
        Closed,
        Paid,
        Skipped
    }

    private enum CancellingResult
    {
        Cancelled,
        RevertedToPaid,
        Skipped
    }
}
