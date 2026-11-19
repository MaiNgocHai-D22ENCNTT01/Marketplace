# Gói 2 · Chat giữa khách hàng và gian hàng

## Cập nhật PostgreSQL 17

Mở `Marketplace.sln` trong Visual Studio. Kiểm tra `ConnectionStrings:Marketplace` của WebAPI trỏ đến database đang chạy Gói 1. Trong **Tools → NuGet Package Manager → Package Manager Console**, chạy:

```powershell
Update-Database -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
Get-Migration -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

Migration `20261114152658_AddRealtimeChat` tạo hai bảng, các khóa ngoại, chỉ mục cho lịch sử và khóa duy nhất cho cặp khách hàng/gian hàng. Chiều nâng cấp giữ nguyên sản phẩm, tài khoản, đơn hàng và wishlist đã có.

**Tạo migration từ model.** Trên nhánh phát triển đã có entity và cấu hình mới nhưng snapshot còn ở Gói 1, dùng lệnh dưới đây. Bản bàn giao đã có migration, designer và snapshot.

```powershell
Add-Migration AddRealtimeChat -Context AppDbContext -Project Marketplace.Infrastructure -StartupProject Marketplace.WebAPI
```

## Cấu hình SignalR và CORS

Với profile HTTP mặc định, mở WebClient tại `http://localhost:5215`. WebAPI chạy tại `http://localhost:5116`. WebAPI đã cho phép origin này. WebClient phục vụ thư viện JavaScript SignalR 8.0.7 từ `wwwroot/lib/signalr`.

Khi triển khai, đặt danh sách origin WebClient vào mục `Chat` trong cấu hình WebAPI. Ví dụ dưới đây là nội dung phần cấu hình chat:

```json
{
  "Chat": {
    "AllowedOrigins": ["https://market.example.vn"]
  }
}
```

Dùng đúng giao thức, hostname và cổng người dùng mở trong trình duyệt. Viết origin không kèm đường dẫn hoặc dấu gạch chéo cuối. Chính sách `ChatClient` áp dụng cho `/hubs/chat` (bao gồm negotiate) và `/api/chat`. Trong `Program.cs`, `UseCors` chạy sau `UseRouting` và trước xác thực JWT.

Trong cấu hình WebClient, `ApiBaseUrl` là địa chỉ WebAPI mà máy chủ MVC gọi. Khi địa chỉ trình duyệt truy cập khác địa chỉ nội bộ, thêm `"ChatPublicApiUrl": "https://api.example.vn/"`. Với WebClient dùng HTTPS, dùng HTTPS cho API và bật chuyển tiếp WebSocket trên reverse proxy ở đường dẫn `/hubs/chat`. Cấu hình `Jwt:Key` của WebAPI theo cơ chế hiện có. Khởi động lại hai project sau khi đổi cấu hình.

WebClient lấy JWT từ phiên đăng nhập bằng POST cùng origin có antiforgery token. Trình duyệt giữ JWT trong bộ nhớ trang chat. REST dùng header Bearer. SignalR dùng JWT khi negotiate và khi mở kết nối. Với WebSocket, token có thể xuất hiện trong query string nên cấu hình proxy để bỏ query string của hub khỏi access log.

## Dùng thử

1. Đăng nhập bằng khách hàng. Mở một sản phẩm rồi bấm **Chat với Shop**. Khung soạn có sẵn sản phẩm để hỏi. Gửi lời nhắn hoặc chỉ gửi thẻ sản phẩm.
2. Trong phiên trình duyệt riêng, đăng nhập bằng chủ gian hàng đó. Chọn **Kênh người bán → Hộp thư khách hàng**, mở cuộc hội thoại và trả lời. Hai bên có thể đọc các tin mới ngay trên trang đang mở.
3. Từ **Đơn mua**, mỗi phần chi tiết đơn theo shop có nút **Chat với Shop**. Nút mở lại hội thoại của khách với đúng shop, kể cả khi một lần thanh toán có nhiều đơn. Menu **Tài khoản → Tin nhắn** mở hộp thư.

## Hợp đồng API

Tất cả endpoint chat yêu cầu JWT của khách hoặc người bán. WebAPI xác định người gửi từ JWT và kiểm tra người tham gia cho từng hội thoại.

| Phương thức | Đường dẫn | Kết quả |
|---|---|---|
| GET | `/api/chat/conversations` | Danh sách theo thời gian gần nhất, tên đối tác, tin gần nhất và số chưa đọc |
| GET | `/api/chat/conversations/{id}/messages` | 50 tin gần nhất theo thứ tự thời gian; truyền `?before={messageId}` để lấy trang cũ hơn |
| POST | `/api/chat/send` | Trả tin đã lưu với thẻ sản phẩm nếu có. Bắt đầu bằng `shopId` hoặc trả lời bằng `conversationId` |
| POST | `/api/chat/conversations/{id}/read` | Nhận mảng tối đa 50 ID tin đã hiển thị và đánh dấu các tin từ đối tác là đã đọc |

Ví dụ nội dung gửi đầu tiên (thay các giá trị ID bằng UUID thực tế):

```json
{
  "shopId": "<shop-id>",
  "content": "Mẫu này còn màu xanh không?",
  "productId": "<product-id>",
  "messageId": "<uuid-do-client-tao>"
}
```

Gửi lại cùng yêu cầu với cùng `messageId` sẽ trả về tin đã lưu. Sau khi giao dịch PostgreSQL hoàn tất, hub phát sự kiện `MessageReceived` chứa `conversationId` và `messageId` cho hai tài khoản. WebClient tải lại phần lịch sử đang xem và danh sách hội thoại. Khi kết nối lại, WebClient đồng bộ từ PostgreSQL để hiển thị cả các tin gửi lúc mất kết nối.

## Mã nguồn theo project

| Project | File |
|---|---|
| Domain | `Entities/Conversation.cs`, `Entities/ChatMessage.cs` |
| Application | `Chat/Contracts.cs` |
| Infrastructure | `AppDbContext.cs`, `Configurations/ChatConfiguration.cs`, `Migrations/20261114152658_AddRealtimeChat.cs`, file designer cùng tên, `Migrations/AppDbContextModelSnapshot.cs` |
| WebAPI | `Program.cs`, `appsettings.json`, `Controllers/ChatController.cs`, `Hubs/ChatHub.cs` |
| WebClient | `Controllers/ChatController.cs`, `Views/Chat/Index.cshtml`, `Views/Products/Detail.cshtml`, `Views/Orders/Index.cshtml`, `Views/Seller/Index.cshtml`, `Views/Shared/_Layout.cshtml`, `wwwroot/js/chat.js`, `wwwroot/css/chat.css`, `wwwroot/lib/signalr/` |
| Kiểm thử | `tests/test_chat.py`, `tests/test_web.py`, `tests/run_postgres.py` |

## Chạy kiểm thử trên database tạm

Từ thư mục chứa solution, dùng Python có thư viện `websockets` (đã kiểm thử với bản 16.0):

```powershell
dotnet build Marketplace.sln -c Release
python -m pip install websockets==16.0
python tests/run_postgres.py --chat
```

Script tự tạo PostgreSQL tạm, áp dụng migration, chạy WebAPI và WebClient trên cổng riêng, rồi dừng các tiến trình sau khi kiểm thử. Nếu PostgreSQL 17 cài khác đường dẫn mặc định, đặt `PG_BIN` trỏ tới thư mục chứa `initdb.exe`.
