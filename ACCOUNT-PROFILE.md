# Quản lý tài khoản cá nhân và sổ địa chỉ

Solution: `Marketplace.sln`. Mở `/Account/Profile` sau khi đăng nhập.

## Chức năng

- Hồ sơ: họ tên, email chỉ đọc, số điện thoại, vai trò và ngày tham gia.
- Mật khẩu: kiểm tra mật khẩu cũ, mật khẩu mới 8–128 ký tự, xác nhận tại WebClient; băm bằng ASP.NET Core `PasswordHasher<User>` giống đăng ký/đăng nhập hiện tại.
- Sổ địa chỉ: thêm, liệt kê, đặt mặc định và xóa. Địa chỉ đầu tiên tự làm mặc định. Khi xóa địa chỉ mặc định, một địa chỉ còn lại được chọn thay thế.
- Mọi API yêu cầu đăng nhập; mỗi truy vấn địa chỉ giới hạn theo user trong JWT, không nhận user ID từ trình duyệt. Các yêu cầu ghi sổ địa chỉ khóa bản ghi chủ sở hữu trong giao dịch PostgreSQL để tránh tranh chấp mặc định.
- WebClient dùng session JWT và bộ lọc chống CSRF hiện có. Giữ nguyên Login/Register/Logout.
- Checkout điền địa chỉ mặc định ở phía server, có danh sách chọn nhanh và vẫn cho nhập thủ công.
- Giao diện hai cột, co thành một cột trên màn hình nhỏ; hộp thoại native hỗ trợ Escape, xác nhận xóa và thông báo kết quả.

## Tương thích dữ liệu

Schema thực tế trong entity configuration và migration hiện tại dùng `addresses`, không phải `user_addresses`. Tính năng dùng lại bảng đó, không đổi tên hoặc tạo bảng mới. Không cần migration và không chạy lại seed trên cơ sở dữ liệu của bạn.

`users` hiện chưa có cột ảnh đại diện: API trả `avatarUrl: null`, giao diện dùng chữ cái đầu. Không bổ sung upload ảnh trong bản này vì ảnh là trường tùy chọn theo yêu cầu.

Địa chỉ chi tiết tối đa 190 ký tự; tỉnh, huyện, xã mỗi trường tối đa 100 ký tự. Tổng địa chỉ ghép có dấu phân cách tối đa 496 ký tự, tương thích giới hạn 500 ký tự của checkout cũ. Họ tên người nhận tối đa 150 ký tự cũng theo checkout hiện tại.

Việc đổi mật khẩu không thu hồi JWT đã cấp; token vẫn hết hạn theo cơ chế xác thực hiện có.

## Endpoint

| Method | Đường dẫn | Kết quả |
|---|---|---|
| GET | /api/account/profile | Hồ sơ và vai trò |
| PUT | /api/account/profile | Cập nhật fullName, phoneNumber; 204 |
| POST | /api/account/change-password | currentPassword, newPassword; 204 |
| GET | /api/account/addresses | Danh sách địa chỉ, mặc định trước |
| POST | /api/account/addresses | recipientName, phone, province, district, ward, detailAddress, isDefault; 201 |
| PUT | /api/account/addresses/{id}/default | Đặt mặc định; 204 |
| DELETE | /api/account/addresses/{id} | Xóa; 204 |

Thông tin không hợp lệ trả 400; mật khẩu cũ sai hoặc mật khẩu mới trùng cũ trả 409; địa chỉ không tồn tại hoặc của tài khoản khác trả 404.

## Build và kiểm thử

```powershell
dotnet build Marketplace.sln -m:1
dotnet build Marketplace.sln -c Release -m:1
python tests/run_postgres.py
```

Bộ kiểm thử tạo PostgreSQL 17 riêng trong thư mục tạm, tự migrate/seed ở đó và dừng các tiến trình khi kết thúc. Có thể đặt `PG_BIN` nếu PostgreSQL không nằm ở vị trí mặc định. Bộ kiểm thử tài khoản được thêm vào runner cùng các kiểm thử cũ.

Mã nguồn đầy đủ của từng file thêm/sửa nằm trong `ACCOUNT-PROFILE-SOURCE.md` và trực tiếp trong solution.

## Kết quả xác minh ngày 19/11/2026

- Build Debug và Release của toàn bộ solution: 0 lỗi, 0 cảnh báo bằng .NET SDK 9.0.318, target net8.0. Chưa mở build trực tiếp trong Visual Studio 2022.
- `python tests/run_postgres.py`: toàn bộ bộ kiểm thử hiện có và bộ kiểm thử tài khoản mới đều PASS trên PostgreSQL 17 tạm riêng.
- Đã kiểm tra: yêu cầu đăng nhập, các vai trò truy cập hồ sơ, bảo vệ địa chỉ của user khác, dữ liệu không hợp lệ, đổi mật khẩu và đăng nhập bằng mật khẩu mới, cạnh tranh đặt mặc định, xóa mặc định, CSRF, form cập nhật hồ sơ và checkout tự điền địa chỉ.
- Các bộ kiểm thử cũ về wishlist, đơn hàng, quản trị, người bán, MVC và flash sale đều PASS. Không sửa seed hoặc migration.
- Kiểm thử giao diện bằng HTML trả về và gửi form HTTP; chưa kiểm tra hình ảnh giao diện bằng trình duyệt thật.
