using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class UserInteractionLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public ActionType ActionType { get; set; }
    public DateTime Timestamp { get; set; }

    public User User { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
