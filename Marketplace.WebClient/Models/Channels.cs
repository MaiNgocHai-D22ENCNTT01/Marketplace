using Marketplace.Application.Shopping;
namespace Marketplace.WebClient.Models;

public record SellerPage(Sales Sales, List<ManagedProduct> Products, List<SellerOrder> Orders);
public record ProductForm(Guid? Id, ProductEdit Product, List<ShopInfo> Categories);
public record AdminPage(Dashboard Stats,List<NamedRow> Shops,List<NamedRow> Users,List<NamedRow> Products,List<AuditEntry> Logs);
