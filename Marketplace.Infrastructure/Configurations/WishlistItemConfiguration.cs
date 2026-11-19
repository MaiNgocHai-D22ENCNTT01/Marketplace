using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> entity)
    {
        entity.ToTable("wishlist_items");
        entity.HasKey(w => w.Id);
        entity.Property(w => w.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(w => w.UserId).HasColumnName("user_id");
        entity.Property(w => w.ProductId).HasColumnName("product_id");
        entity.Property(w => w.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
        entity.HasOne<User>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(w => w.Product).WithMany().HasForeignKey(w => w.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
