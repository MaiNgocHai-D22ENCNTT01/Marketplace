using Marketplace.Domain.Enums;

namespace Marketplace.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public CategoryStatus Status { get; set; } = CategoryStatus.ACTIVE;

    public ICollection<Category> Children { get; } = new List<Category>();
    public ICollection<Product> Products { get; } = new List<Product>();
    public Category? Parent { get; set; }
}
