namespace Marketplace.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid AdminId { get; set; }
    public string Action { get; set; } = null!;
    public string TargetEntity { get; set; } = null!;
    public Guid? TargetId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }

    public User Admin { get; set; } = null!;
}
