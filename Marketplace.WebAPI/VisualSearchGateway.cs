using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace Marketplace.WebAPI;

internal static class VisualSearchGateway
{
    internal static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            if (builder.Configuration["VisualSearch:TrustedWebClientIp"] is string ip)
                options.KnownProxies.Add(IPAddress.Parse(ip));
        });
        int permits = builder.Configuration.GetValue("VisualSearch:RequestsPerMinute", 30);
        builder.Services.AddRateLimiter(options =>
        {
            // Runs before multipart model binding: cap uploads, memory and pending work.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/products/visual-search", StringComparison.OrdinalIgnoreCase)
                ? RateLimitPartition.GetConcurrencyLimiter("visual-upload", _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("VisualSearch:MaxConcurrentRequests", 256),
                    QueueLimit = 0
                })
                : RateLimitPartition.GetNoLimiter("other"));
            options.AddPolicy("visual", context => RateLimitPartition.GetSlidingWindowLimiter(
                context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permits, Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6, QueueLimit = 0
                }));
            options.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.StatusCode = 429;
                ctx.HttpContext.Response.Headers.RetryAfter = "60";
                await ctx.HttpContext.Response.WriteAsJsonAsync("Bạn tìm kiếm quá nhanh. Vui lòng thử lại sau một phút.", token);
            };
        });
        builder.Services.AddSingleton<VisualCache>();
        builder.Services.AddSingleton<AiCircuit>();
        builder.Services.AddTransient<AiCircuitHandler>();
        builder.Services.AddHttpClient("VisualAI", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["AiBaseUrl"] ?? "http://127.0.0.1:8000/");
            client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Ai:TimeoutSeconds", 4));
            if (builder.Configuration["Ai:ApiKey"] is string key && key.Length > 0)
                client.DefaultRequestHeaders.Add("X-AI-Key", key);
        }).ConfigurePrimaryHttpMessageHandler(() => Marketplace.Deployment.CloudHttp.Create(
            builder.Configuration, builder.Configuration["AiBaseUrl"] ?? "http://127.0.0.1:8000/"))
            .AddHttpMessageHandler<AiCircuitHandler>();
    }
}

internal sealed class VisualCache : IDisposable
{
    public MemoryCache Entries { get; } = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });
    // Stripe ownership bounds simultaneous cold queries and coalesces identical uploads.
    public SemaphoreSlim[] Stripes { get; } = Enumerable.Range(0, 128).Select(_ => new SemaphoreSlim(1)).ToArray();
    public void Dispose()
    {
        Entries.Dispose();
        foreach (var stripe in Stripes) stripe.Dispose();
    }
}

internal sealed class AiCircuit
{
    private readonly object sync = new();
    private int failures;
    private long opensUntil;
    private int generation;
    public int Enter()
    {
        lock (sync)
        {
            if (opensUntil > Environment.TickCount64) throw new HttpRequestException("AI busy");
            if (opensUntil != 0) opensUntil = long.MaxValue; // one recovery probe
            return generation;
        }
    }
    public void Finish(int ticket, bool failed)
    {
        lock (sync)
        {
            if (ticket != generation) return;
            if (!failed) { failures = 0; opensUntil = 0; return; }
            if (++failures >= 5)
            {
                opensUntil = Environment.TickCount64 + 5000;
                generation++;
            }
        }
    }
}

internal sealed class AiCircuitHandler(AiCircuit circuit) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var ticket = circuit.Enter();
        bool failed = true;
        try
        {
            var response = await base.SendAsync(request, ct);
            failed = (int)response.StatusCode >= 500;
            return response;
        }
        finally { circuit.Finish(ticket, failed); }
    }
}
