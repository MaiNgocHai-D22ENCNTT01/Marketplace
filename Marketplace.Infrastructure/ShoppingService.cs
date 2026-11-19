using System.Text.Json;
using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure;

public sealed class ShoppingConflict(string message) : Exception(message);

public sealed class ShoppingService(AppDbContext db)
{
    // All mutations for one buyer take this lock before reading the cart.
    private async Task<Cart> LockCart(Guid buyer, CancellationToken ct)
    {
        var carts = await db.Carts.FromSqlInterpolated($"SELECT * FROM carts WHERE user_id = {buyer} FOR UPDATE")
            .ToListAsync(ct);
        return carts.SingleOrDefault() ?? throw new ShoppingConflict("Tài khoản chưa có giỏ hàng.");
    }

    private IQueryable<CartItem> Lines() => db.CartItems.Include(x => x.Variant).ThenInclude(v => v.Product)
        .ThenInclude(p => p.Shop).Include(x => x.Variant).ThenInclude(v => v.Product).ThenInclude(p => p.Category);

    private static bool Available(ProductVariant v) => v.Status == ProductVariantStatus.ACTIVE
        && v.Product.Status == ProductStatus.ACTIVE && v.Product.Shop.Status == ShopStatus.ACTIVE
        && v.Product.Category.Status == CategoryStatus.ACTIVE;

    public async Task<CartResult> ReadCart(Guid buyer, CancellationToken ct)
    {
        var lines = await Lines().AsNoTracking().Where(x => x.Cart.UserId == buyer).OrderBy(x => x.Id).ToListAsync(ct);
        var variantIds = lines.Select(l => l.VariantId).ToArray();
        var prices = await FlashSaleQueries.LiveItems(db, DateTime.UtcNow)
            .Where(i => variantIds.Contains(i.VariantId))
            .ToDictionaryAsync(i => i.VariantId, i => i.FlashPrice, ct);
        return new(lines.GroupBy(x => new ShopInfo(x.Variant.Product.ShopId, x.Variant.Product.Shop.Name))
            .Select(g => new CartShop(g.Key, g.Select(x =>
            {
                var v = x.Variant;
                var info = new VariantInfo(v.Id, v.Sku, JsonSerializer.Deserialize<JsonElement>(v.Attributes), v.Price, v.StockQuantity);
                return new CartLine(v.Id, v.Product.Name, v.Sku, info.Label, prices.GetValueOrDefault(v.Id, v.Price), x.Quantity);
            }).ToList())).ToList());
    }

