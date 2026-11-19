# Gói 3 · Đánh giá sản phẩm và phản hồi của gian hàng

## Chạy với dữ liệu hiện có

Mở `Marketplace.sln`. Kiểm tra `ConnectionStrings:Marketplace` của WebAPI rồi chạy trong **Tools → NuGet Package Manager → Package Manager Console**:

```powershell
Update-Database -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
Get-Migration -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

Migration `20261115073502_AddProductReviews` có sẵn trong solution. Nó giữ khóa duy nhất trên `reviews.order_item_id`, chuyển các đánh giá công khai cũ từ `VISIBLE` sang `ACTIVE`, giữ các bản ghi `HIDDEN` và tính lại điểm từ các đánh giá công khai. API và FastAPI dùng cùng trạng thái mới, vì vậy hãy khởi động lại cả hai sau khi cập nhật.

Bản cập nhật nhận xét bằng chữ dùng các cột hiện có trong bảng `reviews`. Nếu database đã áp dụng `AddProductReviews`, chỉ cần khởi động lại với bản mã nguồn mới.

Chạy WebAPI và WebClient bằng F5 với profile Development, bật `SeedDemoData` theo cấu hình hiện có. Seeder tạo 30 đánh giá trên 6 sản phẩm màu xanh bạc hà, gồm áo thun, túi tote, chuột, ly gốm, sổ tay và tạ tay. Mỗi đánh giá gắn với một món trong đơn mẫu đã hoàn tất. Năm người mua mẫu có tài khoản riêng. Sáu đánh giá dùng ảnh sản phẩm demo làm ảnh minh họa và có phản hồi mẫu. Seeder dùng ID cố định để giữ các chỉnh sửa sau lần khởi tạo. Các đơn mẫu được tính vào báo cáo doanh thu và số đơn của 6 shop.

## Dùng thử

- Mở sản phẩm **Áo thun cổ tròn – Xanh bạc hà** để xem điểm trung bình, tỷ lệ sao, ảnh và phản hồi. Bộ lọc và phân trang chỉ thay đổi danh sách bên dưới. Thống kê luôn lấy toàn bộ đánh giá đang hiển thị của sản phẩm.
- Đăng nhập người mua, vào **Đơn mua**, chọn **Đánh giá** cạnh một món trong đơn hoàn tất. Chọn 1–5 sao, nhập nhận xét và chọn tối đa 3 ảnh. WebClient thu nhỏ ảnh và chuyển thành JPEG trước khi gửi.
- Tại **Kênh người bán → Quản lý Đánh giá**, chọn **Trả lời**. Chủ shop có thể sửa phản hồi của mình tại cùng vị trí. Dashboard hiện tại của solution là `Views/Seller/Index.cshtml`.
- Tại **Quản trị hệ thống → Kiểm duyệt đánh giá**, quản trị viên có thể ẩn hoặc hiển thị lại. Điểm và số lượt của cả sản phẩm lẫn shop được cập nhật trong cùng giao dịch với thao tác và bản ghi kiểm toán.

## API

| Phương thức | Đường dẫn | Nội dung |
|---|---|---|
| POST | `/api/reviews` | JWT khách hàng. JSON gồm `orderItemId`, `rating`, `comment`, `images` tùy chọn. Tạo thành công trả 201 và ID |
| GET | `/api/products/{productId}/reviews` | Công khai. `rating=1..5`, `withImages=true`, `withComment=true`, `page=1`, `pageSize=8` (tối đa 25). `starCounts[0..4]` tương ứng 1–5 sao |
| GET | `/api/seller/reviews?page=1` | Đánh giá đang hiển thị thuộc shop đang đăng nhập, mới nhất trước |
| PUT | `/api/seller/reviews/{reviewId}/reply` | JWT người bán. JSON ví dụ `{"reply":"Cảm ơn bạn đã góp ý!"}` |
| GET | `/api/admin/reviews?hidden=true&page=1` | Danh sách quản trị. `hidden=false` lấy các đánh giá đang hiển thị |
| PUT | `/api/admin/reviews/{reviewId}/status` | JWT quản trị viên. JSON `{"status":"HIDDEN"}` hoặc `{"status":"ACTIVE"}` |

API nhận tối đa 3 ảnh mỗi đánh giá ở dạng URL HTTPS hoặc chuỗi JPEG `data:image/jpeg;base64,...` tối đa 275.000 ký tự mỗi ảnh. Nhận xét tùy chọn, tối đa 1.000 ký tự, phản hồi tối đa 2.000 ký tự. Đơn của người khác trả 404, đơn chưa hoàn tất hoặc món đã được đánh giá trả 409. Kiểm duyệt ẩn vẫn giữ quyền sở hữu và ràng buộc một đánh giá trên từng món.

## Mã nguồn theo project

| Project | File và trách nhiệm |
|---|---|
| Domain | `Entities/Review.cs`, `Entities/Product.cs`, `Entities/Shop.cs`, `Enums/MarketplaceEnums.cs` — ảnh, điểm, số lượt, trạng thái và hành vi REVIEW |
| Application | `Shopping/Management.cs`, `Shopping/ReviewContracts.cs`, `Catalog/ProductSummary.cs` — hợp đồng nhập liệu, thống kê, phân trang và thẻ sản phẩm |
| Infrastructure | `ReviewTotals.cs`, `CatalogQueries.cs`, cấu hình Review/Product/Shop/UserInteractionLog trong `Configurations/`, migration AddProductReviews cùng designer và snapshot trong `Migrations/` |
| WebAPI | `Controllers/ReviewsController.cs`, `Controllers/AfterSalesController.cs`, `Seed/DataSeeder.cs`, `Seed/DataSeeder.Reviews.cs` — quyền, giao dịch, kiểm duyệt và dữ liệu mẫu |
| WebClient | `Controllers/ProductsController.cs`, `SellerController.cs`, `AdminController.cs`, action Review có sẵn của `OrdersController.cs`; các view trong `Views/Orders`, `Views/Products`, `Views/Seller`, `Views/Admin` và `Views/Shared`; `wwwroot/js/review-form.js`, `wwwroot/css/reviews.css` |
| FastAPI | `Marketplace.AIService/main.py` — đọc đánh giá ACTIVE và tính trọng số cho sự kiện REVIEW |
| Kiểm thử | `tests/test_step3.py`, `tests/test_web.py`, `tests/review_totals.sql`, `tests/run_postgres.py` |

Từ thư mục chứa solution, chạy `dotnet build Marketplace.sln -c Release` rồi `python tests/run_postgres.py --chat` để chạy các kiểm thử API, giao diện MVC, PostgreSQL và chat trên một cụm dữ liệu tạm. Các phụ thuộc kiểm thử chat được ghi trong `GOI-2.md`.
