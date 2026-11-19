using System.Globalization;
using System.Net;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

/// <summary>
/// The payment page of the test payment provider, used by the browser tests in the E2E environment.
/// It stands where iyzico's hosted page would: the customer pays or declines, and the browser is sent to the
/// callback with the token in a form post, as iyzico does. With the real provider every request here is 404.
/// </summary>
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("test-payments/{token}")]
public class TestPaymentsController(IPaymentGateway gateway) : ControllerBase
{
    [HttpGet]
    public IActionResult Show(string token)
    {
        if (gateway is not TestPaymentGateway testGateway || testGateway.Find(token) is not { } checkout)
        {
            return NotFound();
        }

        var amount = checkout.Amount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
        return Html($"""
            <h1>Test payment</h1>
            <p class="note">No real payment is made. This page replaces iyzico in the browser tests.</p>
            <p>Order <code>{Encode(checkout.OrderId.ToString())}</code></p>
            <p class="amount">₺{Encode(amount)}</p>
            <form method="post">
              <button name="outcome" value="pay">Pay</button>
              <button name="outcome" value="decline">Decline</button>
            </form>
            """);
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Complete(string token, [FromForm] string? outcome)
    {
        if (gateway is not TestPaymentGateway testGateway)
        {
            return NotFound();
        }

        if (outcome is not ("pay" or "decline"))
        {
            return BadRequest();
        }

        var callbackUrl = testGateway.Complete(token, pay: outcome == "pay");
        if (callbackUrl is null)
        {
            return Html("<h1>This payment page can no longer be used</h1>", StatusCodes.Status409Conflict);
        }

        // Like iyzico, the browser takes the token back to the store in a form post.
        return Html($"""
            <p>Returning to the store…</p>
            <form id="callback" method="post" action="{Encode(callbackUrl)}">
              <input type="hidden" name="token" value="{Encode(token)}">
              <noscript><button>Continue</button></noscript>
            </form>
            <script>document.getElementById('callback').submit()</script>
            """);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static ContentResult Html(string body, int statusCode = StatusCodes.Status200OK) => new()
    {
        StatusCode = statusCode,
        ContentType = "text/html; charset=utf-8",
        Content = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Test payment</title>
              <style>
                body { font-family: system-ui, sans-serif; max-width: 28rem; margin: 4rem auto; padding: 0 1rem; }
                .note { background: #fff4d6; padding: .75rem; border-radius: .5rem; }
                .amount { font-size: 2rem; font-weight: 600; }
                button { font-size: 1rem; padding: .6rem 1.4rem; margin-right: .5rem; }
              </style>
            </head>
            <body>
            {{body}}
            </body>
            </html>
            """
    };
}
