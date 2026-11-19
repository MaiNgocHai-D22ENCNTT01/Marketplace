using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> entity)
    {
        entity.ToTable("shops", table =>
        {
            table.HasCheckConstraint("ck_shops_status", "status IN ('PENDING', 'ACTIVE', 'REJECTED', 'BANNED')");
            table.HasCheckConstraint("ck_shops_rating_avg", "rating_avg >= 0 AND rating_avg <= 5");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ReviewCount).HasColumnName("review_count").HasDefaultValue(0);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.HasIndex(x => x.UserId).IsUnique();

        entity.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.HasIndex(x => x.Name).IsUnique();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        entity.Property(x => x.Logo)
            .HasColumnName("logo")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255);

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(ShopStatus.PENDING)
            .IsRequired();

        entity.Property(x => x.RatingAvg)
            .HasColumnName("rating_avg")
            .HasColumnType("decimal(3,2)")
            .HasDefaultValue(0.00m)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.HasOne(x => x.User)
            .WithOne(x => x.Shop)
            .HasForeignKey<Shop>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
