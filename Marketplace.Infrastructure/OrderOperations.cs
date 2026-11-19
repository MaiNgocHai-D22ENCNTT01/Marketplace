using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure;

public sealed class OrderOperations(AppDbContext db)
{
    public async Task<bool> Transition(Guid actor, Guid id, OrderStatus target, bool seller, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var orders = db.Orders.Where(o => o.Id == id);
        orders = seller ? orders.Where(o => o.Shop.UserId == actor && o.Shop.Status == ShopStatus.ACTIVE)
                        : orders.Where(o => o.OrderGroup.CustomerId == actor);
        if (!await orders.AnyAsync(ct)) return false;
        var eligible = target switch
        {
            OrderStatus.CANCELLED or OrderStatus.CONFIRMED => orders.Where(o => o.OrderStatus == OrderStatus.PENDING),
            OrderStatus.SHIPPING => orders.Where(o => o.OrderStatus == OrderStatus.CONFIRMED || o.OrderStatus == OrderStatus.READY_FOR_PICKUP),
            OrderStatus.COMPLETED => orders.Where(o => o.OrderStatus == OrderStatus.SHIPPING || o.OrderStatus == OrderStatus.DELIVERED),
            _ => throw new ShoppingConflict("Trạng thái không hợp lệ.")
        };
        if (await eligible.ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, target), ct) != 1)
            throw new ShoppingConflict("Trạng thái hiện tại không cho phép thao tác này.");
        if (target == OrderStatus.CANCELLED)
        {
            var items = await db.OrderItems.Where(i => i.OrderId == id).OrderBy(i => i.VariantId).ToListAsync(ct);
            foreach (var item in items)
            {
                await db.ProductVariants.Where(v => v.Id == item.VariantId)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + item.Quantity), ct);
                if (item.FlashSaleItemId is Guid offer)
                    await db.FlashSaleItems.Where(i => i.Id == offer)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.SoldCount, i => i.SoldCount - item.Quantity), ct);
            }
        }
        await tx.CommitAsync(ct);
        return true;
    }
}
