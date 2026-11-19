using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Review
{
    public Guid Id { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? SellerReply { get; set; }
    public string[] Images { get; set; } = [];
    public ReviewStatus Status { get; set; } = ReviewStatus.ACTIVE;
    public DateTime CreatedAt { get; set; }

    public OrderItem OrderItem { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
