namespace FurnitureEcommerce.Application.Orders;

/// <param name="PendingPaymentTimeout">
/// How long an order may wait for payment before it is closed. Must be longer than the life of the
/// provider's payment page (30 minutes), otherwise a customer could still pay a closed order.
/// </param>
/// <param name="CancellingTimeout">
/// How long an order may stay in Cancelling before the refund is tried again. Long enough for a
/// normal cancel request to finish, so the job does not interfere with one that is still running.
/// </param>
/// <param name="BatchSize">The most orders of each kind handled in one run.</param>
public record ReconciliationSettings(TimeSpan PendingPaymentTimeout, TimeSpan CancellingTimeout, int BatchSize = 50)
{
    public static readonly TimeSpan PaymentPageLifetime = TimeSpan.FromMinutes(30);
}