    public async Task Add(Guid buyer, AddItemRequest input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var cart = await LockCart(buyer, ct);
        var v = await db.ProductVariants.Include(v => v.Product.Shop).Include(v => v.Product.Category)
            .SingleOrDefaultAsync(v => v.Id == input.VariantId, ct);
        if (v is null || !Available(v)) throw new ShoppingConflict("Biến thể hiện không bán.");
        var line = await db.CartItems.SingleOrDefaultAsync(x => x.CartId == cart.Id && x.VariantId == input.VariantId, ct);
        var quantity = (long)(line?.Quantity ?? 0) + input.Quantity;
        if (quantity > v.StockQuantity) throw new ShoppingConflict("Số lượng trong giỏ vượt tồn kho.");
        if (line is null) db.CartItems.Add(new CartItem { CartId = cart.Id, VariantId = v.Id, Quantity = input.Quantity });
        else line.Quantity = (int)quantity;
        db.UserInteractionLogs.Add(new() { UserId = buyer, ProductId = v.ProductId, ActionType = ActionType.ADD_TO_CART });
        cart.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task Remove(Guid buyer, Guid variantId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var cart = await LockCart(buyer, ct);
        await db.CartItems.Where(x => x.CartId == cart.Id && x.VariantId == variantId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<CheckoutResult> Checkout(Guid buyer, ShippingAddress address, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var cart = await LockCart(buyer, ct);
        // Lock shared stock rows in a stable order across all shoppers, then read current prices and quantities.
        await db.Database.ExecuteSqlInterpolatedAsync($@"SELECT id FROM product_variants
            WHERE id IN (SELECT variant_id FROM cart_items WHERE cart_id = {cart.Id})
            ORDER BY id FOR UPDATE", ct);
        var lines = await Lines().Where(x => x.CartId == cart.Id).OrderBy(x => x.VariantId).ToListAsync(ct);
        if (lines.Count == 0) throw new ShoppingConflict("Giỏ hàng đang trống.");
        var ids = lines.Select(l => l.VariantId).ToArray();
        // All checkouts and approvals take the variant lock before reading a sale allocation.
        var offers = await FlashSaleQueries.LiveItems(db, DateTime.UtcNow).Where(i => ids.Contains(i.VariantId))
            .ToDictionaryAsync(i => i.VariantId, ct);
        var offerIds = offers.Values.Select(i => i.Id).ToArray();
        var purchases = await db.OrderItems.Where(i => i.FlashSaleItemId.HasValue && offerIds.Contains(i.FlashSaleItemId.Value)
            && i.Order.OrderGroup.CustomerId == buyer && i.Order.OrderStatus != OrderStatus.CANCELLED)
            .GroupBy(i => i.FlashSaleItemId!.Value).Select(g => new { Id = g.Key, Quantity = g.Sum(i => (long)i.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        var group = new OrderGroup { CustomerId = buyer };
        var shipping = JsonSerializer.Serialize(address);
        foreach (var shop in lines.GroupBy(x => x.Variant.Product.ShopId))
        {
            var order = new Order { ShopId = shop.Key, ShippingAddressSnapshot = shipping };
            foreach (var line in shop)
            {
                var v = line.Variant;
                var item = OrderItem.FromCart(line);
                if (offers.TryGetValue(v.Id, out var offer))
                {
                    if (line.Quantity > offer.TotalQuota - offer.SoldCount)
                        throw new ShoppingConflict($"Không đủ suất Flash Sale cho {v.Product.Name}. Còn {offer.TotalQuota-offer.SoldCount} suất.");
                    if (line.Quantity + purchases.GetValueOrDefault(offer.Id) > offer.MaxPerUser)
                        throw new ShoppingConflict($"Flash Sale giới hạn {offer.MaxPerUser} suất cho mỗi khách với {v.Product.Name}.");
                    offer.SoldCount += line.Quantity;
                    item.FlashSaleItemId = offer.Id;
                    item.UnitPriceSnapshot = offer.FlashPrice;
                }
                if (!Available(v) || v.StockQuantity < line.Quantity)
                    throw new ShoppingConflict($"Không đủ hàng hoặc đã ngừng bán: {v.Product.Name} ({v.Sku}).");
                v.StockQuantity -= line.Quantity;
                order.OrderItems.Add(item);
                order.TotalAmount += item.UnitPriceSnapshot * line.Quantity;
            }
            group.Orders.Add(order);
            group.TotalAmount += order.TotalAmount;
        }
        if (group.TotalAmount > 9999999999.99m) throw new ShoppingConflict("Giá trị đơn vượt giới hạn lưu trữ.");
        foreach (var id in lines.Select(x => x.Variant.ProductId).Distinct())
            db.UserInteractionLogs.Add(new() { UserId = buyer, ProductId = id, ActionType = ActionType.PURCHASE });
        db.OrderGroups.Add(group);
        db.CartItems.RemoveRange(lines);
        cart.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(group.Id, group.TotalAmount, group.Orders.Count);
    }

    public async Task<List<PurchasedOrder>> History(Guid buyer, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().Where(o => o.OrderGroup.CustomerId == buyer)
            .Include(o => o.Shop).Include(o => o.OrderItems).ThenInclude(i => i.Review).OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id).ToListAsync(ct);
        return orders.Select(o => new PurchasedOrder(o.Id, o.OrderGroupId, new(o.ShopId, o.Shop.Name), o.CreatedAt,
            o.OrderStatus.ToString(), o.TotalAmount, JsonSerializer.Deserialize<ShippingAddress>(o.ShippingAddressSnapshot)!,
            o.OrderItems.OrderBy(i => i.Id).Select(i => new PurchasedItem(i.ProductNameSnapshot,
                JsonSerializer.Deserialize<JsonElement>(i.VariantSnapshot), i.UnitPriceSnapshot, i.Quantity, i.Id, i.Review != null)).ToList())).ToList();
    }
}
