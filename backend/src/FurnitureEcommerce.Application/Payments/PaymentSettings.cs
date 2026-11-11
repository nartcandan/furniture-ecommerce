namespace FurnitureEcommerce.Application.Payments;

/// <param name="CallbackUrl">
/// The public address of the payment callback endpoint. The provider sends the customer's browser here.
/// </param>
/// <param name="FrontendBaseUrl">Where the customer is sent after the callback has been handled.</param>
public record PaymentSettings(string CallbackUrl, string FrontendBaseUrl);
