namespace FurnitureEcommerce.Application.Orders;

public record AdminOrderQuery
{
    /// <summary>An order status such as Paid or Shipped. Leave empty for all orders.</summary>
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record AdminOrderSummaryResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
    int TotalQuantity,
    string RecipientName,
    string City,
    DateTime? PaidAt,
    DateTime CreatedAt);

public record AdminOrderResponse(
    Guid Id,
    Guid UserId,
    string Status,
    decimal TotalAmount,
    string RecipientFirstName,
    string RecipientLastName,
    string PhoneNumber,
    string City,
    string District,
    string AddressLine,
    string? PostalCode,
    string? PaymentId,
    string? PaymentError,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<OrderItemResponse> Items);
