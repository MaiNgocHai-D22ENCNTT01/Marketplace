# Mã nguồn đầy đủ — Quản lý tài khoản và sổ địa chỉ

Các đường dẫn tương đối với thư mục chứa Marketplace.sln. Nội dung dưới đây là toàn bộ từng file, có thể đối chiếu hoặc chép vào solution. Xem ACCOUNT-PROFILE.md để biết cách chạy và các lựa chọn tương thích schema.

## Marketplace.Application/Accounts/Contracts.cs

```csharp
using System.ComponentModel.DataAnnotations;
namespace Marketplace.Application.Accounts;

public record ProfileResult(Guid Id, string FullName, string Email, string? PhoneNumber,
    string? AvatarUrl, string[] Roles, DateTime CreatedAt);
public record UpdateProfileRequest(
    [param: Required, StringLength(255)] string FullName,
    [param: Phone, StringLength(20)] string? PhoneNumber);
public record ChangePasswordRequest(
    [param: Required, StringLength(128)] string CurrentPassword,
    [param: Required, StringLength(128, MinimumLength = 8)] string NewPassword);
public record AddAddressRequest(
    [param: Required, StringLength(150)] string RecipientName,
    [param: Required, Phone, StringLength(20)] string Phone,
    [param: Required, StringLength(100)] string Province,
    [param: Required, StringLength(100)] string District,
    [param: Required, StringLength(100)] string Ward,
    [param: Required, StringLength(190)] string DetailAddress,
    bool IsDefault);
public record AddressResult(Guid Id, string RecipientName, string Phone, string Province,
    string District, string Ward, string DetailAddress, bool IsDefault)
{
    public string FullAddress => string.Join(", ", DetailAddress, Ward, District, Province);
}
```

## Marketplace.WebAPI/Controllers/AccountController.cs

```csharp
using Marketplace.Application.Accounts;
using Marketplace.Domain.Entities;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize, Route("api/account")]
public sealed class AccountController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.Identity!.Name!);

    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResult>> Profile(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .SingleAsync(u => u.Id == UserId, ct);
        return new ProfileResult(user.Id, user.FullName, user.Email, user.Phone, null,
            user.UserRoles.Select(r => r.Role.Name.ToString()).ToArray(), user.CreatedAt);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest input, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == UserId, ct);
        user.FullName = input.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockedUser(ct);
        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
            return Conflict("Mật khẩu hiện tại không đúng.");
        if (input.CurrentPassword == input.NewPassword)
            return Conflict("Mật khẩu mới phải khác mật khẩu hiện tại.");
        user.PasswordHash = hasher.HashPassword(user, input.NewPassword);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("addresses")]
    public async Task<List<AddressResult>> Addresses(CancellationToken ct) =>
        await db.Addresses.AsNoTracking().Where(a => a.UserId == UserId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .Select(a => new AddressResult(a.Id, a.RecipientName, a.Phone, a.Province,
                a.District, a.Ward, a.DetailAddress, a.IsDefault)).ToListAsync(ct);

    // Lock the owner row so all address mutations for this user serialize, even for an empty book.
    private Task<User> LockedUser(CancellationToken ct) => db.Users
        .FromSqlInterpolated($"SELECT * FROM users WHERE id = {UserId} FOR UPDATE").SingleAsync(ct);

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress(AddAddressRequest input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockedUser(ct);
        var existing = await db.Addresses.Where(a => a.UserId == UserId).ToListAsync(ct);
        var makeDefault = input.IsDefault || existing.Count == 0;
        if (makeDefault) foreach (var item in existing) item.IsDefault = false;
        var address = new Address { UserId = UserId, RecipientName = input.RecipientName.Trim(),
            Phone = input.Phone.Trim(), Province = input.Province.Trim(), District = input.District.Trim(),
            Ward = input.Ward.Trim(), DetailAddress = input.DetailAddress.Trim(), IsDefault = makeDefault };
        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new AddressResult(address.Id, address.RecipientName, address.Phone,
            address.Province, address.District, address.Ward, address.DetailAddress, address.IsDefault));
    }

    [HttpPut("addresses/{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockedUser(ct);
        var addresses = await db.Addresses.Where(a => a.UserId == UserId).ToListAsync(ct);
        if (!addresses.Any(a => a.Id == id)) return NotFound();
        foreach (var address in addresses) address.IsDefault = address.Id == id;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpDelete("addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockedUser(ct);
        var addresses = await db.Addresses.Where(a => a.UserId == UserId).OrderBy(a => a.Id).ToListAsync(ct);
        var address = addresses.FirstOrDefault(a => a.Id == id);
        if (address is null) return NotFound();
        db.Addresses.Remove(address);
        if (address.IsDefault && addresses.FirstOrDefault(a => a.Id != id) is { } next) next.IsDefault = true;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }
}
```

## Marketplace.WebClient/Models/AccountProfileModel.cs

