using Marketplace.Domain.Entities;
using Marketplace.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Seed;

public static partial class DataSeeder
{
    private static async Task SeedFlashSale(AppDbContext db, CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        var end = start.AddHours(3);
        if (await db.FlashSaleCampaigns.AnyAsync(c => c.StartTime <= end && c.EndTime >= start, ct)) return;
        var ids = Collections.Take(8).Select(line => SeedId($"variant/{line.Item}/mint")).ToArray();
        var variants = await db.ProductVariants.Include(v => v.Product)
            .Where(v => ids.Contains(v.Id) && v.StockQuantity > 0 && v.Price >= 1).ToListAsync(ct);
        if (variants.Count == 0) return;
        var campaign = new FlashSaleCampaign { Title = "Flash Sale trong 3 giờ", StartTime = start, EndTime = end,
            Status = CampaignStatus.ACTIVE };
        foreach (var v in variants)
        {
            var quota = Math.Min(10, v.StockQuantity);
            db.FlashSaleItems.Add(new FlashSaleItem { Campaign = campaign, VariantId = v.Id, ShopId = v.Product.ShopId,
                FlashPrice = decimal.Round(v.Price * .75m, 2), TotalQuota = quota, MaxPerUser = Math.Min(2,quota),
                Status = SaleItemStatus.APPROVED });
        }
        await db.SaveChangesAsync(ct);
    }
}
