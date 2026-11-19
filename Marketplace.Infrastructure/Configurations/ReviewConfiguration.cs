using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> entity)
    {
        entity.ToTable("reviews", table =>
        {
            table.HasCheckConstraint("ck_reviews_rating", "rating >= 1 AND rating <= 5");
            table.HasCheckConstraint("ck_reviews_status", "status IN ('ACTIVE', 'HIDDEN')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Images).HasColumnName("images").HasDefaultValueSql("ARRAY[]::text[]");
        entity.HasIndex(x => new { x.ProductId, x.Status, x.CreatedAt });

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.OrderItemId)
            .HasColumnName("order_item_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.HasIndex(x => x.OrderItemId).IsUnique();

        entity.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.Rating)
            .HasColumnName("rating")
            .HasColumnType("int")
            .IsRequired();

        entity.Property(x => x.Comment)
            .HasColumnName("comment")
            .HasColumnType("text");

        entity.Property(x => x.SellerReply)
            .HasColumnName("seller_reply")
            .HasColumnType("text");

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(ReviewStatus.ACTIVE)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.HasOne(x => x.OrderItem)
            .WithOne(x => x.Review)
            .HasForeignKey<Review>(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Customer)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Product)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
