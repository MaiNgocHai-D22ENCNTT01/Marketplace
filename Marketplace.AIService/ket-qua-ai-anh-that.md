# Kết quả thực nghiệm Marketplace Bước 5

MobileNetV3-Small IMAGENET1K_V1, embedding L2 1024 chiều, KNN k=5. Seed 20261114.
Python 3.14.6, ONNX Runtime 1.32.0, CPU Intel64 Family 6 Model 141 Stepping 1, GenuineIntel, 2 luồng suy luận.

## Nhận diện và truy hồi ảnh

| Biến thể | n | Category Acc. (%) | Category LOO (%) | Top-1 (%) | Top-5 (%) | mAP@5 | Latency TB (ms) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Nhiễu Gaussian σ=5/255 | 428 | 100.00 | 78.97 | 97.90 | 100.00 | 0.9895 | 50.98 |
| JPEG Q60 | 428 | 100.00 | 78.97 | 97.90 | 100.00 | 0.9895 | 37.01 |
| Xoay +10° | 428 | 100.00 | 79.44 | 97.90 | 100.00 | 0.9895 | 41.46 |
| Crop giữa 85% mỗi chiều | 428 | 100.00 | 78.50 | 97.90 | 100.00 | 0.9895 | 38.03 |
| Toàn bộ | 1712 | 100.00 | 78.97 | 97.90 | 100.00 | 0.9895 | 41.87 |

Tập real: 428 ảnh gốc của 120 sản phẩm thuộc 18 danh mục tạo 1712 truy vấn. Gallery phục vụ có 548 ảnh đang hoạt động.
Category Acc. đo nhận diện biến thể của sản phẩm đã lập chỉ mục. Category LOO loại toàn bộ ảnh của sản phẩm truy vấn khỏi tập KNN (chỉ số bổ sung trên bộ demo).
Mỗi truy vấn có một product ID liên quan, nên AP@5 bằng nghịch đảo thứ hạng nếu ID nằm trong top 5, ngược lại bằng 0.
Latency sau warm-up đo một yêu cầu FastAPI trong tiến trình, gồm multipart, tiền xử lý, CNN, PostgreSQL và trả JSON. Phép đo loại thời gian tạo biến thể, phép thử LOO, mạng và WebAPI .NET.

## Gợi ý cá nhân hóa

| Kịch bản | n | Precision@5 | Recall@5 | NDCG@5 | Latency TB (ms) |
| --- | --- | --- | --- | --- | --- |
| Hold-out theo thời gian | 20 | 0.0500 | 0.1250 | 0.0606 | 153.9375 |

20 người dùng giả lập, mỗi người mua 10 sản phẩm riêng biệt (8 cùng danh mục ưa thích, 2 ngoài danh mục), thứ tự mua được xáo với seed cố định.
Mỗi lượt mua có VIEW, CLICK, ADD_TO_CART, PURCHASE. Chỉ 8 lượt đầu/người vào tập huấn luyện, 2 lượt cuối làm nhãn.
Cả hồ sơ và điểm phổ biến chỉ thấy 160 lượt mua quá khứ. Bảng đánh giá trống để cô lập hành vi giả lập.
Tập ứng viên gồm toàn bộ sản phẩm khả dụng, kể cả sản phẩm từng xem/mua, giống endpoint. Precision mẫu số 5, Recall mẫu số 2, NDCG dùng độ liên quan nhị phân, trung bình đều theo người dùng.
Latency đo SQL và hàm xếp hạng thực tế sau warm-up trên kết nối mở. Kết quả mô phỏng phụ thuộc giả định về sở thích, chưa đo hiệu quả với khách hàng thực.
