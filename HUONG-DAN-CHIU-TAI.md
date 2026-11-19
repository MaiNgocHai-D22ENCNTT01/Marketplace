# Tìm kiếm ảnh: triển khai và đo tải

## Phạm vi và giới hạn bằng chứng

Mã nguồn được nâng cấp ngay trong Marketplace.sln và Marketplace.AIService. Không cần Docker hoặc Redis để chạy Windows. Có thể mở rộng AI bằng nhiều container qua Nginx. Chưa có bằng chứng cho 10.000 yêu cầu suy luận đồng thời hay hàng trăm ngàn người dùng: số người dùng trực tuyến khác số request đang xử lý. Ví dụ 10.000 người, mỗi người tìm một lần/phút tạo trung bình 167 RPS, nhưng burst đồng thời có thể vượt xa con số đó.

Các mốc giảm 95% băng thông, cache <2 ms, HNSW <15 ms, ONNX nhanh hơn 3–4 lần và hàng ngàn RPS đều phải đo trên phần cứng/dữ liệu triển khai. Ảnh 5 MiB thành tối đa 30 KiB giảm hơn 99% riêng phần ảnh; không phải mọi ảnh đều giảm 95%. Lookup RAM không phải độ trễ HTTP đầu-cuối. Không chỉnh sửa báo cáo để tạo tỷ lệ lỗi 0%.

## Các phần mã nguồn

| Thành phần | Tệp | Hành vi |
|---|---|---|
| Trình duyệt | Marketplace.WebClient/wwwroot/js/visual-search.js | Canvas 224×224, giữ tỉ lệ và nền trắng, WebP/JPEG giảm chất lượng đến <=30 KiB; khóa submit khi chưa nén xong |
| API | Marketplace.WebAPI/VisualSearchGateway.cs | Sliding window 30 lần/phút/IP hoặc user; giới hạn 256 request ảnh đang xử lý; 429 và Retry-After; HTTP pool 128, connect 1 s, timeout 4 s; circuit mở 5 s sau 5 lỗi |
| Cache | Marketplace.WebAPI/Controllers/ProductsController.AI.cs | SHA256 byte ảnh đã nén, namespace mô hình, cache có giới hạn 64 MiB; lưu prediction và JSON 10 phút; hit không gọi AI/DB; 128 khóa chia nhóm chống dồn yêu cầu cache miss |
| ONNX | Marketplace.AIService/export_onnx.py, inference.py | MobileNetV3-Small ImageNet, activation 1024 chiều chuẩn hóa L2; batch động, CPUExecutionProvider |
| Hàng đợi | Marketplace.AIService/inference.py | 128 ảnh chờ, tối đa 32 ảnh/lô, cửa sổ 12 ms; hết chỗ hoặc chờ quá 3 s trả 503; decode/inference chạy ngoài event loop |
| PostgreSQL | Marketplace.AIService/main.py, visual-index.sql | Pool 16 kết nối/process, từ chối nhanh khi bận; HNSW cosine khi có cột ANN; fallback quét chính xác nếu chưa có |
| Triển khai | docker-compose.yml, nginx-ai.conf | 2 AI replicas mặc định, least-connections, DNS động và HTTP keepalive |
| Đo tải | load_test.py | mixed/warm/cold; RPS, success RPS, P50/P95/P99, từng mã HTTP, lỗi và thời gian chờ phía máy phát tải |

SHA256 nhận diện byte giống hệt, không gộp ảnh khác metadata/chất lượng mã hóa dù trông giống nhau. Cache full response là snapshot: tên, giá, tồn kho, trạng thái hiển thị có thể cũ đến 10 phút. Checkout vẫn kiểm tra dữ liệu hiện tại. Nếu yêu cầu gỡ sản phẩm tức thì, cần invalidation khi cập nhật catalog trước khi triển khai chính sách cache này. Cache và limiter hiện nằm trong từng WebAPI instance; tăng replicas API sẽ có cache riêng và quota riêng. Khi mở rộng nhiều API, thêm Redis cho cache/invalidation và rate limiter chung ở gateway. Không giả định limiter trong ASP.NET là chống DDoS đầy đủ; cần bảo vệ upstream tại nơi triển khai.

Nếu AI hỏng, cache còn hạn vẫn phục vụ được; cache miss trả 503 thân thiện, không trả danh sách rỗng giả thành công. Các khóa gom request là theo tiến trình. Thay mô hình/preprocessing phải reindex toàn bộ và đổi namespace cache.

## Chạy Windows không Docker

Chạy các lệnh từ `E:\đồ án tốt nghiệp\Marketplace`, dùng các cửa sổ PowerShell riêng. PostgreSQL 17 và database hiện có phải đang chạy. Giữ cấu hình kết nối hiện tại trong user-secrets hoặc biến môi trường; không ghi mật khẩu vào mã nguồn.

