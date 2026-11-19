using Marketplace.WebClient.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.WebClient.Controllers;

public abstract class ChannelController(ShopApi api) : Controller
{
    protected ShopApi Api { get; } = api;

    protected async Task<IActionResult> Page(Func<Task<IActionResult>> load)
    {
        if (!Api.SignedIn) return RedirectToAction("Login","Account",new { returnUrl = Request.Path.Value });
        try { return await load(); }
        catch (HttpRequestException ex)
        {
            TempData["Notice"] = ex.Message;
            return RedirectToAction("Index","Home");
        }
    }

    protected async Task<IActionResult> Submit(HttpMethod method,string path,object? body,string message,CancellationToken ct)
    {
        try { await Api.Change(method,path,body,ct); TempData["Notice"] = message; }
        catch (HttpRequestException ex) { TempData["Notice"] = ex.Message; }
        return RedirectToAction("Index");
    }
}
