"""CPU inference and catalog ranking for Marketplace Step 5."""
import asyncio
import io
import json
import os
import threading
import secrets
from collections import Counter
from contextlib import asynccontextmanager, contextmanager

from pathlib import Path
from uuid import UUID

import numpy as np
import psycopg2
from psycopg2.pool import ThreadedConnectionPool
from starlette.concurrency import run_in_threadpool
from inference import ImageBatcher, predict, prepare, session
from fastapi import FastAPI, File, HTTPException, Query, UploadFile
from PIL import Image, ImageOps
from starlette.responses import JSONResponse

DIM = 1024
MAX_BYTES = 5 * 1024 * 1024
ROOT = Path(os.getenv("MARKETPLACE_WEBROOT") or
            Path(__file__).resolve().parents[1] / "Marketplace.WebAPI/wwwroot").resolve()
pool = None
has_ann = False
INFERENCE_ONLY = os.getenv("MARKETPLACE_INFERENCE_ONLY", "false").lower() == "true"
API_KEY = os.getenv("MARKETPLACE_AI_API_KEY", "")
DB_CONNECTIONS = int(os.getenv("MARKETPLACE_DB_CONNECTIONS", "16"))
db_slots = threading.BoundedSemaphore(DB_CONNECTIONS)


def decode(raw):
    if len(raw) > MAX_BYTES:
        raise HTTPException(413, "Ảnh vượt quá 5 MiB.")
    try:
        with Image.open(io.BytesIO(raw), formats=("JPEG", "PNG", "WEBP")) as image:
            if image.width * image.height > 12_000_000 or getattr(image, "n_frames", 1) != 1:
                raise HTTPException(400, "Chọn ảnh tĩnh tối đa 12 megapixel.")
            rgba = ImageOps.exif_transpose(image).convert("RGBA")
            rgb = Image.new("RGB", rgba.size, "white")
            rgb.paste(rgba, mask=rgba.getchannel("A"))
            return rgb
    except (ValueError, OSError, Image.DecompressionBombError, SyntaxError):
        raise HTTPException(400, "Cần ảnh JPG, PNG hoặc WEBP hợp lệ.")


def features(raw):
    return predict([prepare(decode(raw))])[0]


def embedding(raw):
    return features(raw)[0]


@contextmanager
def database():
    if not db_slots.acquire(blocking=False):
        raise HTTPException(503, "PostgreSQL đang bận. Vui lòng thử lại.")
    try:
        if pool is None:
            standalone = psycopg2.connect(os.getenv("MARKETPLACE_AI_DSN") or
                    "dbname=marketplace_mvp user=postgres host=localhost", connect_timeout=3)
            try:
                with standalone:
                    yield standalone
            finally:
                standalone.close()
        else:
            db = pool.getconn()
            try:
                with db:
                    yield db
            finally:
                pool.putconn(db)
    except psycopg2.Error as exc:
        raise HTTPException(503, "Không truy cập được PostgreSQL của Marketplace.") from exc
    finally:
        db_slots.release()


def local_image(url):
    if not url.startswith("/images/"):
        return None
    path = (ROOT / url[1:]).resolve()
    return path if path.is_relative_to(ROOT / "images") else None


def stored_vector(raw):
    try:
        v = np.asarray(json.loads(raw), dtype=np.float32)
        if v.shape == (DIM,) and np.isfinite(v).all() and abs(np.linalg.norm(v) - 1) < .001:
            return v
    except (TypeError, ValueError):
        pass
    return None


def index_images(force):
    updated = skipped = indexed = 0
    last = "00000000-0000-0000-0000-000000000000"
    while True:
        with database() as db, db.cursor() as cur:
            cur.execute("SELECT pg_advisory_xact_lock(632810119)")
            cur.execute("""SELECT id,image_url,feature_vector FROM product_images
                           WHERE id > %s ORDER BY id LIMIT 128 FOR UPDATE""", (last,))
            page = cur.fetchall()
            if not page:
                break
            for image_id, url, raw in page:
                last = image_id
                path = local_image(url)
                if path is None:
                    skipped += 1
                    continue
                if force or stored_vector(raw) is None:
                    if not path.is_file():
                        raise HTTPException(409, f"Thiếu tệp ảnh {image_id}. Kiểm tra MARKETPLACE_WEBROOT.")
                    with path.open("rb") as f:
                        v = embedding(f.read(MAX_BYTES + 1))
                    cur.execute("UPDATE product_images SET feature_vector=%s WHERE id=%s",
                                (json.dumps(v.tolist(), separators=(",", ":")), image_id))
                    updated += 1
                indexed += 1
    return dict(indexed=indexed, updated=updated, skipped=skipped, dimension=DIM)


