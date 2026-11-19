using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Marketplace.Application.Catalog;

namespace Marketplace.Application.Shopping;

public record RegisterRequest([param: Required, StringLength(255)] string FullName,
    [param: Required, EmailAddress, StringLength(255)] string Email,
    [param: Required, StringLength(128, MinimumLength = 8)] string Password);
public record LoginRequest([param: Required, EmailAddress, StringLength(255)] string Email,
    [param: Required, StringLength(128)] string Password);
public record LoginResult(string Token, DateTime ExpiresAt, Guid UserId, string[] Roles);
public record AddItemRequest(Guid VariantId, [param: Range(1, int.MaxValue)] int Quantity);
public record ShippingAddress([param: Required, StringLength(150)] string RecipientName,
    [param: Required, Phone, StringLength(20)] string Phone,
    [param: Required, StringLength(500)] string DetailAddress);
public record ShopInfo(Guid Id, string Name);
public record VariantInfo(Guid Id, string Sku, JsonElement Attributes, decimal Price, int StockQuantity)
{
    public string Label => Attributes.ValueKind == JsonValueKind.Object
        ? string.Join(" · ", Attributes.EnumerateObject().Select(p => p.Value.ToString())) : Sku;
}
public record ProductDetail(ProductSummary Product, string? Description, Guid CategoryId, ShopInfo Shop,
    List<VariantInfo> ProductVariants, List<ProductSummary> RelatedProducts);
public record CartLine(Guid VariantId, string ProductName, string Sku, string Variant, decimal Price, int Quantity);
public record CartShop(ShopInfo Shop, List<CartLine> Items)
{
    public decimal Total => Items.Sum(i => i.Price * i.Quantity);
}
public record CartResult(List<CartShop> Shops)
{
    public long Quantity => Shops.SelectMany(s => s.Items).Sum(i => (long)i.Quantity);
    public decimal Total => Shops.Sum(s => s.Total);
}
public record PurchasedItem(string ProductName, JsonElement Variant, decimal UnitPrice, int Quantity, Guid OrderItemId, bool Reviewed);
public record PurchasedOrder(Guid Id, Guid OrderGroupId, ShopInfo Shop, DateTime CreatedAt,
    string Status, decimal Total, ShippingAddress ShippingAddress, List<PurchasedItem> Items);
public record CheckoutResult(Guid OrderGroupId, decimal Total, int OrderCount);
