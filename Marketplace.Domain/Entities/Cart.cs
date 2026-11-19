namespace Marketplace.Domain.Entities;

public sealed class Cart
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CartItem> CartItems { get; } = new List<CartItem>();
    public User User { get; set; } = null!;
}
