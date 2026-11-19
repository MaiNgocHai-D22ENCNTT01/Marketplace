using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Shop
{
    public int ReviewCount { get; set; }
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Logo { get; set; }
    public ShopStatus Status { get; set; } = ShopStatus.PENDING;
    public decimal RatingAvg { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Product> Products { get; } = new List<Product>();
    public ICollection<Order> Orders { get; } = new List<Order>();
    public ICollection<Voucher> Vouchers { get; } = new List<Voucher>();
    public ICollection<Complaint> Complaints { get; } = new List<Complaint>();
    public User User { get; set; } = null!;
}
