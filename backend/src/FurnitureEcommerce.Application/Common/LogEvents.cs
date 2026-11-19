using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Application.Common;

/// <summary>
/// Log events that someone must notice. Each has a fixed name, so it can be searched for in the logs of any
/// environment (for example <c>EventId.Name = "ManualRefundRequired"</c>) instead of matching message text.
/// </summary>
public static class LogEvents
{
    /// <summary>
    /// A payment arrived for an order that was already closed, and giving it back failed.
    /// The customer has paid for nothing until someone refunds the payment in the iyzico panel.
    /// </summary>
    public static readonly EventId ManualRefundRequired = new(9001, nameof(ManualRefundRequired));

    /// <summary>
    /// iyzico refused a cancel because the payment was cancelled before (error 8514), and the code treated it
    /// as refunded. That reading comes from what the sandbox showed, not from iyzico's documentation, so these
    /// are logged every time to be checked against the iyzico panel.
    /// </summary>
    public static readonly EventId PaymentAlreadyCancelled = new(9002, nameof(PaymentAlreadyCancelled));
}
