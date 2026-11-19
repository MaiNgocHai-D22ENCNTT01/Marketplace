using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> entity)
    {
        entity.ToTable("complaints", table =>
        {
            table.HasCheckConstraint("ck_complaints_status", "status IN ('OPEN', 'IN_PROGRESS', 'RESOLVED', 'CLOSED')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.SenderId)
            .HasColumnName("sender_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.TargetShopId)
            .HasColumnName("target_shop_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uuid");

        entity.Property(x => x.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(ComplaintStatus.OPEN)
            .IsRequired();

        entity.Property(x => x.ResolutionNote)
            .HasColumnName("resolution_note")
            .HasColumnType("text");

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.HasOne(x => x.Sender)
            .WithMany(x => x.Complaints)
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.TargetShop)
            .WithMany(x => x.Complaints)
            .HasForeignKey(x => x.TargetShopId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Order)
            .WithMany(x => x.Complaints)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
