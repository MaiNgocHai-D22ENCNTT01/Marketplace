using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid OrderGroupId { get; set; }
    public Guid ShopId { get; set; }
    public string ShippingAddressSnapshot { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public OrderStatus OrderStatus { get; set; } = OrderStatus.PENDING;
    public DateTime CreatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; } = new List<OrderItem>();
    public ICollection<ReturnRefundRequest> ReturnRefundRequests { get; } = new List<ReturnRefundRequest>();
    public ICollection<Complaint> Complaints { get; } = new List<Complaint>();
    public OrderGroup OrderGroup { get; set; } = null!;
    public Shop Shop { get; set; } = null!;
}