```csharp
using Marketplace.Application.Accounts;
using System.ComponentModel.DataAnnotations;
namespace Marketplace.WebClient.Models;
public sealed record AccountProfileModel(ProfileResult Profile, List<AddressResult> Addresses, string Tab);
public sealed class PasswordForm
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
    public string ConfirmPassword { get; set; } = "";
}
```

## Marketplace.WebClient/Controllers/AccountController.cs

```csharp
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
```

## Marketplace.WebClient/Views/Account/Profile.cshtml

```cshtml
@model AccountProfileModel
@{ ViewData["Title"] = "Tài khoản của tôi"; }
<link rel="stylesheet" href="~/css/account.css" asp-append-version="true" />
<div class="account-page">
<aside class="panel account-sidebar">
<div class="account-identity"><span class="account-avatar">@(string.IsNullOrWhiteSpace(Model.Profile.FullName) ? "?" : System.Globalization.StringInfo.GetNextTextElement(Model.Profile.FullName.Trim()).ToUpperInvariant())</span><div><small>Tài khoản của</small><strong>@Model.Profile.FullName</strong></div></div>
<nav aria-label="Quản lý tài khoản">
<a asp-action="Profile" asp-route-tab="profile" class="@(Model.Tab == "profile" ? "active" : "")"><i class="bi bi-person"></i> Hồ sơ của tôi</a>
<a asp-action="Profile" asp-route-tab="addresses" class="@(Model.Tab == "addresses" ? "active" : "")"><i class="bi bi-geo-alt"></i> Sổ địa chỉ nhận hàng</a>
<a asp-action="Profile" asp-route-tab="password" class="@(Model.Tab == "password" ? "active" : "")"><i class="bi bi-shield-lock"></i> Đổi mật khẩu</a>
<a asp-controller="Orders" asp-action="Index"><i class="bi bi-bag"></i> Đơn mua</a>
<a asp-controller="Wishlists" asp-action="Index"><i class="bi bi-heart"></i> Sản phẩm yêu thích</a>
</nav></aside>
<section class="panel account-content">
@if (TempData["AccountError"] is string error) { <div class="alert alert-danger" role="alert">@error</div> }
@if (TempData["AccountSuccess"] is string success) { <div class="alert alert-success" role="status">@success</div> }
@if (Model.Tab == "profile") {
<h1>Hồ sơ của tôi</h1><p class="text-muted">Quản lý thông tin cá nhân của bạn.</p>
<form asp-action="UpdateProfile" method="post" class="form-stack account-form">
<label>Họ tên<input class="form-control" name="FullName" value="@Model.Profile.FullName" required maxlength="255" autocomplete="name" /></label>
<label>Email<input class="form-control" value="@Model.Profile.Email" readonly type="email" /></label>
<label>Số điện thoại<input class="form-control" name="PhoneNumber" value="@Model.Profile.PhoneNumber" type="tel" maxlength="20" autocomplete="tel" /></label>
<div>Vai trò <div class="mt-2">@foreach (var role in Model.Profile.Roles) { <span class="badge text-bg-light me-1">@role</span> }</div></div>
<p class="text-muted">Ngày tham gia: @Model.Profile.CreatedAt.ToString("dd/MM/yyyy")</p>
<button class="btn btn-primary">Lưu thay đổi</button></form>
}
else if (Model.Tab == "addresses") {
<div class="account-heading"><h1>Sổ địa chỉ nhận hàng</h1><button type="button" id="add-address" class="btn btn-primary"><i class="bi bi-plus-lg"></i> Thêm địa chỉ</button></div>
@if (Model.Addresses.Count == 0) { <p class="empty">Bạn chưa có địa chỉ nhận hàng. Hãy thêm địa chỉ đầu tiên.</p> }
@foreach (var address in Model.Addresses) {
<article class="address-card"><div class="account-heading"><strong>@address.RecipientName</strong>@if (address.IsDefault) { <span class="badge text-bg-success">Mặc định</span> }</div>
<p>@address.Phone</p><p>@address.FullAddress</p><div class="address-actions">
@if (!address.IsDefault) { <form asp-action="SetDefault" method="post"><input type="hidden" name="id" value="@address.Id" /><button class="btn btn-outline-primary btn-sm">Đặt làm mặc định</button></form> }
<form asp-action="DeleteAddress" method="post" data-delete-address><input type="hidden" name="id" value="@address.Id" /><button class="btn btn-outline-danger btn-sm">Xóa</button></form>
</div></article>
}
<dialog id="address-dialog" aria-labelledby="address-title"><div class="account-heading"><h2 id="address-title">Thêm địa chỉ nhận hàng</h2><button type="button" id="close-address" class="btn btn-light" aria-label="Đóng">×</button></div>
<form asp-action="AddAddress" method="post" class="form-stack">
<label>Họ tên người nhận<input class="form-control" name="RecipientName" required maxlength="150" autocomplete="name" /></label>
<label>Số điện thoại<input class="form-control" name="Phone" type="tel" required maxlength="20" autocomplete="tel" /></label>
<label>Tỉnh/Thành phố<input class="form-control" name="Province" required maxlength="100" /></label>
<label>Quận/Huyện<input class="form-control" name="District" required maxlength="100" /></label>
<label>Phường/Xã<input class="form-control" name="Ward" required maxlength="100" /></label>
<label>Địa chỉ chi tiết<textarea class="form-control" name="DetailAddress" required maxlength="190" autocomplete="street-address"></textarea></label>
<label><input type="checkbox" name="IsDefault" value="true" /> Đặt làm địa chỉ mặc định</label>
<button class="btn btn-primary">Lưu địa chỉ</button></form></dialog>
}
else {
<h1>Đổi mật khẩu</h1><p class="text-muted">Mật khẩu mới từ 8 đến 128 ký tự.</p>
<form asp-action="ChangePassword" method="post" class="form-stack account-form" id="password-form">
<label>Mật khẩu hiện tại<input class="form-control" name="CurrentPassword" type="password" required maxlength="128" autocomplete="current-password" /></label>
<label>Mật khẩu mới<input class="form-control" name="NewPassword" type="password" required minlength="8" maxlength="128" autocomplete="new-password" /></label>
<label>Xác nhận mật khẩu mới<input class="form-control" name="ConfirmPassword" type="password" required minlength="8" maxlength="128" autocomplete="new-password" /></label>
<button class="btn btn-primary">Cập nhật mật khẩu</button></form>
}
</section></div>
@section Scripts { <script src="~/js/account.js" asp-append-version="true" defer></script> }
```

