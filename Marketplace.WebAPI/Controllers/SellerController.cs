using System.Text.Json;
using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize(Roles = "SELLER"), Route("api/seller")]
public sealed class SellerController(AppDbContext db, OrderOperations operations, IWebHostEnvironment env) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.Identity!.Name!);
    private IQueryable<Shop> Shops => db.Shops.Where(s => s.UserId == Actor && s.Status == ShopStatus.ACTIVE);
    private IQueryable<Product> Products => db.Products.Where(p => Shops.Any(s => s.Id == p.ShopId));
    private IQueryable<Order> Orders => db.Orders.Where(o => Shops.Any(s => s.Id == o.ShopId));

    [HttpGet("categories")]
    public Task<List<ShopInfo>> Categories(CancellationToken ct) => db.Categories.Where(c => c.Status == CategoryStatus.ACTIVE)
        .OrderBy(c => c.Name).Select(c => new ShopInfo(c.Id, c.Name)).ToListAsync(ct);

    [HttpGet("products")]
    public async Task<List<ManagedProduct>> ListProducts(CancellationToken ct)
    {
        var rows = await Products.AsNoTracking().Include(p => p.ProductVariants).Include(p => p.ProductImages)
            .AsSplitQuery().OrderBy(p => p.Name).ToListAsync(ct);
        return rows.Select(p => new ManagedProduct(p.Id, p.Status.ToString(), new(p.Name, p.CategoryId, p.Description,
            p.ProductImages.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault() ?? "/images/demo/tee-mint.png",
            p.ProductVariants.OrderBy(v => v.Sku).Select(v => new VariantEdit(v.Id, v.Sku, v.Price, v.StockQuantity)).ToList()))).ToList();
    }

    [HttpPost("products")]
    public Task<IActionResult> Create(ProductEdit input, CancellationToken ct) => Save(null, input, ct);

    [HttpPut("products/{id:guid}")]
    public Task<IActionResult> Edit(Guid id, ProductEdit input, CancellationToken ct) => Save(id, input, ct);

    // Checkout takes the same variant locks before testing whether a product can be sold.
    private async Task<bool> LockProduct(Guid id, CancellationToken ct)
    {
        if (!await Products.AnyAsync(p => p.Id == id, ct)) return false;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM products WHERE id = {id} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM product_variants WHERE product_id = {id} ORDER BY id FOR UPDATE", ct);
        return true;
    }

    private async Task<IActionResult> Save(Guid? id, ProductEdit input, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(input.ImageUrl, @"\A/images/demo/[a-z0-9-]+\.png\z") ||
            !System.IO.File.Exists(Path.Combine(env.WebRootPath, input.ImageUrl.TrimStart('/'))))
            return BadRequest("Chọn một ảnh demo hiện có.");
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.Status == CategoryStatus.ACTIVE, ct))
            return BadRequest("Danh mục không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Product product;
        if (id.HasValue)
        {
            if (!await LockProduct(id.Value, ct)) return NotFound();
            product = await Products.Include(p => p.ProductVariants).Include(p => p.ProductImages).AsSplitQuery().SingleAsync(p => p.Id == id, ct);
        }
        else
        {
            var shop = await Shops.SingleOrDefaultAsync(ct);
            if (shop == null) return Forbid();
            product = new Product { ShopId = shop.Id };
            db.Products.Add(product);
        }
        var existing = product.ProductVariants.ToDictionary(v => v.Id);
        var supplied = input.Variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToList();
        if (supplied.Count != existing.Count || !existing.Keys.ToHashSet().SetEquals(supplied))
            return BadRequest("Giữ nguyên mã các biến thể đang có.");
        product.Name = input.Name.Trim();
        product.CategoryId = input.CategoryId;
        product.Description = input.Description;
        product.BasePrice = input.Variants.Min(v => v.Price);
        foreach (var edit in input.Variants)
        {
            var variant = edit.Id.HasValue ? existing[edit.Id.Value] : new ProductVariant { Attributes = "{}" };
            if (!edit.Id.HasValue) product.ProductVariants.Add(variant);
            variant.Price = edit.Price;
            variant.StockQuantity = edit.StockQuantity;
            variant.Sku = edit.Sku.Trim();
        }
        var image = product.ProductImages.OrderByDescending(i => i.IsPrimary).FirstOrDefault();
        if (image == null) product.ProductImages.Add(new ProductImage { IsPrimary = true, ImageUrl = input.ImageUrl });
        else image.ImageUrl = input.ImageUrl;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return id.HasValue ? NoContent() : StatusCode(201, new { product.Id });
    }

    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await LockProduct(id, ct)) return NotFound();
        var sold = await db.OrderItems.AnyAsync(i => i.Variant.ProductId == id, ct);
        var next = sold ? ProductStatus.DISCONTINUED : ProductStatus.HIDDEN;
        await Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, next), ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("orders")]
    public async Task<List<SellerOrder>> ListOrders(CancellationToken ct)
    {
        var rows = await Orders.AsNoTracking().Include(o => o.OrderItems).OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        return rows.Select(o =>
        {
            var address = JsonSerializer.Deserialize<ShippingAddress>(o.ShippingAddressSnapshot)!;
            return new SellerOrder(o.Id, o.OrderStatus.ToString(), o.TotalAmount,
                $"{address.RecipientName} · {address.Phone} · {address.DetailAddress}",
                o.OrderItems.Select(i => $"{i.ProductNameSnapshot} × {i.Quantity}").ToList());
        }).ToList();
    }

    [HttpPut("orders/{orderId:guid}/status")]
    public async Task<IActionResult> Change(Guid orderId, StatusInput input, CancellationToken ct)
    {
        if (input.Status is not ("CONFIRMED" or "SHIPPING" or "COMPLETED")) return BadRequest();
        return await operations.Transition(Actor, orderId, Enum.Parse<OrderStatus>(input.Status), true, ct) ? NoContent() : NotFound();
    }

    [HttpGet("analytics")]
    public async Task<Sales> Analytics(CancellationToken ct)
    {
        var completed = Orders.Where(o => o.OrderStatus == OrderStatus.COMPLETED);
        var best = await db.OrderItems.Where(i => completed.Any(o => o.Id == i.OrderId))
            .GroupBy(i => new { i.Variant.ProductId, i.Variant.Product.Name })
            .Select(g => new { g.Key.Name, g.Key.ProductId, Quantity = g.Sum(i => (long)i.Quantity) })
            .OrderByDescending(g => g.Quantity).ThenBy(g => g.ProductId).Take(10).ToListAsync(ct);
        return new(await completed.SumAsync(o => o.TotalAmount, ct), await Orders.CountAsync(ct),
            best.Select(g => new BestSeller(g.Name, g.Quantity)).ToList());
    }
}
