using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Voucher
{
    public Guid Id { get; set; }
    public Guid? ShopId { get; set; }
    public string Code { get; set; } = null!;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinOrderValue { get; set; }
    public int UsageLimit { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public VoucherStatus Status { get; set; } = VoucherStatus.ACTIVE;

    public Shop? Shop { get; set; }
}