## Marketplace.WebClient/wwwroot/css/account.css

```css
body{background:#F5F5FA}.account-page{display:grid;grid-template-columns:270px minmax(0,1fr);gap:24px;align-items:start}.account-sidebar,.account-content{background:#fff;border-radius:18px;padding:24px}.account-identity{display:flex;align-items:center;gap:12px;padding-bottom:22px;overflow-wrap:anywhere}.account-identity strong,.account-identity small{display:block}.account-avatar{width:48px;height:48px;flex-shrink:0;border-radius:50%;display:grid;place-items:center;background:#e9e8ff;color:#5546d3;font-size:22px;font-weight:700}.account-sidebar nav{display:grid;gap:8px}.account-sidebar nav a{padding:12px;border-radius:10px;color:#555;text-decoration:none;transition:background .18s,color .18s}.account-sidebar nav a.active,.account-sidebar nav a:hover{background:#eeecff;color:#5546d3}.account-sidebar i{margin-right:8px}.account-content{min-height:460px;animation:account-in .2s ease-out}.account-content h1{font-size:24px}.account-form{max-width:540px}.account-heading,.address-actions{display:flex;align-items:center;gap:12px;justify-content:space-between;flex-wrap:wrap}.address-card{border:1px solid #e5e5ef;border-radius:12px;padding:20px;margin-top:18px;overflow-wrap:anywhere}.address-card p{margin:10px 0}.address-actions{justify-content:flex-start}#address-dialog{width:min(560px,calc(100% - 24px));max-height:90vh;border:0;border-radius:18px;padding:24px}#address-dialog::backdrop{background:#15152d80}@keyframes account-in{from{opacity:.5;transform:translateY(4px)}to{opacity:1;transform:none}}@media(max-width:760px){.account-page{grid-template-columns:1fr}.account-sidebar,.account-content{padding:18px}.account-content{min-height:0}}@media(prefers-reduced-motion:reduce){.account-content{animation:none}.account-sidebar nav a{transition:none}}
```

## Marketplace.WebClient/wwwroot/js/account.js

```javascript
(() => {
    const dialog = document.getElementById('address-dialog');
    document.getElementById('add-address')?.addEventListener('click', () => dialog.showModal());
    document.getElementById('close-address')?.addEventListener('click', () => dialog.close());
    document.querySelectorAll('[data-delete-address]').forEach(form => form.addEventListener('submit', event => {
        if (!confirm('Xóa địa chỉ nhận hàng này?')) event.preventDefault();
    }));
    const form = document.getElementById('password-form');
    if (form) {
        const check = () => form.elements.ConfirmPassword.setCustomValidity(
            form.elements.NewPassword.value === form.elements.ConfirmPassword.value ? '' : 'Xác nhận mật khẩu mới không khớp.');
        form.elements.NewPassword.addEventListener('input', check);
        form.elements.ConfirmPassword.addEventListener('input', check);
    }
})();
```

## Marketplace.WebClient/Controllers/CartController.cs

