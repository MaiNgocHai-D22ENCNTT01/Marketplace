using System.Net;
using Marketplace.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Marketplace.WebClient.Controllers;

public sealed class ShopsController(IHttpClientFactory clients) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, Guid? categoryId, string? keyword, string sort = "newest",
        int page = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || id == Guid.Empty) return NotFound();
        var path = QueryHelpers.AddQueryString($"api/shops/{id}", new Dictionary<string,string?>
        {
            ["categoryId"] = categoryId?.ToString(), ["keyword"] = keyword, ["sort"] = sort, ["page"] = page.ToString()
        });
        using var client = clients.CreateClient("Marketplace");
        using var response = await client.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return NotFound();
        if (response.StatusCode == HttpStatusCode.BadRequest) return BadRequest();
        response.EnsureSuccessStatusCode();
        return View(await response.Content.ReadFromJsonAsync<PublicShop>(ct));
    }
}
