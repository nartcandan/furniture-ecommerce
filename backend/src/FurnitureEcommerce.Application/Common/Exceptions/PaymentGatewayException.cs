namespace FurnitureEcommerce.Application.Common.Exceptions;

/// <summary>The payment provider could not be reached or gave an answer that cannot be acted on.</summary>
public class PaymentGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);
