using Marketplace.Application.Shopping;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class AccountController(ShopApi api) : Controller
{
    [HttpGet] public IActionResult Login() => View();
    [HttpGet] public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterRequest input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View();
        try
        {
            await api.Post("api/auth/register", input, ct);
            TempData["Notice"] = "Đăng ký thành công. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login));
        }
        catch (HttpRequestException e) { ModelState.AddModelError("", e.Message); return View(); }
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest input, string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View();
        try
        {
            var result = await api.Login(input, ct);
            api.Session.SetString("jwt", result.Token);
            api.Session.SetString("roles", string.Join(",", result.Roles));
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");
        }
        catch (HttpRequestException e) { ModelState.AddModelError("", e.Message); return View(); }
    }

    [HttpPost]
    public IActionResult Logout()
    {
        api.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