```csharp
using Marketplace.Application.Shopping;
using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public sealed class CartController(ShopApi api) : Controller
{
    private IActionResult Login(string? returnUrl) => RedirectToAction("Login", "Account", new { returnUrl });

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Index"));
        try
        {
            var cart = await api.Get<CartResult>("api/cart", ct);
            try { ViewData["Addresses"] = await api.Get<List<Marketplace.Application.Accounts.AddressResult>>("api/account/addresses", ct); }
            catch (HttpRequestException) { ViewData["AddressWarning"] = "Chưa tải được sổ địa chỉ. Bạn vẫn có thể nhập địa chỉ giao hàng bên dưới."; }
            return View(cart);
        }
        catch (HttpRequestException e)
        {
            TempData["Notice"] = e.Message;
            return api.SignedIn ? RedirectToAction("Index", "Home") : Login(Url.Action("Index"));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid productId, AddItemRequest input, CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Detail", "Products", new { id = productId }));
        if (ModelState.IsValid)
        {
            try { await api.Post("api/cart/items", input, ct); return RedirectToAction("Index"); }
            catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        }
        else TempData["Notice"] = "Số lượng không hợp lệ.";
        return RedirectToAction("Detail", "Products", new { id = productId });
    }

    [HttpPost]
    public async Task<IActionResult> Remove(Guid variantId, CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Index"));
        try { await api.Remove(variantId, ct); }
        catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(ShippingAddress input, CancellationToken ct)
    {
        if (!api.SignedIn) return Login(Url.Action("Index"));
        if (ModelState.IsValid)
        {
            try
            {
                await api.Post("api/orders/checkout", input, ct);
                TempData["Notice"] = "Đặt hàng thành công.";
                return RedirectToAction("Index", "Orders");
            }
            catch (HttpRequestException e) { TempData["Notice"] = e.Message; }
        }
        else TempData["Notice"] = "Vui lòng nhập đầy đủ địa chỉ và số điện thoại hợp lệ.";
        return RedirectToAction("Index");
    }
}
```

## Marketplace.WebClient/Views/Cart/Index.cshtml

```cshtml
@model CartResult
@{ ViewData["Title"] = "Giỏ hàng";
    var addresses = ViewData["Addresses"] as List<Marketplace.Application.Accounts.AddressResult> ?? new();
    var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault);
}
<h1>Giỏ hàng</h1>
@if (Model.Quantity == 0) { <div class="empty"><i class="bi bi-bag feature-icon"></i><h2>Giỏ hàng đang trống</h2><a class="btn btn-primary" asp-controller="Home" asp-action="Index">Khám phá sản phẩm</a></div> }
else {
<div class="checkout"><div>
@foreach (var group in Model.Shops) {
<section class="panel"><h2><i class="bi bi-shop"></i> @group.Shop.Name</h2>
@foreach (var item in group.Items) {
<div class="cart-line"><div><strong>@item.ProductName</strong><small>@item.Variant · @item.Sku</small></div><span>@item.Price.ToString("N0") ₫ × @item.Quantity</span>
<form asp-action="Remove" method="post"><input type="hidden" name="variantId" value="@item.VariantId" /><button aria-label="Xóa @item.ProductName" class="btn btn-outline-secondary btn-sm"><i class="bi bi-trash"></i></button></form></div>
}<p class="subtotal">Tạm tính <strong>@group.Total.ToString("N0") ₫</strong></p></section> }
<section class="panel"><h2>Thông tin giao hàng</h2><form id="delivery" asp-action="Checkout" method="post" class="form-stack">
@if (ViewData["AddressWarning"] is string warning) { <p role="status">@warning</p> }
@if (addresses.Count > 0) {
<label>Chọn địa chỉ đã lưu<select id="saved-address" class="form-select"><option value="">Nhập địa chỉ khác</option>
@foreach (var address in addresses) { <option value="@address.Id" selected="@(address.Id == defaultAddress?.Id)" data-name="@address.RecipientName" data-phone="@address.Phone" data-address="@address.FullAddress">@address.RecipientName — @address.FullAddress @(address.IsDefault ? "(Mặc định)" : "")</option> }
</select></label>
}
<a asp-controller="Account" asp-action="Profile" asp-route-tab="addresses">Quản lý sổ địa chỉ</a>
<label>Người nhận<input class="form-control" name="RecipientName" value="@defaultAddress?.RecipientName" autocomplete="name" required maxlength="150" /></label>
<label>Điện thoại<input class="form-control" type="tel" name="Phone" value="@defaultAddress?.Phone" autocomplete="tel" required maxlength="20" /></label>
<label>Địa chỉ đầy đủ<textarea class="form-control" name="DetailAddress" autocomplete="street-address" required maxlength="500">@defaultAddress?.FullAddress</textarea></label></form></section></div>
<aside class="panel summary"><h2>Tóm tắt đơn hàng</h2><p>@Model.Quantity sản phẩm · @Model.Shops.Count gian hàng</p><div class="subtotal">Tổng cộng <strong class="price">@Model.Total.ToString("N0") ₫</strong></div><button form="delivery" class="btn btn-primary w-100">Đặt hàng <i class="bi bi-arrow-right"></i></button></aside></div>
}

@section Scripts { <script src="~/js/checkout-address.js" asp-append-version="true" defer></script> }
```

