# Marketplace: Neon + Hugging Face Spaces + Render

## Đọc trước khi triển khai

Theo tài liệu kiểm tra ngày 19/11/2026, **tạo Docker Space mới yêu cầu gói trả phí** (PRO cho tài khoản cá nhân); CPU Basic không tính phí theo giờ không đồng nghĩa tài khoản miễn phí được tạo Docker Space. Kiểm tra quyền tạo Space của tài khoản trước khi chọn phương án này. Bộ mã dưới đây hỗ trợ Docker Spaces khi tài khoản có quyền, nhưng không cam kết toàn bộ kiến trúc miễn phí.

Render Free có ngủ sau 15 phút không có traffic; 750 giờ dịch vụ miễn phí được chia theo workspace, không phải 750 giờ cho mỗi app. Hai dịch vụ chạy liên tục cả tháng có thể vượt quota chung. Free không nhận traffic private network và không hỗ trợ scale nhiều instance. Dùng URL HTTPS công khai giữa hai dịch vụ. Đây là cấu hình demo, không phải cấu hình chứng minh 10.000 người dùng đồng thời.

Spaces chỉ cho outbound ports 80/443/8080. Vì vậy không thể giữ nguyên psycopg2 kết nối Neon:5432 từ Space. Luồng triển khai đã sửa thành:

```text
Trình duyệt → WebClient (Render) → WebAPI (Render)
                                      ├─ HTTPS → AI (Spaces): embedding + nhãn
                                      └─ TLS → Neon: pgvector + dữ liệu sản phẩm
```

Windows mặc định vẫn chạy luồng Python tìm catalog như trước. Production bật `VisualSearch:SearchInApi=true`. AI cloud không cần mật khẩu Neon và không đọc ảnh từ ổ đĩa của API. Các ảnh catalog phải được backfill embedding trước khi dùng cloud; không chạy `/ai/reindex-all` trên Space inference-only. Recommendation cloud dùng nhánh fallback catalog sẵn có của WebAPI khi AI không cung cấp recommendation; chưa chuyển thuật toán cá nhân hóa Python sang .NET.

## 1. Các file và build context

| Dịch vụ | Dockerfile | Build context |
|---|---|---|
| Render WebAPI | Marketplace.WebAPI/Dockerfile | thư mục Marketplace chứa Marketplace.sln |
| Render WebClient | Marketplace.WebClient/Dockerfile | cùng thư mục Marketplace |
| HF AI | Marketplace.AIService/Dockerfile | riêng Marketplace.AIService |

Hai .NET image dùng build nhiều giai đoạn, chỉ copy publish Release sang ASP.NET runtime, không mang SDK/cache NuGet/PDB vào image cuối. Không bật trimming vì MVC/EF có reflection. Dùng Debian slim thay Alpine để tránh khác biệt glibc với thư viện native. AI dùng Python slim, chỉ ONNX runtime dependencies, không cài torch/torchvision trong image. Không tải mô hình lúc khởi động: commit `mobilenet.onnx` và `labels.json` hiện tại. Các image chạy non-root.

Từ thư mục Marketplace:

```powershell
docker build -f Marketplace.WebAPI/Dockerfile -t marketplace-api .
docker build -f Marketplace.WebClient/Dockerfile -t marketplace-web .
docker build -t marketplace-ai ./Marketplace.AIService
```

Không đặt build context .NET là thư mục con WebAPI/WebClient vì sẽ thiếu project references và `deploy/CloudHttp.cs`. `.dockerignore` ở gốc bỏ bin/obj/.venv/logs/secrets; AI có `.dockerignore` riêng theo whitelist. `.gitignore` ngăn đưa file secrets thông thường lên GitHub nhưng vẫn phải tự rà soát trước khi push. Chưa đẩy repo hay triển khai tài khoản thay bạn.

## 2. Neon PostgreSQL 17 và dữ liệu

Tạo project/database PostgreSQL 17 trong Neon. Với hệ thống hiện tại đã có dữ liệu + vector 1024 + HNSW, nên chuyển database sang một database Neon trống thay vì chỉ chạy migration rồi bỏ trống catalog. Backup/import từ máy Windows bằng PostgreSQL 17 tools; không đặt mật khẩu thật trong Git hoặc tài liệu:

