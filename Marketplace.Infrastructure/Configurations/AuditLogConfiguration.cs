using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("audit_logs");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.AdminId)
            .HasColumnName("admin_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.Action)
            .HasColumnName("action")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.TargetEntity)
            .HasColumnName("target_entity")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.TargetId)
            .HasColumnName("target_id")
            .HasColumnType("uuid");

        entity.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.Property(x => x.Details)
            .HasColumnName("details")
            .HasColumnType("json");

        entity.HasOne(x => x.Admin)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
