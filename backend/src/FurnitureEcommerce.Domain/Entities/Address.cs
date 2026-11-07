namespace FurnitureEcommerce.Domain.Entities;

public class Address
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // The user lives in Infrastructure, so the foreign key is set up in the EF configuration.
    public Guid UserId { get; set; }

    public required string Title { get; set; }
    public required string RecipientFirstName { get; set; }
    public required string RecipientLastName { get; set; }
    public required string PhoneNumber { get; set; }
    public required string City { get; set; }
    public required string District { get; set; }
    public required string AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
