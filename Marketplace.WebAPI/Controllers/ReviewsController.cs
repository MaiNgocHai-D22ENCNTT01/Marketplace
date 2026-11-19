using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController]
public sealed class ReviewsController(AppDbContext db) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.Identity!.Name!);

    [Authorize(Roles = "CUSTOMER"), HttpPost("api/reviews")]
    public async Task<IActionResult> Create(ReviewInput input, CancellationToken ct)
    {
        if (input.Images?.Any(image => !ValidImage(image)) == true) return BadRequest("Ảnh không hợp lệ.");
        var item = await db.OrderItems.Where(i => i.Id == input.OrderItemId && i.Order.OrderGroup.CustomerId == Actor)
            .Select(i => new { i.Order.OrderStatus, i.Variant.ProductId, i.Variant.Product.ShopId }).SingleOrDefaultAsync(ct);
        if (item is null) return NotFound();
        if (item.OrderStatus != OrderStatus.COMPLETED) return Conflict("Đơn hàng chưa hoàn tất.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ReviewTotals.LockShop(db, item.ShopId, ct);
        if (await db.Reviews.AnyAsync(r => r.OrderItemId == input.OrderItemId, ct)) return Conflict("Món này đã được đánh giá.");
        var review = new Review { CustomerId = Actor, OrderItemId = input.OrderItemId, ProductId = item.ProductId,
            Rating = input.Rating, Comment = input.Comment?.Trim(), Images = input.Images ?? [] };
        db.Reviews.Add(review);
        db.UserInteractionLogs.Add(new() { UserId = Actor, ProductId = item.ProductId, ActionType = ActionType.REVIEW });
        await db.SaveChangesAsync(ct);
        await ReviewTotals.Refresh(db, item.ProductId, item.ShopId, ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new { review.Id });
    }

    private static bool ValidImage(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return false;
        if (image.Length <= 2048 && Uri.TryCreate(image, UriKind.Absolute, out var uri) && uri.Scheme == "https") return true;
        const string prefix = "data:image/jpeg;base64,";
        if (image.Length > 275000 || !image.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var buffer = new byte[image.Length * 3 / 4];
        return Convert.TryFromBase64Chars(image.AsSpan(prefix.Length), buffer, out var n)
            && n >= 4 && buffer[0] == 255 && buffer[1] == 216 && buffer[n-2] == 255 && buffer[n-1] == 217;
    }

    [HttpGet("api/products/{productId:guid}/reviews")]
    public async Task<ActionResult<ProductReviews>> Public(Guid productId, [Range(1,5)] int? rating, bool withImages = false, bool withComment = false,
        [Range(1,1000000)] int page = 1, [Range(1,25)] int pageSize = 8, CancellationToken ct = default)
    {
        if (!await db.AvailableProducts().AnyAsync(p => p.Id == productId, ct)) return NotFound();
        var active = db.Reviews.Where(r => r.ProductId == productId && r.Status == ReviewStatus.ACTIVE);
        var counts = await active.GroupBy(r => r.Rating).Select(g => new { Stars = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Stars, x => x.Count, ct);
        var total = counts.Values.Sum();
        var avg = total == 0 ? 0 : Math.Round(counts.Sum(x => (decimal)x.Key * x.Value) / total, 2, MidpointRounding.AwayFromZero);
        if (rating.HasValue) active = active.Where(r => r.Rating == rating);
        if (withComment) active = active.Where(r => !string.IsNullOrWhiteSpace(r.Comment));
        if (withImages) active = active.Where(r => r.Images.Length > 0);
        return new ProductReviews(avg, total, Enumerable.Range(1,5).Select(s => counts.GetValueOrDefault(s)).ToArray(),
            await ReadPage(active, page, pageSize, ct));
    }

    [Authorize(Roles = "SELLER"), HttpGet("api/seller/reviews")]
    public Task<ReviewPage> Seller([Range(1,1000000)] int page = 1, CancellationToken ct = default) =>
        ReadPage(db.Reviews.Where(r => r.Product.Shop.UserId == Actor && r.Status == ReviewStatus.ACTIVE), page, 12, ct);

    [Authorize(Roles = "ADMIN"), HttpGet("api/admin/reviews")]
    public Task<ReviewPage> Admin(bool hidden = false, [Range(1,1000000)] int page = 1, CancellationToken ct = default) =>
        ReadPage(db.Reviews.Where(r => r.Status == (hidden ? ReviewStatus.HIDDEN : ReviewStatus.ACTIVE)), page, 12, ct);

    private static async Task<ReviewPage> ReadPage(IQueryable<Review> query, int page, int size, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page-1)*size).Take(size).Select(r => new ReviewRow(r.Id, r.ProductId, r.Product.Name,
                r.Customer.FullName, r.Rating, r.Comment, r.Images, r.SellerReply, r.CreatedAt)).ToListAsync(ct);
        return new(rows, page, size, total);
    }

    [Authorize(Roles = "SELLER"), HttpPut("api/seller/reviews/{reviewId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid reviewId, ReviewReply input, CancellationToken ct)
    {
        var changed = await db.Reviews.Where(r => r.Id == reviewId && r.Product.Shop.UserId == Actor && r.Status == ReviewStatus.ACTIVE)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.SellerReply, input.Reply.Trim()), ct);
        return changed == 0 ? NotFound() : NoContent();
    }

    [Authorize(Roles = "ADMIN"), HttpPut("api/admin/reviews/{reviewId:guid}/status")]
    public async Task<IActionResult> Moderate(Guid reviewId, StatusInput input, CancellationToken ct)
    {
        if (input.Status is not ("ACTIVE" or "HIDDEN")) return BadRequest("Trạng thái phải là ACTIVE hoặc HIDDEN.");
        var shop = await db.Reviews.Where(r => r.Id == reviewId).Select(r => (Guid?)r.Product.ShopId).SingleOrDefaultAsync(ct);
        if (!shop.HasValue) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ReviewTotals.LockShop(db, shop.Value, ct);
        var review = await db.Reviews.FindAsync([reviewId], ct);
        if (review is null) return NotFound();
        var previous = review.Status.ToString();
        if (previous == input.Status) return NoContent();
        review.Status = Enum.Parse<ReviewStatus>(input.Status);
        db.AuditLogs.Add(new() { AdminId = Actor, TargetEntity = "Review", TargetId = reviewId, Action = "REVIEW_STATUS",
            Details = JsonSerializer.Serialize(new { previous, current = input.Status }) });
        await db.SaveChangesAsync(ct);
        await ReviewTotals.Refresh(db, review.ProductId, shop.Value, ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }
}
