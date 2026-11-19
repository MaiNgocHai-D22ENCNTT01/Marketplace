using System.ComponentModel.DataAnnotations;

namespace Marketplace.Application.Shopping;

public record ReviewRow(Guid Id, Guid ProductId, string ProductName, string Buyer, int Rating, string? Comment,
    string[] Images, string? SellerReply, DateTime CreatedAt);
public record ReviewPage(List<ReviewRow> Items, int Page, int PageSize, int Total);
public record ProductReviews(decimal RatingAvg, int ReviewCount, int[] StarCounts, ReviewPage Results);
public record ReviewReply([param: Required, StringLength(2000)] string Reply);
