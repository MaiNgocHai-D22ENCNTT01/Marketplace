using System.ComponentModel.DataAnnotations;

namespace Marketplace.Application.Shopping;

public record StatusInput([param: Required] string Status);
public record ShopInput([param: Required, StringLength(150)] string Name, [param: StringLength(2000)] string? Description);
public record ReviewInput(Guid OrderItemId, [param: Range(1,5)] int Rating, [param: StringLength(1000)] string? Comment, [param: MaxLength(3)] string[]? Images = null);
public record VariantEdit(Guid? Id, [param: Required, StringLength(100)] string Sku,
    [param: Range(typeof(decimal), "0.01", "9999999999.99")] decimal Price,
    [param: Range(0,int.MaxValue)] int StockQuantity);
public record ProductEdit([param: Required, StringLength(200)] string Name, Guid CategoryId,
    [param: StringLength(4000)] string? Description, [param: Required] string ImageUrl,
    [param: Required, MinLength(1)] List<VariantEdit> Variants);
public record ManagedProduct(Guid Id, string Status, ProductEdit Data);
public record NamedRow(Guid Id, string Name, string Status);
public record SellerOrder(Guid Id, string Status, decimal Total, string Recipient, List<string> Items);
public record BestSeller(string Name, long Quantity);
public record Sales(decimal Revenue, int Orders, List<BestSeller> BestSellers);
public record Dashboard(int Users, int Shops, int Products, int Orders, decimal Revenue);
public record AuditEntry(DateTime Timestamp, string Admin, string Action, string TargetEntity, Guid? TargetId, string? Details);