```powershell
# Chạy trên Windows, đặt PGPASSWORD riêng hoặc dùng pgpass.conf cho nguồn local.
& 'C:\Program Files\PostgreSQL\17\bin\pg_dump.exe' -h localhost -U postgres -d marketplace_mvp --format=custom --no-owner --no-acl --file marketplace-backup.dump
# Tạo extension trên database Neon trống trước khi restore.
# NEON_DIRECT_URL lấy từ Connect của Neon, URI dùng hostname KHÔNG có -pooler.
& 'C:\Program Files\PostgreSQL\17\bin\psql.exe' "$env:NEON_DIRECT_URL" -v ON_ERROR_STOP=1 -c 'CREATE EXTENSION IF NOT EXISTS vector;'
& 'C:\Program Files\PostgreSQL\17\bin\pg_restore.exe' --dbname="$env:NEON_DIRECT_URL" --no-owner --no-acl --exit-on-error marketplace-backup.dump
```

Không chạy restore vào database đang có dữ liệu; không dùng --clean cho database đang phục vụ. Không commit backup (có thể chứa tài khoản/dữ liệu cá nhân). Khi restore gặp lỗi extension/quyền/version, dừng và xử lý lỗi trước khi deploy. Sau restore xác minh bằng SQL:

```sql
SELECT extversion FROM pg_extension WHERE extname='vector';
SELECT count(*) AS images, count(feature_vector_ann) AS indexed FROM product_images;
SELECT indexname, indexdef FROM pg_indexes WHERE tablename='product_images';
```

Dùng pgvector >=0.8 cho iterative scans. File `visual-index.sql` dùng cú pháp psql; nếu đã restore cả schema/index thì không cần tạo lại. EF migration không chứa cột ANN generated, vì vậy nếu chọn database mới hoàn toàn thay vì restore, phải chạy migration, nhập dữ liệu/backfill rồi chạy file SQL này riêng.

Kết nối runtime của .NET là chuỗi **Npgsql**, không dán nguyên `postgresql://...` vào `ConnectionStrings__Marketplace`:

```text
Host=YOUR-ENDPOINT-pooler.REGION.aws.neon.tech;Port=5432;Database=neondb;Username=YOUR_ROLE;Password=YOUR_PASSWORD;SSL Mode=VerifyFull;Pooling=true;Maximum Pool Size=20;Minimum Pool Size=0;Timeout=15;Command Timeout=15
```

Copy đúng host/database/user từ Neon; ví dụ trên không phải địa chỉ thật. Nếu mật khẩu chứa dấu chấm phẩy phải quote theo cú pháp connection string Npgsql. Giữ xác minh chứng chỉ DB; không đặt `Trust Server Certificate=true`. Runtime dùng pooled host, backup/migration dùng direct host. Neon tự quản TLS; không mang chứng chỉ HTTPS localhost của ASP.NET lên cloud.

