using Marketplace.Application.Accounts;
using Marketplace.WebClient.Models;
using Marketplace.Application.Shopping;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class AccountController(ShopApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Profile(string tab = "profile", CancellationToken ct = default)
    {
        if (!api.SignedIn) return RedirectToAction(nameof(Login), new { returnUrl = "/Account/Profile" });
        try
        {
            return View(new AccountProfileModel(await api.Get<ProfileResult>("api/account/profile", ct),
                await api.Get<List<AddressResult>>("api/account/addresses", ct),
                tab is "addresses" or "password" ? tab : "profile"));
        }
        catch (HttpRequestException e)
        {
            TempData["Notice"] = e.Message;
            return api.SignedIn ? RedirectToAction("Index", "Home") : RedirectToAction(nameof(Login));
        }
    }

    [HttpPost]
    public Task<IActionResult> UpdateProfile(UpdateProfileRequest input, CancellationToken ct) =>
        Save("profile", () => api.Change(HttpMethod.Put, "api/account/profile", input, ct));

    [HttpPost]
    public Task<IActionResult> ChangePassword(PasswordForm input, CancellationToken ct) =>
        Save("password", () => api.Post("api/account/change-password",
            new ChangePasswordRequest(input.CurrentPassword, input.NewPassword), ct));

    [HttpPost]
    public Task<IActionResult> AddAddress(AddAddressRequest input, CancellationToken ct) =>
        Save("addresses", () => api.Post("api/account/addresses", input, ct));

    [HttpPost]
    public Task<IActionResult> SetDefault(Guid id, CancellationToken ct) =>
        Save("addresses", () => api.Change(HttpMethod.Put, $"api/account/addresses/{id}/default", null, ct));

    [HttpPost]
    public Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct) =>
        Save("addresses", () => api.Change(HttpMethod.Delete, $"api/account/addresses/{id}", null, ct));

    private async Task<IActionResult> Save(string tab, Func<Task> change)
    {
        if (!api.SignedIn) return RedirectToAction(nameof(Login), new { returnUrl = "/Account/Profile" });
        if (!ModelState.IsValid)
            TempData["AccountError"] = "Vui lòng kiểm tra các trường bắt buộc, độ dài, số điện thoại và xác nhận mật khẩu.";
        else
        {
            try { await change(); TempData["AccountSuccess"] = "Đã lưu thay đổi thành công."; }
            catch (HttpRequestException e) { TempData["AccountError"] = e.Message; }
        }
        return RedirectToAction(nameof(Profile), new { tab });
    }

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
