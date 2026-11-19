namespace Marketplace.Domain.Entities;

public sealed partial class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? FlashSaleItemId { get; set; }
    public Guid VariantId { get; set; }
    public string ProductNameSnapshot { get; set; } = null!;
    public string VariantSnapshot { get; set; } = null!;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }

    public Review? Review { get; set; }
    public Order Order { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
