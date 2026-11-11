using FurnitureEcommerce.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(IPaymentService paymentService, PaymentSettings settings) : ControllerBase
{
    /// <summary>
    /// The payment provider sends the customer's browser here after the payment page.
    /// The request is a form post without a login token, and it can arrive more than once.
    /// Its content is not trusted: the result is always read from the provider.
    /// The answer sends the browser on to the order page of the frontend.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("callback")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status303SeeOther)]
    public async Task<IActionResult> Callback([FromForm] string? token, CancellationToken cancellationToken)
    {
        var result = await paymentService.HandleCallbackAsync(token, cancellationToken);

        var frontend = settings.FrontendBaseUrl.TrimEnd('/');
        var outcome = result.Outcome switch
        {
            PaymentOutcome.Success => "success",
            PaymentOutcome.Failed => "failed",
            PaymentOutcome.Refunded => "refunded",
            PaymentOutcome.RefundFailed => "refund-failed",
            // For the customer both mean the same: the order is still waiting.
            PaymentOutcome.Pending or PaymentOutcome.ProviderUnreachable => "pending",
            _ => "unknown"
        };

        var location = result.OrderId is { } orderId
            ? $"{frontend}/orders/{orderId}?payment={outcome}"
            : $"{frontend}/orders?payment={outcome}";

        // 303 makes the browser load the page with GET instead of repeating the form post.
        Response.Headers.Location = location;
        return StatusCode(StatusCodes.Status303SeeOther);
    }
}
