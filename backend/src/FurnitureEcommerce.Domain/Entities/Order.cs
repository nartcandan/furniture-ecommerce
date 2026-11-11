namespace FurnitureEcommerce.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // The user lives in Infrastructure, so the foreign key is set up in the EF configuration.
    public Guid UserId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public decimal TotalAmount { get; set; }

    // A copy of the address at the time of the order. Later address changes do not affect it.
    public required string RecipientFirstName { get; set; }
    public required string RecipientLastName { get; set; }
    public required string PhoneNumber { get; set; }
    public required string City { get; set; }
    public required string District { get; set; }
    public required string AddressLine { get; set; }
    public string? PostalCode { get; set; }

    // Payment details live here because an order has exactly one payment.
    public string? PaymentToken { get; set; }
    public string? PaymentId { get; set; }
    public string? PaymentError { get; set; }
    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}
