using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.CatalogService.Domain.Entities;

namespace Shop.CatalogService.Infrastructure.Persistence.Configurations;

public sealed class StockReservationItemConfiguration : IEntityTypeConfiguration<StockReservationItem>
{
    public void Configure(EntityTypeBuilder<StockReservationItem> builder)
    {
        builder.ToTable("StockReservationItems", t =>
        {
            t.HasCheckConstraint("CK_StockReservationItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_StockReservationItems_UnitPrice", "\"UnitPrice\" >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ReservationId)
            .IsRequired();

        builder.Property(i => i.ProductId)
            .IsRequired();

        builder.Property(i => i.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("numeric(18,2)");

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.HasIndex(i => new { i.ReservationId, i.ProductId })
            .IsUnique();

        builder.HasIndex(i => i.ReservationId);
    }
}
