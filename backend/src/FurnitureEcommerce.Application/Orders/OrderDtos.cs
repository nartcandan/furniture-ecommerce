namespace FurnitureEcommerce.Application.Orders;

public record CreateOrderRequest(Guid AddressId);

/// <param name="PaymentPageUrl">
/// Where to send the customer to pay. Present while the order waits for payment and its payment page is still open.
/// </param>
/// <param name="CanCancel">
/// True while the customer can still cancel: waiting for payment, or paid today and not shipped.
/// </param>
public record OrderResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
    string? PaymentPageUrl,
    string RecipientFirstName,
    string RecipientLastName,
    string PhoneNumber,
    string City,
    string District,
    string AddressLine,
    string? PostalCode,
    string? PaymentError,
    DateTime? PaidAt,
    DateTime CreatedAt,
    bool CanCancel,
    List<OrderItemResponse> Items);

public record OrderItemResponse(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public record OrderSummaryResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
    int TotalQuantity,
    DateTime CreatedAt);
