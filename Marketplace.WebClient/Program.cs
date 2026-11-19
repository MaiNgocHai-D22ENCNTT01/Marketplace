var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(2);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Marketplace.WebClient.Services.ShopApi>();
builder.Services.AddHttpClient("Marketplace", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("Missing ApiBaseUrl."));
}).ConfigurePrimaryHttpMessageHandler(() => Marketplace.Deployment.CloudHttp.Create(
    builder.Configuration, builder.Configuration["ApiBaseUrl"]!));

var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
