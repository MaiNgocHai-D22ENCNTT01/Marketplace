namespace Marketplace.Domain.Entities;

public enum SaleItemStatus { PENDING, APPROVED, REJECTED }
public sealed class FlashSaleItem
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid VariantId { get; set; }
    public Guid ShopId { get; set; }
    public decimal FlashPrice { get; set; }
    public int TotalQuota { get; set; }
    public int SoldCount { get; set; }
    public int MaxPerUser { get; set; }
    public SaleItemStatus Status { get; set; }
    public FlashSaleCampaign Campaign { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public Shop Shop { get; set; } = null!;
}
