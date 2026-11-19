using Marketplace.Application.Shopping;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class OrdersController(ShopApi api) : ChannelController(api)
{
    [HttpPost] public Task<IActionResult> Cancel(Guid id,CancellationToken ct) =>
        Submit(HttpMethod.Put,$"api/orders/{id}/cancel",null,"Đã hủy đơn và hoàn tồn kho.",ct);
    [HttpPost] public Task<IActionResult> Confirm(Guid id,CancellationToken ct) =>
        Submit(HttpMethod.Put,$"api/orders/{id}/confirm-delivery",null,"Đã xác nhận nhận hàng.",ct);
    [HttpPost] public Task<IActionResult> Review(ReviewInput input,CancellationToken ct) =>
        Submit(HttpMethod.Post,"api/reviews",input,"Đã gửi đánh giá.",ct);
    [HttpPost] public Task<IActionResult> RegisterShop(ShopInput input,CancellationToken ct) =>
        Submit(HttpMethod.Post,"api/seller/register-shop",input,"Đã gửi gian hàng chờ duyệt.",ct);
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (Api.SignedIn)
        {
            try { return View(await Api.Get<List<PurchasedOrder>>("api/orders/my-orders", ct)); }
            catch (HttpRequestException e)
            {
                TempData["Notice"] = e.Message;
                if (Api.SignedIn) return RedirectToAction("Index", "Home");
            }
        }
        return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index") });
    }
}
