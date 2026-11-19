"""Import photographed sample products; see IMPORT_REAL_DATA.md for PowerShell usage.

No schema changes, no remote URLs in product_images. Network work finishes before
the single database transaction. Repeated imports preserve existing stock/orders.
"""
import argparse
import hashlib
import io
import json
import os
import re
import sys
import time
import unicodedata
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from urllib.request import Request, urlopen
from uuid import NAMESPACE_URL, uuid5

ROOT = Path(__file__).resolve().parent
CATEGORIES = {
    "smartphones": "Điện thoại & Máy tính bảng", "tablets": "Điện thoại & Máy tính bảng",
    "laptops": "Laptop", "mens-shirts": "Thời trang Nam", "mens-shoes": "Giày dép & Phụ kiện",
    "womens-dresses": "Thời trang Nữ", "womens-shoes": "Giày dép & Phụ kiện", "tops": "Thời trang Nữ",
    "beauty": "Mỹ phẩm", "skin-care": "Mỹ phẩm", "fragrances": "Nước hoa",
    "furniture": "Nội thất", "home-decoration": "Đồ gia dụng", "kitchen-accessories": "Đồ gia dụng",
    "mobile-accessories": "Phụ kiện điện thoại", "mens-watches": "Đồng hồ",
    "womens-watches": "Đồng hồ", "womens-bags": "Túi xách", "womens-jewellery": "Trang sức",
    "sunglasses": "Kính mắt", "sports-accessories": "Thể thao & Dã ngoại",
    "groceries": "Thực phẩm", "motorcycle": "Xe máy", "vehicle": "Ô tô",
}
COMMENTS = ["Sản phẩm đẹp, đóng gói cẩn thận.", "Hình ảnh rõ ràng, chất lượng phù hợp giá tiền.",
            "Mẫu mã ưng ý, dễ sử dụng."]


def uid(key):
    return str(uuid5(NAMESPACE_URL, "marketplace/import-real/v1/" + key))


def slug(name):
    text = unicodedata.normalize("NFKD", name.lower().replace("đ", "d"))
    return re.sub(r"[^a-z0-9]+", "-", text.encode("ascii", "ignore").decode()).strip("-")


def fetch(url, maximum=20 * 1024 * 1024):
    if not url.startswith("https://"):
        raise ValueError("Nguồn phải dùng HTTPS: " + url)
    for attempt in range(4):
        try:
            with urlopen(Request(url, headers={"User-Agent": "MarketplaceDemoImporter/1.0"}), timeout=30) as response:
                raw = response.read(maximum + 1)
            if len(raw) > maximum:
                raise ValueError("Tệp tải về quá lớn")
            return raw
        except Exception:
            if attempt == 3:
                raise
            time.sleep(2 ** attempt)


def atomic_write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(raw)
    temporary.replace(path)


def prepare_catalog(args):
    import numpy as np
    from PIL import Image, ImageOps
    from inference import predict, prepare, session
    session()  # Requires the existing local ONNX model; never downloads weights.
    manifest = args.cache / "dummyjson-products.json"
    if args.refresh or not manifest.exists():
        if args.offline:
            raise ValueError("Chưa có catalog cache. Chạy một lần có mạng trước.")
        raw = fetch("https://dummyjson.com/products?limit=0")
        payload = json.loads(raw)
        if not isinstance(payload.get("products"), list) or not payload["products"]:
            raise ValueError("API không trả về danh sách sản phẩm hợp lệ")
        atomic_write(manifest, raw)
    products = json.loads(manifest.read_text(encoding="utf-8"))["products"]
    # Round-robin categories keeps a small import representative of the catalog.
    groups = {}
    for p in sorted(products, key=lambda p: int(p["id"])):
        if p["category"] not in CATEGORIES:
            raise ValueError("Cần bổ sung tên tiếng Việt cho danh mục: " + p["category"])
        groups.setdefault(p["category"], []).append(p)
    selected = []
    while any(groups.values()):
        for group in groups.values():
            if group:
                selected.append(group.pop(0))
    selected = selected[:args.limit] if args.limit else selected
    result = []
    for index, p in enumerate(selected):
        images = []
        urls = list(dict.fromkeys([*p.get("images", []), p.get("thumbnail")]))
        urls = [url for url in urls if url][:args.max_images]
        if not urls:
            raise ValueError(f"Sản phẩm {p['id']} không có ảnh")
        for url in urls:
            key = hashlib.sha256(url.encode()).hexdigest()[:24]
            path = args.webroot / "images" / "real" / f"dummyjson-{p['id']}-{key}.jpg"
            if not path.exists():
                if args.offline:
                    raise ValueError(f"Thiếu ảnh cục bộ: {path}")
                with Image.open(io.BytesIO(fetch(url))) as source:
                    if source.width * source.height > 12_000_000 or getattr(source, "n_frames", 1) != 1:
                        raise ValueError("Ảnh phải tĩnh, tối đa 12 megapixel")
                    rgba = ImageOps.exif_transpose(source).convert("RGBA")
                    image = Image.new("RGB", rgba.size, "white")
                    image.paste(rgba, mask=rgba.getchannel("A"))
                    image.thumbnail((1600, 1600))
                    output = io.BytesIO()
                    image.save(output, "JPEG", quality=92)
                    atomic_write(path, output.getvalue())
            # Infer from the actual persisted JPEG, using the service preprocessing.
            with Image.open(path) as image:
                vector = predict([prepare(image.convert("RGB"))])[0][0]
            if vector.shape != (1024,) or not np.isfinite(vector).all() or abs(float(np.linalg.norm(vector)) - 1) > .001:
                raise ValueError("Mô hình không trả vector L2 1024 chiều hợp lệ")
            images.append(("/images/real/" + path.name, json.dumps(vector.tolist(), separators=(",", ":"))))
        price = (Decimal(str(p["price"])) * args.usd_vnd / 1000).quantize(Decimal(1), rounding=ROUND_HALF_UP) * 1000
        if not price.is_finite() or not 0 < price < Decimal("10000000000"):
            raise ValueError(f"Giá không hợp lệ: sản phẩm {p['id']}")
        result.append((p, images, price))
        print(f"[{index + 1}/{len(selected)}] {p['title']}: {len(images)} ảnh đã kiểm tra", flush=True)
    return result


