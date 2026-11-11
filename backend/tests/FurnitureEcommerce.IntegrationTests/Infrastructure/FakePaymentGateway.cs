using System.Collections.Concurrent;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Payments;

namespace FurnitureEcommerce.IntegrationTests.Infrastructure;

/// <summary>Stands in for iyzico, so the tests never call the real provider.</summary>
public class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<Guid, string> tokensByOrder = new();
    private readonly ConcurrentDictionary<string, CheckoutResult> results = new();

    /// <summary>When set, opening a payment page fails with this error.</summary>
    public string? StartError { get; set; }

    /// <summary>When true, the provider cannot be reached for result and cancel requests.</summary>
    public bool IsUnreachable { get; set; }

    /// <summary>When set, cancelling a payment fails with this error.</summary>
    public string? CancelError { get; set; }

    public ConcurrentBag<string> CancelledPaymentIds { get; } = [];

    public void Reset()
    {
        tokensByOrder.Clear();
        results.Clear();
        CancelledPaymentIds.Clear();
        StartError = null;
        CancelError = null;
        IsUnreachable = false;
    }

    public string TokenFor(Guid orderId) => tokensByOrder[orderId];

    public void SetResult(Guid orderId, CheckoutResult result) => results[TokenFor(orderId)] = result;

    public Task<CheckoutStart> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (StartError is not null)
        {
            return Task.FromResult(CheckoutStart.Failed(StartError));
        }

        var token = Guid.NewGuid().ToString();
        tokensByOrder[request.Order.Id] = token;

        return Task.FromResult(CheckoutStart.Started(token, $"https://pay.example.test/?token={token}"));
    }

    public Task<CheckoutResult> GetCheckoutResultAsync(string token, CancellationToken cancellationToken)
    {
        ThrowIfUnreachable();

        return Task.FromResult(results.GetValueOrDefault(token, CheckoutResult.NotCompleted()));
    }

    public Task<PaymentCancellation> CancelPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        ThrowIfUnreachable();

        if (CancelError is not null)
        {
            return Task.FromResult(new PaymentCancellation(false, CancelError));
        }

        CancelledPaymentIds.Add(paymentId);
        return Task.FromResult(new PaymentCancellation(true));
    }

    private void ThrowIfUnreachable()
    {
        if (IsUnreachable)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.");
        }
    }
}