@asynccontextmanager
async def lifespan(app):
    global pool, has_ann
    if os.getenv("MARKETPLACE_REQUIRE_API_KEY", "false").lower() == "true" and not API_KEY:
        raise RuntimeError("Set MARKETPLACE_AI_API_KEY in Space Secrets")
    session()
    if INFERENCE_ONLY:
        app.state.batcher = ImageBatcher(decode)
        worker = asyncio.create_task(app.state.batcher.run())
        try:
            yield
        finally:
            await app.state.batcher.stop(worker)
        return
    pool = ThreadedConnectionPool(1, DB_CONNECTIONS, os.getenv("MARKETPLACE_AI_DSN") or
                                 "dbname=marketplace_mvp user=postgres host=localhost",
                                 connect_timeout=3, options="-c statement_timeout=5000")
    try:
        with database() as db, db.cursor() as cur:
            cur.execute("""SELECT EXISTS(SELECT FROM pg_attribute WHERE attrelid='product_images'::regclass
                           AND attname='feature_vector_ann' AND NOT attisdropped)""")
            has_ann = cur.fetchone()[0]
        if not has_ann:
            await asyncio.to_thread(index_images, False)
        app.state.batcher = ImageBatcher(decode)
        worker = asyncio.create_task(app.state.batcher.run())
        try:
            yield
        finally:
            await app.state.batcher.stop(worker)
    finally:
        pool.closeall()
        pool = None


app = FastAPI(title="Marketplace AI", lifespan=lifespan)


@app.middleware("http")
async def cloud_access(request, call_next):
    if request.url.path.startswith("/ai/"):
        if API_KEY and not secrets.compare_digest(request.headers.get("X-AI-Key", ""), API_KEY):
            return JSONResponse({"detail": "Invalid AI key"}, status_code=401)
        if INFERENCE_ONLY and request.url.path not in ("/ai/features", "/ai/extract-embedding"):
            return JSONResponse({"detail": "Catalog operations run in WebAPI in cloud mode"}, status_code=503)
    return await call_next(request)


@app.post("/ai/features")
async def cloud_features(file: UploadFile = File(...)):
    vector, label, probability = await app.state.batcher.submit(await file.read(MAX_BYTES + 1))
    return {"embedding": vector.tolist(), "label": label, "probability": probability}


@app.get("/health/ready")
async def ready():
    return {"ready": INFERENCE_ONLY or pool is not None, "ann": has_ann,
            "mode": "inference-only" if INFERENCE_ONLY else "catalog",
            "queue_depth": app.state.batcher.queue.qsize()}


@app.post("/ai/reindex-all")
def reindex():
    return index_images(True)


ACTIVE = """FROM products p JOIN categories c ON c.id=p.category_id
            JOIN shops s ON s.id=p.shop_id"""
VISIBLE = "p.status='ACTIVE' AND c.status='ACTIVE' AND s.status='ACTIVE'"


def gallery(cur):
    cur.execute(f"""SELECT p.id,c.name,i.image_url,i.feature_vector,p.name {ACTIVE}
                    JOIN product_images i ON i.product_id=p.id WHERE {VISIBLE}
                    AND i.image_url LIKE '/images/%'""")
    return [(pid, cat, url, v, name) for pid, cat, url, raw, name in cur
            if (v := stored_vector(raw)) is not None]


