using System.Diagnostics;
using Marketplace.Application.Catalog;
using Marketplace.WebClient.Models;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public class HomeController(IHttpClientFactory clients) : Controller
{
    public async Task<IActionResult> Index(string? keyword, Guid? categoryId, string? sort, CancellationToken cancellationToken)
    {
        try
        {
            using var client = clients.CreateClient("Marketplace");
            var path = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("api/products",
                new Dictionary<string, string?> { ["keyword"] = keyword, ["categoryId"] = categoryId?.ToString(), ["sort"] = sort });
            var items = await client.GetFromJsonAsync<List<ProductSummary>>(path, cancellationToken) ?? [];
            ViewBag.ApiBaseUrl = client.BaseAddress;
            return View(items);
        }
        catch (HttpRequestException)
        {
            ViewBag.ApiReady = false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ViewBag.ApiReady = false;
        }
        return View(Array.Empty<ProductSummary>());
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}