def insert(cur, table, **values):
    from psycopg2 import sql
    cur.execute(sql.SQL("INSERT INTO {} ({}) VALUES ({})").format(
        sql.Identifier(table), sql.SQL(",").join(map(sql.Identifier, values)),
        sql.SQL(",").join(sql.Placeholder() for _ in values)), list(values.values()))


def variants(category):
    if category in {"mens-shirts", "womens-dresses", "tops"}:
        return [{"Màu sắc": "Đen", "Kích cỡ": size} for size in ("M", "L", "XL")]
    if category in {"mens-shoes", "womens-shoes"}:
        return [{"Màu sắc": "Theo ảnh", "Kích cỡ": size} for size in ("38", "39", "40")]
    if category in {"smartphones", "tablets", "laptops", "mobile-accessories"}:
        return [{"Màu sắc": color} for color in ("Đen", "Bạc")]
    return [{"Phiên bản": "Tiêu chuẩn"}]


def connect_database():
    import psycopg2
    from psycopg2.extensions import parse_dsn
    # libpq accepts both postgresql:// URLs (Neon) and key=value DSNs.
    dsn = os.getenv("MARKETPLACE_AI_DSN", "")
    options = parse_dsn(dsn)
    host = options.get("host", os.getenv("PGHOST", "localhost"))
    if any(h.endswith(".neon.tech") for h in host.split(",")):
        if options.get("sslmode", os.getenv("PGSSLMODE")) not in {"require", "verify-ca", "verify-full"}:
            options["sslmode"] = "require"
    options.setdefault("connect_timeout", "15")
    if not dsn and not os.getenv("PGDATABASE"):
        options["dbname"] = "marketplace_mvp"
    return psycopg2.connect(**options)


def ann_mode(cur):
    cur.execute("""SELECT is_generated, udt_name FROM information_schema.columns
        WHERE table_schema='public' AND table_name='product_images'
        AND column_name='feature_vector_ann'""")
    column = cur.fetchone()
    if not column:
        return "absent"
    if column[1] != "vector":
        raise ValueError("feature_vector_ann phải có kiểu pgvector vector(1024)")
    cur.execute("""SELECT format_type(atttypid,atttypmod) FROM pg_attribute
        WHERE attrelid='public.product_images'::regclass AND attname='feature_vector_ann'""")
    if cur.fetchone()[0] != "vector(1024)":
        raise ValueError("feature_vector_ann phải khai báo vector(1024)")
    return "generated" if column[0] == "ALWAYS" else "writable"


