using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> entity)
    {
        entity.ToTable("vouchers", table =>
        {
            table.HasCheckConstraint("ck_vouchers_discount_type", "discount_type IN ('PERCENTAGE', 'FIXED_AMOUNT')");
            table.HasCheckConstraint("ck_vouchers_discount_value", "discount_value > 0");
            table.HasCheckConstraint("ck_vouchers_min_order_value", "min_order_value >= 0");
            table.HasCheckConstraint("ck_vouchers_usage_limit", "usage_limit >= 0");
            table.HasCheckConstraint("ck_vouchers_end_date", "end_date > start_date");
            table.HasCheckConstraint("ck_vouchers_status", "status IN ('ACTIVE', 'INACTIVE', 'EXPIRED')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.ShopId)
            .HasColumnName("shop_id")
            .HasColumnType("uuid");

        entity.Property(x => x.Code)
            .HasColumnName("code")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        entity.HasIndex(x => x.Code).IsUnique();

        entity.Property(x => x.DiscountType)
            .HasColumnName("discount_type")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        entity.Property(x => x.DiscountValue)
            .HasColumnName("discount_value")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.MinOrderValue)
            .HasColumnName("min_order_value")
            .HasColumnType("decimal(12,2)")
            .HasDefaultValue(0.00m)
            .IsRequired();

        entity.Property(x => x.UsageLimit)
            .HasColumnName("usage_limit")
            .HasColumnType("int")
            .IsRequired();

        entity.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        entity.Property(x => x.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(VoucherStatus.ACTIVE)
            .IsRequired();

        entity.HasOne(x => x.Shop)
            .WithMany(x => x.Vouchers)
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
