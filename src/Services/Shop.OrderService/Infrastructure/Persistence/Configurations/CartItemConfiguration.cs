using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.OrderService.Domain.Entities;

namespace Shop.OrderService.Infrastructure.Persistence.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", t =>
        {
            t.HasCheckConstraint("CK_CartItems_Quantity", "\"Quantity\" > 0");
        });

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.CartId)
            .IsRequired();

        builder.Property(ci => ci.ProductId)
            .IsRequired();

        builder.Property(ci => ci.Quantity)
            .IsRequired();

        builder.Property(ci => ci.CreatedAtUtc)
            .IsRequired();

        builder.Property(ci => ci.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(ci => new { ci.CartId, ci.ProductId })
            .IsUnique();
    }
}