## Marketplace.WebClient/wwwroot/js/checkout-address.js

```javascript
document.getElementById('saved-address')?.addEventListener('change', event => {
    const option = event.target.selectedOptions[0];
    const form = document.getElementById('delivery');
    form.elements.RecipientName.value = option.dataset.name || '';
    form.elements.Phone.value = option.dataset.phone || '';
    form.elements.DetailAddress.value = option.dataset.address || '';
});
```

## Marketplace.WebClient/Views/Shared/_Layout.cshtml

```cshtml
@inject Marketplace.WebClient.Services.ShopApi Api
@{
    long? count = 0;
    if (Api.HasRole("CUSTOMER"))
    {
        try { count = (await Api.Get<CartResult>("api/cart", Context.RequestAborted)).Quantity; }
        catch (HttpRequestException) { count = null; }
    }
}
<!DOCTYPE html>
<html lang="vi"><head>
<meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" />
<title>@ViewData["Title"] · Marketplace</title>
<link rel="icon" href="~/brand.svg" type="image/svg+xml" />
<link rel="stylesheet" href="~/lib/bootstrap-5.3.3/bootstrap.min.css" />
<link rel="stylesheet" href="~/lib/bootstrap-icons/bootstrap-icons.min.css" />
<link rel="preload" href="~/lib/inter/Inter.ttf" as="font" type="font/ttf" crossorigin />
<link rel="stylesheet" href="~/css/marketplace-modern.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/reviews.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/wishlist-search.css" asp-append-version="true" />
</head><body>
<a class="visually-hidden-focusable" href="#main">Đến nội dung chính</a>
<header class="sticky-top mast"><nav class="container mast-inner" aria-label="Điều hướng chính">
<a class="brand" asp-controller="Home" asp-action="Index"><img src="~/brand.svg" alt="" width="35" height="35"/>Marketplace</a>
<form class="search" asp-controller="Home" asp-action="Index" method="get">
<button aria-label="Tìm kiếm" class="bare"><i class="bi bi-search"></i></button>
<input id="header-keyword" autocomplete="off" role="combobox" aria-haspopup="dialog" aria-expanded="false" aria-controls="search-suggestions" type="search" name="keyword" aria-label="Từ khóa" placeholder="Tìm sản phẩm bạn yêu thích…" value="@Context.Request.Query["keyword"]" />
<button type="button" id="visual-open" data-visual class="camera" aria-label="Tìm kiếm bằng hình ảnh"><i class="bi bi-camera"></i> AI</button>
<div id="search-suggestions" role="dialog" aria-label="Gợi ý sản phẩm" data-url="@Url.Action("Suggestions", "Products")" hidden></div></form>
<div class="account-nav">
@if (!Api.SignedIn || Api.HasRole("CUSTOMER")) {
<a class="basket" asp-controller="Cart" asp-action="Index" aria-label="Giỏ hàng, @(count?.ToString() ?? "chưa rõ số lượng") sản phẩm"><i class="bi bi-bag"></i><b>@(count?.ToString() ?? "–")</b></a>
}
<details class="user-menu"><summary><i class="bi bi-person-circle"></i><span>Tài khoản</span><i class="bi bi-chevron-down small"></i></summary>
<div class="menu-list">
<a asp-controller="Account" asp-action="Profile">Tài khoản của tôi</a>
<a asp-controller="Wishlists" asp-action="Index">Danh sách yêu thích</a>
@if (Api.HasRole("CUSTOMER") || Api.HasRole("SELLER")) { <a asp-controller="Chat" asp-action="Index">Tin nhắn</a> }
@if (!Api.SignedIn || Api.HasRole("CUSTOMER")) { <a asp-controller="Orders" asp-action="Index">Đơn mua</a> }
@if (Api.HasRole("SELLER")) { <a asp-controller="Seller" asp-action="Index">Kênh người bán</a> }
@if (Api.HasRole("ADMIN") || Api.HasRole("SUPER_ADMIN")) { <a asp-controller="Admin" asp-action="Index">Quản trị</a> }
@if (Api.SignedIn) { <form asp-controller="Account" asp-action="Logout" method="post"><button>Đăng xuất</button></form> }
else { <a asp-controller="Account" asp-action="Login">Đăng nhập</a><a asp-controller="Account" asp-action="Register">Đăng ký</a> }
</div></details></div></nav></header>
<main id="main" class="container page">
@if (TempData["Notice"] is string notice) { <div class="alert alert-info" role="status">@notice</div> }
@RenderBody()</main>
<footer><div class="container foot-grid"><div><a class="brand" asp-controller="Home" asp-action="Index"><img src="~/brand.svg" alt="" width="32" height="32"/>Marketplace</a><p>Sàn mua sắm từ các gian hàng độc lập.</p></div>
<div><h2>Hỗ trợ mua sắm</h2><a asp-controller="Orders" asp-action="Index">Theo dõi đơn mua</a><a asp-controller="Home" asp-action="Privacy">Chính sách &amp; hướng dẫn</a></div>
<div><h2>Công nghệ AI</h2><button class="bare" data-visual>Tìm kiếm bằng hình ảnh</button><p>Khám phá sản phẩm tương tự và gợi ý cá nhân hóa.</p></div></div></footer>
<dialog id="visual-dialog" aria-labelledby="visual-title"><button type="button" class="close-dialog" data-close aria-label="Đóng">×</button>
<i class="bi bi-camera feature-icon"></i><h2 id="visual-title">Tìm bằng hình ảnh</h2>
<form asp-controller="Products" asp-action="Visual" method="post" enctype="multipart/form-data">
<label class="drop-zone"><i class="bi bi-cloud-arrow-up"></i><strong>Kéo thả hoặc chọn ảnh</strong><small>JPG, PNG, WEBP · Tối đa 10 MiB</small>
<input type="file" accept="image/jpeg,image/png,image/webp" name="file" required /></label>
<img class="preview" hidden alt="Ảnh xem trước" /><p class="upload-error text-danger" role="alert"></p>
<div hidden class="analysis-state" role="status">Đang phân tích ảnh…<progress aria-label="Đang phân tích"></progress></div>
<button class="btn btn-primary w-100"><i class="bi bi-stars"></i> Tìm sản phẩm tương tự</button>
</form></dialog>
<dialog id="quick-dialog" aria-labelledby="quick-title"><button class="close-dialog" data-close aria-label="Đóng">×</button><img hidden class="quick-image" alt=""/><small class="quick-category"></small><h2 id="quick-title"></h2><p class="price"></p><a class="btn btn-primary">Xem chi tiết &amp; gian hàng</a></dialog>
<script defer src="~/js/visual-search.js" asp-append-version="true"></script>
<script defer src="~/js/wishlist.js" asp-append-version="true"></script>
<script defer src="~/js/suggestions.js" asp-append-version="true"></script>
@await RenderSectionAsync("Scripts", required: false)
</body></html>
```

