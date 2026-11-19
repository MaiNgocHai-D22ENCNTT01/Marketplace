using System.Text.Json;
using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize(Roles = "ADMIN,SUPER_ADMIN"), Route("api/admin")]
public sealed class AdminController(AppDbContext db) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.Identity!.Name!);

    [HttpGet("shops/pending")]
    public Task<List<NamedRow>> Pending(CancellationToken ct) => db.Shops.Where(s => s.Status == ShopStatus.PENDING)
        .OrderBy(s => s.CreatedAt).Select(s => new NamedRow(s.Id,s.Name,"PENDING")).ToListAsync(ct);

    [HttpPut("shops/{shopId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid shopId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var shop = await db.Shops.AsNoTracking().SingleOrDefaultAsync(s => s.Id == shopId,ct);
        if (shop == null) return NotFound();
        if (await db.Shops.Where(s => s.Id == shopId && s.Status == ShopStatus.PENDING)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ShopStatus.ACTIVE), ct) == 0)
            return Conflict("Gian hàng không còn chờ duyệt.");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO roles (name) VALUES ('SELLER') ON CONFLICT (name) DO NOTHING", ct);
        var role = await db.Roles.SingleAsync(r => r.Name == RoleName.SELLER,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO user_roles (user_id,role_id) VALUES ({shop.UserId},{role.Id}) ON CONFLICT DO NOTHING",ct);
        Log("APPROVE_SHOP","shops",shopId,"PENDING","ACTIVE");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("users")]
    public Task<List<NamedRow>> Users(CancellationToken ct) => db.Users.OrderBy(u => u.Email)
        .Select(u => new NamedRow(u.Id,u.Email,u.Status.ToString())).ToListAsync(ct);

    [HttpGet("products")]
    public Task<List<NamedRow>> Products(CancellationToken ct) => db.Products.OrderBy(p => p.Name)
        .Select(p => new NamedRow(p.Id,p.Name,p.Status.ToString())).ToListAsync(ct);

    [HttpPut("users/{userId:guid}/status")]
    public async Task<IActionResult> UserStatusChange(Guid userId, StatusInput input, CancellationToken ct)
    {
        if (input.Status is not ("ACTIVE" or "BANNED" or "INACTIVE")) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={userId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (user == null) return NotFound();
        if (user.Status.ToString() == input.Status) return NoContent();
        Log("USER_STATUS","users",userId,user.Status.ToString(),input.Status);
        user.Status = Enum.Parse<UserStatus>(input.Status);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpPut("products/{productId:guid}/status")]
    public async Task<IActionResult> ProductStatusChange(Guid productId, StatusInput input, CancellationToken ct)
    {
        if (input.Status != "HIDDEN") return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var product = (await db.Products.FromSqlInterpolated($"SELECT * FROM products WHERE id={productId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (product == null) return NotFound();
        if (product.Status == ProductStatus.HIDDEN) return NoContent();
        if (product.Status == ProductStatus.DISCONTINUED) return Conflict("Sản phẩm đã ngừng kinh doanh.");
        Log("PRODUCT_STATUS","products",productId,product.Status.ToString(),input.Status);
        product.Status = ProductStatus.HIDDEN;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    private void Log(string action,string entity,Guid id,string previous,string current) =>
        db.AuditLogs.Add(new AuditLog { AdminId = Actor, Action = action, TargetEntity = entity, TargetId = id,
            Details = JsonSerializer.Serialize(new { previous, current }) });

    [HttpGet("audit-logs")]
    public Task<List<AuditEntry>> Logs(CancellationToken ct) => db.AuditLogs.OrderByDescending(a => a.Timestamp)
        .Select(a => new AuditEntry(a.Timestamp,a.Admin.Email,a.Action,a.TargetEntity,a.TargetId,a.Details)).ToListAsync(ct);

    [HttpGet("dashboard")]
    public async Task<Dashboard> Stats(CancellationToken ct) => new(await db.Users.CountAsync(ct),await db.Shops.CountAsync(ct),
        await db.Products.CountAsync(ct),await db.Orders.CountAsync(ct),
        await db.Orders.Where(o => o.OrderStatus == OrderStatus.COMPLETED).SumAsync(o => o.TotalAmount,ct));
}
