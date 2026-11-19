using System.Text.Json;

namespace Marketplace.Domain.Entities;

public sealed partial class OrderItem
{
    public static OrderItem FromCart(CartItem line)
    {
        var variant = line.Variant;
        using var attributes = JsonDocument.Parse(variant.Attributes);
        return new OrderItem
        {
            VariantId = variant.Id,
            ProductNameSnapshot = variant.Product.Name,
            VariantSnapshot = JsonSerializer.Serialize(new { sku = variant.Sku, attributes = attributes.RootElement }),
            UnitPriceSnapshot = variant.Price,
            Quantity = line.Quantity
        };
    }
}
