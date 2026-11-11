namespace FurnitureEcommerce.Domain.Entities;

public class Cart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // The user lives in Infrastructure, so the foreign key is set up in the EF configuration.
    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}
