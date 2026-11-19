namespace Marketplace.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ShopId { get; set; }
    public DateTime LastMessageAt { get; set; }
    public User Customer { get; set; } = null!;
    public Shop Shop { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; } = new List<ChatMessage>();
}
