namespace Marketplace.Domain.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = "";
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
