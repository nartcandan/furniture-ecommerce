using System.Collections.Concurrent;
using FurnitureEcommerce.Application.Payments;

namespace FurnitureEcommerce.Infrastructure.Payments;

/// <summary>
/// Stands in for iyzico in the E2E environment, where the browser tests run. It behaves like iyzico's
/// hosted page: the API's own test page (<c>/test-payments/{token}</c>) lets the browser pay or decline,
/// then posts the token to the callback, and the API asks this gateway for the result.
/// It keeps everything in memory and can never take money, so it is refused outside that environment.
/// </summary>
public class TestPaymentGateway : IPaymentGateway
{
    /// <summary>The path of the test payment page, under the API's own address.</summary>
    public const string PagePath = "/test-payments";

    private readonly ConcurrentDictionary<string, Checkout> checkouts = new();
    private readonly ConcurrentDictionary<string, byte> cancelledPayments = new();

    public Task<CheckoutStart> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString();
        checkouts[token] = new Checkout(request.Order.Id, request.Order.TotalAmount, request.CallbackUrl);

        // The page is served by the API that receives the callback, so its address comes from the callback's.
        var pageUrl = new Uri(new Uri(request.CallbackUrl), $"{PagePath}/{token}").ToString();
        return Task.FromResult(CheckoutStart.Started(token, pageUrl));
    }

    public Task<CheckoutResult> GetCheckoutResultAsync(string token, CancellationToken cancellationToken)
    {
        if (!checkouts.TryGetValue(token, out var checkout))
        {
            return Task.FromResult(CheckoutResult.Failed("Unknown payment token."));
        }

        return Task.FromResult(checkout.State switch
        {
            CheckoutState.Paid => CheckoutResult.Paid(checkout.PaymentId!, checkout.Amount, checkout.OrderId.ToString()),
            CheckoutState.Failed => CheckoutResult.Failed("The card was declined."),
            _ => CheckoutResult.NotCompleted()
        });
    }

    public Task<PaymentCancellation> CancelPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        // Like iyzico, a payment is given back once. A second request is refused as already cancelled.
        return Task.FromResult(cancelledPayments.TryAdd(paymentId, 0)
            ? new PaymentCancellation(true)
            : new PaymentCancellation(false, "The payment was already cancelled.", WasAlreadyCancelled: true));
    }

    /// <summary>The order and amount behind a token, or null for a token this gateway never gave out.</summary>
    public (Guid OrderId, decimal Amount)? Find(string token) =>
        checkouts.TryGetValue(token, out var checkout) ? (checkout.OrderId, checkout.Amount) : null;

    /// <summary>
    /// Finishes the payment page with a payment or a declined card, and returns the callback to send the
    /// browser to. A page is finished once, like iyzico's: null when the token is unknown or already used.
    /// </summary>
    public string? Complete(string token, bool pay)
    {
        if (!checkouts.TryGetValue(token, out var checkout))
        {
            return null;
        }

        lock (checkout)
        {
            if (checkout.State != CheckoutState.NotCompleted)
            {
                return null;
            }

            checkout.State = pay ? CheckoutState.Paid : CheckoutState.Failed;
            // iyzico's payment ids are eight digits.
            checkout.PaymentId = pay ? Random.Shared.Next(10_000_000, 100_000_000).ToString() : null;
        }

        return checkout.CallbackUrl;
    }

    private sealed class Checkout(Guid orderId, decimal amount, string callbackUrl)
    {
        public Guid OrderId { get; } = orderId;
        public decimal Amount { get; } = amount;
        public string CallbackUrl { get; } = callbackUrl;
        public CheckoutState State { get; set; } = CheckoutState.NotCompleted;
        public string? PaymentId { get; set; }
    }
}
