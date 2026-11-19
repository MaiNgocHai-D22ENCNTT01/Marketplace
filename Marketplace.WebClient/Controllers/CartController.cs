using Marketplace.Application.Shopping;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class CartController(ShopApi api) : Controller
{
    private IActionResult Login(string? returnUrl) => RedirectToAction("Login", "Account", new { returnUrl });

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Index"));
        try { return View(await api.Get<CartResult>("api/cart", ct)); }
        catch (HttpRequestException e)
        {
            TempData["Notice"] = e.Message;
            return api.SignedIn ? RedirectToAction("Index", "Home") : Login(Url.Action("Index"));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid productId, AddItemRequest input, CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Detail", "Products", new { id = productId }));
        if (ModelState.IsValid)
        {
            try { await api.Post("api/cart/items", input, ct); return RedirectToAction("Index"); }
            catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        }
        else TempData["Notice"] = "Số lượng không hợp lệ.";
        return RedirectToAction("Detail", "Products", new { id = productId });
    }

    [HttpPost]
    public async Task<IActionResult> Remove(Guid variantId, CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Index"));
        try { await api.Remove(variantId, ct); }
        catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(ShippingAddress input, CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Index"));
        if (ModelState.IsValid)
        {
            try
            {
                await api.Post("api/orders/checkout", input, ct);
                TempData["Notice"] = "Đặt hàng thành công.";
                return RedirectToAction("Index", "Orders");
            }
            catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        }
        else TempData["Notice"] = "Vui lòng nhập đầy đủ địa chỉ và số điện thoại hợp lệ.";
        return RedirectToAction("Index");
    }
}