```powershell
dotnet build Marketplace.sln -m:1
# Nếu database mới, áp dụng migration trước, rồi chạy WebAPI Development để seed.
dotnet run --project Marketplace.WebAPI -- --migrate
dotnet run --project Marketplace.WebAPI --launch-profile http
```

Cửa sổ AI (đã có .venv và mobilenet.onnx trong bản hiện tại):

```powershell
cd Marketplace.AIService
# Chỉ khi chưa có môi trường:
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements-runtime.txt
# start-local lấy kết nối từ user-secrets WebAPI, hoặc MARKETPLACE_AI_DSN do bạn đặt.
.\start-local.ps1
```

API mặc định dùng `http://127.0.0.1:8000/`, cùng địa chỉ IPv4 của script AI. Nếu đã đặt AiBaseUrl trước đây, cập nhật giá trị đó. Cửa sổ WebClient:

```powershell
dotnet run --project Marketplace.WebClient --launch-profile http
```

Mở http://localhost:5215. Kiểm tra AI: `Invoke-RestMethod http://127.0.0.1:8000/health/ready`. `ann=false` nghĩa là đang dùng fallback quét bảng. Nếu thiếu mô hình hoặc cần xuất lại:

```powershell
cd Marketplace.AIService
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe export_onnx.py
.\.venv\Scripts\python.exe -m unittest test_scaling -v
```

Xuất ONNX cần tải weights torchvision nếu máy chưa có cache. Runtime không cần PyTorch. Không thay weights riêng đã huấn luyện bằng ImageNet mà không xuất lại và đánh giá chất lượng. Client letterbox và preprocessing AI được giữ như mã hiện tại; nên đo recall bằng ảnh thực tế sau nén, không chỉ latency.

## Nâng cấp pgvector trên PostgreSQL 17

Sao lưu database trước thay đổi schema. Chạy reindex khi ít tải; không chạy đồng thời với benchmark:

```powershell
Invoke-RestMethod -Method Post http://127.0.0.1:8000/ai/reindex-all
& 'C:\Program Files\PostgreSQL\17\bin\psql.exe' -h localhost -U postgres -d marketplace_mvp -v ON_ERROR_STOP=1 -f visual-index.sql
```

Sau đó khởi động lại AI để nhận cột ANN. Script kiểm tra extension có trên máy; nếu chưa có, giữ nguyên text và fallback. Nếu có nhưng chưa đủ quyền CREATE EXTENSION, DBA cần cài trước. Dùng pgvector >=0.8 để hỗ trợ iterative scans. Không chạy CREATE INDEX CONCURRENTLY trong transaction. Xem EXPLAIN (ANALYZE, BUFFERS) trên dữ liệu thật; với bảng demo nhỏ planner có thể chọn quét tuần tự hợp lý.

Schema EF hiện ánh xạ `feature_vector` là text. Để không làm hỏng Windows/EF và recommendation, script giữ cột đó, thêm `feature_vector_ann vector(1024)` generated STORED và index:

```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS product_images_cosine_hnsw
ON product_images USING hnsw (feature_vector_ann vector_cosine_ops);
```

Đây là cột vector có index thực sự, được đồng bộ tự động khi text thay đổi; tốn thêm dung lượng lưu trữ. Không chạy trực tiếp ALTER COLUMN feature_vector TYPE vector trên schema hiện tại. Cần reindex vector cũ 2304 chiều trước DDL; dữ liệu sai chiều sẽ làm migration dừng thay vì âm thầm bỏ mất dữ liệu. Với dữ liệu hàng triệu ảnh, lập kế hoạch dung lượng, thời gian backfill/index và đo recall@k; HNSW là tìm kiếm gần đúng. Fallback đọc vector vào RAM theo request, chỉ phù hợp demo nhỏ, không phải phương án chịu tải triệu sản phẩm.

## Docker và mở rộng ngang

```powershell
Copy-Item .env.scaling.example .env
# Điền kết nối PostgreSQL thực vào .env riêng của máy.
# Dừng AI Windows đang dùng cổng 8000 trước.
docker compose up -d --build --scale ai=4
docker compose ps
docker compose logs --tail 100 ai nginx
```

Compose chỉ đóng gói AI và Nginx; PostgreSQL và WebAPI/WebClient tiếp tục dùng bản Windows hiện tại. PostgreSQL phải cho phép container kết nối từ Docker Desktop theo pg_hba.conf/firewall. Không expose DB ra Internet. Khi container AI đặt trên máy khác, dùng địa chỉ PostgreSQL phù hợp và cấp thư mục ảnh catalog cho quá trình indexing. Nginx chỉ publish localhost:8000 nên endpoint reindex không được công khai ra ngoài trong cấu hình này.

