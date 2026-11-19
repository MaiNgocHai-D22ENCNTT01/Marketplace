using System.ComponentModel.DataAnnotations;

namespace Marketplace.Application.Shopping;

public record CampaignInput([param: Required, StringLength(160)] string Title, DateTimeOffset StartTime, DateTimeOffset EndTime);
public record SaleRegistration(Guid VariantId, [param: Range(typeof(decimal),"0.01","9999999999.99")] decimal FlashPrice,
    [param: Range(1,int.MaxValue)] int TotalQuota, [param: Range(1,int.MaxValue)] int MaxPerUser);
public record SaleCampaign(Guid Id, string Title, DateTime StartTime, DateTime EndTime, string Status);
public record SaleItem(Guid Id, Guid CampaignId, string CampaignTitle, Guid VariantId, Guid ProductId, string ProductName,
    string Sku, string ShopName, string? ImageUrl, decimal RegularPrice, decimal FlashPrice,
    int TotalQuota, int SoldCount, int MaxPerUser, string Status)
{
    public decimal SoldPercent => Math.Round(100m * SoldCount / TotalQuota, 2);
}
public record SaleWindow(SaleCampaign Campaign, List<SaleItem> Items, DateTime ServerTime);
