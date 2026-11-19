# Bước 2 · Mua hàng và xác thực

Mở Marketplace.sln bằng Visual Studio 2022, restore NuGet, chạy cả Marketplace.WebAPI và Marketplace.WebClient với profile http. Nếu đang dùng database Bước 1 đã có 120 sản phẩm, giữ kết nối PostgreSQL đã thiết lập. Các cột cần cho checkout đã có trong migration InitialCreate.

Web: http://localhost:5115/  
Swagger: http://localhost:5116/swagger

Trong Development, DataSeeder tạo khách hàng buyer@example.invalid với mật khẩu Marketplace123!. Để thử, mở trang chi tiết hai sản phẩm từ hai gian hàng, thêm mỗi sản phẩm vào giỏ, nhập người nhận, điện thoại và địa chỉ đầy đủ, rồi đặt hàng. Trang Đơn mua hiển thị hai đơn con dưới cùng mã nhóm. Giá tiền tính theo VND. Bước này dùng phí giao hàng và giảm giá bằng 0.

## API cho Web và Mobile

| Phương thức và đường dẫn | Đầu vào hoặc kết quả |
|---|---|
| POST /api/auth/register | JSON fullName, email, password (8–128 ký tự). Trả 201, cấp vai trò CUSTOMER và tạo giỏ trống. |
| POST /api/auth/login | JSON email, password. Trả token, expiresAt, userId, roles. |
| GET /api/products | keyword, categoryId và sort là tùy chọn. sort nhận price_asc hoặc price_desc. Mặc định xếp theo tên. |
| GET /api/products/{id} | product, description, categoryId, shop, productVariants (attributes.color đối với dữ liệu mẫu), relatedProducts. |
| GET /api/cart | Danh sách shops, số lượng quantity và tổng total. |
| POST /api/cart/items | JSON variantId, quantity là số nguyên dương. Cộng dồn nếu đã có biến thể. Trả 204. |
| DELETE /api/cart/items/{variantId} | Xóa biến thể khỏi giỏ hiện tại. Trả 204. |
| POST /api/orders/checkout | JSON recipientName, phone, detailAddress. Trả orderGroupId, total, orderCount. Giỏ rỗng hoặc thiếu hàng trả 409. |
| GET /api/orders/my-orders | Mỗi phần tử là một đơn của shop, có orderGroupId, items và shippingAddress. |

Các endpoint cart và orders yêu cầu CUSTOMER. Trong Swagger, đăng nhập rồi dán giá trị token vào Authorize. Mobile gửi header Authorization với giá trị Bearer theo sau bởi dấu cách và token. WebClient lưu JWT trong Session phía máy chủ. Token có thời hạn 2 giờ. Trong Development, nếu thiếu Jwt:Key, API sinh khóa ngẫu nhiên khi khởi động, vì vậy cần đăng nhập lại sau khi khởi động lại API. Để duy trì khóa giữa các lần chạy, đặt Jwt:Key trong User Secrets bằng chuỗi base64 của ít nhất 32 byte ngẫu nhiên. Ngoài Development phải cấu hình khóa.

## Mã nguồn theo project

Các đường dẫn dưới đây tính từ thư mục chứa Marketplace.sln. Mỗi file chứa mã đầy đủ.

- **Marketplace.Domain** — Entities/OrderItem.cs và Entities/OrderItem.Snapshot.cs tạo snapshot từ dòng giỏ hàng.
- **Marketplace.Application** — Shopping/Contracts.cs định nghĩa hợp đồng đăng nhập, sản phẩm, giỏ, giao hàng và đơn mua; Catalog/ProductSummary.cs giữ hợp đồng danh mục Bước 1.
- **Marketplace.Infrastructure** — CatalogQueries.cs dùng chung điều kiện sản phẩm đang bán. ShoppingService.cs xử lý giỏ, khóa hàng trong PostgreSQL, checkout và đọc lịch sử.
- **Marketplace.WebAPI** — Program.cs, AuthTokens.cs, Marketplace.WebAPI.csproj, Seed/DataSeeder.cs và Controllers/{Auth,Products,Cart,Orders}Controller.cs.
- **Marketplace.WebClient** — Program.cs, Marketplace.WebClient.csproj, Services/ShopApi.cs, Controllers/{Home,Account,Products,Cart,Orders}Controller.cs. Các trang nằm ở Views/Shared/_Layout.cshtml, Views/_ViewImports.cshtml, Views/Home/Index.cshtml, Views/Account/Login.cshtml, Views/Account/Register.cshtml, Views/Products/Detail.cshtml, Views/Cart/Index.cshtml và Views/Orders/Index.cshtml.
