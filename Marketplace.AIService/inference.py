import asyncio
import json
import os
from contextlib import suppress
from functools import lru_cache
from pathlib import Path

import numpy as np
import onnxruntime as ort
from fastapi import HTTPException
from PIL import Image

ROOT = Path(__file__).resolve().parent
LABELS = json.loads((ROOT / "labels.json").read_text(encoding="utf8"))
MEAN = np.array([.485, .456, .406], dtype=np.float32)
STD = np.array([.229, .224, .225], dtype=np.float32)


@lru_cache(maxsize=1)
def session():
    settings = ort.SessionOptions()
    settings.intra_op_num_threads = int(os.getenv("MARKETPLACE_AI_THREADS", "2"))
    return ort.InferenceSession(str(ROOT / "mobilenet.onnx"), settings, providers=["CPUExecutionProvider"])


def prepare(image):
    # Torchvision ImageClassification preset: bilinear short-side 256, center crop 224.
    w, h = image.size
    short = min(w, h)
    w, h = 256 * w // short, 256 * h // short
    x, y = round((w - 224) / 2), round((h - 224) / 2)
    # Crop using the resize source box for very elongated inputs to bound allocation.
    if max(w, h) > 8192:
        sx, sy = image.width / w, image.height / h
        image = image.resize((224, 224), Image.Resampling.BILINEAR,
                             box=(x*sx, y*sy, (x+224)*sx, (y+224)*sy))
    else:
        image = image.resize((w, h), Image.Resampling.BILINEAR).crop((x, y, x+224, y+224))
    pixels = np.asarray(image, dtype=np.float32) / 255
    return ((pixels - MEAN) / STD).transpose(2, 0, 1)


def predict(tensors):
    vectors, logits = session().run(None, {"images": np.stack(tensors)})
    probs = np.exp(logits - logits.max(axis=1, keepdims=True))
    probs /= probs.sum(axis=1, keepdims=True)
    return [(v, LABELS[i], float(p[i])) for v, p, i in zip(vectors, probs, probs.argmax(axis=1))]


class ImageBatcher:
    def __init__(self, decode):
        self.decode = decode
        self.queue = asyncio.Queue(maxsize=int(os.getenv("MARKETPLACE_AI_QUEUE", "128")))
        self.batch_size = int(os.getenv("MARKETPLACE_AI_BATCH_SIZE", "32"))
        self.wait_seconds = float(os.getenv("MARKETPLACE_AI_BATCH_MS", "12")) / 1000
        self.active = []
        self.stopped = False
        if self.queue.maxsize < 1 or not 1 <= self.batch_size <= 32 or not 0 <= self.wait_seconds <= .015:
            raise ValueError("Queue must be positive, batch 1..32, batch window 0..15ms")

    async def submit(self, raw):
        if self.stopped:
            raise HTTPException(503, "AI đang dừng.")
        future = asyncio.get_running_loop().create_future()
        try:
            self.queue.put_nowait((raw, future))
        except asyncio.QueueFull:
            raise HTTPException(503, "Hàng đợi AI đã đầy.")
        try:
            return await asyncio.wait_for(future, 3)
        except TimeoutError:
            raise HTTPException(503, "Quá thời gian chờ AI.")

    def infer(self, jobs):
        results = {}
        tensors, indices = [], []
        for i, (raw, _) in enumerate(jobs):
            try:
                tensors.append(prepare(self.decode(raw)))
                indices.append(i)
            except Exception as exc:
                results[i] = exc
        if tensors:
            results.update(zip(indices, predict(tensors)))
        return results

    async def run(self):
        loop = asyncio.get_running_loop()
        while True:
            jobs = [await self.queue.get()]
            self.active = jobs
            deadline = loop.time() + self.wait_seconds
            while len(jobs) < self.batch_size and loop.time() < deadline:
                try:
                    jobs.append(await asyncio.wait_for(self.queue.get(), deadline - loop.time()))
                except TimeoutError:
                    break
            jobs = [job for job in jobs if not job[1].done()]
            try:
                results = await asyncio.to_thread(self.infer, jobs)
            except Exception as exc:
                results = dict.fromkeys(range(len(jobs)), exc)
            for i, (_, future) in enumerate(jobs):
                if not future.done():
                    value = results[i]
                    if isinstance(value, Exception):
                        future.set_exception(value)
                    else:
                        future.set_result(value)
            self.active = []

    async def stop(self, worker):
        self.stopped = True
        worker.cancel()
        with suppress(asyncio.CancelledError):
            await worker
        while not self.queue.empty():
            self.queue.get_nowait()[1].cancel()
        for _, future in self.active:
            future.cancel()
