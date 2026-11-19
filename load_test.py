"""Closed-loop visual-search benchmark for the local Marketplace WebAPI."""
import argparse
import asyncio
import io
import json
import math
import platform
import secrets
import time
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

import httpx
from PIL import Image, ImageOps


def corpus(path):
    paths = sorted(path.iterdir()) if path.is_dir() else [path]
    blobs = []
    for item in paths:
        if item.suffix.lower() not in {".jpeg", ".jpg", ".png", ".webp"}:
            continue
        with Image.open(item) as image:
            rgb = ImageOps.exif_transpose(image).convert("RGBA")
            rgb.thumbnail((224, 224), Image.Resampling.LANCZOS)
            frame = Image.new("RGB", (224, 224), "white")
            frame.paste(rgb, ((224-rgb.width)//2, (224-rgb.height)//2), rgb)
            buf = io.BytesIO()
            frame.save(buf, "WEBP", quality=80)
            blobs.append(buf.getvalue())
    if not blobs:
        raise ValueError("Thư mục không chứa ảnh.")
    return blobs


def stats(samples):
    samples = sorted(samples)
    return {f"p{p}_ms": round(samples[math.ceil(len(samples)*p/100)-1], 2)
            for p in (50, 95, 99)} if samples else {}


async def run(args):
    blobs = corpus(args.images)
    counts, timings = Counter(), defaultdict(list)
    client_waits = []
    http_timings = []
    admission = asyncio.Semaphore(args.connections)
    run_id = secrets.token_hex(12).encode()
    inflight = peak = failures = 0
    start = time.perf_counter()
    limits = httpx.Limits(max_connections=args.connections, max_keepalive_connections=args.connections)
    async with httpx.AsyncClient(base_url=args.base_url or f"http://127.0.0.1:{args.port}", limits=limits,
                                 timeout=15, trust_env=False) as client:
        if args.mode == "warm":
            for blob in blobs:
                response = await client.post("/api/products/visual-search", files={
                    "file": ("query.webp", blob, "image/webp")})
                response.raise_for_status()
        async def worker(uid):
            nonlocal inflight, peak, failures
            if args.ramp:
                await asyncio.sleep(args.ramp * uid / args.users)
            for i in range(uid, args.requests, args.users):
                begin = time.perf_counter()
                inflight += 1
                peak = max(peak, inflight)
                group = "error"
                try:
                    blob = blobs[i % len(blobs)]
                    if args.mode == "cold":
                        # Valid WebP RIFF with a unique unknown chunk: defeats byte hash,
                        # while keeping image pixels identical. Not a recall benchmark.
                        payload = run_id + str(i).encode()
                        chunk = b"TEST" + len(payload).to_bytes(4, "little") + payload
                        chunk += b"\0" * (len(payload) % 2)
                        blob = blob[:4] + (len(blob) + len(chunk) - 8).to_bytes(4, "little") + blob[8:] + chunk
                    async with admission:
                        client_waits.append((time.perf_counter()-begin)*1000)
                        http_start = time.perf_counter()
                        try:
                            response = await client.post("/api/products/visual-search", files={
                                "file": ("query.webp", blob, "image/webp")})
                        finally:
                            http_timings.append((time.perf_counter()-http_start)*1000)
                    counts[str(response.status_code)] += 1
                    if response.status_code == 200 and isinstance(response.json().get("products"), list):
                        group = response.headers.get("X-Visual-Cache", "success")
                except (httpx.HTTPError, ValueError, AttributeError) as exc:
                    counts[type(exc).__name__] += 1
                finally:
                    inflight -= 1
                timings[group].append((time.perf_counter()-begin)*1000)
                if group == "error":
                    failures += 1
                if args.think and i + args.users < args.requests:
                    await asyncio.sleep(args.think)
        start = time.perf_counter()
        await asyncio.gather(*(worker(i) for i in range(min(args.users, args.requests))))
        seconds = time.perf_counter()-start
    all_samples = [ms for group in timings.values() for ms in group]
    report = dict(utc=datetime.now(timezone.utc).isoformat(), platform=platform.platform(),
                  settings={k:str(v) if isinstance(v, Path) else v for k,v in vars(args).items()},
                  distinct_images=len(set(blobs)), peak_outstanding=peak, elapsed_s=round(seconds,3),
                  requests=len(all_samples), rps=round(len(all_samples)/seconds,2),
                  success_rps=round((len(all_samples)-failures)/seconds,2),
                  error_pct=round(100*failures/len(all_samples),4), errors=failures, status=dict(counts),
                  latency=stats(all_samples),
                  client_admission_wait=stats(client_waits),
                  http_latency=stats(http_timings),
                  note="Closed-loop; latency includes client queue. Users are not simultaneous TCP connections.",
                  groups={g:dict(count=len(v), **stats(v)) for g,v in timings.items()})
    rendered = json.dumps(report, ensure_ascii=False, indent=2)
    args.output.write_text(rendered, encoding="utf8")
    print(json.dumps(report, ensure_ascii=True, indent=2))
    return int(failures > 0)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=5116)
    parser.add_argument("--base-url", help="API origin, e.g. http://192.168.1.10:5116")
    parser.add_argument("--mode", choices=("mixed", "warm", "cold"), default="mixed")
    parser.add_argument("--images", type=Path, default=Path(__file__).resolve().parent /
                        "Marketplace.WebAPI/wwwroot/images/demo/tee-mint.png")
    parser.add_argument("--users", type=int, default=10)
    parser.add_argument("--requests", type=int, default=25)
    parser.add_argument("--connections", type=int, default=128)
    parser.add_argument("--ramp", type=float, default=0)
    parser.add_argument("--think", type=float, default=0)
    parser.add_argument("--output", type=Path, default=Path("visual-load.json"))
    args = parser.parse_args()
    if min(args.users, args.requests, args.connections) < 1 or min(args.ramp, args.think) < 0:
        parser.error("Counts must be positive and times nonnegative.")
    raise SystemExit(asyncio.run(run(args)))
