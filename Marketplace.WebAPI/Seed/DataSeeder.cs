using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Seed;

public static partial class DataSeeder
{
    private sealed record Collection(string Slug, string Category, string Shop, string Item, string Name, decimal Price);

    private static readonly Collection[] Collections =
    [
        new("thoi-trang", "Thời trang", "Studio Vải", "tee", "Áo thun cổ tròn", 159000),
        new("thoi-trang", "Thời trang", "Studio Vải", "polo", "Áo polo cổ bẻ", 229000),
        new("phu-kien", "Phụ kiện", "Tiệm Phố", "tote", "Túi tote canvas", 99000),
        new("phu-kien", "Phụ kiện", "Tiệm Phố", "pack", "Balo đi học", 319000),
        new("dien-tu", "Điện tử", "Góc Công Nghệ", "mouse", "Chuột không dây", 219000),
        new("dien-tu", "Điện tử", "Góc Công Nghệ", "audio", "Tai nghe chụp tai", 459000),
        new("gia-dung", "Gia dụng", "Nhà Mộc", "cup", "Ly gốm 350 ml", 89000),
        new("gia-dung", "Gia dụng", "Nhà Mộc", "bottle", "Bình nước 600 ml", 139000),
        new("van-phong-pham", "Văn phòng phẩm", "Góc Giấy", "book", "Sổ tay A5", 49000),
        new("van-phong-pham", "Văn phòng phẩm", "Góc Giấy", "binder", "Bìa còng A4", 69000),
        new("the-thao", "Thể thao", "Nhịp Sống", "weight", "Tạ tay 2 kg", 179000),
        new("the-thao", "Thể thao", "Nhịp Sống", "mat", "Thảm yoga 6 mm", 269000)
    ];

    private static readonly (string Code, string Name)[] Colors =
    [
        ("mint", "Xanh bạc hà"), ("navy", "Xanh navy"), ("coral", "San hô"),
        ("sand", "Be cát"), ("plum", "Tím mận"), ("olive", "Xanh ô liu"),
        ("sky", "Xanh da trời"), ("ochre", "Vàng đất"), ("rose", "Hồng phấn"), ("slate", "Xám đá")
    ];

    // Fixed identities let repeated startup preserve user edits and unrelated rows.
    private static Guid SeedId(string key) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("Marketplace/demo/v1/" + key)).AsSpan(0, 16));

    public static async Task RunAsync(AppDbContext db, string webRoot, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize simultaneous API startups until commit or rollback.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(632810119)", ct);
        await PopulateAsync(db, ct);
        await SeedReviews(db, ct);
        await SeedFlashSale(db, ct);
        await transaction.CommitAsync(ct);
    }

    internal static async Task PopulateAsync(AppDbContext db, CancellationToken ct)
    {
        var roles = await db.Roles.ToDictionaryAsync(x => x.Name, ct);
        foreach (var name in Enum.GetValues<RoleName>())
        {
            if (roles.ContainsKey(name)) continue;
            var role = new Role { Id = SeedId("role/" + name), Name = name };
            db.Roles.Add(role);
            roles.Add(name, role);
        }

        var buyerId = SeedId("customer/demo");
        if (!await db.Users.AnyAsync(u => u.Id == buyerId || u.Email == "buyer@example.invalid", ct))
        {
            var buyer = new User { Id = buyerId, Email = "buyer@example.invalid", FullName = "Khách hàng mẫu", Cart = new Cart() };
            buyer.PasswordHash = new PasswordHasher<User>().HashPassword(buyer, "Marketplace123!");
            buyer.UserRoles.Add(new UserRole { Role = roles[RoleName.CUSTOMER] });
            db.Users.Add(buyer);
        }

        var adminId = SeedId("admin/demo");
        if (!await db.Users.AnyAsync(x => x.Id == adminId, ct))
        {
            var admin = new User { Id = adminId, Email = "admin@example.invalid", FullName = "Quản trị viên mẫu" };
            admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, "Marketplace123!");
            foreach (var name in new[] { RoleName.ADMIN, RoleName.SUPER_ADMIN })
                admin.UserRoles.Add(new UserRole { Role = roles[name] });
            db.Users.Add(admin);
        }

        var templateIds = Collections.SelectMany(line => Colors.Select(c =>
            SeedId($"product/{line.Item}/{c.Code}"))).ToArray();
        var existing = (await db.Products.Where(p => templateIds.Contains(p.Id))
            .Select(p => p.Id).ToListAsync(ct)).ToHashSet();

        foreach (var group in Collections.GroupBy(x => x.Slug))
        {
            var line = group.First();
            var category = await db.Categories.SingleOrDefaultAsync(x => x.Slug == group.Key, ct);
            if (category is null)
            {
                category = new Category { Id = SeedId("category/" + group.Key), Slug = group.Key, Name = line.Category };
                db.Categories.Add(category);
            }

            var userId = SeedId("seller/" + group.Key);
            var seller = await db.Users.FindAsync([userId], ct);
            if (seller is null)
            {
                seller = new User { Id = userId, Email = $"demo-{group.Key}@example.invalid", FullName = line.Shop };
                seller.PasswordHash = new PasswordHasher<User>().HashPassword(seller,
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
                db.Users.Add(seller);
            }
            if (group.Key == "thoi-trang" && seller.Email == "demo-thoi-trang@example.invalid")
            {
                seller.Email = "seller@example.invalid";
                seller.PasswordHash = new PasswordHasher<User>().HashPassword(seller, "Marketplace123!");
            }
            var sellerRole = roles[RoleName.SELLER];
            if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == sellerRole.Id, ct))
                db.UserRoles.Add(new UserRole { User = seller, Role = sellerRole });

            var shop = await db.Shops.SingleOrDefaultAsync(x => x.UserId == userId, ct);
            if (shop is null)
            {
                shop = new Shop { Id = SeedId("shop/" + group.Key), Name = line.Shop, User = seller, Status = ShopStatus.ACTIVE };
                db.Shops.Add(shop);
            }

            foreach (var collection in group)
            foreach (var color in Colors)
            {
                var key = $"{collection.Item}/{color.Code}";
                var id = SeedId("product/" + key);
                if (existing.Contains(id)) continue;
                var product = new Product
                {
                    Id = id, Name = $"{collection.Name} – {color.Name}", Shop = shop,
                    Category = category, BasePrice = collection.Price,
                    Description = $"{collection.Name}, màu {color.Name.ToLowerInvariant()}, thuộc bộ sưu tập của {line.Shop}."
                };
                product.ProductVariants.Add(new ProductVariant
                {
                    Id = SeedId("variant/" + key), Sku = $"DEMO-{collection.Item}-{color.Code}".ToUpperInvariant(),
                    Attributes = JsonSerializer.Serialize(new { color = color.Name }),
                    Price = collection.Price, StockQuantity = 25
                });
                product.ProductImages.Add(new ProductImage
                {
                    Id = SeedId("image/" + key), ImageUrl = $"/images/demo/{collection.Item}-{color.Code}.png",
                    IsPrimary = true
                });
                db.Products.Add(product);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
