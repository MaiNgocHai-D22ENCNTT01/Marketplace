using Marketplace.Application.Shopping;
using Marketplace.WebClient.Models;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class AdminController(ShopApi api) : ChannelController(api)
{
    public Task<IActionResult> Reviews(bool hidden, CancellationToken ct, int page = 1) => Page(async () =>
    {
        ViewData["Hidden"] = hidden;
        return View(await Api.Get<ReviewPage>($"api/admin/reviews?hidden={hidden}&page={page}", ct));
    });

    [HttpPost]
    public async Task<IActionResult> ReviewStatus(Guid id, bool hidden, int page, CancellationToken ct)
    {
        try
        {
            await Api.Change(HttpMethod.Put, $"api/admin/reviews/{id}/status", new StatusInput(hidden ? "ACTIVE" : "HIDDEN"), ct);
            TempData["Notice"] = "Đã cập nhật trạng thái đánh giá.";
        }
        catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        return RedirectToAction(nameof(Reviews), new { hidden, page });
    }

    public Task<IActionResult> Index(CancellationToken ct) => Page(async () => View(new AdminPage(
        await Api.Get<Dashboard>("api/admin/dashboard",ct), await Api.Get<List<NamedRow>>("api/admin/shops/pending",ct),
        await Api.Get<List<NamedRow>>("api/admin/users",ct), await Api.Get<List<NamedRow>>("api/admin/products",ct),
        await Api.Get<List<AuditEntry>>("api/admin/audit-logs",ct))));
    [HttpPost] public Task<IActionResult> Approve(Guid id,CancellationToken ct) =>
        Submit(HttpMethod.Put,$"api/admin/shops/{id}/approve",null,"Đã duyệt gian hàng.",ct);
    [HttpPost] public Task<IActionResult> UserStatus(Guid id,string status,CancellationToken ct) =>
        Submit(HttpMethod.Put,$"api/admin/users/{id}/status",new StatusInput(status),"Đã cập nhật tài khoản.",ct);
    [HttpPost] public Task<IActionResult> Hide(Guid id,CancellationToken ct) =>
        Submit(HttpMethod.Put,$"api/admin/products/{id}/status",new StatusInput("HIDDEN"),"Đã ẩn sản phẩm.",ct);
}
