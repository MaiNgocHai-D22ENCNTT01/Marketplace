using System.Security.Cryptography;
using System.Text.Json;
using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Seed;

public static partial class DataSeeder
{
    private static async Task SeedReviews(AppDbContext db, CancellationToken ct)
    {
        string[] names = ["Minh Anh", "Quốc Bảo", "Thu Hà", "Bảo Ngọc", "Đức Minh"];
        string[][] comments = [
            ["Vải mềm, mặc vừa vai.", "Màu ngoài đời nhạt hơn ảnh một chút.", "Đường may ở cổ áo rất gọn.", "Hàng giao nhanh, áo được gấp gọn. Mình mặc đi làm cả ngày vẫn thấy mát, màu xanh đúng như hình.", "Mình cao 1m65, chọn theo bảng kích thước thì vừa. Giặt một lần cổ áo vẫn giữ dáng."],
            ["Túi vừa sách khổ A4.", "Quai túi chắc, đeo không cấn vai.", "Mình muốn có thêm một ngăn nhỏ bên trong.", "Quai đủ dài để đeo vai kể cả khi mặc áo khoác. Mình đựng hai quyển sách và một chai nước vẫn thoải mái.", "Đáy túi hơi mềm nên khi để nhiều đồ sẽ võng một chút. Bù lại đường chỉ rất đều."],
            ["Chuột kết nối nhanh với laptop.", "Nút cuộn hơi cứng với mình.", "Cầm vừa tay, bấm khá êm.", "Đầu thu nằm trong ngăn pin nên lúc mở hộp mình tìm một lúc mới thấy. Cắm vào máy là dùng được ngay.", "Dùng trong phòng làm việc yên tĩnh thì tiếng bấm vẫn nghe nhẹ. Di chuột trên bàn gỗ rất mượt."],
            ["Men gốm mịn, màu đẹp.", "Ly vừa đủ cho một phần cà phê sáng.", "Shop bọc kỹ nên ly đến nơi còn nguyên.", "Ly được chèn giấy kín hộp, mở ra không bị sứt miệng. Màu men dưới ánh nắng nhìn rất đẹp.", "Mình dùng pha latte mỗi sáng. Quai vừa hai ngón tay, phần đáy đặt trên bàn khá vững."],
            ["Giấy viết bút mực không bị lem.", "Khổ A5 bỏ vừa túi đi học.", "Bìa đẹp, mình muốn giấy dày hơn chút.", "Mình viết bút gel cả hai mặt, mực không thấm sang trang sau. Khổ sổ vừa với túi đi học.", "Gáy sổ lúc mới mở hơi cứng, dùng vài hôm thì dễ lật hơn. Giấy màu dịu, đọc lâu không chói."],
            ["Tay cầm bám tốt khi tập.", "Tạ khá gọn, dễ cất.", "Đúng mức cân và màu mình chọn.", "Tạ bọc kỹ, nhận đủ hai chiếc mình đã đặt. Lớp phủ cầm chắc tay kể cả khi lòng bàn tay hơi ướt.", "Mình dùng tập vai ở nhà, trọng lượng vừa sức. Đặt xuống sàn gỗ nhẹ nhàng nên ít gây tiếng động."]];
        int[] stars = [5,4,5,5,5, 4,5,3,5,4, 5,3,4,5,4, 5,4,5,5,5, 5,4,3,5,4, 5,4,5,5,4];
        var lines = Collections.GroupBy(x => x.Slug).Select(g => g.First()).ToArray();
        for (var g = 0; g < lines.Length; g++)
        {
            var line = lines[g];
            var variantId = SeedId($"variant/{line.Item}/mint");
            var variant = await db.ProductVariants.Include(v => v.Product).SingleOrDefaultAsync(v => v.Id == variantId, ct);
            if (variant is null) continue;
            for (var i = 0; i < names.Length; i++)
            {
                var key = $"review/{line.Item}/{i}";
                var orderId = SeedId(key + "/order");
                if (await db.Orders.AnyAsync(o => o.Id == orderId, ct)) continue;
                var buyerId = SeedId("review/customer/" + i);
                if (await db.Users.FindAsync([buyerId], ct) is null)
                {
                    var buyer = new User { Id = buyerId, FullName = names[i], Email = $"review-{i}@example.invalid" };
                    buyer.PasswordHash = new PasswordHasher<User>().HashPassword(buyer, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
                    buyer.UserRoles.Add(new() { RoleId = await db.Roles.Where(r => r.Name == RoleName.CUSTOMER).Select(r => r.Id).SingleAsync(ct) });
                    db.Users.Add(buyer);
                }
                var date = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-24 + g*3+i), DateTimeKind.Unspecified);
                var order = new Order { Id = orderId, ShopId = variant.Product.ShopId, OrderStatus = OrderStatus.COMPLETED,
                    CreatedAt = date, TotalAmount = variant.Price,
                    ShippingAddressSnapshot = JsonSerializer.Serialize(new ShippingAddress(names[i], "0900000000", "Địa chỉ minh họa")),
                    OrderGroup = new() { Id = SeedId(key + "/group"), CustomerId = buyerId, TotalAmount = variant.Price, CreatedAt = date } };
                var item = OrderItem.FromCart(new CartItem { Variant = variant, Quantity = 1 });
                item.Id = SeedId(key + "/item");
                item.Review = new Review { Id = SeedId(key), ProductId = variant.ProductId, CustomerId = buyerId,
                    CreatedAt = date.AddDays(2), Rating = stars[g*names.Length+i], Comment = comments[g][i],
                    Images = i == 0 ? [$"/images/demo/{line.Item}-mint.png"] : [],
                    SellerReply = i == 0 ? $"Cảm ơn bạn đã chia sẻ trải nghiệm với {line.Shop}!" : null };
                order.OrderItems.Add(item);
                db.Orders.Add(order);
                db.UserInteractionLogs.Add(new() { UserId = buyerId, ProductId = variant.ProductId,
                    ActionType = ActionType.REVIEW, Timestamp = date.AddDays(2) });
                await ReviewTotals.LockShop(db, order.ShopId, ct);
                await db.SaveChangesAsync(ct);
                await ReviewTotals.Refresh(db, variant.ProductId, order.ShopId, ct);
            }
        }
    }
}
