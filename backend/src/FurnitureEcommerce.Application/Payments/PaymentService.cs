using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Application.Payments;

public class PaymentService(
    IOrderRepository orderRepository,
    IPaymentGateway gateway,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentCallbackResult> HandleCallbackAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PaymentCallbackResult(null, PaymentOutcome.Unknown);
        }

        var order = await orderRepository.GetByPaymentTokenAsync(token, cancellationToken);

        if (order is null)
        {
            return new PaymentCallbackResult(null, PaymentOutcome.Unknown);
        }

        return new PaymentCallbackResult(order.Id, await ResolveAsync(order, token, cancellationToken));
    }

    private async Task<PaymentOutcome> ResolveAsync(Order order, string token, CancellationToken cancellationToken)
    {
        CheckoutResult result;

        try
        {
            // The callback only says "something happened". The result always comes from the provider.
            result = await gateway.GetCheckoutResultAsync(token, cancellationToken);
        }
        catch (PaymentGatewayException exception)
        {
            // The order keeps waiting. The reconciliation job asks the provider again later.
            logger.LogError(exception, "The payment result of order {OrderId} could not be read.", order.Id);
            return PaymentOutcome.ProviderUnreachable;
        }

        switch (result.State)
        {
            case CheckoutState.NotCompleted:
                return PaymentOutcome.Pending;

            case CheckoutState.Failed:
                await orderRepository.TryMarkPaymentFailedAsync(
                    order.Id,
                    result.Error ?? "The payment failed.",
                    cancellationToken);
                return PaymentOutcome.Failed;

            default:
                return await HandlePaidAsync(order, token, result, cancellationToken);
        }
    }

    private async Task<PaymentOutcome> HandlePaidAsync(
        Order order,
        string token,
        CheckoutResult result,
        CancellationToken cancellationToken)
    {
        var paymentId = result.PaymentId!;

        if (result.PaidAmount != order.TotalAmount || result.OrderReference != order.Id.ToString())
        {
            logger.LogError(
                "Payment {PaymentId} does not match order {OrderId}: paid {PaidAmount} for {OrderReference}, expected {TotalAmount}.",
                paymentId, order.Id, result.PaidAmount, result.OrderReference, order.TotalAmount);

            await orderRepository.TryMarkPaymentFailedAsync(
                order.Id,
                "The payment did not match the order and was refunded.",
                cancellationToken);
            return await RefundAsync(order.Id, paymentId, cancellationToken);
        }

        // Only the request that moves the order out of PendingPayment gets true.
        // That request also empties the cart, so a repeated callback changes nothing.
        if (await orderRepository.TryMarkPaidAsync(order.Id, paymentId, cancellationToken))
        {
            return PaymentOutcome.Success;
        }

        var current = await orderRepository.GetByPaymentTokenAsync(token, cancellationToken) ?? order;

        if (current.PaymentId == paymentId)
        {
            // A repeated callback for a payment that is already recorded. It must never be refunded here.
            return current.Status is OrderStatus.Cancelling or OrderStatus.Cancelled
                ? PaymentOutcome.Refunded
                : PaymentOutcome.Success;
        }

        // Late payment: the order failed or timed out, and its stock may already be sold to someone else.
        logger.LogWarning(
            "Payment {PaymentId} arrived for order {OrderId} in status {Status}. Refunding it.",
            paymentId, current.Id, current.Status);

        return await RefundAsync(current.Id, paymentId, cancellationToken);
    }

    private async Task<PaymentOutcome> RefundAsync(Guid orderId, string paymentId, CancellationToken cancellationToken)
    {
        try
        {
            var cancellation = await gateway.CancelPaymentAsync(paymentId, cancellationToken);

            if (cancellation.IsSuccess)
            {
                return PaymentOutcome.Refunded;
            }

            if (cancellation.WasAlreadyCancelled)
            {
                // A late payment can be seen twice: by a repeated callback, or by the callback and the
                // reconciliation job. The first one refunded it, so this is not a failure.
                logger.LogWarning(
                    "Payment {PaymentId} of order {OrderId} was already refunded ({Error}).",
                    paymentId, orderId, cancellation.Error);
                return PaymentOutcome.Refunded;
            }

            logger.LogCritical(
                "Payment {PaymentId} of order {OrderId} must be refunded by hand. The provider answered: {Error}",
                paymentId, orderId, cancellation.Error);
        }
        catch (PaymentGatewayException exception)
        {
            logger.LogCritical(
                exception,
                "Payment {PaymentId} of order {OrderId} must be refunded by hand.",
                paymentId, orderId);
        }

        return PaymentOutcome.RefundFailed;
    }
}
