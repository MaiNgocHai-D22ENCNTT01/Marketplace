using System.Security.Cryptography;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Marketplace.Application.Catalog;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

public sealed partial class ProductsController
{
    private sealed record Hit(Guid Id, double SimilarityScore);
    private sealed record Prediction(string? Category, double Confidence, List<Hit> Matches);

    private sealed record CatalogPage(byte[] Body);
    private static readonly JsonSerializerOptions VisualJson = new(JsonSerializerDefaults.Web);

    [EnableRateLimiting("visual")]
    [HttpPost("visual-search"), RequestSizeLimit(VisualFeatures.MaxBytes + 65536)]
    public async Task<ActionResult<VisualResult>> VisualSearch(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return BadRequest("Vui lòng chọn ảnh.");
        if (file.Length > VisualFeatures.MaxBytes) return StatusCode(413);
        var cache = HttpContext.RequestServices.GetRequiredService<VisualCache>();
        using var stream = file.OpenReadStream();
        byte[] hash = await SHA256.HashDataAsync(stream, ct);
        bool cloudSearch = HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("VisualSearch:SearchInApi");
        string key = (cloudSearch ? "cloud-v1:" : "mobilenet-v1:") + Convert.ToHexString(hash);
        if (cache.Entries.TryGetValue("page:" + key, out CatalogPage? cached) && cached != null)
        {
            Response.Headers["X-Visual-Cache"] = "HIT";
            return File(cached.Body, "application/json");
        }
        Prediction? found;
        var stripe = cache.Stripes[hash[0] % cache.Stripes.Length];
        if (!await stripe.WaitAsync(TimeSpan.FromSeconds(4), ct))
            return StatusCode(503, "Hệ thống đang bận. Vui lòng thử lại.");
        string status = "HIT";
        try
        {
            if (!cache.Entries.TryGetValue(key, out found) || found == null)
            {
                using var client = clients.CreateClient("VisualAI");
                using var data = new MultipartFormDataContent();
                data.Add(new StreamContent(file.OpenReadStream()), "file", "image");
                using var response = await client.PostAsync(cloudSearch ? "ai/features" : "ai/visual-search", data, ct);
                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
                found = cloudSearch
                    ? await SearchCloudCatalog(await response.Content.ReadFromJsonAsync<CloudFeatures>(ct), ct)
                    : await response.Content.ReadFromJsonAsync<Prediction>(ct);
                if (found?.Matches is null) return StatusCode(502);
                cache.Entries.Set(key, found, new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10)).SetSize(512 + found.Matches.Count * 64));
                status = "MISS";
            }
            return await Present(cache, key, found, status, ct);
        }
        catch (Exception e) when (Recoverable(e, ct))
        {
            return StatusCode(503, "Dịch vụ tìm kiếm hình ảnh chưa sẵn sàng.");
        }
        finally { stripe.Release(); }
    }

    private async Task<ActionResult<VisualResult>> Present(VisualCache cache, string key, Prediction prediction, string status, CancellationToken ct)
    {
        Response.Headers["X-Visual-Cache"] = status;
        string pageKey = "page:" + key;
        // Search cards are a ten-minute snapshot. Checkout still validates live price/stock.
        if (cache.Entries.TryGetValue(pageKey, out CatalogPage? page) && page != null)
            return File(page.Body, "application/json");
        var products = db.AvailableProducts();
        var ids = prediction.Matches.Select(h => h.Id).ToArray();
        var visible = await products.Where(p => ids.Contains(p.Id)).Summaries().ToDictionaryAsync(p => p.Id, ct);
        var matches = prediction.Matches.Where(h => visible.ContainsKey(h.Id))
            .Select(h => new SimilarProduct(visible[h.Id], h.SimilarityScore)).ToList();
        var result = new VisualResult(prediction.Category, prediction.Confidence, matches, "python");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result, VisualJson);
        cache.Entries.Set(pageKey, new CatalogPage(bytes), new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(10)).SetSize(256 + bytes.Length));
        return File(bytes, "application/json");
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<List<ProductSummary>>> Recommendations(Guid? productId, CancellationToken ct)
    {
        var available = db.AvailableProducts().Where(p => productId == null || p.Id != productId);
        Guid[]? ids = null;
        try
        {
            using var client = clients.CreateClient("AI");
            var path = "ai/recommendations?limit=8";
            if (productId.HasValue) path += "&productId=" + productId;
            if (Guid.TryParse(User.Identity?.Name, out var buyer)) path += "&userId=" + buyer;
            ids = await client.GetFromJsonAsync<Guid[]>(path, ct);
        }
        catch (Exception e) when (Recoverable(e, ct)) { }
        if (ids is null)
        {
            Response.Headers["X-Recommendation-Engine"] = "catalog";
            return await available.OrderByDescending(p => productId != null && db.Products.Any(q => q.Id == productId && q.CategoryId == p.CategoryId))
                .ThenByDescending(p => p.UserInteractionLogs.Count).ThenBy(p => p.Id).Take(8).Summaries().ToListAsync(ct);
        }
        Response.Headers["X-Recommendation-Engine"] = "python";
        var visible = await available.Where(p => ids.Contains(p.Id)).Summaries().ToDictionaryAsync(p => p.Id, ct);
        return ids.Where(visible.ContainsKey).Select(id => visible[id]).ToList();
    }

    private static bool Recoverable(Exception e, CancellationToken ct) =>
        e is HttpRequestException or JsonException || (e is OperationCanceledException && !ct.IsCancellationRequested);
}
