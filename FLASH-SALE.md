# Flash Sale theo khung giờ

## Cập nhật solution và PostgreSQL 17

Mở `Marketplace.sln` trong Visual Studio 2022. Kiểm tra chuỗi kết nối `ConnectionStrings:Marketplace` của WebAPI. Trong **Tools → NuGet Package Manager → Package Manager Console**, áp dụng migration có sẵn:

```powershell
Update-Database -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
Get-Migration -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

Bản bàn giao đã có `20261115084523_AddFlashSaleFeature`, designer và snapshot. Lệnh sau dành cho nhánh đang phát triển đã có entity và cấu hình mới, nhưng chưa có migration này và snapshot vẫn ở phiên bản trước:

```powershell
Add-Migration AddFlashSaleFeature -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

Chiều nâng cấp tạo `flash_sale_campaigns`, `flash_sale_items` và cột nullable `order_items.flash_sale_item_id`. Các dòng đơn hàng trước nâng cấp giữ giá đã chốt và có giá trị NULL ở cột mới. Các constraint kiểm tra giá dương, khoảng thời gian, trạng thái, tổng suất, số suất bán và mức giới hạn từng khách. Mỗi biến thể chỉ có một đăng ký trong một chiến dịch.

## Chạy và dùng thử

Chạy WebAPI và WebClient bằng F5. Với Development và `SeedDemoData=true`, bộ dữ liệu demo chuẩn có một khung giờ kéo dài 3 giờ, 8 biến thể đã duyệt và giá bằng 75% giá thường. Có tối đa 10 suất mỗi biến thể (phụ thuộc tồn kho thực tế) và tối đa 2 suất/khách. Khi khởi động lại, nếu đã có chiến dịch giao với khoảng 3 giờ tới thì seeder giữ lịch và số suất hiện có. Khi khoảng thời gian đó trống, seeder tạo đợt demo mới, giữ lịch sử cũ.

- **Trang chủ** hiển thị khung giờ, đồng hồ và số suất đã bán ngay dưới banner. Nút thêm giỏ gửi đúng biến thể. Từ 80% trở lên thanh tiến trình tiếp tục tăng; nhãn “Sắp cháy hàng” xuất hiện khi vượt 80% và chuyển thành “Hết suất” khi bán hết. Khi qua mốc bắt đầu hoặc kết thúc, trang cập nhật riêng khu vực Flash Sale.
- **Quản trị → Quản lý Flash Sale** cho phép tạo khung giờ theo giờ Việt Nam (UTC+7) và duyệt hoặc từ chối từng đăng ký. Cả hai quyết định đều ghi audit.
- **Kênh người bán → Flash Sale** liệt kê khung giờ sắp tới, cho chọn SKU, giá giảm, tổng suất và mức giới hạn. Người bán có thể xem trạng thái từng đăng ký của shop.
- **Giỏ hàng** hiển thị giá đang có hiệu lực. Giá được xác định lại lúc đặt hàng. Hết khung giờ, biến thể trở về giá thường. Trong khung giờ, khi vượt suất hoặc vượt giới hạn, checkout trả 409 và giữ giỏ để khách điều chỉnh.

## API

| Phương thức | Đường dẫn | Dữ liệu hoặc kết quả |
|---|---|---|
| POST | `/api/admin/flash-sales` | JSON gồm `title`, `startTime`, `endTime` định dạng ISO 8601 có múi giờ. Trả 201 và ID |
| GET | `/api/admin/flash-sales`, `/api/admin/flash-sales/items` | Danh sách chiến dịch và đăng ký chờ duyệt |
| PUT | `/api/admin/flash-sales/items/{itemId}/approve` | `{"status":"APPROVED"}` hoặc `{"status":"REJECTED"}` |
| GET | `/api/seller/flash-sales/available`, `/api/seller/flash-sales/items` | Khung giờ còn mở đăng ký và các đăng ký thuộc shop |
| POST | `/api/seller/flash-sales/{campaignId}/register` | JSON gồm `variantId`, `flashPrice`, `totalQuota`, `maxPerUser`. Trả 201 và ID |
| GET | `/api/flash-sales/current` | Công khai. Trả `campaign` (gồm `startTime`, `endTime`, `status`), `items` và `serverTime`. Mỗi SKU có giá gốc, giá sale, số suất và `soldPercent` (0–100). Trả 204 khi không còn lịch |
| POST | `/api/orders/checkout` | Giữ hợp đồng địa chỉ và kết quả checkout hiện tại. Lịch sử đơn dùng giá đã chốt cho từng dòng |

Khoảng hiệu lực là `start_time <= now <= end_time`. Nếu nhiều chiến dịch đang diễn ra, trang chủ chọn chiến dịch bắt đầu sớm nhất; nếu chưa có chiến dịch đang chạy, chọn khung giờ sắp tới gần nhất. Checkout xét tất cả khung giờ đang chạy. Hai đăng ký của cùng một SKU ở hai khung giờ giao nhau không thể cùng được duyệt. Giá Flash Sale chỉ áp dụng khi thấp hơn giá bán hiện tại.

Đăng ký và duyệt kiểm tra tồn kho, không trừ hàng trước. Checkout khóa giỏ của khách và các dòng tồn kho theo thứ tự ID. Sau khi lấy khóa, hệ thống kiểm tra quota và lịch sử các đơn chưa hủy theo khách và suất đăng ký. Giá chốt, đơn hàng, tồn kho và số suất bán được ghi trong cùng giao dịch. Hủy một đơn đang chờ hoàn đúng số lượng cho các SKU và suất sale gắn với đơn đó.

## Mã nguồn theo project

| Project | File chính |
|---|---|
| Domain | `Entities/FlashSaleCampaign.cs`, `Entities/FlashSaleItem.cs`, `Entities/OrderItem.cs` |
| Application | `Shopping/FlashSales.cs` |
| Infrastructure | `AppDbContext.cs`, `Configurations/FlashSaleConfiguration.cs`, `Configurations/OrderItemConfiguration.cs`, `FlashSaleQueries.cs`, `ShoppingService.cs`, `OrderOperations.cs`, migration, designer và snapshot trong `Migrations/` |
| WebAPI | `Controllers/FlashSalesController.cs`, `FlashSaleClock.cs`, `Program.cs`, `Seed/DataSeeder.cs`, `Seed/DataSeeder.FlashSale.cs` |
| WebClient | `Controllers/FlashSalesController.cs`, `Models/FlashSalePages.cs`, `ViewComponents/FlashSaleViewComponent.cs`, `Views/FlashSales/`, `Views/Shared/Components/FlashSale/Default.cshtml`, trang Index trong Home, Seller và Admin, `wwwroot/css/flash-sale.css`, `wwwroot/js/flash-sale.js` |
| Kiểm thử | `tests/test_flash_sales.py`, `tests/run_postgres.py` |

Từ thư mục chứa solution, chạy `dotnet build Marketplace.sln -c Release`, rồi `python tests/run_postgres.py --chat`. Bộ kiểm thử dùng PostgreSQL tạm, tạo 6 khách riêng tranh 10 suất, kiểm tra số đơn thành công, giá trên đơn, giới hạn theo lịch sử, giỏ sau lỗi, tồn kho và hoàn suất khi hủy.
