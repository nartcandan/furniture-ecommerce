using FurnitureEcommerce.Domain.Entities;
using FurnitureEcommerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnitureEcommerce.Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.Property(address => address.Title).HasMaxLength(50);
        builder.Property(address => address.RecipientFirstName).HasMaxLength(100);
        builder.Property(address => address.RecipientLastName).HasMaxLength(100);
        builder.Property(address => address.PhoneNumber).HasMaxLength(20);
        builder.Property(address => address.City).HasMaxLength(50);
        builder.Property(address => address.District).HasMaxLength(50);
        builder.Property(address => address.AddressLine).HasMaxLength(500);
        builder.Property(address => address.PostalCode).HasMaxLength(10);

        // Domain cannot see ApplicationUser, so the relationship has no navigation properties.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(address => address.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
