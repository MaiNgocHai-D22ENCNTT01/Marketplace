using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure;

public static class ReviewTotals
{
    // Hold until the review, its interaction/audit log and both aggregates commit.
    public static Task LockShop(AppDbContext db, Guid shop, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({shop.ToString()}, 31))", ct);

    public static async Task Refresh(AppDbContext db, Guid product, Guid shop, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE products SET (rating_avg, review_count) =
              (SELECT coalesce(avg(rating),0), count(*) FROM reviews WHERE product_id={product} AND status='ACTIVE')
            WHERE id={product}
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE shops SET (rating_avg, review_count) =
              (SELECT coalesce(avg(r.rating),0), count(*) FROM reviews r JOIN products p ON p.id=r.product_id
               WHERE p.shop_id={shop} AND r.status='ACTIVE')
            WHERE id={shop}
            """, ct);
    }
}
