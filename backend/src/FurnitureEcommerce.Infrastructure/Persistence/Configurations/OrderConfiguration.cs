using FurnitureEcommerce.Domain.Entities;
using FurnitureEcommerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnitureEcommerce.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public const string PendingPaymentIndexName = "IX_Orders_UserId_PendingPayment";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(table =>
            table.HasCheckConstraint("CK_Orders_TotalAmount_NonNegative", "\"TotalAmount\" >= 0"));

        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(order => order.TotalAmount).HasPrecision(10, 2);
        builder.Property(order => order.RecipientFirstName).HasMaxLength(100);
        builder.Property(order => order.RecipientLastName).HasMaxLength(100);
        builder.Property(order => order.PhoneNumber).HasMaxLength(20);
        builder.Property(order => order.City).HasMaxLength(50);
        builder.Property(order => order.District).HasMaxLength(50);
        builder.Property(order => order.AddressLine).HasMaxLength(500);
        builder.Property(order => order.PostalCode).HasMaxLength(10);
        builder.Property(order => order.PaymentToken).HasMaxLength(200);
        builder.Property(order => order.PaymentPageUrl).HasMaxLength(500);
        builder.Property(order => order.PaymentId).HasMaxLength(100);
        builder.Property(order => order.PaymentError).HasMaxLength(500);

        // A user can have only one order that is waiting for payment.
        // This is the real guard against a double click on the pay button.
        builder.HasIndex(order => order.UserId, PendingPaymentIndexName)
            .IsUnique()
            .HasFilter("\"Status\" = 'PendingPayment'");

        // The payment callback finds the order by this token.
        builder.HasIndex(order => order.PaymentToken).IsUnique();

        // The reconciliation job looks for closed orders whose payment page has to be checked once more.
        // Almost every order has no such date, so the index only holds the few that do.
        builder.HasIndex(order => order.PaymentRecheckAt)
            .HasFilter("\"PaymentRecheckAt\" IS NOT NULL");

        // For the order history, newest first.
        builder.HasIndex(order => new { order.UserId, order.CreatedAt });

        // Domain cannot see ApplicationUser, so the relationship has no navigation properties.
        // Orders are business records, so a user with orders cannot be deleted.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(order => order.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
