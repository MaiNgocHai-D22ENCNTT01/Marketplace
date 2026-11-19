using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.ToTable("orders", table =>
        {
            table.HasCheckConstraint("ck_orders_total_amount", "total_amount >= 0");
            table.HasCheckConstraint("ck_orders_shipping_fee", "shipping_fee >= 0");
            table.HasCheckConstraint("ck_orders_discount_amount", "discount_amount >= 0");
            table.HasCheckConstraint("ck_orders_order_status", "order_status IN ('PENDING', 'READY_FOR_PICKUP', 'CONFIRMED', 'SHIPPING', 'DELIVERED', 'COMPLETED', 'CANCELLED', 'RETURN_REQUESTED', 'RETURNED')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.OrderGroupId)
            .HasColumnName("order_group_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.ShopId)
            .HasColumnName("shop_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.ShippingAddressSnapshot)
            .HasColumnName("shipping_address_snapshot")
            .HasColumnType("json")
            .IsRequired();

        entity.Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.ShippingFee)
            .HasColumnName("shipping_fee")
            .HasColumnType("decimal(12,2)")
            .HasDefaultValue(0.00m)
            .IsRequired();

        entity.Property(x => x.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasColumnType("decimal(12,2)")
            .HasDefaultValue(0.00m)
            .IsRequired();

        entity.Property(x => x.OrderStatus)
            .HasColumnName("order_status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(OrderStatus.PENDING)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.HasIndex(x => new { x.OrderGroupId, x.ShopId }).IsUnique();

        entity.HasOne(x => x.OrderGroup)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.OrderGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Shop)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
