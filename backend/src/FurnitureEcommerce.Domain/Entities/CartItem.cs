namespace FurnitureEcommerce.Domain.Entities;

// Price and totals are not stored here. They are always read from the product.
public class CartItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CartId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
