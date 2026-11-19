# Đo lại sau khi bật pgvector — 19/11/2026

## Điều kiện đo

AI báo `ready=true`, `ann=true`. PostgreSQL có pgvector 0.8.6, 120/120 ảnh có vector. Chỉ mục `product_images_cosine_hnsw` có `indisvalid=true`, `indisready=true`.

API đo riêng chạy build Debug tại 127.0.0.1:5117 với quota tạm 100.000 request/phút; giữ nguyên API người dùng ở cổng 5116 và giao diện 5215. Một AI chạy trên Windows; không tăng replicas. Máy phát tải, API, AI và database cùng máy. Tham số tải giống các phép đo trước; trạng thái máy, cache DB và ứng dụng nền không được kiểm soát tuyệt đối. Mỗi kịch bản được đo một lần, chưa phải kết quả trung bình nhiều lần.

## So sánh số liệu

| Kịch bản | Trước pgvector | Sau pgvector |
|---|---:|---:|
| Cold: 500 request, 16 user/connection — RPS thành công | 15,00 | 24,97 |
| Cold — P95 toàn trình | 1.671,56 ms | 1.156,52 ms |
| Cold — P99 toàn trình | 2.353,89 ms | 1.412,77 ms |
| Cold — tỷ lệ lỗi | 0% | 0% |
| Warm: 10.000 request/user ảo, 128 connection, ramp 30 s — RPS | 99,68 | 96,68 |
| Warm — P95 toàn trình | 67.216,23 ms | 70.126,43 ms |
| Warm — P99 toàn trình | 69.828,54 ms | 73.080,44 ms |
| Warm — tỷ lệ lỗi | 0% | 0% |

Cold tăng throughput khoảng 66,5%, giảm P95 khoảng 30,8% trong lần đo này. Cold dùng cùng pixels một ảnh, thêm chunk WebP riêng cho mỗi request để tránh cache SHA256; 500/500 phản hồi đều MISS. Đây là đo throughput, không phải đánh giá recall.

Warm có 10.000/10.000 HIT, hoàn tất trong 103,434 giây. Cache hit không truy cập database, vì vậy pgvector không trực tiếp tăng tốc đường này. Kết quả warm lần này không cải thiện. P95 chờ phía máy phát tải là 68.753,68 ms; P95 HTTP riêng là 4.033,11 ms. HTTP vẫn gồm ảnh hưởng máy phát tải, mạng loopback, xử lý API và truyền phản hồi, không phải chỉ thời gian thực thi server. Không lấy hai percentile trừ nhau. `peak_outstanding=7395` bao gồm tác vụ chờ phía client, không phải 7395 kết nối đang được phục vụ.

## Xác minh database

Truy vấn mẫu `ORDER BY feature_vector_ann <=> vector LIMIT 12` mất 0,796 ms trong một lần EXPLAIN ANALYZE trên 120 ảnh. Planner chọn Seq Scan + Sort; không chọn HNSW cho truy vấn mẫu này. Chỉ mục tồn tại hợp lệ không có nghĩa mọi truy vấn đều dùng chỉ mục. Không ép tắt seqscan trên cấu hình ứng dụng chỉ để tạo số liệu đẹp.

Đây là truy vấn mẫu riêng, không bao gồm toàn bộ joins/lọc catalog của endpoint AI, và không phải latency đầu-cuối. Chưa chứng minh tốc độ dưới 15 ms ở hàng triệu sản phẩm. Việc chuyển tính cosine từ fallback Python sang PostgreSQL và các khác biệt trạng thái máy có thể góp phần vào thay đổi cold; chưa thể quy toàn bộ cải thiện cho chỉ mục HNSW.

## Kết luận và bước tiếp theo

Hệ thống đã nhận pgvector và hoàn tất các phép thử với lỗi 0%, nhưng CHƯA đạt hoặc chứng minh 10.000 yêu cầu đồng thời với độ trễ thấp. Các bài thử warm vẫn giới hạn 128 connections. Cần máy phát tải riêng, lặp phép đo nhiều lần, tăng corpus và dữ liệu catalog trên database thử nghiệm, đo tải theo từng mức và tài nguyên CPU/RAM/DB trước khi xác định mức tải đáp ứng mục tiêu. Sau đó mới so sánh 1/2/4 AI replicas trên phần cứng đủ tài nguyên. Không dùng lỗi 0% một mình để kết luận đạt yêu cầu hiệu năng.

## Số liệu và lệnh tái lập

- `visual-load-ann-cold-500.json`
- `visual-load-ann-warm-10000.json`
- `visual-load-ann-database.json`
- Đối chứng: `visual-load-cold-500.json`, `visual-load-warm-10000.json`

Trong phiên API thử nghiệm riêng đã cấu hình quota và cổng 5117:

```powershell
Marketplace.AIService/.venv/Scripts/python.exe load_test.py --port 5117 --mode cold --users 16 --requests 500 --connections 16 --output visual-load-ann-cold-500.json
Marketplace.AIService/.venv/Scripts/python.exe load_test.py --port 5117 --mode warm --users 10000 --requests 10000 --connections 128 --ramp 30 --output visual-load-ann-warm-10000.json
```

API thử nghiệm được dừng sau đo. AI và ứng dụng người dùng tiếp tục hoạt động.