def nearest(cur, query, limit):
    cur.execute("SET LOCAL hnsw.iterative_scan = strict_order")
    cur.execute(f"""SELECT p.id,c.name,p.name,i.feature_vector_ann <=> %s::vector AS distance
                    {ACTIVE} JOIN product_images i ON i.product_id=p.id
                    WHERE {VISIBLE} AND i.feature_vector_ann IS NOT NULL AND i.image_url LIKE '/images/%%'
                    ORDER BY distance LIMIT %s""", (json.dumps(query.tolist()), max(64, limit * 4)))
    ranking, names = [], {}
    for pid, cat, name, distance in cur:
        if pid not in names:
            ranking.append((float(np.clip(1-distance, -1, 1)), pid, cat))
            names[pid] = name
    return ranking, names


def identify(ranking):
    neighbors = ranking[:5]
    votes = Counter()
    for score, _, category in neighbors:
        votes[category] += np.exp((score - neighbors[0][0]) / .05)
    category = max(votes, key=votes.get)
    return category, float(votes[category] / sum(votes.values()))


def rank_images(query, rows):
    scores = np.einsum("ij,j->i", np.stack([r[3] for r in rows]), query)
    best = {}
    for (pid, category, _, _, _), score in zip(rows, scores):
        if pid not in best or score > best[pid][0]:
            best[pid] = (float(np.clip(score, -1, 1)), pid, category)
    return sorted(best.values(), key=lambda x: (-x[0], x[1]))


# ImageNet labels with a direct counterpart in the Vietnamese catalog.
PRODUCT_TYPES = {
    "dumbbell": ("tạ tay", "tạ đơn"), "barbell": ("tạ đòn",),
    "backpack": ("balo", "ba lô"), "mouse": ("chuột",),
    "water bottle": ("bình nước",), "coffee mug": ("ly", "cốc"),
    "binder": ("bìa còng",), "jersey": ("áo thể thao", "áo thun"),
}


def search_images(query_features, rows):
    query, label, probability = query_features
    return classify(rank_images(query, rows), {r[0]: r[4] for r in rows}, label, probability)


def classify(ranking, names, label, probability):
    category, confidence = identify(ranking)
    if ranking[0][0] >= .75:
        return category, confidence, ranking

    terms = PRODUCT_TYPES.get(label, ())
    if terms and probability >= .25:
        ids = {pid for pid, name in names.items()
               if any((" " + term + " ") in (" " + name.casefold() + " ") for term in terms)}
        same_type = [hit for hit in ranking if hit[1] in ids]
        if same_type:
            return same_type[0][2], probability, same_type

    # When exact appearance is weak, restrict suggestions to the voted category.
    return category, confidence, [hit for hit in ranking if hit[2] == category]


@app.post("/ai/extract-embedding")
async def extract(file: UploadFile = File(...)):
    vector, _, _ = await app.state.batcher.submit(await file.read(MAX_BYTES + 1))
    return {"embedding": vector.tolist()}


@app.post("/ai/visual-search")
async def visual(file: UploadFile = File(...), limit: int = Query(12, ge=1, le=50)):
    query = await app.state.batcher.submit(await file.read(MAX_BYTES + 1))
    return await run_in_threadpool(search_catalog, query, limit)


def search_catalog(query, limit):
    with database() as db, db.cursor() as cur:
        if has_ann:
            ranking, names = nearest(cur, query[0], limit)
        else:
            rows = gallery(cur)
            ranking = rank_images(query[0], rows) if rows else []
            names = {r[0]: r[4] for r in rows}
    if not ranking:
        raise HTTPException(503, "Chưa có ảnh khả dụng. Hãy chạy /ai/reindex-all sau khi tạo dữ liệu.")
    category, confidence, ranking = classify(ranking, names, query[1], query[2])
    return dict(category=category, confidence=confidence,
                matches=[dict(id=pid, similarityScore=s) for s, pid, _ in ranking[:limit]])


