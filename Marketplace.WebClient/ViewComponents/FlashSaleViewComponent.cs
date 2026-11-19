using System.Net;
using Marketplace.Application.Shopping;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.ViewComponents;

public sealed class FlashSaleViewComponent(IHttpClientFactory clients) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        using var client = clients.CreateClient("Marketplace");
        var ct = HttpContext.RequestAborted;
        try
        {
            using var response = await client.GetAsync("api/flash-sales/current",ct);
            if (!response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent) return Content("");
            return View(await response.Content.ReadFromJsonAsync<SaleWindow>(ct));
        }
        catch (HttpRequestException) { return Content(""); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Content(""); }
    }
}
