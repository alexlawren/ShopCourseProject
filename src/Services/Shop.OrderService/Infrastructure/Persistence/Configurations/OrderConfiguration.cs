using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.OrderService.Domain.Entities;

namespace Shop.OrderService.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_TotalAmount", "\"TotalAmount\" >= 0");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId)
            .IsRequired();

        builder.Property(o => o.CheckoutRequestId)
            .IsRequired();

        builder.Property(o => o.ReservationId)
            .IsRequired();

        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.CreatedAtUtc);
        builder.HasIndex(o => o.CheckoutRequestId)
            .IsUnique();
        builder.HasIndex(o => o.ReservationId)
            .IsUnique();

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(o => o.PaymentStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(o => o.TotalAmount)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(o => o.CreatedAtUtc)
            .IsRequired();

        builder.Property(o => o.UpdatedAtUtc)
            .IsRequired();

        builder.HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.StatusHistory)
            .WithOne(sh => sh.Order)
            .HasForeignKey(sh => sh.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
