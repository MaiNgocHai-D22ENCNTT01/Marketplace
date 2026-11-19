using Marketplace.Application.Catalog;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.ViewComponents;

public sealed class RecommendationsViewComponent(ShopApi api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid? productId)
    {
        var path = "api/products/recommendations" + (productId.HasValue ? "?productId=" + productId : "");
        try { return View(await api.Get<List<ProductSummary>>(path, HttpContext.RequestAborted)); }
        catch (HttpRequestException) { return Content("Chưa tải được gợi ý."); }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        { return Content("Chưa tải được gợi ý."); }
    }
}
