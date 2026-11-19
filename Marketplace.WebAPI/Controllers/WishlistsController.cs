using Marketplace.Application.Catalog;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize, Route("api/wishlists")]
public sealed class WishlistsController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.Identity!.Name!);

    [HttpGet]
    public Task<List<ProductSummary>> Get(CancellationToken ct) =>
        (from w in db.WishlistItems
         join p in db.AvailableProducts() on w.ProductId equals p.Id
         where w.UserId == UserId
         orderby w.CreatedAt descending, p.Id
         select p).Summaries().ToListAsync(ct);

    [HttpPost("toggle/{productId:guid}")]
    public async Task<IActionResult> Toggle(Guid productId, CancellationToken ct)
    {
        var user = UserId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize toggles for this pair even when no wishlist row exists yet.
        var pair = $"{user:N}/{productId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({pair}, 0))", ct);
        var item = await db.WishlistItems.SingleOrDefaultAsync(w => w.UserId == user && w.ProductId == productId, ct);
        if (item is null)
        {
            if (!await db.AvailableProducts().AnyAsync(p => p.Id == productId, ct)) return NotFound();
            db.WishlistItems.Add(new WishlistItem { UserId = user, ProductId = productId });
        }
        else db.WishlistItems.Remove(item);
        db.UserInteractionLogs.Add(new UserInteractionLog { UserId = user, ProductId = productId,
            ActionType = item is null ? ActionType.WISHLIST_ADD : ActionType.WISHLIST_REMOVE });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new WishlistStatus(item is null));
    }
}
