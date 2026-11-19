using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; set; }
    public RoleName Name { get; set; }
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; } = new List<RolePermission>();
}
