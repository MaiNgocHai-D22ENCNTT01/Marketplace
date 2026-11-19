using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize(Roles = "CUSTOMER")]
public sealed class AfterSalesController(AppDbContext db, OrderOperations orders) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.Identity!.Name!);

    [HttpPut("api/orders/{orderId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid orderId, CancellationToken ct) => Change(orderId, OrderStatus.CANCELLED, ct);

    [HttpPut("api/orders/{orderId:guid}/confirm-delivery")]
    public Task<IActionResult> Confirm(Guid orderId, CancellationToken ct) => Change(orderId, OrderStatus.COMPLETED, ct);

    private async Task<IActionResult> Change(Guid id, OrderStatus status, CancellationToken ct) =>
        await orders.Transition(Actor, id, status, false, ct) ? NoContent() : NotFound();

    [HttpPost("api/seller/register-shop")]
    public async Task<IActionResult> Register(ShopInput input, CancellationToken ct)
    {
        var shop = new Shop { UserId = Actor, Name = input.Name.Trim(), Description = input.Description };
        db.Shops.Add(shop);
        await db.SaveChangesAsync(ct);
        return StatusCode(201, new { shop.Id, Status = shop.Status.ToString() });
    }
}
