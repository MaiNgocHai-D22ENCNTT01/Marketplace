namespace Marketplace.Application.Catalog;

public sealed record ProductSummary(Guid Id, string Name, string Category, decimal Price, string? ImageUrl, decimal RatingAvg = 0, int ReviewCount = 0, Guid ShopId = default, string ShopName = "");
