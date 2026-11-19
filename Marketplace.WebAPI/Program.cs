using Marketplace.Infrastructure;
using Marketplace.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using Marketplace.WebAPI.Seed;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Marketplace")
    ?? throw new InvalidOperationException("Missing connection string: Marketplace.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddControllers();
Marketplace.WebAPI.VisualSearchGateway.Configure(builder);
builder.Services.AddHostedService<Marketplace.WebAPI.FlashSaleClock>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, ChatUserIdProvider>();
var chatOrigins = builder.Configuration.GetSection("Chat:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("ChatClient", policy =>
    policy.WithOrigins(chatOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddHttpClient("AI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AiBaseUrl"] ?? "http://127.0.0.1:8000/");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Ai:TimeoutSeconds", 5));
    if (builder.Configuration["Ai:ApiKey"] is string key && key.Length > 0)
        client.DefaultRequestHeaders.Add("X-AI-Key", key);
}).ConfigurePrimaryHttpMessageHandler(() => Marketplace.Deployment.CloudHttp.Create(
    builder.Configuration, builder.Configuration["AiBaseUrl"] ?? "http://127.0.0.1:8000/"));
builder.Services.AddEndpointsApiExplorer();
var tokens = new Marketplace.WebAPI.AuthTokens(builder.Configuration["Jwt:Key"], builder.Environment.IsDevelopment());
builder.Services.AddSingleton(tokens);
builder.Services.AddScoped<ShoppingService>();
builder.Services.AddScoped<OrderOperations>();
builder.Services.AddAuthentication("Bearer").AddJwtBearer(options =>
{
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Path == "/hubs/chat" && context.Request.Query.TryGetValue("access_token", out var token))
                context.Token = token;
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!Guid.TryParse(context.Principal?.Identity?.Name, out var id) ||
                !await db.Users.AnyAsync(x => x.Id == id && x.Status == Marketplace.Domain.Enums.UserStatus.ACTIVE,
                    context.HttpContext.RequestAborted))
                context.Fail("TÃ i khoáº£n Ä‘Ã£ bá»‹ khÃ³a.");
        }
    };
    options.MapInboundClaims = false;
    options.TokenValidationParameters = tokens.Validation;
});
builder.Services.AddAuthorization();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue("SeedDemoData", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply the included migration with Update-Database before starting the API.");
    await DataSeeder.RunAsync(db, app.Environment.WebRootPath);
}
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ShoppingConflict ex)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsync(ex.Message);
    }
    catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsync("Dữ liệu đã tồn tại. Kiểm tra gian hàng, mã SKU hoặc đánh giá.");
    }
});
app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true)
    .RequireCors("ChatClient");
app.Run();
