using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid OrderGroupId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.UNPAID;
    public string? TransactionCode { get; set; }
    public DateTime? PaidAt { get; set; }

    public OrderGroup OrderGroup { get; set; } = null!;
}
