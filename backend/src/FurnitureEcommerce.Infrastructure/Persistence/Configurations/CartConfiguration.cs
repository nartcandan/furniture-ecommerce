using FurnitureEcommerce.Domain.Entities;
using FurnitureEcommerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnitureEcommerce.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        // One cart per user. The unique index also stops two simultaneous requests
        // from creating two carts for the same user.
        builder.HasIndex(cart => cart.UserId).IsUnique();

        // Domain cannot see ApplicationUser, so the relationship has no navigation properties.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Cart>(cart => cart.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
