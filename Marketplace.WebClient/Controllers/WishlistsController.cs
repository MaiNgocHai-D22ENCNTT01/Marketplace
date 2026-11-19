using System.Net;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class WishlistsController(ShopApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action(nameof(Index)));
        try { return View(await api.Wishlist(ct)); }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        { return Login(Url.Action(nameof(Index))); }
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(Guid id, string? returnUrl, CancellationToken ct)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!;
        var json = Request.Headers.Accept.ToString() == "application/json";
        if (!api.SignedIn) return json ? Unauthorized() : Login(destination);
        try
        {
            var status = await api.ToggleWishlist(id, ct);
            if (json) return Json(status);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        { return json ? Unauthorized() : Login(destination); }
        catch (HttpRequestException e) when (json) { return StatusCode((int?)e.StatusCode ?? 503); }
        catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        return LocalRedirect(destination);
    }

    private IActionResult Login(string? returnUrl) => RedirectToAction("Login", "Account", new { returnUrl });
}
