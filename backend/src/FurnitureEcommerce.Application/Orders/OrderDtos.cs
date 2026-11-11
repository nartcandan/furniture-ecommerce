namespace FurnitureEcommerce.Application.Orders;

public record CreateOrderRequest(Guid AddressId);

public record OrderResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
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
