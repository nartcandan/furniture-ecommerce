namespace FurnitureEcommerce.Application.Orders;

/// <summary>
/// Finishes orders that are stuck because a payment was never completed, a callback never arrived,
/// or a refund request got no answer.
/// </summary>
public interface IOrderReconciliationService
{
    /// <summary>Handles the orders that are stuck right now. Safe to run at any time and from several places.</summary>
    Task<ReconciliationSummary> RunOnceAsync(CancellationToken cancellationToken);
}

/// <param name="Closed">Unpaid orders that were closed and whose stock was given back.</param>
/// <param name="Paid">Orders whose payment had succeeded without the callback arriving.</param>
/// <param name="Cancelled">Orders whose refund was confirmed.</param>
/// <param name="RevertedToPaid">Orders whose refund was refused, so they are paid orders again.</param>
/// <param name="Skipped">Orders left for the next run because the provider gave no usable answer.</param>
public record ReconciliationSummary(int Closed, int Paid, int Cancelled, int RevertedToPaid, int Skipped)
{
    public int Total => Closed + Paid + Cancelled + RevertedToPaid + Skipped;
}
