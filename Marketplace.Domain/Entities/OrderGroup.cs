namespace Marketplace.Domain.Entities;

public sealed class OrderGroup
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Order> Orders { get; } = new List<Order>();
    public Payment? Payment { get; set; }
    public User Customer { get; set; } = null!;
}
