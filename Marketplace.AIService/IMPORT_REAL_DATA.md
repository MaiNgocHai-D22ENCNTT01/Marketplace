# Nạp bộ sản phẩm có ảnh chụp

Script dùng cấu trúc hiện tại của dự án (kể cả các bảng bổ sung sau bản 24 bảng), không tạo/sửa schema. Nguồn: https://dummyjson.com/docs/products — đây là dữ liệu sản phẩm mẫu có ảnh chụp, không phải giao dịch hay đánh giá khách hàng thật. Tên danh mục được ánh xạ tiếng Việt bằng từ điển cục bộ; tên model/nhãn hiệu và mô tả gốc được giữ lại.

## 1. Chuẩn bị trong PowerShell

```powershell
Set-Location -LiteralPath 'E:\đồ án tốt nghiệp\Marketplace\Marketplace.AIService'
$python = '.\.venv\Scripts\python.exe'
& $python -m pip install -r requirements-runtime.txt
# evaluate_ai.py dùng TestClient, cần thêm httpx nếu môi trường chưa có:
& $python -m pip install httpx
```

Giữ `mobilenet.onnx`, `labels.json`, `inference.py` có sẵn. Không cần Torch hoặc tải lại mô hình. Cài thư viện và tải dữ liệu khi còn mạng; lúc demo không chạy pip.

Dùng cùng thông tin kết nối với WebAPI, đoạn sau giống cách `start-local.ps1` đọc cấu hình và không in mật khẩu:

```powershell
$settings = Join-Path $env:APPDATA 'Microsoft/UserSecrets/marketplace-api-local-development/secrets.json'
$connection = $env:ConnectionStrings__Marketplace
if (-not $connection -and (Test-Path $settings)) {
    $config = Get-Content -Raw -LiteralPath $settings | ConvertFrom-Json
    $connection = $config.'ConnectionStrings:Marketplace'
    if (-not $connection) { $connection = $config.ConnectionStrings.Marketplace }
}
if (-not $connection) { throw 'Chưa tìm thấy cấu hình kết nối WebAPI. Đặt MARKETPLACE_AI_DSN hoặc các biến PGHOST/PGDATABASE/PGUSER/PGPASSWORD.' }
$db = New-Object System.Data.Common.DbConnectionStringBuilder
$db.set_ConnectionString($connection)
foreach ($pair in @(@('Host','PGHOST'), @('Port','PGPORT'), @('Database','PGDATABASE'), @('Username','PGUSER'), @('Password','PGPASSWORD'))) {
    if ($db.ContainsKey($pair[0])) {
        [Environment]::SetEnvironmentVariable($pair[1], [string]$db[$pair[0]], 'Process')
    }
}
$env:MARKETPLACE_AI_DSN = 'connect_timeout=5'
```

Nếu đã đặt `MARKETPLACE_AI_DSN` hợp lệ thì bỏ qua đoạn đọc cấu hình. Database phải được migrate bằng WebAPI hiện tại và có ít nhất một shop `ACTIVE`.

## 2. Tải và kiểm tra trước khi nạp

```powershell
& $python .\import_real_data.py --limit 120 --download-only
if ($LASTEXITCODE -ne 0) { throw 'Chưa tải đủ ảnh; chạy lại khi có mạng.' }
& $python .\import_real_data.py --limit 120 --offline --dry-run
if ($LASTEXITCODE -ne 0) { throw 'Kiểm tra database chưa đạt; chưa nạp dữ liệu.' }
```

`--dry-run` thực hiện SQL thật rồi rollback. Cache JSON và ảnh vẫn được lưu. Mỗi ảnh được chuyển thành JPEG thật, xử lý nền trắng và trích xuất lại từ chính file đã lưu bằng `inference.prepare/predict`. Vector phải có 1024 phần tử hữu hạn và chuẩn L2 bằng 1. Cột `feature_vector` là JSON dạng text như AIService hiện tại; cột ANN sinh tự động, nếu đã cài, sẽ tự đồng bộ.

## 3. Nạp dữ liệu: chọn một cách

Giữ toàn bộ sản phẩm cũ (mặc định):

```powershell
& $python .\import_real_data.py --limit 120 --offline
```

Hoặc dọn sản phẩm chỉ có SKU `DEMO-%`, chưa từng xuất hiện trong bất kỳ `order_items` nào, rồi nạp:

```powershell
& $python .\import_real_data.py --limit 120 --offline --clean-demo
```

Script khóa sản phẩm/biến thể khi dọn; đơn hàng mọi trạng thái đều được bảo vệ. Các dữ liệu phụ thuộc khai báo cascade của sản phẩm bị dọn (ví dụ giỏ hàng, wishlist) cũng bị xóa theo schema. Nếu gặp tham chiếu cấm xóa như flash sale, cả giao dịch rollback; hãy dùng chế độ giữ cũ hoặc xử lý liên kết đó trước. File ảnh demo cũ không bị xóa. Nếu bật `SeedDemoData`, WebAPI có thể tạo lại bộ demo ở lần khởi động tiếp theo; tắt cấu hình này nếu muốn giữ bộ cũ ở trạng thái đã dọn.

