namespace FurnitureEcommerce.Infrastructure.Identity;

public class AdminUserOptions
{
    public const string SectionName = "AdminUser";

    public string? Email { get; init; }
    public string? Password { get; init; }
}
