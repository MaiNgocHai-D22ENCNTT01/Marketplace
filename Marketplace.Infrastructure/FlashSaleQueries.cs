using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure;

public static class FlashSaleQueries
{
    public static IQueryable<FlashSaleItem> PublicItems(AppDbContext db) => db.FlashSaleItems.Where(i =>
        i.Status == SaleItemStatus.APPROVED && i.Variant.Status == ProductVariantStatus.ACTIVE &&
        i.Shop.Status == ShopStatus.ACTIVE && i.Shop.User.Status == UserStatus.ACTIVE &&
        i.Variant.Product.Status == ProductStatus.ACTIVE && i.Variant.Product.Category.Status == CategoryStatus.ACTIVE &&
        i.FlashPrice < i.Variant.Price);

    public static IQueryable<FlashSaleItem> LiveItems(AppDbContext db, DateTime now) =>
        PublicItems(db).Where(i => i.Campaign.StartTime <= now && i.Campaign.EndTime >= now);

    public static IQueryable<SaleItem> Details(this IQueryable<FlashSaleItem> items) => items
        .OrderBy(i => i.Campaign.StartTime).ThenBy(i => i.Variant.Product.Name).ThenBy(i => i.Id)
        .Select(i => new SaleItem(i.Id, i.CampaignId, i.Campaign.Title, i.VariantId, i.Variant.ProductId,
            i.Variant.Product.Name, i.Variant.Sku, i.Shop.Name,
            i.Variant.Product.ProductImages.OrderByDescending(p => p.IsPrimary).ThenBy(p => p.Id).Select(p => p.ImageUrl).FirstOrDefault(),
            i.Variant.Price, i.FlashPrice, i.TotalQuota, i.SoldCount, i.MaxPerUser, i.Status.ToString()));

    public static IQueryable<SaleCampaign> Windows(this IQueryable<FlashSaleCampaign> campaigns, DateTime now) =>
        campaigns.OrderBy(c => c.StartTime).ThenBy(c => c.Id).Select(c => new SaleCampaign(c.Id, c.Title, c.StartTime,
            c.EndTime, now < c.StartTime ? "UPCOMING" : now > c.EndTime ? "ENDED" : "ACTIVE"));
}
