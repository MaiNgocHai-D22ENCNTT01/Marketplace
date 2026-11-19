using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = null!;
    public string Attributes { get; set; } = null!;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public ProductVariantStatus Status { get; set; } = ProductVariantStatus.ACTIVE;

    public ICollection<ProductImage> ProductImages { get; } = new List<ProductImage>();
    public ICollection<CartItem> CartItems { get; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; } = new List<OrderItem>();
    public Product Product { get; set; } = null!;
}
