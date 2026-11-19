using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public static string Translate(string value, string from, string to) => throw new NotSupportedException();
    public DbSet<FlashSaleCampaign> FlashSaleCampaigns => Set<FlashSaleCampaign>();
    public DbSet<FlashSaleItem> FlashSaleItems => Set<FlashSaleItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<OrderGroup> OrderGroups => Set<OrderGroup>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReturnRefundRequest> ReturnRefundRequests => Set<ReturnRefundRequest>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<UserInteractionLog> UserInteractionLogs => Set<UserInteractionLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(Translate))!).HasName("translate").IsBuiltIn();
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
