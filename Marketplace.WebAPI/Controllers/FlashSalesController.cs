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
public sealed class FlashSalesController(AppDbContext db) : ControllerBase
{
    private const string Admin = "ADMIN,SUPER_ADMIN";
    private Guid Actor => Guid.Parse(User.Identity!.Name!);

    [AllowAnonymous, HttpGet("api/flash-sales/current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var window = await db.FlashSaleCampaigns.Where(c => c.EndTime >= now).Windows(now).FirstOrDefaultAsync(ct);
        if (window is null) return NoContent();
        var items = await FlashSaleQueries.PublicItems(db).Where(i => i.CampaignId == window.Id).Details().ToListAsync(ct);
        return Ok(new SaleWindow(window, items, now));
    }

    [Authorize(Roles = Admin), HttpPost("api/admin/flash-sales")]
    public async Task<IActionResult> Create(CampaignInput input, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var start = input.StartTime.UtcDateTime;
        var end = input.EndTime.UtcDateTime;
        if (input.StartTime == default || end <= start || end <= now) return BadRequest("Khung giờ không hợp lệ.");
        var campaign = new FlashSaleCampaign { Title = input.Title.Trim(), StartTime = start, EndTime = end,
            Status = start > now ? CampaignStatus.UPCOMING : CampaignStatus.ACTIVE };
        db.FlashSaleCampaigns.Add(campaign);
        await db.SaveChangesAsync(ct);
        return StatusCode(201, new { campaign.Id });
    }

    [Authorize(Roles = Admin), HttpGet("api/admin/flash-sales")]
    public Task<List<SaleCampaign>> Campaigns(CancellationToken ct) => db.FlashSaleCampaigns.Windows(DateTime.UtcNow).ToListAsync(ct);

    [Authorize(Roles = Admin), HttpGet("api/admin/flash-sales/items")]
    public Task<List<SaleItem>> Pending(CancellationToken ct) => db.FlashSaleItems
        .Where(i => i.Status == SaleItemStatus.PENDING && i.Campaign.EndTime >= DateTime.UtcNow).Details().ToListAsync(ct);

    [Authorize(Roles = "SELLER"), HttpGet("api/seller/flash-sales/available")]
    public Task<List<SaleCampaign>> Available(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.FlashSaleCampaigns.Where(c => c.StartTime > now).Windows(now).ToListAsync(ct);
    }

    [Authorize(Roles = "SELLER"), HttpGet("api/seller/flash-sales/items")]
    public Task<List<SaleItem>> Mine(CancellationToken ct) =>
        db.FlashSaleItems.Where(i => i.Shop.UserId == Actor).Details().ToListAsync(ct);

    private Task LockVariant(Guid variant, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM product_variants WHERE id={variant} FOR UPDATE", ct);

    [Authorize(Roles = "SELLER"), HttpPost("api/seller/flash-sales/{campaignId:guid}/register")]
    public async Task<IActionResult> Register(Guid campaignId, SaleRegistration input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockVariant(input.VariantId, ct);
        var window = await db.FlashSaleCampaigns.FindAsync([campaignId], ct);
        if (window is null) return NotFound();
        if (window.StartTime <= DateTime.UtcNow) return Conflict("Khung giờ đã đóng đăng ký.");
        var variant = await db.ProductVariants.Where(v => v.Id == input.VariantId && v.Product.Shop.UserId == Actor &&
            v.Status == ProductVariantStatus.ACTIVE && v.Product.Status == ProductStatus.ACTIVE &&
            v.Product.Category.Status == CategoryStatus.ACTIVE && v.Product.Shop.Status == ShopStatus.ACTIVE)
            .Select(v => new { v.Price, v.StockQuantity, v.Product.ShopId }).SingleOrDefaultAsync(ct);
        if (variant is null) return NotFound();
        if (input.TotalQuota > variant.StockQuantity || input.MaxPerUser > input.TotalQuota)
            return Conflict("Số suất phải nằm trong tồn kho và đủ cho giới hạn mỗi khách.");
        if (input.FlashPrice >= variant.Price || decimal.Round(input.FlashPrice,2) != input.FlashPrice)
            return Conflict("Giá sale phải thấp hơn giá bán và có tối đa hai chữ số thập phân.");
        if (await db.FlashSaleItems.AnyAsync(i => i.CampaignId == campaignId && i.VariantId == input.VariantId, ct))
            return Conflict("Biến thể đã đăng ký trong khung giờ này.");
        var item = new FlashSaleItem { CampaignId = campaignId, VariantId = input.VariantId, ShopId = variant.ShopId,
            FlashPrice = input.FlashPrice, TotalQuota = input.TotalQuota, MaxPerUser = input.MaxPerUser };
        db.FlashSaleItems.Add(item);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new { item.Id });
    }

    [Authorize(Roles = Admin), HttpPut("api/admin/flash-sales/items/{itemId:guid}/approve")]
    public async Task<IActionResult> Decide(Guid itemId, StatusInput input, CancellationToken ct)
    {
        if (input.Status is not ("APPROVED" or "REJECTED")) return BadRequest("Chọn APPROVED hoặc REJECTED.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var variant = await db.FlashSaleItems.Where(i => i.Id == itemId).Select(i => (Guid?)i.VariantId).SingleOrDefaultAsync(ct);
        if (!variant.HasValue) return NotFound();
        await LockVariant(variant.Value, ct);
        var item = await db.FlashSaleItems.Include(i => i.Campaign).Include(i => i.Variant).SingleAsync(i => i.Id == itemId, ct);
        if (item.Status != SaleItemStatus.PENDING) return Conflict("Đăng ký này đã được xử lý.");
        if (input.Status == "APPROVED")
        {
            if (item.Campaign.EndTime < DateTime.UtcNow || item.TotalQuota > item.Variant.StockQuantity || item.FlashPrice >= item.Variant.Price)
                return Conflict("Kiểm tra lại thời gian, giá bán hoặc tồn kho trước khi duyệt.");
            if (await db.FlashSaleItems.AnyAsync(i => i.VariantId == variant && i.Status == SaleItemStatus.APPROVED &&
                i.Campaign.StartTime <= item.Campaign.EndTime && i.Campaign.EndTime >= item.Campaign.StartTime, ct))
                return Conflict("Biến thể đã có giá Flash Sale khác trong khung giờ này.");
        }
        item.Status = input.Status == "APPROVED" ? SaleItemStatus.APPROVED : SaleItemStatus.REJECTED;
        db.AuditLogs.Add(new() { AdminId = Actor, TargetEntity = "flash_sale_items", TargetId = itemId, Action = "FLASH_SALE_DECISION",
            Details = JsonSerializer.Serialize(new { previous = "PENDING", current = input.Status }) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }
}
