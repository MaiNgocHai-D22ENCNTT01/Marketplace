namespace Marketplace.Application.Catalog;

public sealed record ShopCategory(Guid Id, string Name, int ProductCount);

public sealed record PublicShop(Guid Id, string ShopName, string? Description, string? LogoUrl,
    decimal RatingAvg, int ReviewCount, DateTime CreatedAt, string Status, int TotalProducts,
    List<ShopCategory> Categories, int FilteredTotal, int Page, int PageSize, List<ProductSummary> Products);
