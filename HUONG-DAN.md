# Marketplace · Bước 1 · EF Core, PostgreSQL và danh mục mẫu

## Mở bản mẫu trong Visual Studio 2022

1. Giải nén toàn bộ ZIP. Mở Marketplace.sln. Trong Visual Studio Installer, kiểm tra workload **ASP.NET and web development** và .NET 8 SDK.
2. Bấm chuột phải solution → Restore NuGet Packages. Chọn Build → Build Solution (Ctrl+Shift+B).
3. Bấm chuột phải solution → Configure Startup Projects (hoặc Properties → Startup Project). Chọn Multiple startup projects. Đặt WebAPI và WebClient thành Start, API đứng trước. Chọn launch profile http.
4. Nhấn F5. Truy cập http://localhost:5116/swagger và http://localhost:5115/. Hoàn tất Phần 3 bên dưới trước khi nhấn F5 lần đầu. Trang MVC gọi GET /api/products bằng HttpClient. Nếu API khởi động sau MVC, tải lại trang.

## Tạo lại từ đầu

1. File → New → Project → Blank Solution. Đặt tên Marketplace.
2. Chuột phải solution → Add → New Project. Tạo từng project trong bảng dưới đây, tất cả chọn .NET 8.0. Với Web API chọn Use controllers và Enable OpenAPI support. Hai project web chọn Authentication type = None tại bước này.
3. Bấm chuột phải Dependencies → Add Project Reference, chọn theo bảng.

| Project | Template | Tham chiếu |
|---|---|---|
| Marketplace.Domain | Class Library | Không có |
| Marketplace.Application | Class Library | Domain |
| Marketplace.Infrastructure | Class Library | Domain và Application |
| Marketplace.WebAPI | ASP.NET Core Web API | Infrastructure và Application |
| Marketplace.WebClient | ASP.NET Core Web App (Model-View-Controller) | Gọi API bằng HTTP |

4. Mở Tools → NuGet Package Manager → Package Manager Console. Khi tự tạo project, chạy các dòng sau. Với ZIP có sẵn, chỉ cần restore.

~~~powershell
Install-Package Npgsql.EntityFrameworkCore.PostgreSQL -Version 8.0.11 -ProjectName Marketplace.Infrastructure
Install-Package Microsoft.EntityFrameworkCore.Relational -Version 8.0.22 -ProjectName Marketplace.Infrastructure
Install-Package Microsoft.EntityFrameworkCore.Design -Version 8.0.22 -ProjectName Marketplace.WebAPI
Install-Package Microsoft.EntityFrameworkCore.Tools -Version 8.0.22 -ProjectName Marketplace.WebAPI
Install-Package Swashbuckle.AspNetCore -Version 6.6.2 -ProjectName Marketplace.WebAPI
~~~

5. Chép Entities và Enums từ ZIP vào Domain. Chép AppDbContext.cs và Configurations vào Infrastructure. Với hai project web, dùng Program.cs, Controllers, appsettings.json và Properties/launchSettings.json trong ZIP. Dùng Views của WebClient cho giao diện Razor. Build và F5 theo hướng dẫn trên.

## Kiểm tra mô hình

AppDbContext khai báo 24 DbSet. ApplyConfigurationsFromAssembly nạp 24 cấu hình, mỗi file tương ứng một bảng. Tên cột giữ dạng snake_case. Các enum dùng chuỗi viết hoa đúng với CHECK. Các trường JSON được lưu bằng kiểu json của PostgreSQL, feature_vector dùng text.

| Quy tắc | Cấu hình và cách sử dụng |
|---|---|
| Một user có tối đa một shop | ShopConfiguration đặt unique index trên user_id. |
| Tồn kho không âm | ProductVariantConfiguration đặt CHECK trên stock_quantity. Service checkout cần trừ kho có điều kiện trong transaction khi nhiều khách cùng mua. |
| Tách đơn theo shop | Service checkout nhóm SKU theo shop rồi tạo một OrderGroup và một Order cho mỗi shop. Unique index bổ sung trên (order_group_id, shop_id) ngăn tạo trùng đơn con. Service phải kiểm tra SKU thuộc shop của đơn. |
| Bảo toàn lịch sử mua hàng | OrderItemConfiguration dùng RESTRICT cho variant_id. Xóa sản phẩm cha cũng bị chặn nếu biến thể của nó còn nằm trong đơn. |
| Snapshot | Checkout phải sao chép tên sản phẩm, thuộc tính biến thể, đơn giá vào OrderItem và địa chỉ nhận hàng vào Order. Khi xem lịch sử, đọc các cột snapshot. |
| RBAC | UserRole và RolePermission có khóa chính ghép. JWT và kiểm tra quyền trên endpoint được triển khai ở bước API. |

