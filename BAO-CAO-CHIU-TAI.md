# Kết quả kiểm tra ngày 19/11/2026 (giờ Việt Nam)

## Môi trường và điều kiện

Windows 11; WebAPI .NET 8 build Debug; một tiến trình AI ONNX CPU, 2 threads, batch tối đa 32/cửa sổ 12 ms; PostgreSQL Windows hiện có. `/health/ready` báo `ann=false`: các phép đo này dùng quét chính xác, chưa dùng HNSW. Máy phát tải, API, AI và DB cùng máy. Corpus đo này chỉ có `tee-mint.png`; cold giữ pixels và thêm chunk WebP để đổi SHA256. Không dùng kết quả này để suy luận chất lượng tìm kiếm hoặc năng lực trên triệu sản phẩm.

Các JSON giữ UTC để máy đọc, tương ứng sáng 19/11/2026 tại Việt Nam. Thử warm/cold lớn dùng quota tạm 100.000 lần/phút riêng tiến trình đo; cấu hình mặc định vẫn 30/phút. Không giả IP và không bỏ qua lỗi.

| Phép đo | Tổng request / user ảo / giới hạn connection | Success RPS | P95 / P99 toàn trình | Lỗi |
|---|---|---:|---|---:|
| Smoke sau sửa IPv4 | 25 / 5 / 5 | 38,73 | 525,02 / 571,49 ms | 0% |
| Warm cache, ramp 30 s | 10.000 / 10.000 / 128 | 99,68 | 67.216,23 / 69.828,54 ms | 0% |
| Cold, bắt buộc gọi AI | 500 / 16 / 16 | 15,00 | 1.671,56 / 2.353,89 ms | 0% |
| Quota mặc định, bản build cuối | 35 / 1 / 1 | 56,54 | 7,93 / 431,32 ms | 5/35 là 429 đúng kỳ vọng |

## Cách diễn giải

10.000 yêu cầu cache đã được xử lý hết nhưng mất 100,325 giây. P95 chờ phía máy phát tải là 66.002,25 ms, gần toàn bộ latency. `peak_outstanding=7012` đếm cả tác vụ đang chờ, không phải kết nối đang phục vụ. Đây KHÔNG phải bằng chứng 10.000 request đồng thời với latency thấp. Cần máy phát tải độc lập, phân tán tải và đo thêm để xác định giới hạn server.

500 cache miss hoàn tất trong 33,331 giây, tất cả trả 200; throughput hiện tại chỉ 15 RPS. Cần pgvector, dữ liệu đủ lớn, phần cứng phù hợp, thử nhiều replicas và kiểm tra query plan trước khi đưa ra mục tiêu hàng ngàn RPS. Không có số đo ONNX nhanh hơn PyTorch 3–4 lần hoặc SQL <15 ms trong đợt này.

Phép thử quota cuối trả chính xác 30 × 200 và 5 × 429. Nhóm 29 cache hit của phép thử này có P95 4,35 ms, P99 7,93 ms. Mục tiêu HTTP <2 ms chưa được chứng minh. Exit code 1 của phép thử quota là đúng thiết kế script vì mọi 429 được tính vào lỗi.

Các lần thử ban đầu `visual-load-smoke.json` và `visual-load-rate-limit.json` có 503 do kết nối AI bằng localhost; giữ lại để truy vết, không dùng làm kết quả bản sửa. Báo cáo cũ `visual-load-windows.json` từ 15/11 ghi 50,22% lỗi, cũng được giữ nguyên.

## Kiểm tra đã hoàn thành

- `dotnet build Marketplace.sln --no-restore -m:1`: thành công, 0 warning, 0 error sau sửa cuối.
- `python -m unittest test_scaling -v`: 4/4 đạt; ONNX batch 32 khớp batch 1, vector L2 1024 chiều; ảnh lỗi không làm hỏng batch khác; queue đầy từ chối; shutdown giải phóng future đang chờ.
- Python syntax kiểm tra đạt; `docker compose --env-file .env.scaling.example config --quiet` đạt.
- Luồng thật WebAPI → AI → PostgreSQL trả kết quả 200 trên Windows không Docker.

Chưa chạy container replicas, chưa cài extension hoặc chạy DDL HNSW trên database hiện tại; chưa đo trình duyệt tự động hoặc throughput nhiều máy. Mã nén client có giới hạn đầu ra 30 KiB; cần kiểm tra Network trên trình duyệt với ảnh thực 5–10 MiB để thu ảnh minh chứng băng thông.

## Tệp số liệu gốc

- `visual-load-smoke-ipv4.json`
- `visual-load-warm-10000.json`
- `visual-load-cold-500.json`
- `visual-load-default-35.json`

Script hiện tại bổ sung `http_latency` bên cạnh latency toàn trình và thời gian chờ client; trường mới có trong phép thử quota cuối. Hai phép đo lớn trước đó đã ghi latency toàn trình và client wait nhưng chưa ghi riêng HTTP. Không suy ra P95 HTTP bằng phép trừ hai P95.