## tests/test_account.py

```python
"""Account integration checks; run via run_postgres.py on an isolated database."""
import html
import re
import os
from concurrent.futures import ThreadPoolExecutor
from http.cookiejar import CookieJar
from urllib.parse import urlencode
from urllib.request import build_opener, HTTPCookieProcessor
from urllib.error import HTTPError
from test_step3 import call, register, login, PASSWORD

owner = register("profile-owner")
other = register("profile-other")
call("GET", "account/profile", expect=401)
for method, path, body in [("PUT", "account/profile", {"fullName":"Guest"}),
    ("POST", "account/change-password", {"currentPassword":PASSWORD,"newPassword":"Changed123!"}),
    ("GET", "account/addresses", None), ("POST", "account/addresses", {})]:
    call(method, path, data=body, expect=401)
for role in ("buyer", "seller", "admin"):
    assert call("GET", "account/profile", login(role)["token"])["roles"]
p = call("GET", "account/profile", owner)
assert p["email"] == "profile-owner@example.invalid" and p["roles"] == ["CUSTOMER"]
assert "passwordHash" not in p and "avatarUrl" in p and p["createdAt"]
call("PUT", "account/profile", owner, {"fullName":" Nguyễn An ","phoneNumber":"0901234567"}, 204)
p = call("GET", "account/profile", owner)
assert p["fullName"] == "Nguyễn An" and p["phoneNumber"] == "0901234567"
call("PUT", "account/profile", owner, {"fullName":"   "}, 400)
call("PUT", "account/profile", owner, {"fullName":"An","phoneNumber":"invalid"}, 400)
address = dict(recipientName="Nguyễn An", phone="0901234567", province="Hà Nội",
               district="Cầu Giấy", ward="Dịch Vọng", detailAddress="12 Đường A", isDefault=False)
call("POST", "account/addresses", owner, dict(address, province=""), 400)
a = call("POST", "account/addresses", owner, address, 201)
assert a["isDefault"]
b = call("POST", "account/addresses", owner, dict(address, detailAddress="34 Đường B"), 201)
assert not b["isDefault"]
for method, suffix in [("PUT", "/default"), ("DELETE", "")]:
    call(method, "account/addresses/" + a["id"] + suffix, other, expect=404)
assert call("GET", "account/addresses", other) == []
with ThreadPoolExecutor(max_workers=8) as pool:
    list(pool.map(lambda i: call("POST", "account/addresses", owner,
        dict(address, detailAddress=str(i), isDefault=True), 201), range(8)))
assert sum(x["isDefault"] for x in call("GET", "account/addresses", owner)) == 1
with ThreadPoolExecutor(max_workers=8) as pool:
    list(pool.map(lambda i: call("PUT", "account/addresses/" + (a if i % 2 else b)["id"] + "/default",
        owner, expect=204), range(12)))
assert sum(x["isDefault"] for x in call("GET", "account/addresses", owner)) == 1
call("PUT", "account/addresses/" + a["id"] + "/default", owner, expect=204)
call("DELETE", "account/addresses/" + a["id"], owner, expect=204)
assert sum(x["isDefault"] for x in call("GET", "account/addresses", owner)) == 1
call("POST", "account/change-password", owner, {"currentPassword":"wrong","newPassword":"Changed123!"}, 409)
call("POST", "account/change-password", owner, {"currentPassword":PASSWORD,"newPassword":"short"}, 400)
call("POST", "account/change-password", owner, {"currentPassword":PASSWORD,"newPassword":PASSWORD}, 409)
call("POST", "account/change-password", owner, {"currentPassword":PASSWORD,"newPassword":"Changed123!"}, 204)
call("POST", "auth/login", data={"email":p["email"],"password":PASSWORD}, expect=401)
owner = call("POST", "auth/login", data={"email":p["email"],"password":"Changed123!"})["token"]

base = os.environ["MARKETPLACE_TEST_WEB"]
browser = build_opener(HTTPCookieProcessor(CookieJar()))
def page(path, data=None):
    with browser.open(base + path, None if data is None else urlencode(data).encode(), timeout=20) as response:
        return html.unescape(response.read().decode())
def token(markup):
    return re.search(r'name="__RequestVerificationToken"[^>]+value="([^"]+)"', markup)[1]
assert "Đăng nhập" in page("/Account/Profile")
page("/Account/Login", dict(email=p["email"],password="Changed123!",
    __RequestVerificationToken=token(page("/Account/Login"))))
profile = page("/Account/Profile")
assert "Nguyễn An" in profile and "Hồ sơ của tôi" in profile
try:
    page("/Account/UpdateProfile", {"FullName":"Forged"})
    raise AssertionError("Missing antiforgery accepted")
except HTTPError as e:
    assert e.code == 400
updated = page("/Account/UpdateProfile", dict(FullName="An Updated", PhoneNumber="0987654321",
    __RequestVerificationToken=token(profile)))
assert "An Updated" in updated and "Đã lưu thay đổi thành công" in updated
page("/Account/AddAddress", dict(RecipientName="Web Receiver", Phone="0901234567", Province="Hà Nội",
    District="Cầu Giấy", Ward="Dịch Vọng", DetailAddress="99 Web Street", IsDefault="true",
    __RequestVerificationToken=token(page("/Account/Profile?tab=addresses"))))
book = page("/Account/Profile?tab=addresses")
assert "Web Receiver" in book and 'id="address-dialog"' in book
# Put an item in the cart, then verify server-rendered checkout prefills the default.
products = call("GET", "products")
for product in products:
    detail = call("GET", "products/" + product["id"])
    variants = detail.get("productVariants", [])
    variant = next((v for v in variants if v["stockQuantity"] > 0), None)
    if variant:
        call("POST", "cart/items", owner, {"variantId":variant["id"], "quantity":1}, 204)
        break
else:
    raise AssertionError("No available fixture product")
checkout = page("/Cart")
assert 'id="saved-address"' in checkout and 'value="Web Receiver"' in checkout
assert "99 Web Street, Dịch Vọng, Cầu Giấy, Hà Nội</textarea>" in checkout
password_page = page("/Account/Profile?tab=password")
failed = page("/Account/ChangePassword", dict(CurrentPassword="Changed123!", NewPassword="Next12345!",
    ConfirmPassword="Mismatch123!", __RequestVerificationToken=token(password_page)))
assert "alert-danger" in failed
call("POST", "auth/login", data={"email":p["email"],"password":"Changed123!"})
for item in call("GET", "account/addresses", owner):
    call("DELETE", "account/addresses/" + item["id"], owner, expect=204)
assert call("GET", "account/addresses", owner) == []
print("PASS account: authorization, ownership, validation, password hashing, concurrent defaults, deletion, MVC CSRF, profile forms, checkout prefill")
```