Mỗi sản phẩm có SKU `REAL-DJ-...`, ID ổn định, 2–3 đánh giá tiếng Việt. Vì `reviews.order_item_id` là bắt buộc và duy nhất, script tạo ba tài khoản mô phỏng không đăng nhập được, cùng các `order_groups/orders/order_items` hoàn tất cho đánh giá. Các đơn này sẽ ảnh hưởng thống kê số đơn/doanh thu: chỉ dùng trên database demo. Không tạo thanh toán hoặc yêu cầu giao hàng. Đánh giá có nhãn `[Đánh giá mô phỏng]`, địa chỉ có nhãn không giao hàng, mô tả sản phẩm ghi rõ nguồn và dữ liệu giả lập. Tổng đánh giá và điểm trung bình shop/sản phẩm được tính lại.

Giá mặc định dùng tỷ giá giả định 25.000 VND/USD, làm tròn 1.000 đồng (`--usd-vnd 26000` để đổi). Màu/cỡ là dữ liệu mô phỏng phù hợp nhóm hàng, không phải thuộc tính được nguồn xác thực. Tồn kho lấy từ nguồn, chia giữa các biến thể. Tối đa 4 ảnh mỗi sản phẩm; chỉ dùng những ảnh nguồn thực sự cung cấp, không dựng góc chụp phụ giả khi nguồn có ít ảnh.

Chạy lại sẽ bỏ qua sản phẩm đã có ID của importer để giữ tồn kho, đánh giá và đơn hàng hiện tại. `--refresh` chỉ làm mới catalog nguồn, không cập nhật sản phẩm đã nhập. Có thể dùng `--limit 0` để lấy tất cả, nhưng phải tải trước cùng lựa chọn đó. Mọi lỗi trong giai đoạn SQL đều rollback toàn bộ; file tải thành công được giữ để lần sau tiếp tục. Nếu mất kết nối đúng thời điểm commit, chạy lại với cùng tham số: ID ổn định ngăn tạo trùng.

## 4. Chạy lại bảng đánh giá AI

Trong cùng cửa sổ PowerShell đã cấu hình database:

```powershell
& $python .\evaluate_ai.py --dataset real --output .\ket-qua-ai-anh-that.md
```

Chương trình tự mở FastAPI trong tiến trình bằng TestClient; không cần chạy thêm uvicorn cho phép đo. Bộ ảnh truy vấn chọn `/images/real/`; gallery tìm kiếm vẫn chứa tất cả sản phẩm đang hoạt động để phản ánh hệ thống thực tế. Báo cáo tính số ảnh/danh mục thực tế, hỗ trợ nhiều ảnh mỗi sản phẩm. Bài gợi ý chỉ chọn danh mục có ít nhất 8 sản phẩm và có 2 sản phẩm bên ngoài; nếu không đủ, báo rõ thay vì lỗi. Các truy vấn ảnh là biến đổi từ ảnh đã lập chỉ mục; chỉ số này đo độ bền với biến đổi ảnh, không chứng minh độ chính xác trên ảnh chụp mới ngoài tập dữ liệu.

Để mở dịch vụ cho demo bình thường:

```powershell
.\start-local.ps1
```

Chạy WebAPI/WebClient như trước. Không cần gọi reindex vì importer đã lưu vector. Giữ thư mục `../Marketplace.WebAPI/wwwroot/images/real/`, cache `real_data_cache/`, model, môi trường Python và database trên máy demo. Nếu chạy WebAPI từ thư mục publish hoặc Docker, cần sao chép/mount `images/real` vào đúng wwwroot được phục vụ; AIService phải trỏ `MARKETPLACE_WEBROOT` vào cùng bộ ảnh. Ảnh sản phẩm và suy luận AI sau khi chuẩn bị không cần Internet.

## Kiểm tra đã thực hiện

- Kiểm tra cú pháp hai file Python.
- Tải thực tế 2 sản phẩm, 6 ảnh từ DummyJSON và trích xuất vector bằng mô hình ONNX cục bộ.
- Chạy lại hoàn toàn offline, thực hiện SQL trên PostgreSQL hiện tại rồi rollback thành công: 2 sản phẩm cùng biến thể, ảnh, đơn mô phỏng và đánh giá.
- Chưa commit bộ 120 sản phẩm hoặc chạy báo cáo đầy đủ trên bộ ảnh thật. Không thể bảo đảm mọi mạng/schema/máy triển khai luôn không lỗi; script có kiểm tra trước và rollback để lỗi được báo rõ, không để lại bộ dữ liệu nạp dở.
