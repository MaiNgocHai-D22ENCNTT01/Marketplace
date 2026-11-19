using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class OrderGroupConfiguration : IEntityTypeConfiguration<OrderGroup>
{
    public void Configure(EntityTypeBuilder<OrderGroup> entity)
    {
        entity.ToTable("order_groups", table =>
        {
            table.HasCheckConstraint("ck_order_groups_total_amount", "total_amount >= 0");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.HasOne(x => x.Customer)
            .WithMany(x => x.OrderGroups)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
