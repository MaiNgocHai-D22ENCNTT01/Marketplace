using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ReturnRefundRequestConfiguration : IEntityTypeConfiguration<ReturnRefundRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRefundRequest> entity)
    {
        entity.ToTable("return_refund_requests", table =>
        {
            table.HasCheckConstraint("ck_return_refund_requests_refund_amount", "refund_amount >= 0");
            table.HasCheckConstraint("ck_return_refund_requests_status", "status IN ('PENDING', 'APPROVED', 'REJECTED', 'DISPUTED', 'RESOLVED')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasColumnType("text")
            .IsRequired();

        entity.Property(x => x.EvidenceImages)
            .HasColumnName("evidence_images")
            .HasColumnType("json")
            .IsRequired();

        entity.Property(x => x.RefundAmount)
            .HasColumnName("refund_amount")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(ReturnRefundRequestStatus.PENDING)
            .IsRequired();

        entity.Property(x => x.AdminNote)
            .HasColumnName("admin_note")
            .HasColumnType("text");

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.HasOne(x => x.Order)
            .WithMany(x => x.ReturnRefundRequests)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Customer)
            .WithMany(x => x.ReturnRefundRequests)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
