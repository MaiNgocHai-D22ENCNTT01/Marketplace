using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class FlashSaleConfiguration : IEntityTypeConfiguration<FlashSaleCampaign>, IEntityTypeConfiguration<FlashSaleItem>
{
    public void Configure(EntityTypeBuilder<FlashSaleCampaign> e)
    {
        e.ToTable("flash_sale_campaigns", t => {
            t.HasCheckConstraint("ck_flash_window","end_time > start_time");
            t.HasCheckConstraint("ck_flash_status","status IN ('UPCOMING','ACTIVE','ENDED')");
        });
        e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(160);
        e.Property(x => x.StartTime).HasColumnName("start_time");
        e.Property(x => x.EndTime).HasColumnName("end_time");
        e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.HasIndex(x => x.StartTime);
    }
    public void Configure(EntityTypeBuilder<FlashSaleItem> e)
    {
        e.ToTable("flash_sale_items", t => {
            t.HasCheckConstraint("ck_flash_price","flash_price > 0");
            t.HasCheckConstraint("ck_flash_quota","total_quota > 0 AND sold_count BETWEEN 0 AND total_quota AND max_per_user BETWEEN 1 AND total_quota");
            t.HasCheckConstraint("ck_flash_item_status","status IN ('PENDING','APPROVED','REJECTED')");
        });
        e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        e.Property(x => x.CampaignId).HasColumnName("campaign_id");
        e.Property(x => x.VariantId).HasColumnName("variant_id");
        e.Property(x => x.ShopId).HasColumnName("shop_id");
        e.Property(x => x.FlashPrice).HasColumnName("flash_price").HasPrecision(12,2);
        e.Property(x => x.TotalQuota).HasColumnName("total_quota");
        e.Property(x => x.SoldCount).HasColumnName("sold_count").HasDefaultValue(0);
        e.Property(x => x.MaxPerUser).HasColumnName("max_per_user");
        e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        e.HasIndex(x => new { x.CampaignId, x.VariantId }).IsUnique();
        e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId);
        e.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Restrict);
    }
}
