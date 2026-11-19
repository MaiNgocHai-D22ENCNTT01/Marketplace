namespace Marketplace.Application.Catalog;

public sealed record SimilarProduct(ProductSummary Product, double SimilarityScore);
public sealed record VisualResult(string? Category, double Confidence, List<SimilarProduct> Products, string Engine);
