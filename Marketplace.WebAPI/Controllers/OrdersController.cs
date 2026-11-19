using Marketplace.Application.Shopping;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize(Roles = "CUSTOMER"), Route("api/orders")]
public sealed class OrdersController(ShoppingService shopping) : ControllerBase
{
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResult>> Checkout(ShippingAddress input, CancellationToken ct)
    {
        try { return await shopping.Checkout(Guid.Parse(User.Identity!.Name!), input, ct); }
        catch (ShoppingConflict e) { return Conflict(e.Message); }
    }

    [HttpGet("my-orders")]
    public Task<List<PurchasedOrder>> History(CancellationToken ct) =>
        shopping.History(Guid.Parse(User.Identity!.Name!), ct);
}
