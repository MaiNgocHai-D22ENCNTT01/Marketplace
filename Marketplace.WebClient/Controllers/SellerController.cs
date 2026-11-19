using Marketplace.Application.Shopping;
using Marketplace.WebClient.Models;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class SellerController(ShopApi api) : ChannelController(api)
{
    [HttpGet]
    public Task<IActionResult> Reviews(CancellationToken ct, int page = 1) => Page(async () =>
        View(await Api.Get<ReviewPage>($"api/seller/reviews?page={page}", ct)));

    [HttpPost]
    public async Task<IActionResult> Reply(Guid id, ReviewReply input, int page, CancellationToken ct)
    {
        try
        {
            await Api.Change(HttpMethod.Put, $"api/seller/reviews/{id}/reply", input, ct);
            TempData["Notice"] = "Đã lưu phản hồi.";
        }
        catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        return RedirectToAction(nameof(Reviews), new { page });
    }

    public Task<IActionResult> Index(CancellationToken ct) => Page(async () => View(new SellerPage(
        await Api.Get<Sales>("api/seller/analytics",ct), await Api.Get<List<ManagedProduct>>("api/seller/products",ct),
        await Api.Get<List<SellerOrder>>("api/seller/orders",ct))));

    [HttpGet]
    public Task<IActionResult> Edit(Guid? id,CancellationToken ct) => Page(async () =>
    {
        var categories = await Api.Get<List<ShopInfo>>("api/seller/categories",ct);
        var product = id.HasValue ? (await Api.Get<List<ManagedProduct>>("api/seller/products",ct)).Find(p => p.Id == id)?.Data
            : new ProductEdit("",Guid.Empty,null,"/images/demo/tee-mint.png",[new(null,"",1000,0)]);
        return product == null ? NotFound() : View(new ProductForm(id,product,categories));
    });

    [HttpPost]
    public async Task<IActionResult> Save(Guid? id,ProductEdit product,CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await Page(async () => View("Edit",new ProductForm(id,product,
                await Api.Get<List<ShopInfo>>("api/seller/categories",ct))));
        return await Submit(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/seller/products/{id}" : "api/seller/products",product,"Đã lưu sản phẩm.",ct);
    }

    [HttpPost] public Task<IActionResult> Remove(Guid id,CancellationToken ct) =>
        Submit(HttpMethod.Delete,$"api/seller/products/{id}",null,"Đã ngừng hiển thị sản phẩm.",ct);
    [HttpPost] public Task<IActionResult> Advance(Guid id,string status,CancellationToken ct) =>
        Submit(HttpMethod.Put,$"api/seller/orders/{id}/status",new StatusInput(status),"Đã cập nhật đơn.",ct);
}