def import_catalog(args, catalog):
    from psycopg2.extras import Json
    connection = connect_database()
    added = skipped = removed = 0
    try:
        with connection.cursor() as cur:
            cur.execute("SET LOCAL lock_timeout='10s'")
            cur.execute("SET LOCAL statement_timeout='60s'")
            cur.execute("SET LOCAL search_path TO public")
            cur.execute("SELECT pg_advisory_xact_lock(632810119)")
            # Validate the actual schema before any write, including mandatory review FKs.
            for table, cols in {
                "products": "id,shop_id,category_id,name,description,base_price,status,review_count,rating_avg",
                "product_variants": "id,product_id,sku,attributes,price,stock_quantity,status",
                "product_images": "id,product_id,image_url,is_primary,feature_vector",
                "reviews": "id,order_item_id,customer_id,product_id,rating,comment,status,images",
                "categories": "id,name,slug,status", "shops": "id,status,rating_avg,review_count",
                "order_items": "id,order_id,variant_id,product_name_snapshot,variant_snapshot,unit_price_snapshot,quantity",
                "orders": "id,order_group_id,shop_id,shipping_address_snapshot,total_amount,order_status",
                "order_groups": "id,customer_id,total_amount", "users": "id,email,password_hash,full_name,status",
            }.items():
                cur.execute(f"SELECT {cols} FROM {table} LIMIT 0")
            mode = ann_mode(cur)
            print(f"Cột ANN: {mode}; vector JSON luôn được lưu trong feature_vector.", flush=True)
            cur.execute("SELECT id FROM shops WHERE status='ACTIVE' ORDER BY id FOR SHARE")
            shops = [str(row[0]) for row in cur.fetchall()]
            if not shops:
                raise ValueError("Cần ít nhất một gian hàng ACTIVE trước khi nạp")
            if args.replace_demo:
                # Lock variants too, so concurrent order inserts cannot race this check.
                cur.execute("""SELECT p.id FROM products p WHERE EXISTS
                    (SELECT 1 FROM product_variants v WHERE v.product_id=p.id AND v.sku LIKE 'DEMO-%')
                    AND NOT EXISTS (SELECT 1 FROM product_variants v WHERE v.product_id=p.id AND v.sku NOT LIKE 'DEMO-%')
                    ORDER BY p.id FOR UPDATE""")
                for (pid,) in cur.fetchall():
                    cur.execute("SELECT id FROM product_variants WHERE product_id=%s ORDER BY id FOR UPDATE", (pid,))
                    cur.fetchall()
                    cur.execute("""SELECT 1 FROM order_items oi JOIN product_variants v ON v.id=oi.variant_id
                                   WHERE v.product_id=%s LIMIT 1""", (pid,))
                    if cur.fetchone():
                        continue
                    # Unknown/restricting references abort and roll back the entire import.
                    cur.execute("DELETE FROM products WHERE id=%s", (pid,))
                    removed += 1
            customers = []
            for n in range(3):
                customer = uid(f"reviewer/{n}")
                cur.execute("SELECT id FROM users WHERE id=%s", (customer,))
                if not cur.fetchone():
                    insert(cur, "users", id=customer, email=f"real-import-reviewer-{n}@example.invalid",
                           password_hash="!disabled-synthetic-account", full_name=f"Khách mô phỏng {n+1}", status="INACTIVE")
                customers.append(customer)
            for index, (p, images, price) in enumerate(catalog):
                source = str(p["id"])
                pid = uid("product/" + source)
                cur.execute("SELECT id FROM products WHERE id=%s", (pid,))
                if cur.fetchone():
                    skipped += 1
                    continue  # Never reset stock or alter purchased variants on a rerun.
                category = CATEGORIES[p["category"]]
                category_slug = slug(category)
                cur.execute("""INSERT INTO categories(id,name,slug,status) VALUES(%s,%s,%s,'ACTIVE')
                    ON CONFLICT(slug) DO UPDATE SET name=EXCLUDED.name,status='ACTIVE' RETURNING id""",
                    (uid("category/" + category_slug), category, category_slug))
                cid = cur.fetchone()[0]
                shop = shops[index % len(shops)]
                title = p["title"][:255]
                insert(cur, "products", id=pid, shop_id=shop, category_id=cid, name=title,
                       description=f"{category} — {title}.\n{p.get('description', '')}\n"
                       "[DỮ LIỆU MÔ PHỎNG] Nguồn: DummyJSON. Giá quy đổi tham khảo; màu/cỡ, tồn kho và đánh giá là dữ liệu demo.",
                       base_price=price, status="ACTIVE")
                attrs = variants(p["category"])
                for n, attributes in enumerate(attrs):
                    insert(cur, "product_variants", id=uid(f"variant/{source}/{n}"), product_id=pid,
                           sku=f"REAL-DJ-{source}-{n+1}", attributes=Json(attributes), price=price + n * 1000,
                           stock_quantity=max(0, int(p.get("stock", 0))) // len(attrs) + (n < int(p.get("stock", 0)) % len(attrs)),
                           status="ACTIVE")
                for n, (url, vector) in enumerate(images):
                    extra = {"feature_vector_ann": vector} if mode == "writable" else {}
                    insert(cur, "product_images", id=uid(f"image/{source}/{n}"), product_id=pid,
                           image_url=url, is_primary=n == 0, feature_vector=vector, **extra)
                    if mode != "absent":
                        cur.execute("""SELECT feature_vector_ann IS NOT NULL AND
                            feature_vector_ann = feature_vector::vector(1024)
                            FROM product_images WHERE id=%s""", (uid(f"image/{source}/{n}"),))
                        if cur.fetchone()[0] is not True:
                            raise ValueError("Cột ANN không đồng bộ với vector ảnh; rollback")
                for n in range(2 + int(p["id"]) % 2):
                    key = f"{source}/{n}"
                    group, order, item = (uid(prefix + key) for prefix in ("group/", "order/", "item/"))
                    insert(cur, "order_groups", id=group, customer_id=customers[n], total_amount=price)
                    insert(cur, "orders", id=order, order_group_id=group, shop_id=shop,
                           shipping_address_snapshot=Json({"FullName": "Khách mô phỏng", "Phone": "0000000000",
                               "DetailAddress": "DỮ LIỆU MÔ PHỎNG — KHÔNG GIAO HÀNG", "Source": "import_real_data"}),
                           total_amount=price, order_status="COMPLETED", shipping_fee=0, discount_amount=0)
                    insert(cur, "order_items", id=item, order_id=order, variant_id=uid(f"variant/{source}/0"),
                           product_name_snapshot=title, variant_snapshot=Json(attrs[0]), unit_price_snapshot=price, quantity=1)
                    insert(cur, "reviews", id=uid("review/" + key), order_item_id=item,
                           customer_id=customers[n], product_id=pid, rating=4 + n % 2,
                           comment="[Đánh giá mô phỏng] " + COMMENTS[n], status="ACTIVE", images=[])
                added += 1
            cur.execute("""UPDATE products p SET review_count=t.n,rating_avg=t.avg FROM
                (SELECT p0.id,count(r.id)::int n,coalesce(avg(r.rating),0) avg FROM products p0
                 LEFT JOIN reviews r ON r.product_id=p0.id AND r.status='ACTIVE' GROUP BY p0.id) t WHERE t.id=p.id""")
            cur.execute("""UPDATE shops s SET review_count=t.n,rating_avg=t.avg FROM
                (SELECT s0.id,count(r.id)::int n,coalesce(avg(r.rating),0) avg FROM shops s0
                 LEFT JOIN products p ON p.shop_id=s0.id LEFT JOIN reviews r ON r.product_id=p.id
                 AND r.status='ACTIVE' GROUP BY s0.id) t WHERE t.id=s.id""")
        if args.dry_run:
            connection.rollback()
        else:
            connection.commit()
    except BaseException:
        connection.rollback()
        raise
    finally:
        connection.close()
    print(f"{'THỬ, ĐÃ ROLLBACK' if args.dry_run else 'ĐÃ COMMIT'}: thêm {added}, bỏ qua {skipped}, dọn demo {removed}.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--limit", type=int, default=120, help="Số sản phẩm, 0 = tất cả")
    parser.add_argument("--max-images", type=int, default=4)
    parser.add_argument("--usd-vnd", type=Decimal, default=Decimal("25000"), help="Tỷ giá giả định cho demo")
    parser.add_argument("--cache", type=Path, default=ROOT / "real_data_cache")
    parser.add_argument("--webroot", type=Path, default=Path(os.getenv("MARKETPLACE_WEBROOT") or ROOT.parent / "Marketplace.WebAPI/wwwroot"))
    parser.add_argument("--offline", action="store_true", help="Chỉ dùng catalog và ảnh cục bộ")
    parser.add_argument("--refresh", action="store_true", help="Tải lại catalog nguồn")
    parser.add_argument("--replace-demo", "--clean-demo", dest="replace_demo", action="store_true",
                        help="Dọn sản phẩm chỉ có SKU DEMO- và chưa có đơn hàng")
    parser.add_argument("--download-only", action="store_true", help="Chuẩn bị ảnh, kiểm tra vector, không kết nối DB")
    parser.add_argument("--dry-run", action="store_true", help="Thử toàn bộ SQL rồi rollback; vẫn lưu cache/ảnh")
    args = parser.parse_args()
    if args.limit < 0 or args.max_images < 1 or not args.usd_vnd.is_finite() or args.usd_vnd <= 0:
        parser.error("limit >= 0, max-images >= 1, usd-vnd phải hữu hạn và > 0")
    if args.offline and args.refresh:
        parser.error("Không kết hợp --offline với --refresh")
    catalog = prepare_catalog(args)
    if not args.download_only:
        import_catalog(args, catalog)


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    try:
        main()
    except KeyboardInterrupt:
        print("Đã hủy; giao dịch database chưa commit được rollback.", file=sys.stderr)
        sys.exit(130)
    except Exception as exc:
        print(f"Nạp thất bại ({type(exc).__name__}): {exc}", file=sys.stderr)
        sys.exit(1)
