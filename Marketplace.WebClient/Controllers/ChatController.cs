using Marketplace.Application.Shopping;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChatController(ShopApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid? shopId, Guid? productId, string? shopName, CancellationToken ct)
    {
        if (!api.SignedIn)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", new { shopId, productId, shopName }) });
        if (!api.HasRole("CUSTOMER") && !api.HasRole("SELLER")) return StatusCode(403);
        if (shopId.HasValue && !api.HasRole("CUSTOMER"))
        {
            TempData["Notice"] = "Mở cuộc hội thoại của khách hàng trong hộp thư bên dưới.";
            return RedirectToAction(nameof(Index));
        }
        if (productId.HasValue)
        {
            try
            {
                var detail = await api.Get<ProductDetail>($"api/products/{productId}", ct);
                shopId = detail.Shop.Id;
                shopName = detail.Shop.Name;
                ViewData["Product"] = detail.Product;
            }
            catch (HttpRequestException e)
            {
                TempData["Notice"] = e.Message;
                return RedirectToAction(nameof(Index));
            }
        }
        ViewData["ShopId"] = shopId;
        ViewData["ShopName"] = shopName;
        return View();
    }

    [HttpPost]
    public IActionResult Token() => api.Session.GetString("jwt") is string token ? Json(new { token }) : Unauthorized();
}
