using Marketplace.Application.Shopping;
using Marketplace.WebClient.Models;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class FlashSalesController(ShopApi api) : ChannelController(api)
{
    public Task<IActionResult> Admin(CancellationToken ct) => Page(async () => View(new AdminSalesPage(
        await Api.Get<List<SaleCampaign>>("api/admin/flash-sales",ct),
        await Api.Get<List<SaleItem>>("api/admin/flash-sales/items",ct))));

    public Task<IActionResult> Seller(CancellationToken ct) => Page(async () => View(new SellerSalesPage(
        await Api.Get<List<SaleCampaign>>("api/seller/flash-sales/available",ct),
        await Api.Get<List<ManagedProduct>>("api/seller/products",ct),
        await Api.Get<List<SaleItem>>("api/seller/flash-sales/items",ct))));

    [HttpGet]
    public IActionResult Current() => ViewComponent("FlashSale");

    [HttpPost]
    public Task<IActionResult> Create(string title, DateTime startTime, DateTime endTime, CancellationToken ct)
    {
        static DateTimeOffset VietnamTime(DateTime local) =>
            new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
        if (startTime == default || endTime <= startTime)
        {
            TempData["Notice"] = "Chọn thời gian bắt đầu và kết thúc hợp lệ.";
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Admin)));
        }
        return Save(HttpMethod.Post, "api/admin/flash-sales", new CampaignInput(title,VietnamTime(startTime),VietnamTime(endTime)),
            nameof(Admin),"Đã tạo khung giờ Flash Sale.",ct);
    }

    [HttpPost]
    public Task<IActionResult> Decide(Guid id, string status, CancellationToken ct) => Save(HttpMethod.Put,
        $"api/admin/flash-sales/items/{id}/approve", new StatusInput(status), nameof(Admin), "Đã xử lý đăng ký.",ct);

    [HttpPost]
    public Task<IActionResult> Register(Guid campaignId, SaleRegistration input, CancellationToken ct) => Save(HttpMethod.Post,
        $"api/seller/flash-sales/{campaignId}/register", input, nameof(Seller), "Đã gửi đăng ký chờ duyệt.",ct);

    private async Task<IActionResult> Save(HttpMethod method, string path, object data, string action, string notice, CancellationToken ct)
    {
        if (!ModelState.IsValid) TempData["Notice"] = "Vui lòng kiểm tra thông tin đã nhập.";
        else
        {
            try { await Api.Change(method,path,data,ct); TempData["Notice"] = notice; }
            catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        }
        return RedirectToAction(action);
    }
}