## Phần 3 · Tạo database bằng migration đã có

1. Mở Marketplace.sln bằng VS 2022 và build solution. Mở pgAdmin, kiểm tra có kết nối được PostgreSQL của bạn. Ghi lại host, port và tên tài khoản. Tài khoản cần quyền tạo database nếu marketplace_mvp chưa tồn tại.
2. Bấm phải Marketplace.WebAPI → **Manage User Secrets**. Dán JSON sau vào secrets.json. Thay CHANGEME bằng mật khẩu PostgreSQL trên máy bạn, đổi port hoặc username nếu cần. File appsettings.json đã có khóa cùng tên và giữ các giá trị mặc định không chứa mật khẩu.

~~~json
{
  "ConnectionStrings": {
    "Marketplace": "Host=localhost;Port=5432;Database=marketplace_mvp;Username=postgres;Password=CHANGEME"
  }
}
~~~

3. Bấm phải Marketplace.WebAPI → Set as Startup Project. Mở Tools → NuGet Package Manager → Package Manager Console. Trong bản này đã có InitialCreate và AppDbContextModelSnapshot. Chạy lệnh sau để áp dụng migration đã đóng gói.

~~~powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
Update-Database -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
~~~

4. Trong pgAdmin, nhấn Refresh tại Databases, mở marketplace_mvp → Schemas → public → Tables. Có 24 bảng nghiệp vụ và bảng __EFMigrationsHistory. Khi bạn chỉnh thiết kế lần sau, tạo migration tên mới bằng Add-Migration, sau đó mới Update-Database. Nếu tự học từ bản phần 1–2 còn chưa có migration, lệnh tạo lần đầu là:

~~~powershell
Add-Migration InitialCreate -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
~~~

## Phần 4 · Chạy và kiểm tra sản phẩm mẫu

1. Đặt lại Multiple startup projects với WebAPI và WebClient cùng Start, profile http. Nhấn F5 sau khi hoàn tất Update-Database.
2. Trong môi trường Development, API bắt đầu một transaction và lấy khóa PostgreSQL dành cho tác vụ seed. DataSeeder dùng ID ổn định để bổ sung sản phẩm demo còn thiếu và giữ giá/tồn kho đã sửa của sản phẩm còn tồn tại. Nếu có lỗi giữa chừng, transaction rollback. Các tài khoản chủ shop mẫu dùng mật khẩu ngẫu nhiên.
3. Trang http://localhost:5115/ hiển thị danh mục. Để xem dữ liệu thô, mở http://localhost:5116/api/products. Ảnh minh họa PNG nằm trong WebAPI/wwwroot/images/demo, mỗi mẫu có ảnh đúng hình dáng và màu. Địa chỉ ảnh trong JSON là đường dẫn tương đối theo host của API.
4. Trên database mới có 120 sản phẩm từ 12 dòng hàng với 10 màu mỗi dòng, thuộc 6 ngành hàng và 6 shop. Mỗi sản phẩm có một SKU và một ảnh chính. Truy vấn kiểm tra sau chạy trong Query Tool của marketplace_mvp:

~~~sql
SELECT COUNT(DISTINCT p.id) AS products, COUNT(DISTINCT v.id) AS skus,
       COUNT(DISTINCT i.id) AS primary_images, COUNT(DISTINCT p.shop_id) AS shops,
       COUNT(DISTINCT p.category_id) AS categories
FROM products p
JOIN product_variants v ON v.product_id = p.id AND v.sku LIKE 'DEMO-%'
LEFT JOIN product_images i ON i.product_id = p.id AND i.is_primary;
~~~

5. Dừng ứng dụng, nhấn F5 lần hai rồi chạy lại truy vấn. Trên database demo chưa chỉnh sửa, cả hai lần đều phải trả về 120, 120, 120, 6, 6. Khi muốn giữ nguyên cả những sản phẩm mẫu bạn đã chủ động xóa, thêm "SeedDemoData": false ở cấp gốc trong Marketplace.WebAPI/appsettings.Development.json.

Nếu API báo lỗi 28P01, kiểm tra username và password trong User Secrets. Nếu báo connection refused, kiểm tra dịch vụ PostgreSQL và port. Nếu API báo còn migration đang chờ, chạy Update-Database trước. API kiểm tra migration và chỉ ghi demo khi chạy Development; môi trường khác không seed tự động.
