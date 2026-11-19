using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> entity)
    {
        entity.ToTable("order_items", table =>
        {
            table.HasCheckConstraint("ck_order_items_unit_price_snapshot", "unit_price_snapshot >= 0");
            table.HasCheckConstraint("ck_order_items_quantity", "quantity > 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.FlashSaleItemId).HasColumnName("flash_sale_item_id");
        entity.HasOne<FlashSaleItem>().WithMany().HasForeignKey(x => x.FlashSaleItemId).OnDelete(DeleteBehavior.Restrict);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.VariantId)
            .HasColumnName("variant_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.ProductNameSnapshot)
            .HasColumnName("product_name_snapshot")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.VariantSnapshot)
            .HasColumnName("variant_snapshot")
            .HasColumnType("json")
            .IsRequired();

        entity.Property(x => x.UnitPriceSnapshot)
            .HasColumnName("unit_price_snapshot")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("int")
            .IsRequired();

        entity.HasOne(x => x.Order)
            .WithMany(x => x.OrderItems)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Variant)
            .WithMany(x => x.OrderItems)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
