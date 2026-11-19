namespace Marketplace.Domain.Entities;

public enum CampaignStatus { UPCOMING, ACTIVE, ENDED }
public sealed class FlashSaleCampaign
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
