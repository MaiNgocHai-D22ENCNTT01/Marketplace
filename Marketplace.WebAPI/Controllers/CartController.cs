using Marketplace.Application.Shopping;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize(Roles = "CUSTOMER"), Route("api/cart")]
public sealed class CartController(ShoppingService shopping) : ControllerBase
{
    private Guid Buyer => Guid.Parse(User.Identity!.Name!);
    [HttpGet]
    public Task<CartResult> Get(CancellationToken ct) => shopping.ReadCart(Buyer, ct);

    [HttpPost("items")]
    public async Task<IActionResult> Add(AddItemRequest input, CancellationToken ct)
    {
        try { await shopping.Add(Buyer, input, ct); return NoContent(); }
        catch (ShoppingConflict e) { return Conflict(e.Message); }
    }

    [HttpDelete("items/{variantId:guid}")]
    public async Task<IActionResult> Remove(Guid variantId, CancellationToken ct)
    {
        try { await shopping.Remove(Buyer, variantId, ct); return NoContent(); }
        catch (ShoppingConflict e) { return Conflict(e.Message); }
    }
}