def recommend(cur, product_id, user_id, limit, now):
    # Step 4 uses CURRENT_TIMESTAMP in a timestamp-without-time-zone column.
    # Use the database session clock for the identical time zone convention.
    cur.execute(f"""
        WITH clock AS (SELECT coalesce(%s::timestamp, LOCALTIMESTAMP) AS cutoff),
        latest_wishlist AS (
          SELECT DISTINCT ON (user_id, product_id) user_id, product_id, action_type, timestamp
          FROM user_interaction_logs CROSS JOIN clock
          WHERE action_type IN ('WISHLIST_ADD', 'WISHLIST_REMOVE') AND timestamp <= cutoff
          ORDER BY user_id, product_id, timestamp DESC, id DESC
        ), events AS (
          SELECT user_id, product_id, action_type, timestamp FROM user_interaction_logs
          WHERE action_type NOT IN ('WISHLIST_ADD', 'WISHLIST_REMOVE')
          UNION ALL
          SELECT * FROM latest_wishlist WHERE action_type = 'WISHLIST_ADD'
        ), history AS (
          SELECT product_id, user_id, action_type,
            (CASE action_type WHEN 'PURCHASE' THEN 5.0 WHEN 'ADD_TO_CART' THEN 3.0
             WHEN 'REVIEW' THEN 3.0 WHEN 'WISHLIST_ADD' THEN 2.5 WHEN 'CLICK' THEN 1.5 ELSE 1.0 END) *
             exp(-ln(2) * extract(epoch FROM (cutoff - timestamp))/2592000) w
          FROM events CROSS JOIN clock WHERE timestamp <= cutoff
        ), stats AS (
          SELECT product_id,count(*) n,count(*) FILTER (WHERE action_type <> 'VIEW') engaged,
            sum(w) FILTER (WHERE user_id=%s) personal FROM history GROUP BY product_id
        )
        SELECT p.id,c.id,i.feature_vector,i.image_url,
          ARRAY(SELECT DISTINCT a.key || '=' || a.value FROM product_variants v
                CROSS JOIN LATERAL jsonb_each_text(v.attributes::jsonb) a
                WHERE v.product_id=p.id AND v.status='ACTIVE'),
          coalesce(n,0),coalesce(engaged,0),coalesce(personal,0),coalesce(r.rating_avg,0)
        {ACTIVE}
        LEFT JOIN LATERAL (SELECT id,image_url,feature_vector FROM product_images WHERE product_id=p.id
                           ORDER BY is_primary DESC,id LIMIT 1) i ON true
        LEFT JOIN stats ON stats.product_id=p.id
        LEFT JOIN (SELECT product_id,avg(rating) rating_avg FROM reviews
                   WHERE status='ACTIVE' GROUP BY product_id) r ON r.product_id=p.id
        WHERE {VISIBLE} ORDER BY p.id""", (now, user_id))
    items = [dict(id=p, cat=c, vec=stored_vector(raw) if url and local_image(url) else None, attrs=set(attrs),
                  n=n, rate=engaged/max(n,1), weight=float(w), rating=float(r)/5)
             for p,c,raw,url,attrs,n,engaged,w,r in cur]
    peak = max((p["n"] for p in items), default=0)
    profile = sorted((p for p in items if p["weight"] > 0),
                     key=lambda p: -p["weight"])[:10]
    total = sum(p["weight"] for p in profile)
    source = next((p for p in items if p["id"] == product_id), None)

    def affinity(a, b):
        image = float(a["vec"] @ b["vec"]) if a["vec"] is not None and b["vec"] is not None else 0
        union = a["attrs"] | b["attrs"]
        category = int(a["cat"] == b["cat"])
        metadata = (category + len(a["attrs"] & b["attrs"])/len(union))/2 if union else category
        return .6 * image + .4 * metadata

    def score(p):
        volume = np.log1p(p["n"])/np.log1p(peak) if peak else 0
        cold = .5*volume + .3*p["rate"] + .2*p["rating"]
        personal = sum(q["weight"]*affinity(p,q) for q in profile)/total if total else cold
        if source:
            return .7*affinity(p,source) + .2*personal + .1*cold
        return .85*personal + .15*cold if total else cold

    ranked = sorted((p for p in items if p is not source), key=lambda p: (-score(p),p["id"]))
    return [p["id"] for p in ranked[:limit]]


@app.get("/ai/recommendations")
def recommendations(productId: UUID | None = None, userId: UUID | None = None,
                    limit: int = Query(8, ge=1, le=50)):
    with database() as db, db.cursor() as cur:
        return recommend(cur, str(productId) if productId else None,
                         str(userId) if userId else None, limit, None)
