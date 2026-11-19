# Đánh giá AI Marketplace

Kết quả Bước 5 trên 120 sản phẩm, 480 truy vấn ảnh và 20 người dùng giả lập được lưu trong [bảng thực nghiệm](Marketplace.AIService/ket-qua-thuc-nghiem.md). Xem [hướng dẫn chạy lại](Marketplace.AIService/README.md) để tái lập phép đo với PostgreSQL và CPU của máy triển khai.
## Đối chứng lịch sử — Bước 4

## Giao thức đánh giá ảnh

Ảnh được lấy mẫu ở tâm 24 × 24 ô. Mỗi ô biểu diễn mức khác biệt với màu nền góc và ba kênh RGB có trọng số theo mức khác biệt đó. Vector 2.304 chiều được chuẩn hóa L2. Tích vô hướng giữa hai vector chuẩn hóa là độ tương đồng cosine. Danh mục được dự đoán theo ảnh gần nhất. Điểm tin cậy bằng cosine tốt nhất nhân với tỷ trọng softmax của danh mục thắng, tính trên cosine tốt nhất của từng danh mục với hệ số 24. Đây là điểm tin cậy thực nghiệm, chưa hiệu chuẩn thành xác suất dự báo đúng.

Dùng cả 120 ảnh làm truy vấn lần lượt. Mỗi lần loại đúng ảnh nguồn khỏi 120 ứng viên trước khi xếp hạng, kể cả khi truy vấn là bản nén hoặc xoay. Nhãn chuẩn lấy từ họ sản phẩm của tệp để chấm sau khi xếp hạng. Accuracy@1 là tỷ lệ đúng danh mục ở vị trí đầu. Precision@5 là số kết quả đúng danh mục chia cho 5, lấy trung bình trên các truy vấn.

| Truy vấn | Số truy vấn | Ứng viên mỗi truy vấn | Accuracy@1 | Precision@5 theo danh mục |
|---|---:|---:|---:|---:|
| PNG gốc | 120 | 119 | 100% | 100% |
| JPEG chất lượng 60 | 120 | 119 | 100% | 100% |
| Xoay 8 độ | 120 | 119 | 100% | 99,5% |

Cả ba phép đo dùng cùng 120 ảnh nguồn. Các biến thể màu của cùng một hình vẽ vẫn có trong danh mục đối sánh. Bộ số liệu phản ánh khả năng truy hồi minh họa demo. Khi đưa vào báo cáo, tách riêng thí nghiệm ảnh chụp thực tế: thu ít nhất 10 ảnh mới cho mỗi danh mục, thay đổi nền, góc chụp, khoảng cách, giữ nguyên kho đối sánh và ghi nhãn thủ công trước khi chạy. Ghi top 5, nhãn dự đoán, độ tin cậy và lập ma trận nhầm lẫn 6 × 6. Với ảnh khác loại sản phẩm, ghi riêng tỷ lệ bị gán nhãn với điểm tin cậy cao. Hệ thống hiện chọn trong các danh mục đã biết.

## Gợi ý

Điểm nội dung gồm 45% cosine ảnh, 40% trùng danh mục và 15% độ trùng thuộc tính (Jaccard trên các cặp khóa–giá trị). Điểm cá nhân là trung bình điểm nội dung với các sản phẩm trong lịch sử, có trọng số VIEW 1, CLICK 2, ADD_TO_CART 4, PURCHASE 8. Độ phổ biến dùng log(1 + tổng trọng số tương tác) cộng với tổng sao hiển thị chia cho 5 × (số đánh giá + 3). Hệ số 3 giảm ảnh hưởng của sản phẩm mới chỉ có một đánh giá.

| Kịch bản | Cách kiểm tra | Tiêu chí |
|---|---|---|
| Guest / người dùng mới | Tạo tương tác cho một sản phẩm trên DB kiểm thử, mở trang chủ ở phiên khách | Sản phẩm vừa có tín hiệu phổ biến tăng hạng. Khi hoàn toàn thiếu tín hiệu, thứ tự hòa điểm ổn định theo ID |
| Cá nhân hóa | Dùng hai tài khoản có lịch sử tập trung vào hai nhóm khác nhau, so top 8 với phiên khách | Ghi tỷ lệ top 8 thuộc danh mục ưa thích của từng người |
| Thứ tự trọng số | Từ các lịch sử trống, tạo riêng từng loại sự kiện cho các sản phẩm tương đương, giữ nguyên điểm phổ biến khi so hồ sơ | Đóng góp vào hồ sơ tăng theo thứ tự 1, 2, 4, 8 |
| Gợi ý theo ngữ cảnh | Mở chi tiết một sản phẩm, kiểm tra top 8 | Loại chính sản phẩm đang xem. Đánh giá theo danh mục, ảnh và thuộc tính |
| Khả năng hiển thị | Chuyển ứng viên đầu sang HIDDEN rồi DISCONTINUED | ID đó biến mất khỏi kết quả tìm ảnh và gợi ý |
| Chất lượng gợi ý trên lịch sử thực | Chia tương tác theo thời gian, giữ lại lần mua cuối mỗi người. Xây hồ sơ và thống kê phổ biến chỉ từ giai đoạn trước | Báo cáo HitRate@8 và MRR@8 cho người có lịch sử, so với xếp hạng phổ biến. Báo cáo riêng người mới và số người đủ dữ liệu |

Kịch bản chia theo thời gian cần lịch sử mua độc lập đủ lớn. Chưa có số đo HitRate/MRR trên người dùng thật.

## Tái lập

Sau khi cài requirements.txt, từ thư mục chứa solution chạy:

```powershell
dotnet clean Marketplace.sln -c Release
dotnet build Marketplace.sln -c Release
python tests/evaluate_ai.py
python tests/run_postgres.py --ai
```

Lệnh cuối tự tạo rồi dọn cụm PostgreSQL riêng với cổng tạm, thực thi migration, khởi động FastAPI, WebAPI và WebClient, gửi các yêu cầu thật, dừng FastAPI rồi chạy lại qua C#. Cần PostgreSQL 17 tại thư mục cài đặt mặc định của Windows hoặc đặt PG_BIN trỏ tới thư mục bin. Kiểm thử xác nhận vector lưu trữ, đối chiếu vector Python/C#, ba định dạng ảnh, dữ liệu lỗi, giới hạn byte, bộ lọc trạng thái, các khối gợi ý và form MVC có token. Bộ kiểm thử tiếp tục chạy các luồng hồi quy Bước 3. Thao tác xem trước của hộp thoại có thể kiểm tra trực quan bằng kịch bản trình diễn trong BUOC-4.md.

