using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> entity)
    {
        entity.ToTable("payments", table =>
        {
            table.HasCheckConstraint("ck_payments_payment_method", "payment_method IN ('COD', 'VNPAY', 'MOMO', 'CREDIT_CARD')");
            table.HasCheckConstraint("ck_payments_amount", "amount >= 0");
            table.HasCheckConstraint("ck_payments_payment_status", "payment_status IN ('UNPAID', 'PAID', 'FAILED', 'REFUNDED', 'PARTIALLY_REFUNDED')");
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

        entity.HasIndex(x => x.OrderGroupId).IsUnique();

        entity.Property(x => x.PaymentMethod)
            .HasColumnName("payment_method")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        entity.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.PaymentStatus)
            .HasColumnName("payment_status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(PaymentStatus.UNPAID)
            .IsRequired();

        entity.Property(x => x.TransactionCode)
            .HasColumnName("transaction_code")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255);

        entity.Property(x => x.PaidAt)
            .HasColumnName("paid_at")
            .HasColumnType("timestamp without time zone");

        entity.HasOne(x => x.OrderGroup)
            .WithOne(x => x.Payment)
            .HasForeignKey<Payment>(x => x.OrderGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
