# Gói 1 · Wishlist và gợi ý tìm kiếm

## Cập nhật cơ sở dữ liệu hiện có

Mở `Marketplace.sln` trong Visual Studio. Kiểm tra `ConnectionStrings:Marketplace` của WebAPI trỏ đến đúng PostgreSQL 17 đang dùng. Mở **Tools → NuGet Package Manager → Package Manager Console** và chạy:

```powershell
Update-Database -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
Get-Migration -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

Kiểm tra `20261114114245_AddWishlistFeature` đã được áp dụng. Migration tạo bảng `wishlist_items`, hai khóa ngoại, chỉ mục duy nhất trên `(user_id, product_id)` và mở rộng tập giá trị được phép của `user_interaction_logs.action_type`. Chiều nâng cấp giữ dữ liệu hiện có. Sau đó khởi động lại WebAPI, WebClient và FastAPI nếu đang chạy.

**Lệnh tạo migration cho nhánh phát triển.** Chỉ chạy lệnh dưới đây trên nhánh đã có thay đổi entity/configuration nhưng chưa có migration tương ứng và snapshot vẫn ở phiên bản trước. Bản bàn giao đã chứa cả migration và snapshot nên chỉ cần lệnh cập nhật ở trên.

```powershell
Add-Migration AddWishlistFeature -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

## Tra cứu mã nguồn

| Project | File chính | Chức năng |
|---|---|---|
| Marketplace.Domain | `Entities/WishlistItem.cs`, `Enums/MarketplaceEnums.cs` | Entity và hai hành vi thêm/bỏ yêu thích |
| Marketplace.Application | `Catalog/WishlistStatus.cs` | Trạng thái trả về sau mỗi lần bấm |
| Marketplace.Infrastructure | `AppDbContext.cs`, `Configurations/WishlistItemConfiguration.cs`, `Configurations/UserInteractionLogConfiguration.cs`, `Migrations/` | Ánh xạ PostgreSQL, ràng buộc, migration và snapshot |
| Marketplace.WebAPI | `Controllers/WishlistsController.cs`, `Controllers/ProductsController.Suggestions.cs` | Toggle và ghi log trong cùng giao dịch, lấy danh sách cá nhân, tìm theo tên hoặc danh mục |
| Marketplace.WebClient | `Services/ShopApi.cs`, `Controllers/WishlistsController.cs`, `Controllers/ProductsController.cs` | Gọi API bằng phiên người dùng và trả về giao diện MVC |
| Marketplace.WebClient | `Views/Shared/_Layout.cshtml`, `_ProductCard.cshtml`, `_WishlistHeart.cshtml` trong cùng thư mục, `Views/Products/Detail.cshtml`, `Views/Products/_Suggestions.cshtml`, `Views/Wishlists/Index.cshtml` | Menu, ô tìm kiếm, nút trái tim và trang danh sách |
| Marketplace.WebClient | `wwwroot/js/wishlist.js`, `wwwroot/js/suggestions.js`, `wwwroot/css/wishlist-search.css` | Đồng bộ màu trái tim, chờ 180 ms sau khi gõ, hủy truy vấn cũ và hiển thị gợi ý |
| Marketplace.AIService | `main.py` | Dùng trạng thái yêu thích gần nhất của từng cặp người dùng/sản phẩm khi tính điểm |

## Kiểm tra

Trong thư mục chứa solution, chạy:

```powershell
dotnet build Marketplace.sln -c Release
python tests/run_postgres.py
```

Bộ kiểm thử dùng PostgreSQL 17 cục bộ và khởi tạo cụm dữ liệu tạm. Nếu PostgreSQL nằm ngoài thư mục cài mặc định, đặt biến môi trường `PG_BIN` trỏ tới thư mục chứa `initdb.exe`.

- Với JWT của người mua, gọi `POST /api/wishlists/toggle/{productId}` hai lần. Trường `isWishlisted` lần lượt là `true` và `false` nếu sản phẩm ban đầu chưa được thích. `GET /api/wishlists` trả về danh sách của chính tài khoản đó.
- Gõ `ao thun` hoặc `dien tu` ở Header. Từ hai ký tự, API `GET /api/products/suggestions?q=...` trả tối đa sáu sản phẩm đang hiển thị, khớp tên hoặc danh mục. Mỗi dòng gợi ý dẫn đến trang chi tiết, có tên, danh mục, giá và ảnh nếu có.
- Bấm trái tim trên thẻ sản phẩm, mở **Tài khoản → Danh sách yêu thích**, rồi bỏ thích tại trang chi tiết. Khi mở lại danh sách, sản phẩm đã được gỡ.
