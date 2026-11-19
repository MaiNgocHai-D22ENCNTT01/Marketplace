using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Product
{
    public decimal RatingAvg { get; set; }
    public int ReviewCount { get; set; }
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ShopId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.ACTIVE;

    public ICollection<ProductVariant> ProductVariants { get; } = new List<ProductVariant>();
    public ICollection<ProductImage> ProductImages { get; } = new List<ProductImage>();
    public ICollection<Review> Reviews { get; } = new List<Review>();
    public ICollection<UserInteractionLog> UserInteractionLogs { get; } = new List<UserInteractionLog>();
    public Shop Shop { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
