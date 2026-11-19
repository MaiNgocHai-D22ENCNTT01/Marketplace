# Trang gian hàng công khai

## Chạy trên Visual Studio 2022

Mở `Marketplace.sln` bằng Visual Studio 2022 có workload ASP.NET và SDK .NET 8. Trong Package Manager Console, kiểm tra WebAPI đang trỏ đến đúng PostgreSQL 17 rồi chạy:

```powershell
Update-Database -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

Migration `20261115083726_AddProductCreatedAt` đã nằm trong source. Nó thêm `products.created_at` với giá trị mặc định `CURRENT_TIMESTAMP` và chỉ mục theo shop, thời điểm, ID. Sản phẩm có trước nâng cấp nhận cùng mốc thời gian áp dụng migration vì schema cũ chưa ghi ngày đăng. Các sản phẩm thêm sau dùng thời điểm ghi vào database. Không cần tạo lại dữ liệu mẫu.

Chạy WebAPI và WebClient. Trên trang chủ, bấm tên gian hàng ở chân thẻ sản phẩm, hoặc mở chi tiết sản phẩm và bấm **Xem Shop**. Khách vãng lai có thể mở trực tiếp `/Shops/Detail/{shopId}`. Logo trống dùng biểu tượng cửa hàng. Huy hiệu **Đang hoạt động** lấy theo trạng thái shop. Tên, mô tả, ngày tham gia và điểm lấy từ dữ liệu của shop.

## API

`GET /api/shops/{shopId}?categoryId=...&keyword=...&sort=price_asc&page=1&pageSize=12`

- Các tham số truy vấn là tùy chọn. `categoryId` là UUID, `keyword` tìm theo tên. `sort` nhận `newest` (mặc định), `price_asc` hoặc `price_desc`. Trang bắt đầu từ 1, mặc định 12 sản phẩm, tối đa 48.
- Thông tin đầu trang gồm `id`, `shopName`, `description`, `logoUrl`, `ratingAvg`, `reviewCount`, `createdAt`, `status` và `totalProducts`. Danh mục có sản phẩm đang hiển thị nằm trong `categories`. `filteredTotal` là số kết quả sau lọc và `products` là danh sách trên trang hiện tại.
- Shop đang chờ duyệt, bị khóa, có chủ bị khóa hoặc không tồn tại trả 404. Shop hoạt động nhưng chưa có sản phẩm trả 200 với danh sách trống. Sản phẩm thuộc shop khác, sản phẩm ngừng bán và sản phẩm trong danh mục ẩn không xuất hiện.

## File theo project

| Project | File |
|---|---|
| Application | `Catalog/PublicShop.cs`, `Catalog/ProductSummary.cs` |
| Domain | `Entities/Product.cs` |
| Infrastructure | `CatalogQueries.cs`, `Configurations/ProductConfiguration.cs`, migration `AddProductCreatedAt`, designer và `AppDbContextModelSnapshot.cs` |
| WebAPI | `Controllers/ShopsController.cs` |
| WebClient | `Controllers/ShopsController.cs`, `Controllers/HomeController.cs`, `Views/Shops/Detail.cshtml`, `Views/Home/Index.cshtml`, `Views/Products/Detail.cshtml`, `Views/Shared/_ProductCard.cshtml`, `wwwroot/css/shop.css` |
| Kiểm thử | `tests/test_step3.py`, `tests/test_web.py` |

Từ thư mục chứa solution, dùng `dotnet build Marketplace.sln -c Release`, rồi `python tests/run_postgres.py --chat` để kiểm tra bằng cụm PostgreSQL tạm. Các phụ thuộc của bộ kiểm thử chat được ghi trong `GOI-2.md`.
