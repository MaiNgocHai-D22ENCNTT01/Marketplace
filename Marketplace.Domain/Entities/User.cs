using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public UserStatus Status { get; set; } = UserStatus.ACTIVE;
    public DateTime CreatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; } = new List<UserRole>();
    public ICollection<Address> Addresses { get; } = new List<Address>();
    public ICollection<AuditLog> AuditLogs { get; } = new List<AuditLog>();
    public Shop? Shop { get; set; }
    public Cart? Cart { get; set; }
    public ICollection<OrderGroup> OrderGroups { get; } = new List<OrderGroup>();
    public ICollection<Review> Reviews { get; } = new List<Review>();
    public ICollection<ReturnRefundRequest> ReturnRefundRequests { get; } = new List<ReturnRefundRequest>();
    public ICollection<Complaint> Complaints { get; } = new List<Complaint>();
    public ICollection<UserInteractionLog> UserInteractionLogs { get; } = new List<UserInteractionLog>();
    public ICollection<Notification> Notifications { get; } = new List<Notification>();
}
