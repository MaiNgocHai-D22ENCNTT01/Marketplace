using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.Shopping;

namespace Marketplace.WebClient.Services;

public sealed class ShopApi(IHttpClientFactory clients, IHttpContextAccessor accessor)
{
    private Task<List<Marketplace.Application.Catalog.ProductSummary>>? wishlist;
    private HashSet<Guid>? wishlistIds;
    public Task<List<Marketplace.Application.Catalog.ProductSummary>> Wishlist(CancellationToken ct) =>
        wishlist ??= Get<List<Marketplace.Application.Catalog.ProductSummary>>("api/wishlists", ct);
    public async Task<HashSet<Guid>> WishlistIds(CancellationToken ct) =>
        wishlistIds ??= (await Wishlist(ct)).Select(p => p.Id).ToHashSet();

    public ISession Session => accessor.HttpContext!.Session;
    public bool SignedIn => Session.GetString("jwt") is not null;
    public bool HasRole(string role) => Session.GetString("roles")?.Split(',').Contains(role) == true;

    public async Task<Marketplace.Application.Catalog.WishlistStatus> ToggleWishlist(Guid id, CancellationToken ct)
    {
        using var client = Client();
        using var response = await client.PostAsync($"api/wishlists/toggle/{id}", null, ct);
        await Check(response, ct);
        return (await response.Content.ReadFromJsonAsync<Marketplace.Application.Catalog.WishlistStatus>(ct))!;
    }

    public async Task Change(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var client = Client();
        using var request = new HttpRequestMessage(method, path);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, ct);
        await Check(response, ct);
    }

    private HttpClient Client()
    {
        var client = clients.CreateClient("Marketplace");
        if (Session.GetString("jwt") is string token)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<T> Get<T>(string path, CancellationToken ct)
    {
        using var client = Client();
        using var response = await client.GetAsync(path, ct);
        await Check(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }

    public async Task<LoginResult> Login(LoginRequest input, CancellationToken ct)
    {
        using var client = Client();
        using var response = await client.PostAsJsonAsync("api/auth/login", input, ct);
        await Check(response, ct);
        return (await response.Content.ReadFromJsonAsync<LoginResult>(ct))!;
    }

    public async Task Post<T>(string path, T body, CancellationToken ct)
    {
        using var client = Client();
        using var response = await client.PostAsJsonAsync(path, body, ct);
        await Check(response, ct);
    }

    public async Task Remove(Guid variantId, CancellationToken ct)
    {
        using var client = Client();
        using var response = await client.DeleteAsync($"api/cart/items/{variantId}", ct);
        await Check(response, ct);
    }

    private async Task Check(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Unauthorized) Session.Clear();
        var message = response.StatusCode switch
        {
            HttpStatusCode.Conflict => (await response.Content.ReadAsStringAsync(ct)).Trim('"'),
            HttpStatusCode.Forbidden => "Tài khoản chưa có quyền thực hiện thao tác này.",
            HttpStatusCode.Unauthorized => "Vui lòng kiểm tra thông tin đăng nhập.",
            HttpStatusCode.BadRequest => "Vui lòng kiểm tra lại thông tin đã nhập.",
            _ => "Không thể tải dữ liệu từ Marketplace."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
