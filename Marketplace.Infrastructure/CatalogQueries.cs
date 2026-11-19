using Marketplace.Application.Catalog;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure;

public static class CatalogQueries
{
    public static IQueryable<Product> AvailableProducts(this AppDbContext db) =>
        db.Products.AsNoTracking().Where(p => p.Status == ProductStatus.ACTIVE
            && p.Shop.Status == ShopStatus.ACTIVE && p.Category.Status == CategoryStatus.ACTIVE);

    public static IQueryable<ProductSummary> Summaries(this IQueryable<Product> products) =>
        products.Select(p => new ProductSummary(p.Id, p.Name, p.Category.Name, p.BasePrice,
            p.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Id)
                .Select(i => i.ImageUrl).FirstOrDefault(), p.RatingAvg, p.ReviewCount, p.ShopId, p.Shop.Name));
}
