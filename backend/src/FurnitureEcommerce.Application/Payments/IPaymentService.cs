namespace FurnitureEcommerce.Application.Payments;

public interface IPaymentService
{
    /// <summary>
    /// Handles the provider's callback. Safe to call any number of times for the same token.
    /// </summary>
    Task<PaymentCallbackResult> HandleCallbackAsync(string? token, CancellationToken cancellationToken);
}

public enum PaymentOutcome
{
    /// <summary>The token does not belong to any order.</summary>
    Unknown,

    /// <summary>The provider says the customer has not finished the payment page.</summary>
    Pending,

    /// <summary>The provider gave no usable answer, so nothing is known about the payment.</summary>
    ProviderUnreachable,
    Success,
    Failed,

    /// <summary>The money arrived for an order that could no longer be paid, and it was given back.</summary>
    Refunded,

    /// <summary>The money arrived for an order that could no longer be paid, and giving it back failed.</summary>
    RefundFailed
}

public record PaymentCallbackResult(Guid? OrderId, PaymentOutcome Outcome);
