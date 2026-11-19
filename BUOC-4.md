# Marketplace Bước 4

## Chạy với database Bước 3

1. Giải nén, mở Marketplace.sln. Giữ nguyên kết nối của Marketplace.WebAPI trong User Secrets. Trên máy mới, tạo database PostgreSQL 17 và cấu hình kết nối như trong HUONG-DAN.md. Dùng Windows x64, SDK hỗ trợ .NET 8 và Python 3.14 64-bit.
2. Chạy FastAPI theo [hai lệnh trong README](Marketplace.AIService/README.md). Cấu hình cùng database với WebAPI. Có thể kiểm tra riêng kết nối bằng GET /ai/recommendations trong FastAPI Docs sau khi WebAPI đã tạo dữ liệu mẫu.
3. Trong thư mục solution, chạy các lệnh sau. Bước 4 tái sử dụng cột feature_vector của Bước 3. Lệnh migrate cũng áp dụng được trên database mới.

```powershell
dotnet build Marketplace.sln -c Release
dotnet run --project Marketplace.WebAPI --launch-profile http -- --migrate
dotnet run --project Marketplace.WebAPI --launch-profile http
```

4. Mở cửa sổ khác tại thư mục solution để chạy giao diện.

```powershell
dotnet run --project Marketplace.WebClient --launch-profile http
```

5. Mở [Marketplace](http://localhost:5115) hoặc [WebAPI Swagger](http://localhost:5116/swagger). Trong Visual Studio cũng có thể đặt WebAPI và WebClient cùng Start, profile http. Seeder chạy ở Development với SeedDemoData=true (mặc định), bổ sung vector còn thiếu của ảnh demo kể cả trên database Bước 3 đã có sản phẩm. Dữ liệu mẫu mới có 120 sản phẩm, 12 kiểu minh họa và 6 danh mục. Xem BUOC-3.md để thử ba vai trò.

## Trình diễn

- Nhấn biểu tượng máy ảnh cạnh ô tìm kiếm, chọn ảnh tee-mint.png trong Marketplace.WebAPI/wwwroot/images/demo. Kiểm tra ảnh xem trước, nhấn **Tìm sản phẩm**. Kết quả đầu là áo thun xanh bạc hà, nhóm Thời trang. Điểm giống nhau là cosine, quy đổi ra phần trăm.
- Dừng Uvicorn rồi gửi lại ảnh. WebAPI dùng SkiaSharp để trích xuất cùng loại vector và xếp hạng bằng C#. Trường engine trong kết quả lần lượt là python hoặc csharp. Ảnh hỏng hoặc đồng màu nhận 400, ảnh vượt 5 MiB nhận 413. Giới hạn giải mã là 12 triệu pixel, chỉ chấp nhận ảnh tĩnh.
- Xem trang chủ khi chưa đăng nhập. Đăng nhập buyer@example.invalid bằng Marketplace123!, nhấn các thẻ cùng nhóm, thêm một sản phẩm vào giỏ và đặt hàng. Trở lại trang chủ để xem gợi ý. Vào chi tiết để xem các sản phẩm tương tự. Mua và thêm vào giỏ được ghi cùng giao dịch với thay đổi dữ liệu mua sắm.
- GET /api/products/recommendations nhận productId tùy chọn. Danh tính cá nhân hóa lấy từ JWT. FastAPI kết hợp đặc trưng ảnh, danh mục, thuộc tính biến thể, lịch sử tương tác và đánh giá hiển thị. Khi dịch vụ AI dừng, WebAPI dùng danh mục và số tương tác để duy trì khu vực gợi ý. Header X-Recommendation-Engine cho biết python hoặc catalog.
- Lọc sản phẩm, danh mục và shop đang hoạt động trước khi hiển thị. Khi cấu hình cổng khác mặc định, đặt AiBaseUrl ở WebAPI, ApiBaseUrl ở WebClient. Hai URL đều là địa chỉ gốc của dịch vụ tương ứng.

## Kiểm tra vector

```sql
SELECT count(*) AS demo_images_with_vector
FROM product_images
WHERE image_url LIKE '/images/demo/%'
  AND json_array_length(feature_vector::json) = 2304;
```

Trên database mẫu mới, kết quả mong đợi là 120. Database có sản phẩm do người bán tạo thêm có thể có nhiều dòng hơn. Hướng dẫn đo chất lượng và kết quả thực nghiệm nằm trong [Đánh giá AI](DANH-GIA-AI.md).
