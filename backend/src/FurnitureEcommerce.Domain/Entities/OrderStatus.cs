namespace FurnitureEcommerce.Domain.Entities;

// Stored as text in the database, so the names must not be changed without a migration.
public enum OrderStatus
{
    PendingPayment,
    Paid,
    PaymentFailed,
    Cancelling,
    Cancelled,
    Shipped,
    Delivered
}
