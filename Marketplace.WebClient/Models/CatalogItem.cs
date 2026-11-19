namespace Marketplace.WebClient.Models;

public sealed record CatalogItem(Guid Id, string Name, string Category, decimal Price, string? ImageUrl);