Nếu cần chạy EF migration bằng máy local lên Neon (không cần nếu đã restore migration history), đặt ConnectionStrings__Marketplace, Jwt__Key trong cửa sổ riêng, rồi:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
dotnet run --project Marketplace.WebAPI -c Release --no-launch-profile -- --migrate
```

Không chạy migration tự động trên mỗi replica; lệnh trên chỉ cập nhật schema EF. Không tự seed dữ liệu demo trong Production.

## 3. Hugging Face Docker Space

Khi tài khoản có quyền tạo Docker Space, chọn Docker SDK và CPU Basic. Chọn Public nếu muốn WebAPI truy cập trực tiếp như cấu hình này; nội dung source sẽ công khai, endpoints `/ai/*` vẫn yêu cầu khóa ứng dụng. Space Private cần xác thực Hugging Face riêng, chưa được cấu hình trong bộ này.

GitHub không tự động deploy sang Spaces như Render. Copy các file sau từ thư mục Marketplace.AIService vào **gốc repo của Space**, hoặc upload bằng giao diện Files:

```text
Dockerfile
.dockerignore
README.md
requirements-runtime.txt
main.py
inference.py
labels.json
mobilenet.onnx
```

README đã có YAML `sdk: docker`, `app_port: 7860`. Không đổi thành cổng 8000 trên Spaces. Dockerfile dùng UID 1000 và lắng nghe 0.0.0.0:7860. `mobilenet.onnx` phải là file nhị phân thật, không phải một Git LFS pointer chưa được tải.

Trong Space Settings:

| Loại | Tên | Giá trị |
|---|---|---|
| Secret | MARKETPLACE_AI_API_KEY | khóa bí mật ngẫu nhiên riêng, cùng giá trị Ai__ApiKey ở Render |
| Variable | MARKETPLACE_AI_THREADS | 2 |
| Variable | MARKETPLACE_AI_QUEUE | 128 |
| Variable | MARKETPLACE_AI_BATCH_SIZE | 32 |
| Variable | MARKETPLACE_AI_BATCH_MS | 12 |

Dockerfile đã đặt `MARKETPLACE_INFERENCE_ONLY=true`, `MARKETPLACE_REQUIRE_API_KEY=true`. Không đặt MARKETPLACE_AI_DSN. Health công khai: `https://YOUR-SPACE.hf.space/health/ready` phải trả `ready=true`, `mode=inference-only`; `ann=false` ở Space là đúng vì DB nằm ở WebAPI. Các yêu cầu AI dùng `X-AI-Key`; không có khóa trả 401.

## 4. Render WebAPI

Repo GitHub nên có Marketplace.sln ở gốc. Tạo New → Web Service → GitHub repo → Docker → Free. Nếu repo của bạn chứa thêm thư mục ngoài Marketplace, đặt Root Directory=Marketplace; nếu solution ở gốc repo, để trống. Dockerfile Path=`Marketplace.WebAPI/Dockerfile`, Docker Build Context=`.`. Không điền Docker Command riêng.

Health Check Path: `/health/live`. Đây là liveness, không khẳng định Neon/AI đã sẵn sàng. Cổng dùng biến PORT Render cung cấp, mặc định 10000. Public HTTPS do Render xử lý, container nhận HTTP; không mount dev certificate và không bật HTTPS redirection về localhost.

Environment/Secrets:

| Tên | Giá trị |
|---|---|
| ASPNETCORE_ENVIRONMENT | Production (đã có trong image) |
| ConnectionStrings__Marketplace | connection string Npgsql Neon ở trên — SECRET |
| Jwt__Key | base64 của ít nhất 32 byte ngẫu nhiên — SECRET, giữ ổn định qua các lần deploy |
| AiBaseUrl | https://YOUR-SPACE.hf.space/ |
| Ai__ApiKey | cùng MARKETPLACE_AI_API_KEY của Space — SECRET |
| Chat__AllowedOrigins__0 | https://YOUR-WEBCLIENT.onrender.com (không dấu / cuối) |
| VisualSearch__SearchInApi | true (đã có trong appsettings.Production.json) |
| Ai__TimeoutSeconds | 20 (đã có trong JSON; cold start dài hơn vẫn có thể trả 503) |

Tạo hai khóa riêng trên máy rồi dán vào Secrets, không đưa vào Git:

```powershell
python -c "import secrets,base64; print(base64.b64encode(secrets.token_bytes(32)).decode())"
```

Chạy hai lần để lấy Jwt__Key và AI key độc lập. Session cũ phát bằng JWT key Development không dùng được sau chuyển cloud; đăng nhập lại.

## 5. Render WebClient

Tạo Web Service Docker thứ hai, cùng repo/build context, Dockerfile Path=`Marketplace.WebClient/Dockerfile`, Free, Health Check Path=`/health/live`. Đặt:

| Tên | Giá trị |
|---|---|
| ASPNETCORE_ENVIRONMENT | Production |
| ApiBaseUrl | https://YOUR-WEBAPI.onrender.com/ |
| ChatPublicApiUrl | https://YOUR-WEBAPI.onrender.com/ |

ApiBaseUrl phải là **URL công khai HTTPS**, vì các Razor views dùng URL này để dựng link ảnh. Không dùng hostname mạng nội bộ, localhost hoặc :10000 trong URL trình duyệt. Sau khi có URL WebClient, quay lại cấu hình Chat__AllowedOrigins__0 ở API và redeploy nếu cần.

ASP.NET Core tự đọc biến môi trường sau appsettings. Tên chứa `__` ánh xạ sang `:`; không viết `${VAR}` trong JSON vì .NET không tự nội suy chuỗi đó. Các trường bắt buộc được để rỗng trong Production để không âm thầm kết nối localhost. Cấu hình Development hiện có được giữ nguyên.

## 6. Bỏ qua chứng chỉ HTTPS nội bộ khi cần

Render/Hugging Face public HTTPS có chứng chỉ hợp lệ; để mặc định kiểm tra TLS. Container phía sau reverse proxy dùng HTTP nên không có lỗi chứng chỉ HTTPS nội bộ cần bỏ qua trong kiến trúc trên.

Nếu bạn có một endpoint HTTPS nội bộ dùng chứng chỉ tự ký, bộ mã có công tắc chủ động, chỉ áp dụng đúng hostname trong danh sách:

```text
Http__AllowInvalidInternalCertificates=true
Http__InsecureTlsHosts__0=api.internal.example
```

Đặt ở WebClient nếu bỏ qua chứng chỉ cho ApiBaseUrl; đặt ở WebAPI nếu bỏ qua chứng chỉ cho AiBaseUrl. Chỉ hostname, không scheme, port, path hoặc wildcard. Phải đồng thời bật cờ và khớp hostname. Handler tắt redirect để không gửi khóa hoặc mở rộng bypass sang host khác. Khi bật cho một host, kết nối đến host đó không còn được xác minh danh tính TLS; chỉ dùng tạm trong mạng nội bộ do bạn kiểm soát. Thiết lập này không ảnh hưởng chứng chỉ DB Neon hoặc trình duyệt. Không cần bật cho `*.onrender.com`/`*.hf.space` công khai.

## 7. Dữ liệu, ảnh, proxy và giới hạn

- Docker API chứa ảnh đã có trong wwwroot khi build. File mới ghi lên filesystem Render Free sẽ mất khi restart/sleep/redeploy. Với upload lâu dài cần bổ sung object storage; bộ này chưa thay storage provider. Không coi ảnh động trên ổ container là bền vững.
- Session WebClient và cache API ở RAM, mất khi restart/sleep. Data Protection keys không có persistent disk; người dùng có thể phải đăng nhập lại sau deploy. Chưa phải kiến trúc nhiều WebClient replica.
- Rate limit mặc định 30/phút theo user/IP; phía proxy có thể nhiều người bị gộp vào IP của proxy. Không tự tin cậy mọi X-Forwarded-For từ Internet. Khi cấu hình proxy thực, chỉ thêm proxy tin cậy qua VisualSearch__TrustedWebClientIp theo topology đã xác minh; không lấy IP tùy ý do client gửi. Đây là giới hạn cần xử lý trước tải công cộng lớn.
- Không chạy load test 10.000 yêu cầu trên tài nguyên miễn phí như bằng chứng chịu tải. Dịch vụ ngủ, quota và CPU chia sẻ sẽ làm số đo khác máy local. Warm các dịch vụ bằng một lượt kiểm tra trước buổi demo; không gửi ping liên tục nhằm né quota/ngủ.
- Điểm `/health/live` chỉ kiểm tra process. Kiểm tra thêm GET `/api/products`, mở ảnh và tìm kiếm ảnh để xác nhận cả chuỗi hoạt động.

## 8. Kiểm tra bàn giao

Đã build solution Release thành công, 0 warning/0 error; 5 kiểm thử Python đạt (cloud không DB, key, invalid image, batch/cancellation). Đã chạy riêng luồng cloud trên Windows: AI inference-only → WebAPI pgvector → 12 sản phẩm, phản hồi khớp luồng local với ảnh thử và cache HIT ổn định. Chưa build image Linux vì Docker Desktop engine chưa chạy; chưa deploy Neon/HF/Render hoặc đo dung lượng image thật. Không khẳng định đã xác minh container/cloud khi chưa có kết quả.

## Nguồn chính thức

- https://huggingface.co/docs/hub/spaces-overview — điều kiện tạo Docker Space, network và lifecycle
- https://huggingface.co/docs/hub/spaces-sdks-docker — app_port 7860 và UID 1000
- https://render.com/docs/free — sleep, quota chung, filesystem, private network
- https://render.com/docs/web-services — PORT
- https://render.com/docs/docker — Docker build
- https://neon.com/docs/connect/connect-from-any-app — chuỗi kết nối và pooled/direct host
