---
title: Marketplace AI
emoji: 🔎
colorFrom: blue
colorTo: green
sdk: docker
app_port: 7860
pinned: false
---

# Marketplace Bước 5 — AIService và thực nghiệm

Docker Space chạy inference-only, không kết nối PostgreSQL. Đặt Secret `MARKETPLACE_AI_API_KEY`; WebAPI gửi cùng khóa qua `Ai__ApiKey`. Dùng URL gốc `https://TEN-SPACE.hf.space/`. Health: `/health/ready`; inference: `POST /ai/features` với multipart `file` và header `X-AI-Key`. Không upload `.venv`, secrets hoặc dữ liệu khách hàng. Xem `../TRIEN-KHAI-CLOUD.md` trong repo Marketplace để biết cấu hình Neon/Render và giới hạn gói dịch vụ.

Hướng dẫn ONNX, micro-batching, Docker replicas và kiểm thử tải hiện tại: xem `../HUONG-DAN-CHIU-TAI.md`. Số liệu đo thực tế và giới hạn bằng chứng: `../BAO-CAO-CHIU-TAI.md`. Runtime dùng `requirements-runtime.txt`; chỉ cần PyTorch khi xuất lại mô hình.

Chạy WebAPI Bước 5 một lần ở chế độ Development để tạo dữ liệu mẫu trên PostgreSQL 17. Dùng Python 3.14 64-bit. Từ thư mục Marketplace.AIService, trong PowerShell:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
$env:MARKETPLACE_AI_DSN = 'host=localhost port=5432 dbname=marketplace_mvp user=postgres'
$env:PGPASSWORD = Read-Host 'Mật khẩu PostgreSQL' -MaskInput
python evaluate_ai.py
```

Read-Host -MaskInput dùng PowerShell 7. Với Windows PowerShell 5.1 có thể dùng pgpass.conf của PostgreSQL. Đặt tên database, port và user giống cấu hình WebAPI. Lần đầu torchvision tải khoảng 10 MB trọng số ImageNet chính thức và lưu cache. Tạo môi trường ảo một lần. Script in hai bảng Markdown và lưu UTF-8 vào `ket-qua-thuc-nghiem.md`. Dùng `--output duong-dan.md` để đổi tên. Đánh giá tự mở ứng dụng FastAPI trong tiến trình, tự đồng bộ vector vào DB và dùng bảng tạm riêng cho hành vi giả lập. Chạy với bộ demo đủ 120 sản phẩm đang hoạt động.

Chạy dịch vụ để dùng giao diện mua sắm:

```powershell
python -m uvicorn main:app --host 127.0.0.1 --port 8000
```

Trong cửa sổ khác, sau khi thay ảnh hoặc bổ sung sản phẩm:

```powershell
Invoke-RestMethod -Method Post http://127.0.0.1:8000/ai/reindex-all
```

Kết quả trên bộ demo chuẩn có indexed=120, dimension=1024. Dịch vụ đọc ảnh từ Marketplace.WebAPI/wwwroot/images. Có thể đặt MARKETPLACE_WEBROOT nếu triển khai ở vị trí khác. Ảnh ngoài thư mục ảnh nội bộ được đếm vào skipped. Khi khởi động, vector cũ 2304 chiều và vector rỗng được thay trong một giao dịch. Dùng một worker để dùng chung chỉ mục bộ nhớ trong tiến trình. Đặt MARKETPLACE_AI_THREADS để điều chỉnh số luồng CPU (mặc định 2). Độ trễ phụ thuộc CPU, kích thước ảnh, DB và tải.

POST /ai/visual-search nhận multipart file, limit tùy chọn. category và confidence là nhãn và tỷ lệ phiếu KNN có trọng số theo cosine (k=5, nhiệt độ 0,05). Confidence chưa hiệu chuẩn xác suất. matches xếp theo cosine của embedding L2 ở đầu ra 1024 chiều ngay trước lớp logits ImageNet. Mỗi sản phẩm xuất hiện một lần với điểm ảnh giống nhất. WebAPI giữ nhãn KNN và trả 503 khi dịch vụ AI mất kết nối.

GET /ai/recommendations nhận productId, userId và limit. Độ tương đồng gồm 60% cosine ảnh chính và 40% metadata (cùng danh mục, Jaccard trên cặp thuộc tính–giá trị). Hồ sơ lấy tối đa 10 sản phẩm có điểm hành vi cao nhất, trọng số mua 5, thêm giỏ 3, click 1,5, xem 1, chu kỳ bán rã 30 ngày. Cold-start dùng 50% lượng tương tác log chuẩn hóa, 30% tỷ lệ sự kiện ngoài VIEW, 20% điểm sao trung bình của review sản phẩm hiển thị. Schema Bước 4 chỉ lưu rating_avg trực tiếp cho shop. Với người dùng có lịch sử, hệ thống phối 85% điểm sở thích và 15% điểm phổ biến. Khi có sản phẩm đang xem, hệ thống phối 70% tương đồng nguồn, 20% sở thích (hoặc phổ biến nếu chưa có lịch sử), 10% phổ biến.
