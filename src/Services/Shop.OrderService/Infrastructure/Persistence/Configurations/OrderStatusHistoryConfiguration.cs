using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.OrderService.Domain.Entities;

namespace Shop.OrderService.Infrastructure.Persistence.Configurations;

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistory");

        builder.HasKey(sh => sh.Id);

        builder.Property(sh => sh.OrderId)
            .IsRequired();

        builder.HasIndex(sh => sh.OrderId);

        builder.Property(sh => sh.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(sh => sh.ChangedAtUtc)
            .IsRequired();
    }
}
