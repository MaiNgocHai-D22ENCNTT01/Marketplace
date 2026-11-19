using System.Text.Json;
using Marketplace.Application.Catalog;
using Marketplace.Application.Shopping;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Route("api/products")]
public sealed partial class ProductsController(AppDbContext db, IHttpClientFactory clients) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductSummary>>> Get(string? keyword, Guid? categoryId, string? sort, CancellationToken ct)
    {
        var query = db.AvailableProducts();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Name.ToLower().Contains(keyword.Trim().ToLower()));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (sort is not (null or "" or "price_asc" or "price_desc"))
            return BadRequest("sort phải là price_asc hoặc price_desc.");
        query = sort switch
        {
            "price_asc" => query.OrderBy(p => p.BasePrice).ThenBy(p => p.Id),
            "price_desc" => query.OrderByDescending(p => p.BasePrice).ThenBy(p => p.Id),
            _ => query.OrderBy(p => p.Name).ThenBy(p => p.Id)
        };
        return await query.Summaries().ToListAsync(ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetail>> Detail(Guid id, bool clicked, CancellationToken ct)
    {
        var products = db.AvailableProducts();
        var p = await products.Include(x => x.Shop).Include(x => x.ProductVariants)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        if (Guid.TryParse(User.Identity?.Name, out var buyer))
        {
            if (clicked)
                db.UserInteractionLogs.Add(new() { UserId = buyer, ProductId = id, ActionType = ActionType.CLICK });
            db.UserInteractionLogs.Add(new() { UserId = buyer, ProductId = id, ActionType = ActionType.VIEW });
            await db.SaveChangesAsync(ct);
        }
        var summary = await products.Where(x => x.Id == id).Summaries().SingleAsync(ct);
        var related = await products.Where(x => x.Id != id && x.CategoryId == p.CategoryId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(6).Summaries().ToListAsync(ct);
        return new ProductDetail(summary, p.Description, p.CategoryId, new(p.ShopId, p.Shop.Name),
            p.ProductVariants.Where(v => v.Status == ProductVariantStatus.ACTIVE).OrderBy(v => v.Sku)
                .Select(v => new VariantInfo(v.Id, v.Sku, JsonSerializer.Deserialize<JsonElement>(v.Attributes),
                    v.Price, v.StockQuantity)).ToList(), related);
    }
}
