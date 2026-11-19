using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class ReturnRefundRequest
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Reason { get; set; } = null!;
    public string EvidenceImages { get; set; } = null!;
    public decimal RefundAmount { get; set; }
    public ReturnRefundRequestStatus Status { get; set; } = ReturnRefundRequestStatus.PENDING;
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }

    public Order Order { get; set; } = null!;
    public User Customer { get; set; } = null!;
}
