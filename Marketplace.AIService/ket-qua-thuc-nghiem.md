# Kết quả thực nghiệm Marketplace Bước 5

MobileNetV3-Small IMAGENET1K_V1, embedding L2 1024 chiều, KNN k=5. Seed 20261114.
Python 3.14.6, PyTorch 2.12.1+cpu, CPU Intel64 Family 6 Model 141 Stepping 1, GenuineIntel, 2 luồng suy luận.

## Nhận diện và truy hồi ảnh

| Biến thể | n | Category Acc. (%) | Category LOO (%) | Top-1 (%) | Top-5 (%) | mAP@5 | Latency TB (ms) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Nhiễu Gaussian σ=5/255 | 120 | 100.00 | 100.00 | 36.67 | 75.83 | 0.4885 | 89.27 |
| JPEG Q60 | 120 | 100.00 | 100.00 | 98.33 | 100.00 | 0.9903 | 86.26 |
| Xoay +10° | 120 | 100.00 | 100.00 | 86.67 | 99.17 | 0.9186 | 92.96 |
| Crop giữa 85% mỗi chiều | 120 | 100.00 | 100.00 | 97.50 | 100.00 | 0.9875 | 87.78 |
| Toàn bộ | 480 | 100.00 | 100.00 | 79.79 | 93.75 | 0.8462 | 89.07 |

120 ảnh gốc thuộc 6 danh mục tạo 480 truy vấn. Gallery phục vụ có 120 ảnh đang hoạt động.
Category Acc. đo nhận diện biến thể của sản phẩm đã lập chỉ mục. Category LOO loại toàn bộ ảnh của sản phẩm truy vấn khỏi tập KNN (chỉ số bổ sung trên bộ demo).
Mỗi truy vấn có một product ID liên quan, nên AP@5 bằng nghịch đảo thứ hạng nếu ID nằm trong top 5, ngược lại bằng 0.
Latency sau warm-up đo một yêu cầu FastAPI trong tiến trình, gồm multipart, tiền xử lý, CNN, PostgreSQL và trả JSON. Phép đo loại thời gian tạo biến thể, phép thử LOO, mạng và WebAPI .NET.

## Gợi ý cá nhân hóa

| Kịch bản | n | Precision@5 | Recall@5 | NDCG@5 | Latency TB (ms) |
| --- | --- | --- | --- | --- | --- |
| Hold-out theo thời gian | 20 | 0.0300 | 0.0750 | 0.0404 | 6.7975 |

20 người dùng giả lập, mỗi người mua 10 sản phẩm riêng biệt (8 cùng danh mục ưa thích, 2 ngoài danh mục), thứ tự mua được xáo với seed cố định.
Mỗi lượt mua có VIEW, CLICK, ADD_TO_CART, PURCHASE. Chỉ 8 lượt đầu/người vào tập huấn luyện, 2 lượt cuối làm nhãn.
Cả hồ sơ và điểm phổ biến chỉ thấy 160 lượt mua quá khứ. Bảng đánh giá trống để cô lập hành vi giả lập.
Tập ứng viên gồm toàn bộ sản phẩm khả dụng, kể cả sản phẩm từng xem/mua, giống endpoint. Precision mẫu số 5, Recall mẫu số 2, NDCG dùng độ liên quan nhị phân, trung bình đều theo người dùng.
Latency đo SQL và hàm xếp hạng thực tế sau warm-up trên kết nối mở. Kết quả mô phỏng phụ thuộc giả định về sở thích, chưa đo hiệu quả với khách hàng thực.
