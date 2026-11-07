using Microsoft.AspNetCore.Identity;

namespace FurnitureEcommerce.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
