namespace Marketplace.Domain.Entities;

public sealed class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Module { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; } = new List<RolePermission>();
}
