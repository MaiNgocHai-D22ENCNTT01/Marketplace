using Marketplace.Domain.Entities;
using Marketplace.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI;

// Public reads and checkout evaluate the exact interval independently of this stored-status refresh.
public sealed class FlashSaleClock(IServiceScopeFactory scopes, ILogger<FlashSaleClock> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var tick = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                await db.FlashSaleCampaigns.Where(c => c.Status != CampaignStatus.ENDED && c.EndTime < now)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CampaignStatus.ENDED), stop);
                await db.FlashSaleCampaigns.Where(c => c.Status == CampaignStatus.UPCOMING && c.StartTime <= now && c.EndTime >= now)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CampaignStatus.ACTIVE), stop);
            }
            catch (Exception ex) when (!stop.IsCancellationRequested) { log.LogWarning(ex, "Flash sale status refresh failed"); }
        } while (await tick.WaitForNextTickAsync(stop));
    }
}
