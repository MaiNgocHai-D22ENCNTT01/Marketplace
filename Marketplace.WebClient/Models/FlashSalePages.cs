using Marketplace.Application.Shopping;

namespace Marketplace.WebClient.Models;

public record AdminSalesPage(List<SaleCampaign> Campaigns, List<SaleItem> Pending);
public record SellerSalesPage(List<SaleCampaign> Campaigns, List<ManagedProduct> Products, List<SaleItem> Items);
