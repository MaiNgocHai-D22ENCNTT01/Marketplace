"""Run: python -m unittest test_scaling -v (no PostgreSQL required)."""
import asyncio
import unittest
from unittest.mock import patch

import numpy as np
from fastapi import HTTPException
from PIL import Image
import inference


class ModelTests(unittest.TestCase):
    def test_dynamic_batch_matches_single(self):
        tensor = inference.prepare(Image.new("RGB", (224, 224), "red"))
        one = inference.predict([tensor])[0][0]
        batch = inference.predict([tensor] * 32)
        self.assertEqual(len(batch), 32)
        self.assertEqual(one.shape, (1024,))
        self.assertAlmostEqual(float(np.linalg.norm(one)), 1, places=5)
        np.testing.assert_allclose(batch[-1][0], one, atol=1e-5)


class QueueTests(unittest.IsolatedAsyncioTestCase):
    async def test_full_queue_rejects_without_waiting(self):
        batcher = inference.ImageBatcher(lambda raw: raw)
        batcher.queue = asyncio.Queue(maxsize=1)
        batcher.queue.put_nowait((b"", asyncio.get_running_loop().create_future()))
        with self.assertRaises(HTTPException) as error:
            await batcher.submit(b"new")
        self.assertEqual(error.exception.status_code, 503)

    async def test_bad_image_does_not_poison_other_jobs(self):
        def decode(raw):
            if raw == b"bad":
                raise HTTPException(400, "invalid")
            return Image.new("RGB", (224, 224))
        batcher = inference.ImageBatcher(decode)
        with patch.object(inference, "predict", side_effect=lambda items: ["ok"] * len(items)):
            worker = asyncio.create_task(batcher.run())
            try:
                results = await asyncio.gather(batcher.submit(b"bad"), batcher.submit(b"good"),
                                               return_exceptions=True)
                self.assertIsInstance(results[0], HTTPException)
                self.assertEqual(results[1], "ok")
            finally:
                await batcher.stop(worker)

    async def test_shutdown_resolves_active_and_queued_requests(self):
        batcher = inference.ImageBatcher(lambda raw: raw)
        pending = asyncio.create_task(batcher.submit(b"a"))
        await asyncio.sleep(0)
        worker = asyncio.create_task(batcher.run())
        await asyncio.sleep(0)
        await batcher.stop(worker)
        with self.assertRaises(asyncio.CancelledError):
            await pending
        with self.assertRaises(HTTPException):
            await batcher.submit(b"late")


if __name__ == "__main__":
    unittest.main()
