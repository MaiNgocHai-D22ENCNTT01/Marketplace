"""Run from Marketplace.AIService. Writes a reproducible UTF-8 Markdown report."""
import argparse
import io
import math
import platform
import random
from collections import defaultdict
from datetime import datetime, timedelta
from pathlib import Path
from time import perf_counter
from uuid import UUID

import numpy as np
import onnxruntime
from fastapi.testclient import TestClient
from PIL import Image

import main as ai

SEED = 20261114


def variants(image, rng):
    pixels = np.asarray(image, dtype=np.float32)
    noisy = np.clip(pixels + rng.normal(0, 5, pixels.shape), 0, 255).astype("uint8")
    w, h = image.size
    dx, dy = round(.075*w), round(.075*h)
    for name, frame, fmt in [
        ("Nhiễu Gaussian σ=5/255", Image.fromarray(noisy), "PNG"),
        ("JPEG Q60", image, "JPEG"),
        ("Xoay +10°", image.rotate(10, Image.Resampling.BICUBIC, fillcolor=image.getpixel((0,0))), "PNG"),
        ("Crop giữa 85% mỗi chiều", image.crop((dx, dy, w-dx, h-dy)), "PNG"),
    ]:
        buf = io.BytesIO()
        frame.save(buf, fmt, **({"quality": 60} if fmt == "JPEG" else {}))
        yield name, buf.getvalue()


def table(headers, rows):
    lines = [headers, ["---"] * len(headers), *rows]
    return "\n".join("| " + " | ".join(str(v) for v in row) + " |" for row in lines)


def visual_eval(client, rows):
    rng = np.random.default_rng(SEED)
    data = defaultdict(list)
    def search(raw):
        response = client.post("/ai/visual-search?limit=5", files={"file": ("image", raw)})
        response.raise_for_status()
        return response.json()
    search(ai.local_image(rows[0][2]).read_bytes())
    for pid, cat, url, _, _ in rows:
        image = ai.decode(ai.local_image(url).read_bytes())
        for name, raw in variants(image, rng):
            start = perf_counter()
            result = search(raw)
            ms = (perf_counter()-start)*1000
            hits = [m["id"] for m in result["matches"]]
            rr = 1/(hits.index(pid)+1) if pid in hits else 0
            # Product-level leave-one-out supplementary classification score.
            unseen_cat, _, _ = ai.search_images(ai.features(raw), [r for r in rows if r[0] != pid])
            data[name].append([cat == result["category"], cat == unseen_cat, hits[0] == pid,
                               pid in hits, rr, ms])
    data["Toàn bộ"] = [v for group in data.values() for v in group]
    output = []
    for name, values in data.items():
        *acc, ap, latency = np.mean(values, axis=0)
        output.append([name, len(values), *(f"{x*100:.2f}" for x in acc),
                       f"{ap:.4f}", f"{latency:.2f}"])
    return table(["Biến thể", "n", "Category Acc. (%)", "Category LOO (%)",
                  "Top-1 (%)", "Top-5 (%)", "mAP@5", "Latency TB (ms)"], output)


def rec_eval(items):
    rng = random.Random(SEED)
    groups = defaultdict(list)
    for pid, category in items:
        groups[category].append(pid)
    eligible = sorted(c for c, pool in groups.items() if len(pool) >= 8 and len(items)-len(pool) >= 2)
    if not eligible:
        return "Không đủ dữ liệu: cần một danh mục có ít nhất 8 sản phẩm và 2 sản phẩm ngoài danh mục."
    anchor = datetime(2026, 1, 1)
    metrics = []
    held = []
    events = []
    for i in range(20):
        user = str(UUID(int=i+1))
        category = eligible[i % len(eligible)]
        pool = groups[category]
        # Eight unique products from a preferred category, two from other categories.
        purchases = rng.sample(pool, 8) + rng.sample([p for p,c in items if c != category], 2)
        rng.shuffle(purchases)
        cutoff = math.floor(.8*len(purchases))
        for j, pid in enumerate(purchases[:cutoff]):
            for hours, action in enumerate(("VIEW", "CLICK", "ADD_TO_CART", "PURCHASE")):
                events.append((user, pid, action, anchor + timedelta(days=3*j, hours=hours)))
        held.append((user, set(purchases[cutoff:])))
    now = anchor + timedelta(days=24)
    with ai.database() as db, db.cursor() as cur:
        # Session-local tables shadow production events and reviews for this benchmark.
        cur.execute("CREATE TEMP TABLE user_interaction_logs (user_id uuid,product_id uuid,action_type text,timestamp timestamp,id uuid DEFAULT gen_random_uuid()) ON COMMIT DROP")
        cur.execute("CREATE TEMP TABLE reviews (product_id uuid,rating integer,status text) ON COMMIT DROP")
        cur.executemany("INSERT INTO pg_temp.user_interaction_logs VALUES (%s,%s,%s,%s)", events)
        ai.recommend(cur, None, held[0][0], 5, now)
        for user, truth in held:
            start = perf_counter()
            hits = ai.recommend(cur, None, user, 5, now)
            ms = (perf_counter()-start)*1000
            relevant = [int(p in truth) for p in hits]
            dcg = sum(r/math.log2(j+2) for j,r in enumerate(relevant))
            ideal = sum(1/math.log2(j+2) for j in range(min(5,len(truth))))
            metrics.append([sum(relevant)/5, sum(relevant)/len(truth), dcg/ideal, ms])
    return table(["Kịch bản", "n", "Precision@5", "Recall@5", "NDCG@5", "Latency TB (ms)"],
                 [["Hold-out theo thời gian", len(metrics), *(f"{v:.4f}" for v in np.mean(metrics, axis=0))]])


