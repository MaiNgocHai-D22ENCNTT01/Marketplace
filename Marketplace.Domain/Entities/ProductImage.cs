namespace Marketplace.Domain.Entities;

public sealed class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public string? FeatureVector { get; set; }

    public Product Product { get; set; } = null!;
    public ProductVariant? Variant { get; set; }
}
