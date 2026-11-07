using FurnitureEcommerce.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnitureEcommerce.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("Roles");

        // Seed values must be constants, otherwise every migration would rewrite these rows.
        builder.HasData(
            new IdentityRole<Guid>
            {
                Id = new Guid("019a5c10-0000-7000-8000-000000000001"),
                Name = Roles.Admin,
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "019a5c10-0000-7000-8000-0000000000a1"
            },
            new IdentityRole<Guid>
            {
                Id = new Guid("019a5c10-0000-7000-8000-000000000002"),
                Name = Roles.Customer,
                NormalizedName = "CUSTOMER",
                ConcurrencyStamp = "019a5c10-0000-7000-8000-0000000000a2"
            });
    }
}
