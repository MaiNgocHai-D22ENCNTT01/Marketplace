using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class UserInteractionLogConfiguration : IEntityTypeConfiguration<UserInteractionLog>
{
    public void Configure(EntityTypeBuilder<UserInteractionLog> entity)
    {
        entity.ToTable("user_interaction_logs", table =>
        {
            table.HasCheckConstraint("ck_user_interaction_logs_action_type", "action_type IN ('VIEW', 'CLICK', 'ADD_TO_CART', 'PURCHASE', 'WISHLIST_ADD', 'WISHLIST_REMOVE', 'REVIEW')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.ActionType)
            .HasColumnName("action_type")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        entity.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.HasOne(x => x.User)
            .WithMany(x => x.UserInteractionLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Product)
            .WithMany(x => x.UserInteractionLogs)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
