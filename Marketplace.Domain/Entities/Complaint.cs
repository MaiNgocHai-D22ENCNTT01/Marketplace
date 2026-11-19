using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Complaint
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public Guid TargetShopId { get; set; }
    public Guid? OrderId { get; set; }
    public string Content { get; set; } = null!;
    public ComplaintStatus Status { get; set; } = ComplaintStatus.OPEN;
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Sender { get; set; } = null!;
    public Shop TargetShop { get; set; } = null!;
    public Order? Order { get; set; }
}