## tests/run_postgres.py

```python
"""Build first. Starts a temporary PostgreSQL cluster, WebAPI and WebClient on loopback."""
import os
import secrets
import socket
import subprocess
import sys
import tempfile
import time
from contextlib import ExitStack
from pathlib import Path
from urllib.error import URLError
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]


def free_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def run():
    pg_bin = Path(os.environ.get("PG_BIN", r"C:\Program Files\PostgreSQL\17\bin"))
    pg = lambda name: str(pg_bin / name)
    with tempfile.TemporaryDirectory(prefix="marketplace-step3-") as tmp, ExitStack() as stack:
        directory = Path(tmp)
        cluster = directory / "pgdata"
        password = secrets.token_hex(24)
        pwfile = directory / "pw"
        pwfile.write_text(password, encoding="ascii")
        pg_port = free_port()
        subprocess.run([pg("initdb"), "-D", cluster, "-U", "postgres", "-A", "scram-sha-256",
                        "--pwfile", pwfile, "--encoding=UTF8", "--locale=C"], check=True)
        pwfile.unlink()
        subprocess.run([pg("pg_ctl"), "-D", cluster, "-l", directory / "postgres.log",
                        "-o", f"-h 127.0.0.1 -p {pg_port}", "-w", "start"], check=True)
        stack.callback(subprocess.run, [pg("pg_ctl"), "-D", cluster, "-m", "fast", "-w", "stop"])
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", SeedDemoData="true",
                   ConnectionStrings__Marketplace=f"Host=127.0.0.1;Port={pg_port};Database=postgres;Username=postgres;Password={password}")
        def app(name, *args):
            folder = ROOT / ("Marketplace." + name)
            dll = folder / "bin/Release/net8.0" / ("Marketplace." + name + ".dll")
            return ["dotnet", str(dll), *args], folder

        command, cwd = app("WebAPI", "--migrate")
        subprocess.run(command, cwd=cwd, env=env, check=True)
        def start(name, ready):
            url = f"http://127.0.0.1:{free_port()}"
            command, cwd = app(name, "--urls", url)
            log = stack.enter_context((directory / (name + ".log")).open("w+", encoding="utf-8"))
            process = subprocess.Popen(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT)
            def stop():
                if process.poll() is None:
                    process.terminate()
                    process.wait(timeout=15)
            stack.callback(stop)
            for _ in range(120):
                if process.poll() is not None:
                    break
                try:
                    with urlopen(url + ready, timeout=2) as response:
                        if response.status == 200:
                            return url
                except (OSError, URLError):
                    time.sleep(0.25)
            log.seek(0)
            raise RuntimeError(log.read()[-6000:])
        with_ai = "--ai" in sys.argv
        if "--visual-load" in sys.argv:
            env["VisualSearch__RequestsPerMinute"] = "50000"
        if with_ai:
            ai_port = free_port()
            env["AiBaseUrl"] = f"http://127.0.0.1:{ai_port}"
            env["MARKETPLACE_AI_DSN"] = f"host=127.0.0.1 port={pg_port} dbname=postgres user=postgres password={password}"
            env["MARKETPLACE_TEST_API"] = start("WebAPI", "/api/status")
            log = stack.enter_context((directory / "ai.log").open("w+", encoding="utf-8"))
            ai = subprocess.Popen([sys.executable,"-m","uvicorn","main:app","--port",str(ai_port)],
                                  cwd=ROOT/"Marketplace.AIService",env=env,stdout=log,stderr=subprocess.STDOUT)
            def stop_ai():
                if ai.poll() is None:
                    ai.terminate()
                    ai.wait(timeout=15)
            stack.callback(stop_ai)
            for _ in range(100):
                try:
                    with urlopen(env["AiBaseUrl"]+"/docs", timeout=1): break
                except OSError: time.sleep(.2)
            else:
                log.seek(0)
                raise RuntimeError(log.read()[-3000:])
        if not with_ai:
            env["MARKETPLACE_TEST_API"] = start("WebAPI", "/api/status")
        env["ApiBaseUrl"] = env["MARKETPLACE_TEST_API"] + "/"
        env["MARKETPLACE_TEST_WEB"] = start("WebClient", "/Account/Login")
        if "--chat" in sys.argv:
            subprocess.run([sys.executable, str(ROOT / "tests/test_chat.py")], env=env, check=True)
        if with_ai:
            test = [sys.executable, str(ROOT / "tests/test_ai.py")]
            subprocess.run(test + ["--online"], env=env, check=True)
            if "--visual-load" in sys.argv:
                port = env["MARKETPLACE_TEST_API"].rsplit(":", 1)[1]
                subprocess.run([sys.executable, str(ROOT / "load_test.py"), "--port", port,
                                "--users", "10000", "--requests", "10000", "--connections", "128",
                                "--ramp", "30", "--images", str(ROOT / "Marketplace.WebAPI/wwwroot/images/demo"),
                                "--output", str(ROOT / "visual-load-windows.json")], env=env, check=True)
            stop_ai()
            subprocess.run(test, env=env, check=True)
        for suite in ("test_wishlist.py","test_step3.py","test_web.py","test_flash_sales.py","test_account.py"):
            subprocess.run([sys.executable, str(ROOT / "tests" / suite)], env=env, check=True)
        subprocess.run([pg("psql"), "-h", "127.0.0.1", "-p", str(pg_port), "-U", "postgres", "-d", "postgres",
                        "-v", "ON_ERROR_STOP=1", "-f", "-"], input=(ROOT / "tests/review_totals.sql").read_bytes(),
                       env=dict(env, PGPASSWORD=password), check=True)


if __name__ == "__main__":
    run()
```
