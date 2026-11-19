using System.ComponentModel.DataAnnotations;
using Marketplace.Application.Catalog;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, AllowAnonymous, Route("api/shops")]
public sealed class ShopsController(AppDbContext db) : ControllerBase
{
    [HttpGet("{shopId:guid}")]
    public async Task<ActionResult<PublicShop>> Detail(Guid shopId, Guid? categoryId, [StringLength(255)] string? keyword,
        string sort = "newest", [Range(1, 1000000)] int page = 1, [Range(1, 48)] int pageSize = 12,
        CancellationToken ct = default)
    {
        if (sort is not ("newest" or "price_asc" or "price_desc")) return BadRequest("Kiểu sắp xếp không hợp lệ.");
        var shop = await db.Shops.Where(s => s.Id == shopId && s.Status == ShopStatus.ACTIVE && s.User.Status == UserStatus.ACTIVE)
            .Select(s => new { s.Id, s.Name, s.Description, s.Logo, s.RatingAvg, s.ReviewCount, s.CreatedAt })
            .SingleOrDefaultAsync(ct);
        if (shop is null) return NotFound();

        var products = db.AvailableProducts().Where(p => p.ShopId == shopId);
        var categories = await products.GroupBy(p => new { p.CategoryId, p.Category.Name })
            .OrderBy(g => g.Key.Name).ThenBy(g => g.Key.CategoryId)
            .Select(g => new ShopCategory(g.Key.CategoryId, g.Key.Name, g.Count())).ToListAsync(ct);
        if (categoryId.HasValue) products = products.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLowerInvariant();
            products = products.Where(p => p.Name.ToLower().Contains(term));
        }
        var filteredTotal = await products.CountAsync(ct);
        var sorted = sort switch
        {
            "price_asc" => products.OrderBy(p => p.BasePrice),
            "price_desc" => products.OrderByDescending(p => p.BasePrice),
            _ => products.OrderByDescending(p => p.CreatedAt)
        };
        var items = await sorted.ThenBy(p => p.Id).Skip((page-1)*pageSize).Take(pageSize).Summaries().ToListAsync(ct);
        return new PublicShop(shop.Id, shop.Name, shop.Description, shop.Logo, shop.RatingAvg, shop.ReviewCount,
            shop.CreatedAt, "ACTIVE", categories.Sum(c => c.ProductCount), categories, filteredTotal, page, pageSize, items);
    }
}