Tăng replicas không làm CPU vật lý tăng: 4 replicas × 2 inference threads cần đủ CPU; tối đa 4 × 16 kết nối AI cộng pool WebAPI và các ứng dụng khác phải nằm trong ngân sách PostgreSQL. Đặt MARKETPLACE_AI_THREADS, MARKETPLACE_DB_CONNECTIONS, MARKETPLACE_AI_QUEUE, MARKETPLACE_AI_BATCH_SIZE và MARKETPLACE_AI_BATCH_MS trong .env. Không tăng hàng đợi vô hạn. Health endpoint giúp kiểm tra startup/queue; chưa thay thế giám sát CPU, RAM, DB và latency.

## Đo tải và trình bày báo cáo

Cài thư viện đo tải nếu môi trường Python chưa có: `python -m pip install -r requirements-load.txt`. Hoặc dùng `Marketplace.AIService/.venv/Scripts/python.exe` thay cho `python` trong các lệnh dưới.

1. Thử đúng quota sản xuất: chạy 35 yêu cầu từ một IP trong dưới một phút; 429 sau quota là hành vi đúng, vẫn được tính là lỗi trong thống kê tải.

```powershell
python load_test.py --users 1 --requests 35 --connections 1 --output rate-limit.json
```

2. Đo năng lực trên môi trường thử nghiệm riêng: vì các user ảo cùng một IP, khởi động API thử nghiệm với quota cao. Không sửa quota mặc định và không giả IP bằng X-Forwarded-For:

```powershell
$env:VisualSearch__RequestsPerMinute='100000'
$env:AiBaseUrl='http://127.0.0.1:8000/'
dotnet run --no-build --project Marketplace.WebAPI --launch-profile http
# Khi kết thúc: dừng API, Remove-Item Env:VisualSearch__RequestsPerMinute
```

3. Cửa sổ khác chạy lần lượt, từ mức nhỏ rồi tăng dần:

```powershell
python load_test.py --mode warm --users 100 --requests 2000 --connections 100 --output warm-100.json
python load_test.py --mode cold --users 16 --requests 500 --connections 16 --output cold-16.json
python load_test.py --mode warm --users 10000 --requests 10000 --connections 128 --ramp 30 --output warm-10000.json
# Chỉ chạy sau khi máy phát tải/server đủ tài nguyên:
python load_test.py --mode cold --users 1000 --requests 10000 --connections 1000 --ramp 60 --output cold-1000.json
```

`warm` nạp cache trước khi bấm giờ; mặc định dùng một ảnh. `mixed` dùng tập ảnh và cache hiện có. `cold` thêm RIFF chunk riêng cho từng ảnh WebP để SHA256 khác nhau, bắt buộc gọi AI; pixels vẫn giống ảnh gốc nên đây là đo throughput, không phải đánh giá độ chính xác hay độ đa dạng ảnh. Dùng `--images Marketplace.WebAPI/wwwroot/images/demo` hoặc corpus thực. Không đánh đồng corpus 120 ảnh với triệu sản phẩm.

`--base-url http://IP-MAY-CHU:5116` cho phép đo từ máy riêng. Script chạy closed-loop: user gửi yêu cầu tiếp theo khi yêu cầu trước hoàn tất, có `--think` và `--ramp`. `--connections` là giới hạn phát tải, `peak_outstanding` gồm cả tác vụ chờ phía client. 10.000 user với 128 connections KHÔNG phải 10.000 kết nối đồng thời. Latency gồm thời gian chờ client và thời gian HTTP; báo riêng `client_admission_wait`. Khi cần thử đúng 10.000 kết nối, dùng nhiều máy phát tải và báo tổng connection thực; đừng lấy số coroutine làm bằng chứng.

JSON ghi mọi mã 429/503, timeout, lỗi parse; exit code 1 nếu có bất kỳ lỗi, 0 khi tất cả request thành công đúng cấu trúc. Warmup thất bại làm script dừng trước phép đo. Báo cáo ghi RPS toàn bộ và success_rps riêng; không gọi tổng RPS là throughput thành công khi có lỗi. Ghi thêm cấu hình CPU/RAM, replicas, dataset, ANN, quota, thời gian test và phần trăm cache hit khi trình hội đồng. Chạy lâu hơn 10 phút để thấy hành vi cache hết hạn và lặp ít nhất ba lần.

## Nguồn kỹ thuật

- ASP.NET rate limiting và giới hạn khi nhiều instances: https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-8.0
- pgvector, cosine HNSW và iterative scans: https://github.com/pgvector/pgvector
- ONNX Runtime CPU threads: https://onnxruntime.ai/docs/performance/tune-performance/threading.html
