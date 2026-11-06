using FurnitureEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnitureEcommerce.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Products_Price_NonNegative", "\"Price\" >= 0");
            table.HasCheckConstraint("CK_Products_StockQuantity_NonNegative", "\"StockQuantity\" >= 0");
        });

        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Slug).HasMaxLength(220);
        builder.Property(p => p.Price).HasPrecision(10, 2);
        builder.Property(p => p.WidthCm).HasPrecision(6, 1);
        builder.Property(p => p.DepthCm).HasPrecision(6, 1);
        builder.Property(p => p.HeightCm).HasPrecision(6, 1);
        builder.Property(p => p.WeightKg).HasPrecision(6, 2);
        builder.Property(p => p.Material).HasMaxLength(100);
        builder.Property(p => p.Color).HasMaxLength(50);

        builder.HasIndex(p => p.Slug).IsUnique();

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
