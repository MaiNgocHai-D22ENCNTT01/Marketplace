# Marketplace · Bước 3

## Chạy trên Visual Studio 2022

1. Giải nén ZIP và mở Marketplace.sln. Dùng workload ASP.NET and web development, .NET 8 SDK và PostgreSQL. Restore NuGet Packages.
2. Giữ cấu hình kết nối PostgreSQL của Bước 2. WebAPI tiếp tục dùng User Secrets ID `marketplace-api-local-development` và khóa `ConnectionStrings:Marketplace`. Nếu dùng máy mới, tạo database và cấu hình kết nối như mục PostgreSQL trong HUONG-DAN.md.
3. Trong **Tools → NuGet Package Manager → Package Manager Console**, chạy lệnh dưới đây trước khi khởi động. Với database Bước 2, migration bổ sung trạng thái CONFIRMED vào ràng buộc của bảng orders. Với database mới, EF áp dụng cả hai migration.

~~~powershell
Update-Database -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
~~~

4. Trong **Configure Startup Projects**, chọn Multiple startup projects, đặt Marketplace.WebAPI và Marketplace.WebClient thành Start, dùng profile **http** và môi trường **Development**. Nhấn F5. Mở [WebClient](http://localhost:5115/) hoặc [Swagger](http://localhost:5116/swagger).
5. Seeder nâng cấp chủ shop mẫu **Studio Vải** thành tài khoản seller dưới đây, giữ mã gian hàng, sản phẩm và đơn hàng đã có. Ba tài khoản mẫu dùng mật khẩu **Marketplace123!**. Đăng nhập các vai trò bằng các trình duyệt hoặc hồ sơ trình duyệt riêng để thử cùng lúc.

| Vai trò | Email | Trang sau đăng nhập |
|---|---|---|
| Khách hàng | buyer@example.invalid | Đơn mua |
| Người bán | seller@example.invalid | Kênh người bán |
| Quản trị viên | admin@example.invalid | Quản trị |

## Thử luồng bán hàng và hậu mãi

1. **Seller** vào Kênh người bán → Đăng sản phẩm, nhập tên, danh mục, chọn ảnh demo và khai báo SKU riêng, giá, tồn kho. Có thể thêm nhiều biến thể. Chọn Chỉnh sửa để sửa giá hoặc tồn từng biến thể.
2. **Buyer** tìm sản phẩm vừa đăng, đặt mua 2 món. Mở Đơn mua → Hủy đơn khi còn PENDING. **Seller** kiểm tra tồn kho tăng lại 2. Nếu đặt từ hai shop trong một lần, mỗi đơn con có trạng thái riêng.
3. **Buyer** đặt đơn khác. **Seller** lần lượt chuyển đơn sang CONFIRMED rồi SHIPPING. **Buyer** bấm Xác nhận nhận hàng, chọn 1–5 sao và nhập bình luận cho từng dòng hàng. Khi đánh giá đã được lưu, trang hiển thị “Đã đánh giá”. Seller cũng có thể hoàn tất đơn SHIPPING từ kênh của mình.
4. **Seller** xem doanh thu và số lượng bán chạy, tính từ các đơn COMPLETED. Tổng đơn bao gồm mọi trạng thái. Thử Ngừng bán trên một sản phẩm có lịch sử đặt hàng và một sản phẩm chưa bán. Trạng thái tương ứng là DISCONTINUED và HIDDEN. Đơn mua vẫn giữ tên và giá tại thời điểm đặt.
5. **Buyer** vào Đơn mua → Đăng ký mở gian hàng, gửi tên mới. **Admin** vào Quản trị → Gian hàng chờ duyệt → Duyệt gian hàng. Buyer đăng xuất và đăng nhập lại để lấy JWT có cả CUSTOMER và SELLER rồi mở Kênh người bán của mình.
6. **Admin** vào Quản trị, mở Quản lý người dùng để khóa/mở tài khoản, hoặc Kiểm duyệt sản phẩm để ẩn sản phẩm. Bảng nhật ký hiển thị người thực hiện, đối tượng và trạng thái trước/sau. Thử khóa trên một tài khoản khách mẫu để vẫn có thể tiếp tục dùng tài khoản Admin.

## Gọi từ ứng dụng mobile

Đăng nhập qua POST /api/auth/login và gửi Authorization: Bearer <token> cho từng yêu cầu. Trong Swagger, dùng nút Authorize. WebClient giữ JWT trong Session phía máy chủ. Các thao tác hậu mãi dùng orderId của đơn con và orderItemId trong GET /api/orders/my-orders.

| Nhóm | Endpoint |
|---|---|
| Customer | PUT /api/orders/{orderId}/cancel · PUT /api/orders/{orderId}/confirm-delivery · POST /api/reviews · POST /api/seller/register-shop |
| Seller | GET, POST /api/seller/products · PUT, DELETE /api/seller/products/{id} · GET /api/seller/categories |
| Seller | GET /api/seller/orders · PUT /api/seller/orders/{orderId}/status · GET /api/seller/analytics |
| Admin | GET /api/admin/shops/pending · PUT /api/admin/shops/{shopId}/approve · GET /api/admin/users · PUT /api/admin/users/{userId}/status |
| Admin | GET /api/admin/products · PUT /api/admin/products/{productId}/status · GET /api/admin/dashboard · GET /api/admin/audit-logs |

Swagger hiển thị cấu trúc JSON cho từng lệnh. Khi sửa sản phẩm, gửi lại các biến thể hiện có cùng ID; để ID trống cho biến thể mới. Giá hiển thị của sản phẩm bằng giá biến thể thấp nhất. Dữ liệu bán hàng được lọc theo danh tính trong JWT. Truy cập tài nguyên của shop khác trả 404. Sai vai trò trả 403. Trạng thái xung đột hoặc đánh giá trùng trả 409.

## Chạy lại bộ kiểm thử

Chạy các lệnh sau từ thư mục chứa Marketplace.sln. Cần Python 3.9+ và bộ công cụ PostgreSQL có initdb và pg_ctl. Trên Windows bộ chạy mặc định tìm PostgreSQL 17 trong Program Files, có thể đặt PG_BIN để chỉ thư mục bin khác.

~~~powershell
dotnet clean Marketplace.sln -c Release
dotnet build Marketplace.sln -c Release -warnaserror
python tests/run_postgres.py
~~~

Bộ chạy tạo một cụm PostgreSQL tạm có mật khẩu, áp dụng migration, khởi động hai ứng dụng trên địa chỉ loopback rồi chạy kiểm thử qua HTTP. Dữ liệu được kiểm tra gồm quyền theo shop, hủy đơn đồng thời, tồn kho, đánh giá, số liệu doanh thu và audit log. Kiểm thử MVC đăng nhập từng vai trò và gửi form tạo sản phẩm với Session và mã chống giả mạo. Các tiến trình và dữ liệu kiểm thử được dọn sau khi chạy.