def run(output, dataset="demo"):
    with TestClient(ai.app) as client:
        with ai.database() as db, db.cursor() as cur:
            all_rows = ai.gallery(cur)
        rows = sorted([r for r in all_rows if r[2].startswith(f"/images/{dataset}/")], key=lambda r: (r[0], r[2]))
        items = sorted({(r[0], r[1]) for r in rows})
        if dataset == "demo" and (len(rows) != 120 or len(items) != 120 or len({c for _,c in items}) != 6):
            raise SystemExit(f"Cần bộ demo Bước 4 đang hoạt động: tìm thấy {len(rows)} ảnh, {len(items)} sản phẩm.")
        if len(items) < 2:
            raise SystemExit("Cần ít nhất 2 sản phẩm đã lập chỉ mục để đánh giá.")
        visual = visual_eval(client, rows)
        recs = rec_eval(items)
    report = f"""# Kết quả thực nghiệm Marketplace Bước 5

MobileNetV3-Small IMAGENET1K_V1, embedding L2 {ai.DIM} chiều, KNN k=5. Seed {SEED}.
Python {platform.python_version()}, ONNX Runtime {onnxruntime.__version__}, CPU {platform.processor()}, {ai.session().get_session_options().intra_op_num_threads} luồng suy luận.

## Nhận diện và truy hồi ảnh

{visual}

Tập {dataset}: {len(rows)} ảnh gốc của {len(items)} sản phẩm thuộc {len({c for _,c in items})} danh mục tạo {len(rows)*4} truy vấn. Gallery phục vụ có {len(all_rows)} ảnh đang hoạt động.
Category Acc. đo nhận diện biến thể của sản phẩm đã lập chỉ mục. Category LOO loại toàn bộ ảnh của sản phẩm truy vấn khỏi tập KNN (chỉ số bổ sung trên bộ demo).
Mỗi truy vấn có một product ID liên quan, nên AP@5 bằng nghịch đảo thứ hạng nếu ID nằm trong top 5, ngược lại bằng 0.
Latency sau warm-up đo một yêu cầu FastAPI trong tiến trình, gồm multipart, tiền xử lý, CNN, PostgreSQL và trả JSON. Phép đo loại thời gian tạo biến thể, phép thử LOO, mạng và WebAPI .NET.

## Gợi ý cá nhân hóa

{recs}

20 người dùng giả lập, mỗi người mua 10 sản phẩm riêng biệt (8 cùng danh mục ưa thích, 2 ngoài danh mục), thứ tự mua được xáo với seed cố định.
Mỗi lượt mua có VIEW, CLICK, ADD_TO_CART, PURCHASE. Chỉ 8 lượt đầu/người vào tập huấn luyện, 2 lượt cuối làm nhãn.
Cả hồ sơ và điểm phổ biến chỉ thấy 160 lượt mua quá khứ. Bảng đánh giá trống để cô lập hành vi giả lập.
Tập ứng viên gồm toàn bộ sản phẩm khả dụng, kể cả sản phẩm từng xem/mua, giống endpoint. Precision mẫu số 5, Recall mẫu số 2, NDCG dùng độ liên quan nhị phân, trung bình đều theo người dùng.
Latency đo SQL và hàm xếp hạng thực tế sau warm-up trên kết nối mở. Kết quả mô phỏng phụ thuộc giả định về sở thích, chưa đo hiệu quả với khách hàng thực.
"""
    output.write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=Path("ket-qua-thuc-nghiem.md"))
    parser.add_argument("--dataset", choices=("demo", "real"), default="demo")
    args = parser.parse_args()
    run(args.output, args.dataset)
