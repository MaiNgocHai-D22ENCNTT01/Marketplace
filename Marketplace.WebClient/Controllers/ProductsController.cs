using System.Net;
using Marketplace.Application.Shopping;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class ProductsController(ShopApi api, IHttpClientFactory clients) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Suggestions(string? q, CancellationToken ct)
    {
        if (q?.Trim().Length is not (>= 2 and <= 256)) return NoContent();
        var rows = await api.Get<List<Marketplace.Application.Catalog.ProductSummary>>(
            "api/products/suggestions?q=" + Uri.EscapeDataString(q), ct);
        return PartialView("_Suggestions", rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Visual(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > 5 * 1024 * 1024)
            ViewData["Error"] = "Chọn ảnh có dung lượng tối đa 5 MiB.";
        else
        {
            try
            {
                using var client = clients.CreateClient("Marketplace");
                if (HttpContext.Connection.RemoteIpAddress is { } ip)
                    client.DefaultRequestHeaders.Add("X-Forwarded-For", ip.ToString());
                if (api.Session.GetString("jwt") is string jwt)
                    client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);
                using var data = new MultipartFormDataContent();
                data.Add(new StreamContent(file.OpenReadStream()), "file", "upload");
                using var response = await client.PostAsync("api/products/visual-search", data, ct);
                if (response.IsSuccessStatusCode)
                    return View(await response.Content.ReadFromJsonAsync<Marketplace.Application.Catalog.VisualResult>(ct));
                if ((int)response.StatusCode == 429) Response.StatusCode = 429;
                ViewData["Error"] = (int)response.StatusCode == 429
                    ? "Bạn tìm kiếm quá nhanh. Vui lòng chờ một phút rồi thử lại."
                    : response.StatusCode == HttpStatusCode.BadRequest
                    ? "Ảnh không hợp lệ hoặc không đủ chi tiết. Hãy chọn ảnh JPG, PNG hoặc WEBP khác."
                    : "Chưa thể tìm sản phẩm bằng ảnh này.";
            }
            catch (HttpRequestException) { ViewData["Error"] = "Chưa kết nối được WebAPI."; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { ViewData["Error"] = "Quá thời gian chờ."; }
        }
        return View();
    }

    public async Task<IActionResult> Detail(Guid id, bool clicked, int? rating, bool withImages, bool withComment, CancellationToken ct, int page = 1)
    {
        if (rating is < 1 or > 5 || page is < 1 or > 1000000) return BadRequest();
        try
        {
            var detail = await api.Get<ProductDetail>($"api/products/{id}?clicked={clicked}", ct);
            ViewData["Reviews"] = await api.Get<ProductReviews>(
                $"api/products/{id}/reviews?page={page}&withImages={withImages}&withComment={withComment}&rating={rating}", ct);
            ViewData["WithComment"] = withComment;
            ViewData["Rating"] = rating;
            ViewData["WithImages"] = withImages;
            ViewData["ProductId"] = id;
            return View(detail);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound) { return NotFound(); }
    }
}
