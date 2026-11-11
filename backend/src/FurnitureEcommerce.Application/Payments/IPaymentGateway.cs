using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Payments;

/// <summary>The payment provider. The rest of the application does not know which one it is.</summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Opens a hosted payment page for the order. Never throws: a provider or network error
    /// comes back as a failed result, because no payment can exist without the page address.
    /// </summary>
    Task<CheckoutStart> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);

    /// <summary>Asks the provider what happened to the payment page with this token.</summary>
    /// <exception cref="Common.Exceptions.PaymentGatewayException">
    /// The provider could not be reached or its answer is not clear. The payment state is unknown.
    /// </exception>
    Task<CheckoutResult> GetCheckoutResultAsync(string token, CancellationToken cancellationToken);

    /// <summary>Gives the whole payment back to the customer.</summary>
    /// <exception cref="Common.Exceptions.PaymentGatewayException">The provider could not be reached.</exception>
    Task<PaymentCancellation> CancelPaymentAsync(string paymentId, CancellationToken cancellationToken);
}

public record PaymentBuyer(Guid Id, string FirstName, string LastName, string Email, string? IpAddress);

public record CheckoutRequest(Order Order, PaymentBuyer Buyer, string CallbackUrl);

public record CheckoutStart(string? Token, string? PaymentPageUrl, string? Error)
{
    public bool IsSuccess => Token is not null && PaymentPageUrl is not null;

    public static CheckoutStart Started(string token, string paymentPageUrl) => new(token, paymentPageUrl, null);

    public static CheckoutStart Failed(string error) => new(null, null, error);
}

public enum CheckoutState
{
    /// <summary>The customer has not finished the payment page yet.</summary>
    NotCompleted,
    Paid,
    Failed
}

/// <param name="OrderReference">The order id that was sent when the payment page was opened.</param>
public record CheckoutResult(
    CheckoutState State,
    string? PaymentId = null,
    decimal? PaidAmount = null,
    string? OrderReference = null,
    string? Error = null)
{
    public static CheckoutResult NotCompleted() => new(CheckoutState.NotCompleted);

    public static CheckoutResult Paid(string paymentId, decimal paidAmount, string orderReference) =>
        new(CheckoutState.Paid, paymentId, paidAmount, orderReference);

    public static CheckoutResult Failed(string error) => new(CheckoutState.Failed, Error: error);
}

public record PaymentCancellation(bool IsSuccess, string? Error = null);
