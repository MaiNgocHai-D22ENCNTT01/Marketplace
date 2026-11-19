namespace Marketplace.Domain.Entities;

public sealed class Address
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RecipientName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Province { get; set; } = null!;
    public string District { get; set; } = null!;
    public string Ward { get; set; } = null!;
    public string DetailAddress { get; set; } = null!;
    public bool IsDefault { get; set; }

    public User User { get; set; } = null!;
}
